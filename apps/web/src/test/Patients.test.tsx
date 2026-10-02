import { act, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, expect, it, vi } from 'vitest';
import App from '../App';
import { setSession } from '../services/api';
import type { Patient } from '../modules/admin/patients/types';

const basePatient: Patient = {
  id: 'patient-1', userId: null, firstName: 'Ana', lastName: 'Paciente', ci: 'CI-123', birthDate: '1995-05-10',
  gender: null, phone: '70000000', email: 'clinical@example.test', address: 'Calle 1',
  emergencyContactName: 'Contacto', emergencyContactPhone: '71111111', bloodType: 'O+', notes: 'Nota sintética',
  isActive: true, createdAt: '2026-10-02T12:00:00Z', updatedAt: '2026-10-02T12:00:00Z',
};
let patients: Patient[];
let permissions: string[];
let failure: { method: string; status: number; title: string } | null;
let listWait: Promise<void> | null;
const fetchMock = vi.fn(async (input: string | URL | Request, init?: RequestInit) => {
  const url = new URL(String(input)); const path = url.pathname; const method = init?.method ?? 'GET';
  if (path === '/api/auth/refresh') return Response.json({ accessToken: 'test-token', expiresAt: '', user: {
    id: 'admin', firstName: 'Admin', lastName: 'Prueba', email: 'admin@example.test', roles: [], permissions,
  } });
  if (!path.startsWith('/api/admin/patients')) throw new Error(`Petición inesperada: ${method} ${path}`);
  if (failure?.method === method) return Response.json({ title: failure.title }, { status: failure.status });
  const body = init?.body ? JSON.parse(String(init.body)) : {};
  if (path === '/api/admin/patients') {
    if (method === 'GET') {
      if (listWait) await listWait;
      const page = Number(url.searchParams.get('page'));
      return Response.json({ items: patients.slice((page - 1) * 10, page * 10), total: patients.length, page, pageSize: 10 });
    }
    if (method === 'POST') {
      const created = { ...basePatient, ...body, id: 'patient-new' };
      patients = [...patients, created]; return Response.json(created, { status: 201 });
    }
  }
  const id = path.split('/')[4]; const patient = patients.find(p => p.id === id);
  if (!patient) return Response.json({ title: 'Paciente inexistente.' }, { status: 404 });
  if (method === 'GET') return Response.json(patient);
  if (method === 'PUT' || method === 'PATCH') {
    Object.assign(patient, body);
    return method === 'PATCH' ? new Response(null, { status: 204 }) : Response.json(patient);
  }
  throw new Error(`Petición inesperada: ${method} ${path}`);
});
beforeEach(() => {
  setSession(null); window.history.replaceState(null, '', '#/patients');
  patients = [structuredClone(basePatient)]; failure = null; listWait = null;
  permissions = ['patients.view', 'patients.create', 'patients.update', 'patients.disable'];
  fetchMock.mockClear(); vi.stubGlobal('fetch', fetchMock);
});
const writes = (method: string) => fetchMock.mock.calls.filter(([, init]) => init?.method === method && String(init.body ?? '').includes('firstName'));

