import { useEffect, useId, useRef } from 'react';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import './location-picker.css';
import LocationSearch from './LocationSearch';

export function validCoordinates(latitude: number | null, longitude: number | null) {
  return latitude !== null && longitude !== null && Number.isFinite(latitude) && Number.isFinite(longitude)
    && latitude >= -90 && latitude <= 90 && longitude >= -180 && longitude <= 180;
}
export function googleMapsLink(latitude: number | null, longitude: number | null) {
  return validCoordinates(latitude, longitude) ? `https://www.google.com/maps?q=${latitude},${longitude}` : null;
}
export default function LocationPicker({ latitude, longitude, onChange }: {
  latitude: number | null; longitude: number | null; onChange: (latitude: number | null, longitude: number | null) => void;
}) {
  const id = useId(); const container = useRef<HTMLDivElement>(null); const map = useRef<L.Map | null>(null);
  const marker = useRef<L.Marker | null>(null); const change = useRef(onChange); change.current = onChange;
  const lastPosition = useRef<L.LatLngTuple>([-16.29, -63.59]);
  useEffect(() => {
    const instance = L.map(container.current!, { scrollWheelZoom: false }).setView([-16.29, -63.59], 5); map.current = instance;
    L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', { maxZoom: 19, attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors' }).addTo(instance);
    instance.on('click', (event: L.LeafletMouseEvent) => {
      const { lat, lng } = event.latlng.wrap();
      if (validCoordinates(lat, lng)) change.current(Number(lat.toFixed(7)), Number(lng.toFixed(7)));
    });
    const observer = typeof ResizeObserver !== 'undefined' ? new ResizeObserver(() => instance.invalidateSize()) : null;
    observer?.observe(container.current!);
    return () => { observer?.disconnect(); instance.remove(); map.current = null; marker.current = null; };
  }, []);
  useEffect(() => {
    const instance = map.current; if (!instance) return;
    if (!validCoordinates(latitude, longitude)) { marker.current?.remove(); marker.current = null; return; }
    const position: L.LatLngTuple = [latitude!, longitude!];
    lastPosition.current = position;
    if (!marker.current) {
      marker.current = L.marker(position, { draggable: true, title: 'Ubicación de la sucursal',
        icon: L.divIcon({ className: 'location-marker', html: '<span></span>', iconSize: [24, 24], iconAnchor: [12, 12] }) }).addTo(instance);
      marker.current.on('dragend', () => {
        const { lat, lng } = marker.current!.getLatLng().wrap();
        if (validCoordinates(lat, lng)) change.current(Number(lat.toFixed(7)), Number(lng.toFixed(7)));
        else marker.current?.setLatLng(lastPosition.current);
      });
    } else marker.current.setLatLng(position);
    instance.setView(position, Math.max(instance.getZoom(), 15));
  }, [latitude, longitude]);
  const href = googleMapsLink(latitude, longitude);
  return <div className="location-picker full-width"><p id={id}>Selecciona la ubicación de la sucursal</p>
    <LocationSearch onSelect={(lat, lng) => {
      if (validCoordinates(lat, lng)) { onChange(Number(lat.toFixed(7)), Number(lng.toFixed(7))); map.current?.setView([lat, lng], 16); }
    }} />
    <div ref={container} className="location-map" role="region" aria-labelledby={id} />
    <p className="muted">Haz clic en el mapa o arrastra el marcador. También puedes ajustar las coordenadas.</p>
    <div className="location-coordinates"><label>Latitud<input name="latitude" type="number" step="any" min={-90} max={90} value={latitude ?? ''} onChange={e => onChange(e.target.value === '' ? null : e.target.valueAsNumber, longitude)} /></label>
      <label>Longitud<input name="longitude" type="number" step="any" min={-180} max={180} value={longitude ?? ''} onChange={e => onChange(latitude, e.target.value === '' ? null : e.target.valueAsNumber)} /></label></div>
    {href && <a href={href} target="_blank" rel="noopener noreferrer">Ver en Google Maps</a>}
  </div>;
}
