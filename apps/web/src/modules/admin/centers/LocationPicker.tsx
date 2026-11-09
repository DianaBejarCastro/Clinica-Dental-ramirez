import { useId, useMemo } from 'react';
import GoogleMap from '../../../components/maps/GoogleMap';
import { googleMapsLink } from '../../../components/maps/coordinates';
export { googleMapsLink, validCoordinates } from '../../../components/maps/coordinates';
import './location-picker.css';
export default function LocationPicker({ latitude, longitude, onChange }: {
  latitude: number | null; longitude: number | null; onChange: (latitude: number | null, longitude: number | null) => void;
}) {
  const id = useId();
  const locations = useMemo(() => [{ id: 'branch', name: 'Ubicación de la sucursal', latitude, longitude }], [latitude, longitude]);
  const href = googleMapsLink(latitude, longitude);
  return <div className="location-picker full-width"><p id={id}>Ubicación en el mapa</p>
    <p className="muted">Coloca el marcador en la ubicación exacta de la sucursal o introduce sus coordenadas.</p>
    <GoogleMap locations={locations} label="Ubicación en el mapa" className="location-map" onPosition={onChange} />
    <p className="muted">Haz clic en el mapa o arrastra el marcador. También puedes ajustar las coordenadas.</p>
    <div className="location-coordinates"><label>Latitud<input name="latitude" type="number" step="any" min={-90} max={90} value={latitude ?? ''} onChange={e => onChange(e.target.value === '' ? null : e.target.valueAsNumber, longitude)} /></label>
      <label>Longitud<input name="longitude" type="number" step="any" min={-180} max={180} value={longitude ?? ''} onChange={e => onChange(latitude, e.target.value === '' ? null : e.target.valueAsNumber)} /></label></div>
    {href && <a href={href} target="_blank" rel="noopener noreferrer">Ver en Google Maps</a>}
  </div>;
}
