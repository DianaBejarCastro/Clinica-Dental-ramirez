import { act, fireEvent, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, expect, it, vi } from 'vitest';
import App from '../App';
import { setSession } from '../services/api';
import { localToday } from '../modules/patient/PatientFields';

const endpoint = '/api/admin/dental-assistants';
const initial = { id: 'assistant', userId: 'person', firstName: 'Ana', lastName: 'Asistente', documentNumber: 'CI-TEST', birthDate: '1990-01-01', phone: '70000000', gender: null, address: null, email: 'ana@example.test', isActive: true, userIsActive: true, canLogin: true };
let assistants: typeof initial[];
let permissions: string[];
let fail = 0;
let listWait: Promise<void> | null;
const fetchMock = vi.fn(async (input: string | URL | Request, init?: RequestInit) => {
  const path = new URL(String(input)).pathname; const method = init?.method ?? 'GET';
  if (path === '/api/auth/refresh') return Response.json({ accessToken: 'token', user: { id: 'admin', firstName: 'Admin', lastName: 'Test', email: 'admin@example.test', roles: [], permissions } });
  if (path === '/api/admin/doctors') return Response.json({ items: [], total: 0 });
  if (!path.startsWith(endpoint)) throw new Error(`Unexpected ${path}`);
  if (method === 'POST' || method === 'PUT') {
    if (fail) return Response.json({ title: 'El CI ya se encuentra registrado.' }, { status: fail });
    const { password: _password, confirmPassword: _confirm, ...body } = JSON.parse(String(init?.body));
    if (method === 'POST') { const created = { ...initial, ...body, id: 'new', userId: 'new-user' }; assistants.push(created); return Response.json(created, { status: 201 }); }
    assistants[0] = { ...assistants[0], ...body }; return Response.json(assistants[0]);
  }
  if (path === endpoint) { if (listWait) await listWait; return Response.json({ items: assistants, total: assistants.length, page: 1, pageSize: 10 }); }
  return Response.json(assistants[0]);
});
beforeEach(() => {
  setSession(null); window.location.hash = '/doctors'; assistants = [structuredClone(initial)];
  permissions = ['doctors.view', 'doctors.create', 'doctors.update']; fail = 0; listWait = null;
  fetchMock.mockClear(); vi.stubGlobal('fetch', fetchMock);
});
async function open() { render(<App />); await userEvent.click(await screen.findByRole('button', { name: 'Crear asistente dental' })); }
function fill() {
  const values = { firstName: 'Nueva', lastName: 'Asistente', documentNumber: 'CI-NEW', birthDate: '2000-01-01', phone: '71111111', email: 'new@example.test', password: 'TestPassword123!', confirmPassword: 'TestPassword123!' };
  for (const [name, value] of Object.entries(values)) fireEvent.change(document.querySelector(`[name="${name}"]`)!, { target: { value } });
}
it('botones juntos, listado separado y formulario sin campos de doctor', async () => {
  await open();
  const create = screen.getByRole('button', { name: 'Crear asistente dental' });
  expect(create.parentElement).toBe(screen.getByRole('button', { name: 'Crear doctor' }).parentElement);
  expect(await screen.findByRole('table', { name: 'Asistentes dentales' })).toBeTruthy();
  expect(screen.getByText('No hay doctores registrados.')).toBeTruthy();
  for (const label of ['Matrícula profesional', 'Foto URL', 'Biografía', 'Usuario existente (ID)']) expect(screen.queryByLabelText(label)).toBeNull();
  for (const label of ['Nombres', 'Apellidos', 'Número de documento', 'Fecha de nacimiento', 'Teléfono', 'Email de acceso', 'Contraseña', 'Confirmar contraseña'])
    expect((screen.getByLabelText(label, { exact: true }) as HTMLInputElement).required).toBe(true);
  expect((screen.getByLabelText('Fecha de nacimiento') as HTMLInputElement).max).toBe(localToday());
  expect(screen.getByLabelText('Nombres').closest('form')?.classList.contains('editor')).toBe(true);
  expect(screen.getByRole('table', { name: 'Asistentes dentales' }).parentElement?.classList.contains('table-card')).toBe(true);
});
it('crea asistente y actualiza listado', async () => {
  await open(); fill(); await userEvent.click(screen.getByRole('button', { name: 'Guardar asistente dental' }));
  expect(await screen.findByText('Nueva Asistente')).toBeTruthy();
  const write = fetchMock.mock.calls.find(([url, init]) => String(url).includes(endpoint) && init?.method === 'POST'); const body = JSON.parse(String(write?.[1]?.body));
  expect(body.email).toBe('new@example.test'); expect(body.userId).toBeUndefined(); expect(body.roles).toBeUndefined(); expect(body.professionalLicense).toBeUndefined();
});
it('editar conserva UserId y no pide ni envía credenciales', async () => {
  render(<App />); await userEvent.click(await screen.findByRole('button', { name: 'Editar asistente' }));
  const name = await screen.findByLabelText('Nombres'); fireEvent.change(name, { target: { value: 'Editada' } });
  expect(screen.queryByLabelText('Contraseña', { exact: true })).toBeNull(); expect(screen.queryByLabelText('Email de acceso')).toBeNull();
  await userEvent.click(screen.getByRole('button', { name: 'Guardar asistente dental' })); await screen.findByText('Editada Asistente');
  const body = JSON.parse(String(fetchMock.mock.calls.find(([, init]) => init?.method === 'PUT')?.[1]?.body));
  expect(body.userId).toBeUndefined(); expect(body.password).toBeUndefined(); expect(assistants[0].userId).toBe('person');
});
it.each(['future', 'confirmation'])('bloquea %s sin enviar', async invalid => {
  await open(); fill();
  fireEvent.change(document.querySelector(`[name="${invalid === 'future' ? 'birthDate' : 'confirmPassword'}"]`)!, { target: { value: invalid === 'future' ? '2999-01-01' : 'Different123!' } });
  fireEvent.submit(screen.getByRole('button', { name: 'Guardar asistente dental' }).closest('form')!);
  expect(screen.getByRole('alert').textContent).toContain(invalid === 'future' ? 'futura' : 'no coinciden');
  expect(fetchMock.mock.calls.some(([url, init]) => String(url).includes(endpoint) && init?.method === 'POST')).toBe(false);
});
it.each(['doctors.create', 'doctors.update'])('oculta acciones sin %s', async permission => {
  permissions = permissions.filter(p => p !== permission); render(<App />); await screen.findByText('Ana Asistente');
  expect(screen.queryByRole('button', { name: permission === 'doctors.create' ? 'Crear asistente dental' : 'Editar asistente' })).toBeNull();
});
it('sin doctors.view no consulta asistentes', async () => {
  permissions = ['doctors.create']; render(<App />); await screen.findByText('Acceso restringido');
  expect(fetchMock.mock.calls.some(([url]) => String(url).includes(endpoint))).toBe(false);
});
it('409 conserva formulario y muestra error', async () => {
  fail = 409; await open(); fill(); await userEvent.click(screen.getByRole('button', { name: 'Guardar asistente dental' }));
  expect((await screen.findByRole('alert')).textContent).toContain('CI ya se encuentra registrado'); expect(screen.getByDisplayValue('CI-NEW')).toBeTruthy();
});
it('loading y lista vacía', async () => {
  assistants = []; let release = () => {}; listWait = new Promise<void>(resolve => { release = resolve; }); render(<App />);
  await screen.findByText('Cargando asistentes dentales…'); await act(async () => release());
  expect(await screen.findByText('No hay asistentes dentales en esta página.')).toBeTruthy();
  expect(within(screen.getByRole('region', { name: 'Asistentes dentales' })).queryByRole('table')).toBeNull();
});