it('lista datos clínicos y distingue pacientes con y sin cuenta', async () => {
  patients.push({ ...basePatient, id: 'patient-2', firstName: 'Luis', userId: '10000000-0000-0000-0000-000000000001' });
  render(<App />);
  await screen.findByText('Ana Paciente');
  const table = within(screen.getByRole('table'));
  expect(table.getByText('Paciente clínico sin cuenta')).toBeTruthy();
  expect(table.getByText('Cuenta vinculada')).toBeTruthy();
  expect(table.getAllByText('CI-123')).toHaveLength(2);
  expect(table.getAllByText('1995-05-10')).toHaveLength(2);
  expect(table.getAllByText('70000000')).toHaveLength(2);
  expect(table.getAllByText('clinical@example.test')).toHaveLength(2);
  expect(screen.getByRole('link', { name: 'Pacientes' }).getAttribute('aria-current')).toBe('page');
});
it('crea paciente sin User ni contraseña', async () => {
  render(<App />); const user = userEvent.setup();
  await user.click(await screen.findByRole('button', { name: 'Crear paciente' }));
  await user.type(screen.getByLabelText('Nombre'), 'María'); await user.type(screen.getByLabelText('Apellido'), 'Nueva');
  expect(screen.queryByLabelText(/contraseña/i)).toBeNull();
  await user.click(screen.getByRole('button', { name: 'Guardar paciente' }));
  expect(await screen.findByText('María Nueva')).toBeTruthy();
  const body = JSON.parse(String(writes('POST')[0][1]?.body));
  expect(body).toMatchObject({ firstName: 'María', lastName: 'Nueva', userId: null, isActive: true });
  expect(body.password).toBeUndefined();
});
it('edita datos y conserva la cuenta vinculada', async () => {
  patients[0].userId = '10000000-0000-0000-0000-000000000001';
  render(<App />); const user = userEvent.setup();
  await user.click(await screen.findByRole('button', { name: 'Editar' }));
  const name = await screen.findByLabelText('Nombre'); await user.clear(name); await user.type(name, 'Editada');
  expect((screen.getByLabelText('Notas') as HTMLTextAreaElement).value).toBe('Nota sintética');
  await user.click(screen.getByRole('button', { name: 'Guardar paciente' }));
  expect(await screen.findByText('Editada Paciente')).toBeTruthy();
  expect(JSON.parse(String(writes('PUT')[0][1]?.body))).toMatchObject({ userId: patients[0].userId, notes: 'Nota sintética' });
});
it.each([true, false])('cambia estado desde isActive=%s con permiso', async active => {
  patients[0].isActive = active;
  render(<App />);
  await userEvent.click(await screen.findByRole('button', { name: active ? 'Desactivar' : 'Activar' }));
  expect(await screen.findByRole('button', { name: active ? 'Activar' : 'Desactivar' })).toBeTruthy();
  const call = fetchMock.mock.calls.find(([, init]) => init?.method === 'PATCH');
  expect(JSON.parse(String(call?.[1]?.body))).toEqual({ isActive: !active });
});
it.each([
  ['patients.create', 'Crear paciente'], ['patients.update', 'Editar'], ['patients.disable', 'Desactivar'],
])('oculta acción sin %s', async (permission, button) => {
  permissions = permissions.filter(p => p !== permission); render(<App />);
  await screen.findByText('Ana Paciente'); expect(screen.queryByRole('button', { name: button })).toBeNull();
});
it.each([true, false])('sin disable conserva estado %s en edición', async active => {
  permissions = permissions.filter(p => p !== 'patients.disable'); patients[0].isActive = active;
  render(<App />); const user = userEvent.setup();
  await user.click(await screen.findByRole('button', { name: 'Editar' }));
  const status = await screen.findByLabelText('Paciente activo') as HTMLInputElement;
  expect(status.disabled).toBe(true); expect(status.checked).toBe(active);
  await user.click(screen.getByRole('button', { name: 'Guardar paciente' }));
  await screen.findByText('Paciente guardado.');
  expect(JSON.parse(String(writes('PUT')[0][1]?.body)).isActive).toBe(active);
});
it.each([
  [400, 'El usuario debe tener el rol PATIENT activo.'], [403, 'No tiene permiso para cambiar el estado.'],
  [404, 'Usuario inexistente.'], [409, 'CI ya registrado.'],
])('muestra error %s al guardar y conserva el formulario', async (status, title) => {
  failure = { method: 'PUT', status, title }; render(<App />); const user = userEvent.setup();
  await user.click(await screen.findByRole('button', { name: 'Editar' }));
  await screen.findByLabelText('Nombre'); await user.click(screen.getByRole('button', { name: 'Guardar paciente' }));
  expect((await screen.findByRole('alert')).textContent).toBe(title);
  expect(screen.getByDisplayValue('Ana')).toBeTruthy();
});
it('muestra loading hasta obtener pacientes', async () => {
  let release = () => {}; listWait = new Promise<void>(resolve => { release = resolve; });
  render(<App />); expect(await screen.findByText('Cargando pacientes…')).toBeTruthy();
  expect(screen.queryByRole('table')).toBeNull(); await act(async () => release());
  expect(await screen.findByText('Ana Paciente')).toBeTruthy();
});
it('pagina hacia adelante y atrás', async () => {
  patients = Array.from({ length: 11 }, (_, index) => ({ ...basePatient, id: `p-${index}`, firstName: `Paciente ${index}` }));
  render(<App />); const user = userEvent.setup(); await screen.findByText('11 pacientes · Página 1');
  await user.click(screen.getByRole('button', { name: 'Siguiente' }));
  expect(await screen.findByText('Paciente 10 Paciente')).toBeTruthy();
  expect((screen.getByRole('button', { name: 'Siguiente' }) as HTMLButtonElement).disabled).toBe(true);
  await user.click(screen.getByRole('button', { name: 'Anterior' }));
  expect(await screen.findByText('Paciente 0 Paciente')).toBeTruthy();
});
it('restringe ruta y navegación sin patients.view', async () => {
  permissions = ['patients.create', 'patients.update', 'patients.disable']; render(<App />);
  expect(await screen.findByText('Acceso restringido')).toBeTruthy();
  expect(screen.queryByRole('link', { name: 'Pacientes' })).toBeNull();
  expect(fetchMock.mock.calls.some(([url]) => String(url).includes('/api/admin/patients'))).toBe(false);
});
it('presenta error de listado sin mostrar datos vacíos como exitosos', async () => {
  failure = { method: 'GET', status: 403, title: 'Acceso denegado.' }; render(<App />);
  expect((await screen.findByRole('alert')).textContent).toBe('Acceso denegado.');
  expect(screen.queryByRole('table')).toBeNull(); expect(screen.queryByText('Cargando pacientes…')).toBeNull();
});
