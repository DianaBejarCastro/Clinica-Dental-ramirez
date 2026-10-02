import { beforeEach, expect, it, vi } from 'vitest';
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import App from '../App';
import { setSession } from '../services/api';
import type { DoctorSchedule } from '../modules/admin/types';
import { validateSchedule, type ScheduleInput } from '../modules/admin/schedules/scheduleValidation';

const doctorId = 'doctor-1';
const centers = [
  { id: 'center-a', code: 'CENTRO', name: 'Sucursal Centro', isActive: true },
  { id: 'center-b', code: 'SUR', name: 'Sucursal Sur', isActive: true },
];
const originalBlock: DoctorSchedule = {
  id: 'block-1', doctorId, centerId: 'center-a', centerName: centers[0].name,
  dayOfWeek: 1, startTime: '09:00:00', endTime: '10:00:00', hasBreak: false,
  breakStart: null, breakEnd: null, isActive: true, createdAt: '2026-09-30T12:00:00Z', updatedAt: null,
};
let permissions: string[];
let blocks: DoctorSchedule[];
let assignedCenters = centers;
let rejectSave = false;
let weekFailure: boolean;
let weekWait: Promise<void> | null;
const fetchMock = vi.fn(async (input: string | URL | Request, init?: RequestInit) => {
  const path = new URL(String(input)).pathname;
  const method = init?.method ?? 'GET';
  let status = 200; let data: unknown;
  if (path === '/api/auth/refresh') {
    data = { accessToken: 'test-access', expiresAt: '', user: { id: 'admin', firstName: 'Ana', lastName: 'Prueba', email: 'ana@example.com', roles: [], permissions } };
  } else if (path === '/api/admin/doctors' && method === 'GET') {
    data = { items: [{ id: doctorId, isActive: true, user: { firstName: 'Diana', lastName: 'Ramírez' } }], page: 1, pageSize: 100, total: 1 };
  } else if (path === `/api/admin/doctors/${doctorId}/centers` && method === 'GET') {
    data = assignedCenters;
  } else if (path === `/api/admin/doctors/${doctorId}/schedules` && method === 'GET') {
    if (weekWait) await weekWait;
    status = weekFailure ? 503 : 200;
    data = weekFailure ? { title: 'No se pudo cargar la semana.' } : blocks.map(block => ({ ...block }));
  } else if (path.includes(`/api/admin/doctors/${doctorId}/schedules`) && ['POST', 'PUT', 'PATCH'].includes(method)) {
    if (rejectSave) { status = 409; data = { title: 'Otro usuario creó un horario que se solapa. Actualiza la semana.' }; }
    else {
      const body = JSON.parse(String(init?.body)) as ScheduleInput;
      if (method === 'POST') {
        const block = { ...originalBlock, ...body, id: `block-${blocks.length + 1}`, centerName: centers.find(center => center.id === body.centerId)!.name };
        blocks = [...blocks, block]; data = block; status = 201;
      } else {
        const id = path.split('/')[6];
        blocks = blocks.map(block => block.id === id ? { ...block, ...body } : block);
        data = blocks.find(block => block.id === id);
        if (method === 'PATCH') status = 204;
      }
    }
  } else throw new Error(`Petición inesperada: ${method} ${path}`);
  return { ok: status >= 200 && status < 300, status, json: async () => data } as Response;
});

beforeEach(() => {
  permissions = ['doctors.view', 'schedules.view', 'schedules.create', 'schedules.update', 'schedules.disable'];
  blocks = [{ ...originalBlock }]; assignedCenters = centers; rejectSave = false;
  weekFailure = false; weekWait = null;
  setSession(null); window.location.hash = '/schedules'; fetchMock.mockClear(); vi.stubGlobal('fetch', fetchMock);
});

const writes = (method: string) => fetchMock.mock.calls.filter(([url, init]) => init?.method === method && String(url).includes('/schedules'));

async function selectDoctor() {
  const user = userEvent.setup();
  await screen.findByRole('option', { name: 'Diana Ramírez' });
  await user.selectOptions(screen.getByLabelText('Doctor'), doctorId);
  await screen.findByRole('heading', { name: 'Lunes' });
  return user;
}

