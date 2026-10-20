import { act, fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import PatientAutocomplete from '../modules/admin/patients/PatientAutocomplete';
import { setSession } from '../services/api';

const patient = { patientId: 'profile-id', userId: 'user-id', firstName: 'Andrea', lastName: 'Bejar', ci: '1234556', phone: '71234567', email: 'andrea@gmail.com' };
const changed = vi.fn(); const fetchMock = vi.fn();
beforeEach(() => { setSession(null); changed.mockReset(); fetchMock.mockReset(); fetchMock.mockImplementation(async () => Response.json([patient])); vi.stubGlobal('fetch', fetchMock); });
afterEach(() => vi.useRealTimers());
it('combobox reemplaza select y no consulta al abrir ni con un carácter', async () => {
  render(<PatientAutocomplete onChange={changed} />); expect(screen.getByPlaceholderText('Buscar por CI, nombre, email o teléfono…')).toBeTruthy();
  expect(document.querySelector('select')).toBeNull(); await userEvent.type(screen.getByRole('combobox'), 'a');
  await new Promise(r => setTimeout(r, 350)); expect(fetchMock).not.toHaveBeenCalled();
});
it('debounce de 300 ms reinicia al escribir y limita resultados', async () => {
  vi.useFakeTimers(); render(<PatientAutocomplete onChange={changed} />); fireEvent.change(screen.getByRole('combobox'), { target: { value: 'an' } });
  await act(async () => { vi.advanceTimersByTime(299); }); expect(fetchMock).not.toHaveBeenCalled();
  fireEvent.change(screen.getByRole('combobox'), { target: { value: 'andr' } }); await act(async () => { vi.advanceTimersByTime(299); }); expect(fetchMock).not.toHaveBeenCalled();
  await act(async () => { vi.advanceTimersByTime(1); }); expect(fetchMock).toHaveBeenCalledTimes(1);
  const url = new URL(String(fetchMock.mock.calls[0][0])); expect(url.searchParams.get('q')).toBe('andr'); expect(url.searchParams.get('limit')).toBe('10');
});
it('muestra identidad y selecciona PatientProfile.Id sin requerir CanLogin', async () => {
  render(<PatientAutocomplete onChange={changed} />); await userEvent.type(screen.getByRole('combobox'), '1234');
  const option = await screen.findByRole('option'); expect(option.textContent).toContain('1234556'); expect(option.textContent).toContain('71234567'); expect(option.textContent).toContain('andrea@gmail.com');
  await userEvent.click(option); expect(changed.mock.lastCall?.[0].patientId).toBe('profile-id'); expect(changed.mock.lastCall?.[0].patientId).not.toBe('user-id');
  expect(screen.getByText(/Paciente seleccionado/)).toBeTruthy();
  await userEvent.type(screen.getByRole('combobox'), 'x'); expect(changed.mock.lastCall).toEqual([null]); expect(screen.queryByText(/Paciente seleccionado/)).toBeNull();
});
it('flechas y Enter seleccionan; Escape cierra sin seleccionar', async () => {
  render(<PatientAutocomplete onChange={changed} />); await userEvent.type(screen.getByRole('combobox'), 'and'); await screen.findByRole('option');
  await userEvent.keyboard('{Escape}'); expect(screen.queryByRole('option')).toBeNull(); expect(changed.mock.lastCall).toEqual([null]);
  await userEvent.keyboard('{ArrowDown}'); await screen.findByRole('option'); await userEvent.keyboard('{ArrowDown}{Enter}');
  expect(changed.mock.lastCall?.[0].patientId).toBe('profile-id'); expect(screen.getByRole('combobox').getAttribute('aria-expanded')).toBe('false');
  await userEvent.click(screen.getByRole('button', { name: 'Cambiar paciente' })); expect((screen.getByRole('combobox') as HTMLInputElement).value).toBe(''); expect(changed.mock.lastCall).toEqual([null]);
});
it('loading y respuestas antiguas no reemplazan la búsqueda actual', async () => {
  let resolve!: (r: Response) => void; fetchMock.mockImplementationOnce(() => new Promise(r => { resolve = r; }));
  render(<PatientAutocomplete onChange={changed} />); await userEvent.type(screen.getByRole('combobox'), 'old'); await screen.findByText('Buscando…');
  await userEvent.clear(screen.getByRole('combobox')); await userEvent.type(screen.getByRole('combobox'), 'new'); await screen.findByRole('option');
  await act(async () => resolve(Response.json([{ ...patient, patientId: 'old', firstName: 'Obsoleto' }]))); expect(screen.queryByText('Obsoleto Bejar')).toBeNull();
});
it('sin coincidencias', async () => {
  fetchMock.mockResolvedValue(Response.json([])); render(<PatientAutocomplete onChange={changed} />); await userEvent.type(screen.getByRole('combobox'), 'zz'); expect(await screen.findByText('No se encontraron coincidencias.')).toBeTruthy();
});
it('error controlado no selecciona texto libre', async () => {
  fetchMock.mockResolvedValue(Response.json({ title: 'Búsqueda no disponible.' }, { status: 503 })); render(<PatientAutocomplete onChange={changed} />);
  await userEvent.type(screen.getByRole('combobox'), 'an'); expect((await screen.findByRole('alert')).textContent).toBe('Búsqueda no disponible.'); expect(changed.mock.lastCall).toEqual([null]);
});
