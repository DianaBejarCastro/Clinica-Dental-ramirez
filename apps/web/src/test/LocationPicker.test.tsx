import { useState } from 'react';
import { act, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import L from 'leaflet';
import LocationPicker, { googleMapsLink } from '../modules/admin/centers/LocationPicker';

afterEach(() => vi.restoreAllMocks());
function Harness({ initial = [null, null] }: { initial?: [number | null, number | null] }) {
  const [position, setPosition] = useState(initial);
  return <LocationPicker latitude={position[0]} longitude={position[1]} onChange={(lat, lng) => setPosition([lat, lng])} />;
}
it('renderiza mapa OSM compacto con controles y vista inicial de Bolivia sin marcador', () => {
  const spy = vi.spyOn(L, 'map'); render(<Harness />);
  const map = spy.mock.results[0].value as L.Map;
  expect(map.getCenter().lat).toBeCloseTo(-16.29); expect(map.getZoom()).toBe(5);
  expect(screen.getByRole('region').classList.contains('location-map')).toBe(true);
  expect(screen.getByRole('link', { name: 'OpenStreetMap' })).toBeTruthy();
  expect(screen.queryByRole('link', { name: 'Ver en Google Maps' })).toBeNull();
  expect(document.querySelector('.location-marker')).toBeNull();
});
it('click real de Leaflet actualiza coordenadas y enlace', () => {
  const spy = vi.spyOn(L, 'map'); render(<Harness />); const map = spy.mock.results[0].value as L.Map;
  act(() => { map.fire('click', { latlng: L.latLng(-16.5, -68.15) }); });
  expect((screen.getByLabelText('Latitud') as HTMLInputElement).value).toBe('-16.5');
  expect((screen.getByLabelText('Longitud') as HTMLInputElement).value).toBe('-68.15');
  expect(screen.getByRole('link', { name: 'Ver en Google Maps' }).getAttribute('href')).toBe('https://www.google.com/maps?q=-16.5,-68.15');
});
it('coordenadas existentes centran y muestran marcador arrastrable', () => {
  const spy = vi.spyOn(L, 'marker'); render(<Harness initial={[-16.5, -68.15]} />);
  const marker = spy.mock.results[0].value as L.Marker; expect(marker.dragging?.enabled()).toBe(true);
  expect(document.querySelector('.location-marker')).toBeTruthy();
  act(() => { marker.setLatLng([-17, -66]); marker.fire('dragend'); });
  expect((screen.getByLabelText('Latitud') as HTMLInputElement).value).toBe('-17');
  expect((screen.getByLabelText('Longitud') as HTMLInputElement).value).toBe('-66');
});
it('permite corregir coordenadas manualmente y quitar ubicación', () => {
  render(<Harness initial={[0, 0]} />);
  expect(screen.getByRole('link', { name: 'Ver en Google Maps' }).getAttribute('rel')).toBe('noopener noreferrer');
  fireEvent.change(screen.getByLabelText('Latitud'), { target: { value: '-18' } });
  expect(screen.getByRole('link', { name: 'Ver en Google Maps' }).getAttribute('href')).toContain('-18,0');
  fireEvent.change(screen.getByLabelText('Longitud'), { target: { value: '' } });
  expect(screen.queryByRole('link', { name: 'Ver en Google Maps' })).toBeNull(); expect(document.querySelector('.location-marker')).toBeNull();
});
it('no admite posiciones de mapa fuera de rango y limpia instancia al desmontar', () => {
  const spy = vi.spyOn(L, 'map'); const view = render(<Harness />); const map = spy.mock.results[0].value as L.Map;
  const remove = vi.spyOn(map, 'remove'); act(() => { map.fire('click', { latlng: L.latLng(95, 0) }); });
  expect((screen.getByLabelText('Latitud') as HTMLInputElement).value).toBe(''); view.unmount(); expect(remove).toHaveBeenCalled();
});
it.each([[null, null], [91, 0], [0, -181], [NaN, 0]])('no genera enlace inválido %s %s', (lat, lng) => {
  expect(googleMapsLink(lat, lng)).toBeNull();
});