async function fillBlock(start: string, end: string, centerId = 'center-b') {
  const user = userEvent.setup();
  await user.click(screen.getByRole('button', { name: 'Agregar bloque' }));
  await user.selectOptions(screen.getByLabelText('Sucursal'), centerId);
  fireEvent.change(screen.getByLabelText('Inicio', { exact: true }), { target: { value: start } });
  fireEvent.change(screen.getByLabelText('Fin', { exact: true }), { target: { value: end } });
  return user;
}

it('requiere seleccionar doctor y después muestra lunes a domingo', async () => {
  render(<App />);
  await screen.findByRole('option', { name: 'Diana Ramírez' });
  expect(screen.queryByRole('heading', { name: 'Lunes' })).toBeNull();
  expect(fetchMock.mock.calls.some(([url]) => String(url).endsWith('/schedules'))).toBe(false);
  await selectDoctor();
  for (const day of ['Lunes', 'Martes', 'Miércoles', 'Jueves', 'Viernes', 'Sábado', 'Domingo']) expect(screen.getByRole('heading', { name: day })).toBeTruthy();
  expect(screen.getByText('09:00 – 10:00')).toBeTruthy();
});

it('crea múltiples bloques válidos y permite que el siguiente empiece al terminar otro', async () => {
  render(<App />); await selectDoctor();
  const user = await fillBlock('10:00', '11:00');
  await user.click(screen.getByRole('button', { name: 'Guardar horario' }));
  expect(await screen.findByText('10:00 – 11:00')).toBeTruthy();
  expect(JSON.parse(String(writes('POST')[0][1]?.body))).toEqual({ centerId: 'center-b', dayOfWeek: 1, startTime: '10:00:00', endTime: '11:00:00', hasBreak: false, breakStart: null, breakEnd: null, isActive: true });
  await fillBlock('12:00', '13:00', 'center-a');
  await user.click(screen.getByRole('button', { name: 'Guardar horario' }));
  expect(await screen.findByText('12:00 – 13:00')).toBeTruthy();
  expect(screen.getByText('09:00 – 10:00')).toBeTruthy();
  expect(screen.getByText('10:00 – 11:00')).toBeTruthy();
  expect(writes('POST')).toHaveLength(2);
});

it.each(['center-a', 'center-b'])('rechaza el solapamiento en %s antes de enviar a la API', async centerId => {
  render(<App />); await selectDoctor(); const user = await fillBlock('09:30', '10:30', centerId);
  await user.click(screen.getByRole('button', { name: 'Guardar horario' }));
  expect((await screen.findByRole('alert')).textContent).toContain('incluso si pertenece a otra sucursal');
  expect(writes('POST')).toHaveLength(0);
});

it('valida el descanso dentro del bloque y envía un descanso válido', async () => {
  render(<App />); await selectDoctor(); const user = await fillBlock('10:00', '12:00');
  await user.click(screen.getByLabelText('Incluir descanso'));
  fireEvent.change(screen.getByLabelText('Inicio del descanso'), { target: { value: '10:00' } });
  fireEvent.change(screen.getByLabelText('Fin del descanso'), { target: { value: '10:30' } });
  await user.click(screen.getByRole('button', { name: 'Guardar horario' }));
  expect((await screen.findByRole('alert')).textContent).toContain('El descanso');
  expect(writes('POST')).toHaveLength(0);
  fireEvent.change(screen.getByLabelText('Inicio del descanso'), { target: { value: '10:15' } });
  await user.click(screen.getByRole('button', { name: 'Guardar horario' }));
  expect(await screen.findByText('Descanso: 10:15–10:30')).toBeTruthy();
  expect(JSON.parse(String(writes('POST')[0][1]?.body))).toMatchObject({ hasBreak: true, breakStart: '10:15:00', breakEnd: '10:30:00' });
});

it('muestra el rechazo final del backend y conserva el formulario para corregirlo', async () => {
  rejectSave = true; render(<App />); await selectDoctor(); const user = await fillBlock('10:00', '11:00');
  await user.click(screen.getByRole('button', { name: 'Guardar horario' }));
  expect((await screen.findByRole('alert')).textContent).toContain('Otro usuario creó un horario');
  expect((screen.getByLabelText('Inicio', { exact: true }) as HTMLInputElement).value).toBe('10:00');
  expect(screen.queryByText('Horario guardado.')).toBeNull();
  expect(writes('POST')).toHaveLength(1);
});

