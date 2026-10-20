import { useState } from 'react';
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, expect, it, vi } from 'vitest';
import LocationPicker from '../modules/admin/centers/LocationPicker';

const result = { displayName: 'Av. 10 de Noviembre, Llallagua, Potosí', latitude: -18.42, longitude: -66.58 };
const fetchMock = vi.fn();
beforeEach(() => { fetchMock.mockReset(); fetchMock.mockResolvedValue(Response.json([result])); vi.stubGlobal('fetch', fetchMock); });
function Harness() {
  const [coords, setCoords] = useState<[number | null, number | null]>([null, null]);
  return <form onSubmit={e => { e.preventDefault(); throw new Error('No debe guardar sucursal'); }}><LocationPicker latitude={coords[0]} longitude={coords[1]} onChange={(a, b) => setCoords([a, b])} /></form>;
}
async function type() { await userEvent.type(screen.getByLabelText('Buscar ubicación'), 'Llallagua'); }
it('solo busca por botón, limita a cinco y muestra resultados', async () => {
  fetchMock.mockResolvedValue(Response.json(Array.from({ length: 7 }, (_, i) => ({ ...result, displayName: `Lugar ${i}` }))));
  render(<Harness />); await type(); expect(fetchMock).not.toHaveBeenCalled();
  await userEvent.click(screen.getByRole('button', { name: 'Buscar' })); await screen.findByRole('button', { name: 'Lugar 0' });
  expect(screen.queryByRole('button', { name: 'Lugar 5' })).toBeNull(); expect(String(fetchMock.mock.calls[0][0])).toContain('/api/location/search?q=Llallagua');
});
it('muestra loading y evita búsquedas duplicadas', async () => {
  let finish!: (response: Response) => void; fetchMock.mockReturnValue(new Promise<Response>(resolve => { finish = resolve; }));
  render(<Harness />); await type(); await userEvent.click(screen.getByRole('button', { name: 'Buscar' }));
  expect(screen.getByRole('status').textContent).toContain('Buscando'); fireEvent.keyDown(screen.getByLabelText('Buscar ubicación'), { key: 'Enter' });
  expect(fetchMock).toHaveBeenCalledTimes(1); await act(async () => finish(Response.json([])));
});
it('seleccionar centra marcador y actualiza coordenadas sin guardar formulario', async () => {
  render(<Harness />); await type(); await userEvent.click(screen.getByRole('button', { name: 'Buscar' }));
  await userEvent.click(await screen.findByRole('button', { name: result.displayName }));
  expect((screen.getByLabelText('Latitud') as HTMLInputElement).value).toBe('-18.42');
  expect((screen.getByLabelText('Longitud') as HTMLInputElement).value).toBe('-66.58');
  expect(document.querySelector('.location-marker')).toBeTruthy(); expect(screen.queryByRole('button', { name: result.displayName })).toBeNull();
});
it('muestra resultado vacío', async () => {
  fetchMock.mockResolvedValue(Response.json([])); render(<Harness />); await type(); await userEvent.click(screen.getByRole('button', { name: 'Buscar' }));
  expect(await screen.findByText('No se encontraron resultados')).toBeTruthy();
});
it('muestra error sin afectar el mapa', async () => {
  fetchMock.mockResolvedValue(Response.json({ title: 'Servicio no disponible' }, { status: 502 })); render(<Harness />); await type(); await userEvent.click(screen.getByRole('button', { name: 'Buscar' }));
  expect((await screen.findByRole('alert')).textContent).toContain('Servicio no disponible'); expect(screen.getByLabelText('Latitud')).toBeTruthy();
});
it('Enter busca sin enviar formulario y Escape cierra resultados', async () => {
  render(<Harness />); await type(); await userEvent.keyboard('{Enter}'); await screen.findByRole('button', { name: result.displayName });
  await userEvent.keyboard('{Escape}'); expect(screen.queryByRole('button', { name: result.displayName })).toBeNull();
});
it('consulta corta no genera petición', async () => {
  render(<Harness />); await userEvent.type(screen.getByLabelText('Buscar ubicación'), 'ab'); await userEvent.keyboard('{Enter}');
  expect(screen.getByRole('alert').textContent).toContain('3 y 250'); expect(fetchMock).not.toHaveBeenCalled();
});
it('descarta respuesta si se cambia la consulta', async () => {
  let finish!: (response: Response) => void; fetchMock.mockReturnValue(new Promise<Response>(resolve => { finish = resolve; }));
  render(<Harness />); await type(); await userEvent.keyboard('{Enter}'); await userEvent.type(screen.getByLabelText('Buscar ubicación'), ' nueva');
  await act(async () => finish(Response.json([result]))); await waitFor(() => expect(screen.queryByText('Buscando ubicación…')).toBeNull());
  expect(screen.queryByRole('button', { name: result.displayName })).toBeNull();
});
