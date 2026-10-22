import { beforeEach, expect, it, vi } from 'vitest';
import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import App from '../App';
import { setSession } from '../services/api';
import type { Assignment, Doctor, DoctorSchedule } from '../modules/admin/types';

const doctorId = '10000000-0000-0000-0000-000000000001';
const specialties: Assignment[] = [
  { id: '20000000-0000-0000-0000-000000000001', code: 'GENERAL', name: 'Odontología general', isActive: true },
  { id: '20000000-0000-0000-0000-000000000002', code: 'ORTODONCIA', name: 'Ortodoncia', isActive: true },
  { id: '20000000-0000-0000-0000-000000000003', code: 'INACTIVA', name: 'Especialidad inactiva', isActive: false },
];
const centers: Assignment[] = [
  { id: '30000000-0000-0000-0000-000000000001', code: 'CENTRAL', name: 'Sede Central', isActive: true },
  { id: '30000000-0000-0000-0000-000000000002', code: 'SUR', name: 'Sede Sur', isActive: true },
  { id: '30000000-0000-0000-0000-000000000003', code: 'INACTIVA', name: 'Sede inactiva', isActive: false },
];
const initialDoctor: Doctor = {
  id: doctorId, userId: '40000000-0000-0000-0000-000000000001',
  user: { id: '40000000-0000-0000-0000-000000000001', firstName: 'Lucía', lastName: 'Ramírez', email: 'lucia@example.test', isActive: true },
  professionalLicense: 'LP-123', documentNumber: '1234567', birthDate: '1985-04-10', gender: null,
  phone: '70000000', address: null, photoUrl: null, bio: 'Atención odontológica integral.',
  isActive: true, createdAt: '2026-09-30T00:00:00Z', updatedAt: null,
  specialties: [specialties[0]], centers: [centers[0]],
};
const schedule: DoctorSchedule = {
  id: '50000000-0000-0000-0000-000000000001', doctorId, centerId: centers[0].id,
  centerName: centers[0].name, dayOfWeek: 1, startTime: '09:00:00', endTime: '12:00:00',
  hasBreak: false, breakStart: null, breakEnd: null, isActive: true,
  createdAt: '2026-09-30T00:00:00Z', updatedAt: null,
};
let doctor: Doctor;
let permissions: string[];
let rejectAssignment = false;
let createdDoctor: Doctor | null;
let listFailure: boolean;
let listWait: Promise<void> | null;
const fetchMock = vi.fn(async (input: string | URL | Request, init?: RequestInit) => {
  const url = new URL(String(input));
  const path = url.pathname;
  const method = init?.method ?? 'GET';
  let status = 200;
  let data: unknown;
  if (path === '/api/admin/dental-assistants') return Response.json({ items: [], total: 0, page: 1, pageSize: 10 });
  if (path === '/api/auth/refresh') {
    data = { accessToken: 'test-access', expiresAt: '', user: { id: 'admin', firstName: 'Ana', lastName: 'Prueba', email: 'admin@example.test', roles: [], permissions } };
  } else if (path === '/api/admin/doctors' && method === 'GET') {
    if (listWait) await listWait;
    status = listFailure ? 503 : 200;
    const items = createdDoctor ? [doctor, createdDoctor] : [doctor];
    data = listFailure ? { title: 'No se pudo cargar los doctores.' } : { items, total: items.length, page: 1, pageSize: 10 };
  } else if (path === '/api/admin/doctors' && method === 'POST') {
    const profile = JSON.parse(String(init?.body));
    const assignedUserId = '40000000-0000-0000-0000-000000000002';
    createdDoctor = {
      ...initialDoctor, ...profile, id: '10000000-0000-0000-0000-000000000002', userId: assignedUserId,
      user: { id: assignedUserId, firstName: profile.firstName, lastName: profile.lastName, email: profile.email, isActive: true },
      specialties: [], centers: [],
    };
    data = createdDoctor;
    status = 201;
  } else if (path === `/api/admin/doctors/${doctorId}`) {
    if (method === 'PUT') doctor = { ...doctor, ...JSON.parse(String(init?.body)) };
    data = doctor;
  } else if (path === `/api/admin/doctors/${doctorId}/status` && method === 'PATCH') {
    doctor = { ...doctor, isActive: JSON.parse(String(init?.body)).isActive };
    status = 204;
  } else if (path === `/api/admin/doctors/${doctorId}/schedules`) {
    data = [schedule];
  } else if (path === '/api/admin/specialties') {
    data = specialties;
  } else if (path === '/api/admin/centers') {
    data = centers;
  } else if (path === `/api/admin/doctors/${doctorId}/specialties` || path === `/api/admin/doctors/${doctorId}/centers`) {
    const kind = path.endsWith('/specialties') ? 'specialties' : 'centers';
    if (method === 'PUT') {
      if (rejectAssignment) {
        status = 400;
        data = { title: 'La selección contiene una opción inactiva.' };
      } else {
        const body = JSON.parse(String(init?.body));
        const ids: string[] = kind === 'specialties' ? body.specialtyIds : body.centerIds;
        doctor = { ...doctor, [kind]: (kind === 'specialties' ? specialties : centers).filter(item => ids.includes(item.id)) };
        status = 204;
      }
    } else data = doctor[kind];
  } else throw new Error(`Solicitud inesperada: ${method} ${path}`);
  return { ok: status >= 200 && status < 300, status, json: async () => data } as Response;
});