it.each([true, false])('permite editar el propio bloque sin solapamiento y conserva isActive=%s sin permiso disable', async isActive => {
  blocks[0].isActive = isActive;
  permissions = ['doctors.view', 'schedules.view', 'schedules.update'];
  render(<App />); const user = await selectDoctor();
  expect(screen.queryByRole('button', { name: 'Agregar bloque' })).toBeNull();
  expect(screen.queryByRole('button', { name: 'Desactivar bloque' })).toBeNull();
  expect(screen.queryByRole('button', { name: 'Activar bloque' })).toBeNull();
  await user.click(screen.getByRole('button', { name: 'Editar bloque' }));
  expect((screen.getByLabelText('Bloque activo') as HTMLInputElement).disabled).toBe(true);
  await user.click(screen.getByRole('button', { name: 'Guardar horario' }));
  expect(await screen.findByText('Horario guardado.')).toBeTruthy();
  expect(JSON.parse(String(writes('PUT')[0][1]?.body))).toMatchObject({ isActive, startTime: '09:00:00', endTime: '10:00:00' });
  expect(writes('PATCH')).toHaveLength(0);
});

it('oculta acciones de crear, editar y estado al usuario con solo lectura', async () => {
  permissions = ['doctors.view', 'schedules.view']; render(<App />); await selectDoctor();
  for (const name of ['Agregar bloque', 'Editar bloque', 'Desactivar bloque', 'Activar bloque']) expect(screen.queryByRole('button', { name })).toBeNull();
  expect(writes('POST')).toHaveLength(0); expect(writes('PUT')).toHaveLength(0); expect(writes('PATCH')).toHaveLength(0);
});

it('actualiza el estado mediante PATCH cuando tiene schedules.disable', async () => {
  permissions = ['doctors.view', 'schedules.view', 'schedules.disable']; render(<App />); const user = await selectDoctor();
  await user.click(screen.getByRole('button', { name: 'Desactivar bloque' }));
  await screen.findByRole('button', { name: 'Activar bloque' });
  expect(JSON.parse(String(writes('PATCH')[0][1]?.body))).toEqual({ isActive: false });
  await user.click(screen.getByRole('button', { name: 'Activar bloque' }));
  await screen.findByRole('button', { name: 'Desactivar bloque' });
  expect(JSON.parse(String(writes('PATCH')[1][1]?.body))).toEqual({ isActive: true });
});

it('consulta horarios por ID sin solicitar el catálogo si falta doctors.view', async () => {
  permissions = ['schedules.view']; window.location.hash = `/schedules?doctor=${doctorId}`;
  render(<App />); await screen.findByText('09:00 – 10:00');
  expect((screen.getByLabelText('Doctor') as HTMLInputElement).value).toBe(doctorId);
  expect(screen.getByText(/Para seleccionar doctores y sus sedes necesitas acceso/)).toBeTruthy();
  expect(fetchMock.mock.calls.some(([url]) => new URL(String(url)).pathname === '/api/admin/doctors')).toBe(false);
  expect(fetchMock.mock.calls.some(([url]) => String(url).endsWith('/centers'))).toBe(false);
});

it('impide crear bloques sin una sucursal activa asignada', async () => {
  assignedCenters = []; render(<App />); await selectDoctor();
  await waitFor(() => expect((screen.getByRole('button', { name: 'Agregar bloque' }) as HTMLButtonElement).disabled).toBe(true));
  expect(screen.getByText('Asigna primero una sucursal activa al doctor.')).toBeTruthy();
});

it('la validación permite límites adyacentes aunque los formatos horarios difieran', () => {
  const input = { ...originalBlock, startTime: '10:00', endTime: '11:00' };
  expect(validateSchedule(input, [{ ...originalBlock, endTime: '10:00:00.0000000' }])).toBe('');
});

it('la validación ignora bloques inactivos pero mantiene reservado el descanso de los activos', () => {
  const input = { ...originalBlock, startTime: '09:15:00', endTime: '09:45:00' };
  expect(validateSchedule(input, [{ ...originalBlock, isActive: false }])).toBe('');
  expect(validateSchedule(input, [{ ...originalBlock, hasBreak: true, breakStart: '09:10:00', breakEnd: '09:50:00' }])).toContain('solapa');
});

