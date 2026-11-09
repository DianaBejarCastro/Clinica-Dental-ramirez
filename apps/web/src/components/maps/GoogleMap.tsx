import { useEffect, useRef, useState } from 'react';
import { loadGoogleMaps, mapFailureEvent } from './googleMapsLoader';
import { validCoordinates } from './coordinates';
import './google-map.css';

export interface MapLocation { id: string; name: string; latitude: number | null; longitude: number | null }
export default function GoogleMap({ locations, label, className = '', onPosition }: {
  locations: MapLocation[]; label: string; className?: string;
  onPosition?: (latitude: number, longitude: number) => void;
}) {
  const container = useRef<HTMLDivElement>(null);
  const changed = useRef(onPosition); changed.current = onPosition;
  const [ready, setReady] = useState<{ api: typeof google.maps; map: google.maps.Map } | null>(null);
  const [failed, setFailed] = useState(false);
  useEffect(() => {
    let disposed = false; let map: google.maps.Map | undefined; let click: google.maps.MapsEventListener | undefined;
    const fail = () => { if (!disposed) setFailed(true); };
    window.addEventListener(mapFailureEvent, fail);
    loadGoogleMaps().then(api => {
      if (disposed || !container.current) return;
      map = new api.Map(container.current, {
        center: { lat: -16.29, lng: -63.59 }, zoom: 5,
        mapId: import.meta.env.VITE_GOOGLE_MAPS_MAP_ID?.trim() || 'DEMO_MAP_ID',
        disableDefaultUI: true, zoomControl: true, keyboardShortcuts: true,
        streetViewControl: false, mapTypeControl: false, fullscreenControl: false,
        gestureHandling: 'cooperative',
      });
      click = map.addListener('click', (event: google.maps.MapMouseEvent) => {
        const lat = event.latLng?.lat(), lng = event.latLng?.lng();
        if (lat !== undefined && lng !== undefined && validCoordinates(lat, lng))
          changed.current?.(Number(lat.toFixed(7)), Number(lng.toFixed(7)));
      });
      setReady({ api, map });
    }).catch(fail);
    return () => { disposed = true; window.removeEventListener(mapFailureEvent, fail); click?.remove(); if (map) map.unbindAll(); };
  }, []);
  useEffect(() => {
    if (!ready || failed) return;
    const { api, map } = ready;
    const valid = locations.filter(p => validCoordinates(p.latitude, p.longitude));
    const listeners: google.maps.MapsEventListener[] = [];
    const markers = valid.map(p => {
      const pin = document.createElement('span'); pin.className = 'google-clinic-pin';
      const position = { lat: p.latitude!, lng: p.longitude! };
      const marker = new api.marker.AdvancedMarkerElement({ map, position, title: p.name, content: pin, gmpDraggable: !!onPosition });
      if (onPosition) listeners.push(marker.addListener('dragend', (event: google.maps.MapMouseEvent) => {
        const lat = event.latLng?.lat(), lng = event.latLng?.lng();
        if (lat !== undefined && lng !== undefined && validCoordinates(lat, lng))
          changed.current?.(Number(lat.toFixed(7)), Number(lng.toFixed(7)));
        else marker.position = position;
      }));
      return marker;
    });
    const frame = () => {
      if (valid.length === 1) { map.setCenter({ lat: valid[0].latitude!, lng: valid[0].longitude! }); map.setZoom(15); }
      else if (valid.length > 1) {
        const bounds = new api.LatLngBounds();
        valid.forEach(p => bounds.extend({ lat: p.latitude!, lng: p.longitude! }));
        map.fitBounds(bounds, 32);
      }
    };
    frame();
    const observer = typeof ResizeObserver === 'undefined' ? null : new ResizeObserver(frame);
    if (container.current) observer?.observe(container.current);
    return () => { observer?.disconnect(); listeners.forEach(l => l.remove()); markers.forEach(m => { m.map = null; }); };
  }, [ready, locations, !!onPosition, failed]);
  return <div className={`google-clinic-map ${className}`} role="region" aria-label={label}>
    <div ref={container} className="google-clinic-canvas" hidden={failed} />
    {failed ? <p className="google-map-message" role="status">No se pudo cargar el mapa. Puedes consultar la dirección y usar las coordenadas.</p>
      : !ready && <p className="google-map-message" role="status">Cargando mapa…</p>}
  </div>;
}
