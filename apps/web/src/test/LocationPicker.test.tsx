import { useState } from 'react';
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, expect, it, vi } from 'vitest';
import { maps, markers, mockApi, resetMaps } from './googleMapsMock';
import { loadGoogleMaps } from '../components/maps/googleMapsLoader';
import LocationPicker, { googleMapsLink } from '../modules/admin/centers/LocationPicker';
vi.mock('../components/maps/googleMapsLoader', () => ({ loadGoogleMaps: vi.fn(), mapFailureEvent: 'maps-failure' }));
beforeEach(() => { resetMaps(); vi.mocked(loadGoogleMaps).mockResolvedValue(mockApi); });
function Harness({ initial = [null, null] }: { initial?: [number | null, number | null] }) {
  const [position, setPosition] = useState(initial);
  return <LocationPicker latitude={position[0]} longitude={position[1]} onChange={(lat, lng) => setPosition([lat, lng])} />;
}
it('loads Google map without inventing a marker and preserves click', async () => {
  render(<Harness />); await waitFor(() => expect(maps).toHaveLength(1));
  expect(markers).toHaveLength(0);
  act(() => maps[0].fire('click', -16.5, -68.15));
  expect((screen.getByLabelText('Latitud') as HTMLInputElement).value).toBe('-16.5');
  expect((screen.getByLabelText('Longitud') as HTMLInputElement).value).toBe('-68.15');
  expect(screen.getByRole('link', { name: 'Ver en Google Maps' }).getAttribute('href')).toBe('https://www.google.com/maps/dir/?api=1&destination=-16.5,-68.15');
});
it('centers existing coordinates and preserves dragging', async () => {
  render(<Harness initial={[-16.5, -68.15]} />); await waitFor(() => expect(markers).toHaveLength(1));
  expect(markers[0].position).toEqual({ lat: -16.5, lng: -68.15 }); expect(markers[0].gmpDraggable).toBe(true);
  act(() => markers[0].fire('dragend', -17, -66));
  expect((screen.getByLabelText('Latitud') as HTMLInputElement).value).toBe('-17');
  expect((screen.getByLabelText('Longitud') as HTMLInputElement).value).toBe('-66');
});
it('preserves manual coordinates even when the API fails', async () => {
  vi.mocked(loadGoogleMaps).mockRejectedValue(new Error('missing key'));
  render(<Harness initial={[0, 0]} />); await screen.findByText(/No se pudo cargar el mapa/);
  fireEvent.change(screen.getByLabelText('Latitud'), { target: { value: '-18' } });
  expect(screen.getByRole('link', { name: 'Ver en Google Maps' }).getAttribute('href')).toContain('-18,0');
  fireEvent.change(screen.getByLabelText('Longitud'), { target: { value: '' } });
  expect(screen.queryByRole('link')).toBeNull();
});
it('rejects invalid interaction and removes listeners/markers on unmount', async () => {
  const view = render(<Harness initial={[0, 0]} />); await waitFor(() => expect(markers).toHaveLength(1));
  act(() => maps[0].fire('click', 95, 0)); act(() => markers[0].fire('dragend', NaN, 0));
  expect((screen.getByLabelText('Latitud') as HTMLInputElement).value).toBe('0');
  view.unmount(); expect(markers[0].map).toBeNull(); expect(maps[0].handlers.click).toBeUndefined();
});
it.each([[null, null], [91, 0], [0, -181], [NaN, 0]])('rejects invalid coordinates %s %s', (lat, lng) => expect(googleMapsLink(lat, lng)).toBeNull());