function requestBody(path: string, method: string) {
  const request = fetchMock.mock.calls.find(([input, init]) => new URL(String(input)).pathname === path && init?.method === method);
  expect(request).toBeDefined();
  return JSON.parse(String(request?.[1]?.body));
}

beforeEach(() => {
  setSession(null);
  doctor = structuredClone(initialDoctor);
  permissions = ['doctors.view', 'doctors.update', 'specialties.view', 'centers.view'];
  rejectAssignment = false;
  createdDoctor = null;
  listFailure = false;
  listWait = null;
  window.location.hash = '/doctors';
  fetchMock.mockClear();
  vi.stubGlobal('fetch', fetchMock);
});

it('lista doctores con matrícula, especialidades y sucursales', async () => {
  render(<App />);
  expect(await screen.findByText('Lucía Ramírez')).toBeTruthy();
  const table = screen.getByRole('table');
  expect(within(table).getByText('LP-123')).toBeTruthy();
  expect(within(table).getByText('Odontología general')).toBeTruthy();
  expect(within(table).getByText('Sede Central')).toBeTruthy();
  expect(screen.getByText('1 doctores · Página 1')).toBeTruthy();
});

it('muestra detalle del doctor sin consultar horarios si falta schedules.view', async () => {
  render(<App />);
  await userEvent.click(await screen.findByRole('button', { name: 'Detalle' }));
  expect(await screen.findByRole('heading', { name: 'Lucía Ramírez' })).toBeTruthy();
  expect(screen.getByText('Atención odontológica integral.')).toBeTruthy();
  expect(screen.getByText('Estado de la cuenta')).toBeTruthy();
  expect(screen.queryByRole('link', { name: 'Ver semana del doctor' })).toBeNull();
  expect(fetchMock.mock.calls.some(([url]) => String(url).endsWith('/schedules'))).toBe(false);
});

it('muestra horarios del detalle y el enlace a la semana con permiso', async () => {
  permissions.push('schedules.view');
  render(<App />);
  await userEvent.click(await screen.findByRole('button', { name: 'Detalle' }));
  const link = await screen.findByRole('link', { name: 'Ver semana del doctor' });
  expect(link.getAttribute('href')).toBe(`#/schedules?doctor=${doctorId}`);
  expect(screen.getByText(/Lunes · Sede Central · 09:00–12:00/)).toBeTruthy();
});

it('asigna múltiples especialidades conservando la selección existente', async () => {
  render(<App />);
  const user = userEvent.setup();
  await user.click(await screen.findByRole('button', { name: 'Especialidades' }));
  const current = await screen.findByRole('checkbox', { name: 'Odontología general' });
  expect((current as HTMLInputElement).checked).toBe(true);
  expect(screen.queryByRole('checkbox', { name: 'Especialidad inactiva' })).toBeNull();
  await user.click(screen.getByRole('checkbox', { name: 'Ortodoncia' }));
  await user.click(screen.getByRole('checkbox', { name: 'Ortodoncia' }));
  await user.click(screen.getByRole('checkbox', { name: 'Ortodoncia' }));
  await user.click(screen.getByRole('button', { name: 'Guardar especialidades' }));
  await screen.findByText('Asignaciones actualizadas.');
  expect(requestBody(`/api/admin/doctors/${doctorId}/specialties`, 'PUT')).toEqual({ specialtyIds: specialties.slice(0, 2).map(item => item.id) });
  expect(within(screen.getByRole('table')).getByText('Odontología general, Ortodoncia')).toBeTruthy();
});