it('la validación rechaza días fuera de semana y horas inválidas o invertidas', () => {
  expect(validateSchedule({ ...originalBlock, dayOfWeek: 8 }, [])).toContain('lunes y domingo');
  expect(validateSchedule({ ...originalBlock, startTime: '25:00:00' }, [])).toContain('inicio');
  expect(validateSchedule({ ...originalBlock, startTime: '10:00:00', endTime: '09:00:00' }, [])).toContain('inicio');
});

it.each([['11:00', '11:00'], ['12:00', '11:00']])('rechaza inicio %s mayor o igual que fin %s sin enviar', async (start, end) => {
  render(<App />); await selectDoctor(); const user = await fillBlock(start, end);
  await user.click(screen.getByRole('button', { name: 'Guardar horario' }));
  expect((await screen.findByRole('alert')).textContent).toContain('La hora de inicio debe ser anterior');
  expect(writes('POST')).toHaveLength(0);
});

it('no permite cambiar el estado al crear sin schedules.disable', async () => {
  permissions = ['doctors.view', 'schedules.view', 'schedules.create'];
  render(<App />); await selectDoctor(); const user = await fillBlock('10:00', '11:00');
  const status = screen.getByLabelText('Bloque activo') as HTMLInputElement;
  expect(status.disabled).toBe(true);
  expect(status.checked).toBe(true);
  await user.click(status);
  expect(status.checked).toBe(true);
  await user.click(screen.getByRole('button', { name: 'Guardar horario' }));
  await screen.findByText('Horario guardado.');
  expect(JSON.parse(String(writes('POST')[0][1]?.body)).isActive).toBe(true);
  expect(screen.queryByRole('button', { name: 'Editar bloque' })).toBeNull();
  expect(screen.queryByRole('button', { name: 'Desactivar bloque' })).toBeNull();
});

it('muestra loading y espera la respuesta antes de presentar la semana', async () => {
  let release = () => {};
  weekWait = new Promise<void>(resolve => { release = resolve; });
  render(<App />);
  await screen.findByRole('option', { name: 'Diana Ramírez' });
  await userEvent.selectOptions(screen.getByLabelText('Doctor'), doctorId);
  expect(await screen.findByText('Cargando semana…')).toBeTruthy();
  expect(screen.queryByRole('heading', { name: 'Lunes' })).toBeNull();
  expect((screen.getByRole('button', { name: 'Agregar bloque' }) as HTMLButtonElement).disabled).toBe(true);
  await act(async () => release());
  expect(await screen.findByText('09:00 – 10:00')).toBeTruthy();
  expect(screen.queryByText('Cargando semana…')).toBeNull();
});

it('muestra error de carga sin confundirlo con una semana vacía y permite volver a seleccionar', async () => {
  weekFailure = true;
  render(<App />);
  await screen.findByRole('option', { name: 'Diana Ramírez' });
  const user = userEvent.setup();
  await user.selectOptions(screen.getByLabelText('Doctor'), doctorId);
  expect((await screen.findByRole('alert')).textContent).toBe('No se pudo cargar la semana.');
  expect(screen.queryByRole('heading', { name: 'Lunes' })).toBeNull();
  expect(screen.queryByText('Sin bloques')).toBeNull();
  expect(screen.queryByText('Cargando semana…')).toBeNull();
  expect((screen.getByRole('button', { name: 'Agregar bloque' }) as HTMLButtonElement).disabled).toBe(true);
  weekFailure = false;
  await user.selectOptions(screen.getByLabelText('Doctor'), '');
  await user.selectOptions(screen.getByLabelText('Doctor'), doctorId);
  expect(await screen.findByText('09:00 – 10:00')).toBeTruthy();
  expect(screen.queryByRole('alert')).toBeNull();
});

it('restringe la ruta y no consulta datos sin schedules.view', async () => {
  permissions = ['doctors.view', 'schedules.create', 'schedules.update', 'schedules.disable'];
  render(<App />);
  expect(await screen.findByRole('heading', { name: 'Acceso restringido' })).toBeTruthy();
  expect(screen.queryByRole('link', { name: 'Horarios' })).toBeNull();
  expect(fetchMock.mock.calls.some(([url]) => String(url).includes('/api/admin/'))).toBe(false);
});
