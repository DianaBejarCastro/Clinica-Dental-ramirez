import { fireEvent, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, expect, it, vi } from 'vitest';
import App from '../App';
import { setSession } from '../services/api';
import { localToday } from '../modules/patient/PatientFields';

const catalog = [
  { id: 'admin', code: 'ADMINISTRATOR', name: 'Administrador' }, { id: 'cashier', code: 'CASHIER', name: 'Cajero' },
  { id: 'secretary', code: 'SECRETARY', name: 'Secretaria' }, { id: 'patient', code: 'PATIENT', name: 'Paciente' },
  { id: 'doctor', code: 'DOCTOR', name: 'Doctor' }, { id: 'assistant', code: 'DENTAL_ASSISTANT', name: 'Asistente dental' },
  { id: 'custom', code: 'CUSTOM', name: 'Coordinación' },
].map(r => ({ ...r, isActive: true, isSystem: r.id !== 'custom' }));
let permissions: string[]; let sessionRoles: string[]; let user: ReturnType<typeof initial>; let failure = 0;
function initial() { return { id: 'person', firstName: 'Ana', lastName: 'Persona', documentNumber: 'CI-123', birthDate: '1990-01-01', email: 'ana@example.test', phone: '70000000', canLogin: true, isActive: true, roles: [catalog[3]] }; }
const fetchMock = vi.fn(async (input: string | URL | Request, init?: RequestInit) => {
  const url = new URL(String(input)); const path = url.pathname; const method = init?.method ?? 'GET';
  if (path.endsWith('/refresh')) return Response.json({ accessToken: 'token', user: { id: 'actor', firstName: 'Admin', lastName: 'Test', email: 'admin@example.test', roles: sessionRoles, permissions } });
  if (path === '/api/admin/roles') return Response.json(catalog);
  if (method === 'POST' || method === 'PUT') {
    if (failure) return Response.json({ title: 'El CI ya se encuentra registrado.' }, { status: failure });
    const body = JSON.parse(String(init?.body));
    const role = path.endsWith('/doctor-profile') ? catalog[4] : path.endsWith('/patient-profile') ? catalog[3] : path.endsWith('/dental-assistant-profile') ? catalog[5] : null;
    if (role) user = { ...user, roles: [...user.roles, role] };
    else if (path.endsWith('/roles')) user = { ...user, roles: [...user.roles.filter(r => ['PATIENT', 'DOCTOR', 'DENTAL_ASSISTANT'].includes(r.code)), ...catalog.filter(r => body.roleIds.includes(r.id))] };
    else user = { ...user, ...body };
    return Response.json(user, { status: method === 'POST' && !role ? 201 : 200 });
  }
  if (path === '/api/admin/users/person') return Response.json(user);
  if (path === '/api/patient/me') return Response.json(user);
  if (path === '/api/admin/dashboard') return Response.json({ totalUsers: 1 });
  return Response.json({ items: [user], total: 1 });
});
beforeEach(() => {
  setSession(null); window.location.hash = '/users'; user = initial(); failure = 0; sessionRoles = ['ADMINISTRATOR'];
  permissions = ['users.view', 'users.create', 'users.update', 'users.disable', 'roles.view', 'roles.assign_permissions', 'permissions.view', 'doctors.create', 'patients.create'];
  fetchMock.mockClear(); vi.stubGlobal('fetch', fetchMock);
});
function fillStaff() {
  for (const [name, value] of Object.entries({ firstName: 'Nueva', lastName: 'Persona', documentNumber: 'NEW-CI', birthDate: '2000-01-01', phone: '71111111', email: 'new@example.test', password: 'Password12345!', confirmPassword: 'Password12345!' }))
    fireEvent.change(document.querySelector(`[name="${name}"]`)!, { target: { value } });
}
it('menú Administración tiene el orden exacto y Usuarios sigue a Secretarias', async () => {
  render(<App />); const nav = within(await screen.findByRole('navigation'));
  expect(nav.getAllByRole('link').map(a => a.textContent)).toEqual(['Resumen', 'Administradores', 'Cajeros', 'Secretarias', 'Usuarios', 'Roles', 'Permisos']);
  expect(screen.queryByText('Rol de prueba')).toBeNull();
});
it.each([['administrators', 'Administradores', 'administrador'], ['cashiers', 'Cajeros', 'cajero'], ['secretaries', 'Secretarias', 'secretaria']])('lista y crea %s con datos compartidos', async (route, title, singular) => {
  window.location.hash = `/${route}`; render(<App />); await screen.findByRole('heading', { name: title }); await screen.findByText('Ana Persona');
  await userEvent.click(screen.getByRole('button', { name: `Crear ${singular}` }));
  expect(screen.queryByRole('checkbox')).toBeNull();
  expect((screen.getByLabelText('Fecha de nacimiento') as HTMLInputElement).max).toBe(localToday());
  expect(screen.getByLabelText('Nombres').closest('form')?.classList.contains('editor')).toBe(true);
  fillStaff(); await userEvent.click(screen.getByRole('button', { name: `Guardar ${singular}` })); await screen.findByText('Nueva Persona');
  const write = fetchMock.mock.calls.find(([url, init]) => String(url).endsWith(`/api/admin/${route}`) && init?.method === 'POST');
  const body = JSON.parse(String(write?.[1]?.body)); expect(body.documentNumber).toBe('NEW-CI'); expect(body.roleIds).toBeUndefined();
});
it('gestiona múltiples roles simples sin casillas de dominio', async () => {
  render(<App />); await userEvent.click(await screen.findByRole('button', { name: 'Asignar roles' }));
  for (const label of ['Paciente', 'Doctor', 'Asistente dental']) expect(screen.queryByRole('checkbox', { name: label })).toBeNull();
  await userEvent.click(screen.getByRole('checkbox', { name: 'Administrador' })); await userEvent.click(screen.getByRole('checkbox', { name: 'Cajero' }));
  await userEvent.click(screen.getByRole('button', { name: 'Guardar roles' }));
  expect(await screen.findByText('Paciente, Administrador, Cajero')).toBeTruthy();
  expect(screen.queryByRole('button', { name: 'Crear usuario' })).toBeNull();
});
it('añade Doctor a PATIENT con el mismo usuario sin solicitar identidad ni contraseña', async () => {
  render(<App />); await userEvent.click(await screen.findByRole('button', { name: 'Añadir rol Doctor' }));
  expect(screen.queryByLabelText('Nombres')).toBeNull(); expect(screen.queryByLabelText('Contraseña')).toBeNull();
  fireEvent.change(screen.getByLabelText('Matrícula profesional'), { target: { value: 'LIC-123' } });
  await userEvent.click(screen.getByRole('button', { name: 'Confirmar rol Doctor' }));
  expect(await screen.findByText('Paciente, Doctor')).toBeTruthy(); expect(user.id).toBe('person');
  expect(screen.queryByRole('button', { name: 'Añadir rol Doctor' })).toBeNull();
  expect(fetchMock.mock.calls.some(([url, init]) => String(url).endsWith('/api/admin/users/person/doctor-profile') && init?.method === 'POST')).toBe(true);
});
it('añade Paciente pidiendo solo datos específicos', async () => {
  user.roles = [catalog[2]]; render(<App />); await userEvent.click(await screen.findByRole('button', { name: 'Añadir rol Paciente' }));
  expect(screen.queryByLabelText('CI')).toBeNull();
  fireEvent.change(screen.getByLabelText('Contacto de emergencia'), { target: { value: 'Familiar' } });
  fireEvent.change(screen.getByLabelText('Teléfono de emergencia'), { target: { value: '70000001' } });
  await userEvent.selectOptions(screen.getByLabelText('Grupo sanguíneo'), 'UNKNOWN');
  await userEvent.click(screen.getByRole('button', { name: 'Confirmar rol Paciente' })); expect(await screen.findByText('Secretaria, Paciente')).toBeTruthy();
});
it('añade asistente dental sin crear otra cuenta', async () => {
  render(<App />); await userEvent.click(await screen.findByRole('button', { name: 'Añadir rol Asistente dental' }));
  expect(screen.queryByLabelText('Contraseña')).toBeNull(); await userEvent.click(screen.getByRole('button', { name: 'Confirmar rol Asistente dental' }));
  expect(await screen.findByText('Paciente, Asistente dental')).toBeTruthy(); expect(user.id).toBe('person');
});
it('error de dominio conserva formulario y roles', async () => {
  failure = 409; render(<App />); await userEvent.click(await screen.findByRole('button', { name: 'Añadir rol Asistente dental' }));
  await userEvent.click(screen.getByRole('button', { name: 'Confirmar rol Asistente dental' }));
  expect((await screen.findByRole('alert')).textContent).toContain('CI ya se encuentra registrado'); expect(user.roles.map(r => r.code)).toEqual(['PATIENT']);
});
it('búsqueda explícita y edición común sin credenciales', async () => {
  render(<App />); await screen.findByText('Ana Persona');
  await userEvent.type(screen.getByLabelText('Buscar por nombre, documento o email'), 'CI-123'); await userEvent.click(screen.getByRole('button', { name: 'Buscar' }));
  await userEvent.click(await screen.findByRole('button', { name: 'Editar' }));
  expect(screen.queryByLabelText('Contraseña')).toBeNull(); expect((screen.getByLabelText('Número de documento') as HTMLInputElement).value).toBe('CI-123');
  fireEvent.change(screen.getByLabelText('Nombres'), { target: { value: 'Editada' } }); await userEvent.click(screen.getByRole('button', { name: 'Guardar usuario' }));
  expect(await screen.findByText('Editada Persona')).toBeTruthy();
  expect(fetchMock.mock.calls.some(([url]) => String(url).includes('q=CI-123'))).toBe(true);
});
it.each(['roles.assign_permissions', 'users.update'])('sin %s no ofrece añadir perfiles', async permission => {
  permissions = permissions.filter(p => p !== permission); render(<App />); await screen.findByText('Ana Persona');
  expect(screen.queryByRole('button', { name: /Añadir rol/ })).toBeNull(); expect(screen.queryByRole('button', { name: 'Asignar roles' })).toBeNull();
});
it.each(['administrators', 'cashiers', 'secretaries'])('ruta %s protegida', async route => {
  permissions = []; window.location.hash = `/${route}`; render(<App />); expect(await screen.findByText('Acceso restringido')).toBeTruthy();
  expect(fetchMock.mock.calls.some(([url]) => String(url).includes('/api/admin/'))).toBe(false);
});
it('PATIENT más un rol de equipo no queda encerrado en el portal', async () => {
  sessionRoles = ['PATIENT', 'ADMINISTRATOR']; render(<App />); expect(await screen.findByText('Ana Persona')).toBeTruthy();
  expect(window.location.hash).toBe('#/users'); expect(screen.getByRole('navigation')).toBeTruthy();
});
it('sin permiso de asignar roles no permite altas con rol automático', async () => {
  permissions = permissions.filter(p => p !== 'roles.assign_permissions'); window.location.hash = '/cashiers'; render(<App />);
  await screen.findByRole('heading', { name: 'Cajeros' }); expect(screen.queryByRole('button', { name: 'Crear cajero' })).toBeNull();
});
it('usuario multirrol puede abrir su portal y volver al espacio de trabajo', async () => {
  sessionRoles = ['PATIENT', 'DOCTOR']; render(<App />); await screen.findByText('Ana Persona');
  await userEvent.click(screen.getByRole('link', { name: 'Mi portal de paciente' }));
  expect(await screen.findByRole('heading', { name: 'Mi cuenta' })).toBeTruthy();
  expect(screen.getByRole('link', { name: 'Espacio de trabajo' }).getAttribute('href')).toBe('#/');
});
it('rol legado sin perfil ofrece completar datos explícitamente', async () => {
  Object.assign(user, { hasPatientProfile: false }); render(<App />);
  await userEvent.click(await screen.findByRole('button', { name: 'Completar perfil Paciente' }));
  expect(screen.getByRole('heading', { name: 'Completar perfil Paciente' })).toBeTruthy();
  expect((screen.getByLabelText('Contacto de emergencia') as HTMLInputElement).required).toBe(true);
  expect(fetchMock.mock.calls.some(([url, init]) => String(url).endsWith('/patient-profile') && init?.method === 'POST')).toBe(false);
});