it('asigna múltiples sucursales conservando la selección existente', async () => {
  render(<App />);
  const user = userEvent.setup();
  await user.click(await screen.findByRole('button', { name: 'Sucursales' }));
  expect(((await screen.findByRole('checkbox', { name: 'Sede Central' })) as HTMLInputElement).checked).toBe(true);
  expect(screen.queryByRole('checkbox', { name: 'Sede inactiva' })).toBeNull();
  await user.click(screen.getByRole('checkbox', { name: 'Sede Sur' }));
  await user.click(screen.getByRole('checkbox', { name: 'Sede Sur' }));
  await user.click(screen.getByRole('checkbox', { name: 'Sede Sur' }));
  await user.click(screen.getByRole('button', { name: 'Guardar sucursales' }));
  await screen.findByText('Asignaciones actualizadas.');
  expect(requestBody(`/api/admin/doctors/${doctorId}/centers`, 'PUT')).toEqual({ centerIds: centers.slice(0, 2).map(item => item.id) });
  expect(within(screen.getByRole('table')).getByText('Sede Central, Sede Sur')).toBeTruthy();
});

it('oculta asignaciones si falta permiso para ver sus catálogos', async () => {
  permissions = ['doctors.view', 'doctors.update'];
  render(<App />);
  await screen.findByText('Lucía Ramírez');
  expect(screen.getByRole('button', { name: 'Editar' })).toBeTruthy();
  expect(screen.queryByRole('button', { name: 'Especialidades' })).toBeNull();
  expect(screen.queryByRole('button', { name: 'Sucursales' })).toBeNull();
  expect(fetchMock.mock.calls.some(([url]) => /\/admin\/(specialties|centers)$/.test(String(url)))).toBe(false);
});

it('mantiene el detalle disponible y oculta mutaciones con acceso de lectura', async () => {
  permissions = ['doctors.view', 'specialties.view', 'centers.view'];
  render(<App />);
  expect(await screen.findByRole('button', { name: 'Detalle' })).toBeTruthy();
  for (const name of ['Crear doctor', 'Editar', 'Especialidades', 'Sucursales', 'Desactivar']) {
    expect(screen.queryByRole('button', { name })).toBeNull();
  }
});

it.each([true, false])('deshabilita el estado sin doctors.disable y preserva isActive=%s al editar', async isActive => {
  doctor.isActive = isActive;
  render(<App />);
  const user = userEvent.setup();
  await user.click(await screen.findByRole('button', { name: 'Editar' }));
  const status = screen.getByRole('checkbox', { name: 'Perfil profesional activo' }) as HTMLInputElement;
  expect(status.disabled).toBe(true);
  expect(status.checked).toBe(isActive);
  expect(screen.queryByRole('button', { name: /^(Activar|Desactivar)$/ })).toBeNull();
  await user.clear(screen.getByLabelText('Teléfono'));
  await user.type(screen.getByLabelText('Teléfono'), '71111111');
  await user.click(screen.getByRole('button', { name: 'Guardar doctor' }));
  await screen.findByText('Perfil profesional actualizado.');
  expect(requestBody(`/api/admin/doctors/${doctorId}`, 'PUT')).toMatchObject({ phone: '71111111', isActive });
});

it.each([true, false])('permite cambiar isActive=%s con doctors.disable mediante el endpoint de estado', async isActive => {
  doctor.isActive = isActive;
  permissions.push('doctors.disable');
  render(<App />);
  await userEvent.click(await screen.findByRole('button', { name: isActive ? 'Desactivar' : 'Activar' }));
  await screen.findByText('Estado profesional actualizado.');
  expect(requestBody(`/api/admin/doctors/${doctorId}/status`, 'PATCH')).toEqual({ isActive: !isActive });
  expect(screen.getByRole('button', { name: isActive ? 'Activar' : 'Desactivar' })).toBeTruthy();
});

it.each([
  ['Especialidades', 'Ortodoncia', 'Guardar especialidades'],
  ['Sucursales', 'Sede Sur', 'Guardar sucursales'],
])('conserva el formulario y presenta el rechazo de la API al asignar %s', async (button, choice, save) => {
  rejectAssignment = true;
  render(<App />);
  const user = userEvent.setup();
  await user.click(await screen.findByRole('button', { name: button }));
  await user.click(await screen.findByRole('checkbox', { name: choice }));
  await user.click(screen.getByRole('button', { name: save }));
  expect((await screen.findByRole('alert')).textContent).toContain('La selección contiene una opción inactiva.');
  await waitFor(() => expect((screen.getByRole('button', { name: save }) as HTMLButtonElement).disabled).toBe(false));
  expect((screen.getByRole('checkbox', { name: choice }) as HTMLInputElement).checked).toBe(true);
});

it.each([
  ['specialties', 'Especialidades', 'Odontología general', 'Guardar especialidades', 'specialtyIds'],
  ['centers', 'Sucursales', 'Sede Central', 'Guardar sucursales', 'centerIds'],
] as const)('quita la última asignación de %s y permite dejar la lista vacía', async (kind, button, choice, save, field) => {
  render(<App />);
  const user = userEvent.setup();
  await user.click(await screen.findByRole('button', { name: button }));
  const checkbox = await screen.findByRole('checkbox', { name: choice });
  expect((checkbox as HTMLInputElement).checked).toBe(true);
  await user.click(checkbox);
  await user.click(screen.getByRole('button', { name: save }));
  await screen.findByText('Asignaciones actualizadas.');
  expect(requestBody(`/api/admin/doctors/${doctorId}/${kind}`, 'PUT')).toEqual({ [field]: [] });
  expect(within(screen.getByRole('table')).queryByText(choice)).toBeNull();
  expect(within(screen.getByRole('table')).getByText('Sin asignaciones')).toBeTruthy();
  await user.click(screen.getByRole('button', { name: button }));
  expect(((await screen.findByRole('checkbox', { name: choice })) as HTMLInputElement).checked).toBe(false);
});

it('muestra una sucursal asignada aunque la sucursal esté inactiva', async () => {
  doctor.centers = [centers[2]];
  render(<App />);
  expect(await screen.findByText('Sede inactiva (Inactiva)')).toBeTruthy();
  expect(within(screen.getByRole('table')).queryByText('Sin asignaciones')).toBeNull();
});

it('crea doctor con cuenta obligatoria sin selector de usuario', async () => {
  permissions.push('doctors.create'); render(<App />);
  await userEvent.click(await screen.findByRole('button', { name: 'Crear doctor' }));
  expect(screen.queryByRole('combobox', { name: 'Cuenta del doctor' })).toBeNull();
  expect(document.querySelector('[name="userId"]')).toBeNull();
  for (const [name, value] of Object.entries({ firstName: 'Luis', lastName: 'Prueba', documentNumber: 'CI-TEST', birthDate: '1990-01-01', phone: '70000000', email: 'luis@example.test', password: 'test-password-1234', confirmPassword: 'test-password-1234', professionalLicense: 'LP-456' })) {
    const input = document.querySelector(`[name="${name}"]`) as HTMLInputElement;
    expect(input.required).toBe(true); fireEvent.change(input, { target: { value } });
  }
  await userEvent.click(screen.getByRole('button', { name: 'Guardar doctor' }));
  await screen.findByText('Luis Prueba');
  expect(requestBody('/api/admin/doctors', 'POST')).toMatchObject({ professionalLicense: 'LP-456', isActive: true, firstName: 'Luis', email: 'luis@example.test', confirmPassword: 'test-password-1234' });
  expect(requestBody('/api/admin/doctors', 'POST').userId).toBeUndefined();
});
it('bloquea nacimiento futuro y confirmacion distinta', async () => {
  permissions.push('doctors.create'); render(<App />);
  await userEvent.click(await screen.findByRole('button', { name: 'Crear doctor' }));
  const birth = document.querySelector('[name="birthDate"]') as HTMLInputElement;
  expect(birth.max).toMatch(/^\d{4}-\d{2}-\d{2}$/);
  fireEvent.change(birth, { target: { value: '2999-01-01' } });
  const form = screen.getByRole('button', { name: 'Guardar doctor' }).closest('form')!;
  fireEvent.submit(form); expect(screen.getByRole('alert').textContent).toContain('futura');
  fireEvent.change(birth, { target: { value: '1990-01-01' } });
  fireEvent.change(document.querySelector('[name="password"]')!, { target: { value: 'test-password-1234' } });
  fireEvent.submit(form); expect(screen.getByRole('alert').textContent).toContain('no coinciden');
  expect(fetchMock.mock.calls.some(([, init]) => init?.method === 'POST' && String(init.body).includes('professionalLicense'))).toBe(false);
});

it('muestra loading hasta recibir el listado de doctores', async () => {
  let release = () => {};
  listWait = new Promise<void>(resolve => { release = resolve; });
  render(<App />);
  expect(await screen.findByText('Cargando doctores…')).toBeTruthy();
  expect(screen.queryByRole('table')).toBeNull();
  await act(async () => release());
  expect(await screen.findByText('Lucía Ramírez')).toBeTruthy();
  expect(screen.queryByText('Cargando doctores…')).toBeNull();
});

it('muestra el error de carga del listado', async () => {
  listFailure = true;
  render(<App />);
  expect((await screen.findByRole('alert')).textContent).toBe('No se pudo cargar los doctores.');
  expect(screen.queryByText('Cargando doctores…')).toBeNull();
});

it('restringe el acceso sin doctors.view y no solicita doctores', async () => {
  permissions = ['doctors.create', 'doctors.update', 'doctors.disable'];
  render(<App />);
  expect(await screen.findByRole('heading', { name: 'Acceso restringido' })).toBeTruthy();
  expect(screen.queryByRole('link', { name: 'Doctores' })).toBeNull();
  expect(fetchMock.mock.calls.some(([url]) => String(url).includes('/api/admin/doctors'))).toBe(false);
});
