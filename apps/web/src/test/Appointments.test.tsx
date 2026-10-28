import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, expect, it, vi } from 'vitest';
import App from '../App';
import { setSession } from '../services/api';
import { appointmentStates, type Appointment, type Slot } from '../modules/admin/appointments/types';

const original: Appointment = { id: 'a1', patientId: 'p1', patientName: 'Ana Paciente', doctorName: 'Marta Doctor', centerName: 'Central', doctorId: 'd1', centerId: 'c1', specialtyId: 's1',
  appointmentDate: '2030-01-07', startTime: '08:00:00', endTime: '08:30:00', status: 'SCHEDULED',
  reason: 'Control', notes: 'Nota', createdByUserId: 'admin', createdAt: '2026-10-06T12:00:00Z', updatedAt: '2026-10-06T12:00:00Z' };
const available: Slot = { startTime: '09:00:00', endTime: '09:30:00' };
vi.mock('../modules/admin/appointments/DailyAgenda',async()=>({...await vi.importActual('../modules/admin/appointments/DailyAgenda'),clinicToday:()=> '2030-01-07'}));
let permissions: string[], appointments: Appointment[], slots: Slot[];
let failure: { method: string; status: number; title: string } | null;
let availabilityWait: Promise<void> | null, listWait: Promise<void> | null;
let extraPage: boolean;
const fetchMock = vi.fn(async (input: string | URL | Request, init?: RequestInit) => {
  const url = new URL(String(input)); const path = url.pathname; const method = init?.method ?? 'GET';
  if (path === '/api/auth/refresh') return Response.json({ accessToken: 'test', expiresAt: '', user: {
    id: 'admin', firstName: 'Admin', lastName: 'Prueba', email: 'admin@example.test', roles: [], permissions,
  } });
  if (path === '/api/admin/patients/search') return Response.json([
    { patientId: 'p1', userId: 'u1', firstName: 'Ana', lastName: 'Paciente', documentNumber: '12345', phone: '71234567', email: 'ana@example.test' },
    { patientId: 'p2', userId: 'u2', firstName: 'Luis', lastName: 'Paciente' },
  ]);
  const centers = [{ id: 'c1', name: 'Central', isActive: true }, { id: 'c2', name: 'Norte', isActive: true }];
  const specialties = [{ id: 's1', name: 'General', isActive: true }, { id: 's2', name: 'Ortodoncia', isActive: true }];
  if (path === '/api/admin/centers') return Response.json(centers);
  if (path === '/api/admin/specialties') return Response.json(specialties);
  if (path === '/api/admin/doctors') return Response.json({ items: ['d1', 'd2'].map((id, index) => ({
    id, isActive: true, user: { firstName: index ? 'Luis' : 'Marta', lastName: 'Doctor', isActive: true }, centers, specialties,
  })), total: 2 });
  if (!path.startsWith('/api/admin/appointments')) throw new Error(`Unexpected ${path}`);
  if (failure?.method === method) return Response.json({ title: failure.title }, { status: failure.status });
  if (path.endsWith('/availability')) {
    const captured = structuredClone(slots);
    if (availabilityWait) await availabilityWait;
    return Response.json({ slots: captured });
  }
  if(path.endsWith('/agenda')){
    if(listWait)await listWait;
    const date=url.searchParams.get('date')!;
    const entries=appointments.filter(a=>a.appointmentDate===date&&['doctorId','centerId','specialtyId','status'].every(k=>!url.searchParams.get(k)||a[k as keyof Appointment]===url.searchParams.get(k)));
    const ids=[...new Set(appointments.map(a=>a.doctorId))].filter(id=>!url.searchParams.get('doctorId')||id===url.searchParams.get('doctorId'));
    return Response.json({date,slotMinutes:30,doctors:ids.map(id=>({id,userId:'user-'+id,firstName:appointments.find(a=>a.doctorId===id)?.doctorName||'Doctor no disponible',lastName:'',specialties:['General']})),workingIntervals:ids.map(doctorId=>({doctorId,centerId:'c1',centerName:'Central',startTime:'08:00:00',endTime:'12:00:00',breakStart:null,breakEnd:null})),appointments:entries});
  }
  const body = init?.body ? JSON.parse(String(init.body)) : {};
  if (path === '/api/admin/appointments') {
    if (method === 'GET') {
      if (listWait) await listWait;
      if (extraPage) return Response.json({ items: Number(url.searchParams.get('page')) === 1 ? [appointments[0]] : appointments.slice(1), total: appointments.length });
      return Response.json({ items: appointments, total: appointments.length });
    }
    if (method === 'POST') { const created = { ...original, ...body, id: 'new' }; appointments.push(created); return Response.json(created, { status: 201 }); }
  }
  const appointment = appointments.find(a => a.id === path.split('/')[4]);
  if (!appointment) return Response.json({ title: 'Cita inexistente.' }, { status: 404 });
  if (method === 'GET') return Response.json(appointment);
  if (method === 'PUT' || method === 'PATCH') {
    Object.assign(appointment, body);
    return method === 'PATCH' ? new Response(null, { status: 204 }) : Response.json(appointment);
  }
  throw new Error(`Unexpected ${method} ${path}`);
});
beforeEach(() => {
  HTMLDialogElement.prototype.showModal=function(){this.setAttribute('open','');};
  setSession(null); window.history.replaceState(null, '', '#/appointments');
  permissions = ['appointments.view', 'appointments.create', 'appointments.update', 'appointments.cancel',
    'patients.view', 'doctors.view', 'centers.view', 'specialties.view'];
  appointments = [structuredClone(original)]; slots = [structuredClone(available)];
  failure = null; availabilityWait = null; listWait = null; extraPage = false;
  fetchMock.mockClear(); vi.stubGlobal('fetch', fetchMock);
});
const writes = (method: string) => fetchMock.mock.calls.filter(([url, init]) => String(url).includes('/appointments') && init?.method === method);
const availabilityCalls = () => fetchMock.mock.calls.filter(([url]) => String(url).includes('/availability?'));
const table = () => within(screen.getByRole('region',{name:'Agenda diaria'}));
it('daily grid shows names without UUIDs or catalog permissions', async () => {
  permissions = ['appointments.view'];
  appointments = [{ ...original, patientId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', doctorId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', centerId: 'cccccccc-cccc-cccc-cccc-cccccccccccc' }];
  render(<App />); await screen.findByText('Ana Paciente');
  expect(screen.getByRole('region',{name:'Horarios por doctor'})).toBeTruthy();
  expect(table().getByText('Marta Doctor',{selector:'strong'})).toBeTruthy(); expect(table().getByText('Central')).toBeTruthy();
  for (const id of [appointments[0].patientId, appointments[0].doctorId, appointments[0].centerId]) expect(document.body.textContent).not.toContain(id);
  expect(fetchMock.mock.calls.some(([url]) => /\/api\/admin\/(doctors|centers|patients)(\?|$)/.test(String(url)))).toBe(false);
});
it('missing presentation names have controlled fallbacks', async () => {
  permissions = ['appointments.view']; appointments = [{ ...original, patientName: '', doctorName: '', centerName: '' }];
  render(<App />); await screen.findByText('Paciente no disponible');
  expect(table().getByText('Doctor no disponible',{selector:'strong'})).toBeTruthy(); expect(table().getByText('Sucursal no disponible')).toBeTruthy(); expect(screen.queryByText(/ID:/)).toBeNull();
});
async function openNew() {
  const user = userEvent.setup();
  await waitFor(() => expect((screen.getByRole('button', { name: 'Nueva cita' }) as HTMLButtonElement).disabled).toBe(false));
  await user.click(screen.getByRole('button', { name: 'Nueva cita' })); return user;
}
async function fill() {
  const user = await openNew();
  await user.type(screen.getByLabelText('Paciente'), 'Ana');
  await user.click(await screen.findByRole('option', { name: /Ana Paciente/ }));
  await user.selectOptions(screen.getByLabelText('Especialidad'), 's1');
  await user.selectOptions(screen.getByLabelText('Doctor'), 'd1');
  await user.selectOptions(screen.getByLabelText('Sucursal'), 'c1');
  await user.type(screen.getByLabelText('Fecha'), '2030-01-07');
  await screen.findByRole('option', { name: '09:00 – 09:30' });
  return user;
}
async function choose() {
  const user = await fill();
  await user.selectOptions(screen.getByLabelText('Horario disponible'), '09:00:00|09:30:00'); return user;
}

it('restringe ruta y navegación sin appointments.view', async () => {
  permissions = permissions.filter(p => p !== 'appointments.view'); render(<App />);
  expect(await screen.findByText('Acceso restringido')).toBeTruthy();
  expect(screen.queryByRole('link', { name: 'Agenda / Citas' })).toBeNull();
  expect(fetchMock.mock.calls.some(([url]) => String(url).includes('/appointments'))).toBe(false);
});
it('lista citas con nombres, horas, motivo y navegación', async () => {
  render(<App />); await screen.findByText('Ana Paciente');
  expect(table().getByText('Marta Doctor',{selector:'strong'})).toBeTruthy(); expect(table().getByText('Control')).toBeTruthy();
  expect(table().getByText('Central')).toBeTruthy(); expect(table().queryByRole('columnheader', { name: 'Especialidad' })).toBeNull();
  expect(table().getByText('Programada', { selector: 'span' })).toBeTruthy();
  expect(screen.getByRole('link', { name: 'Agenda / Citas' }).getAttribute('aria-current')).toBe('page');
});
it('abre nueva cita y no ofrece horas de escritura libre', async () => {
  render(<App />); await openNew(); expect(screen.getByRole('heading', { name: 'Nueva cita' })).toBeTruthy();
  expect((screen.getByLabelText('Horario disponible') as HTMLSelectElement).disabled).toBe(true);
  expect(document.querySelector('input[type=time]')).toBeNull(); expect((screen.getByRole('button', { name: 'Guardar cita' }) as HTMLButtonElement).disabled).toBe(true);
  expect(screen.getByRole('combobox', { name: 'Paciente' }).tagName).toBe('INPUT');
  expect(fetchMock.mock.calls.some(([url]) => new URL(String(url)).pathname.startsWith('/api/admin/patients'))).toBe(false);
});
it('texto modificado invalida paciente y bloquea envío incluso con slot válido', async () => {
  render(<App />); const user = await choose(); await user.type(screen.getByLabelText('Paciente'), 'modificado');
  expect((screen.getByRole('button', { name: 'Guardar cita' }) as HTMLButtonElement).disabled).toBe(true);
  await user.click(screen.getByRole('button', { name: 'Guardar cita' })); expect(writes('POST')).toHaveLength(0);
});
it('consulta disponibilidad con todos los parámetros', async () => {
  render(<App />); await fill();
  const url = new URL(String(availabilityCalls()[0][0]));
  expect(Object.fromEntries(url.searchParams)).toEqual({ doctorId: 'd1', centerId: 'c1', specialtyId: 's1', date: '2030-01-07', durationMinutes: '30' });
});
it('muestra disponibilidad vacía e impide guardar', async () => {
  slots = []; render(<App />); const user = await openNew();
  await user.type(screen.getByLabelText('Paciente'), 'Ana');
  await user.click(await screen.findByRole('option', { name: /Ana Paciente/ }));
  for (const [label, value] of [ ['Especialidad', 's1'], ['Doctor', 'd1'], ['Sucursal', 'c1']])
    await user.selectOptions(screen.getByLabelText(label), value);
  await user.type(screen.getByLabelText('Fecha'), '2030-01-07');
  expect(await screen.findByText('No hay horarios disponibles para una nueva reserva.')).toBeTruthy();
  expect((screen.getByRole('button', { name: 'Guardar cita' }) as HTMLButtonElement).disabled).toBe(true);
});
it('selecciona slot y crea usando inicio y fin devueltos por API', async () => {
  render(<App />); const user = await choose(); await user.type(screen.getByLabelText('Motivo'), 'Limpieza');
  await user.click(screen.getByRole('button', { name: 'Guardar cita' })); await screen.findByText('Limpieza');
  expect(JSON.parse(String(writes('POST')[0][1]?.body))).toEqual({ patientId: 'p1', doctorId: 'd1', centerId: 'c1', specialtyId: 's1',
    appointmentDate: '2030-01-07', startTime: available.startTime, endTime: available.endTime, reason: 'Limpieza', notes: null });
  expect(availabilityCalls()).toHaveLength(2);
});
it('cambiar doctor limpia sucursal, slots y selección', async () => {
  render(<App />); const user = await choose(); await user.selectOptions(screen.getByLabelText('Doctor'), 'd2');
  expect((screen.getByLabelText('Sucursal') as HTMLSelectElement).value).toBe(''); expect((screen.getByLabelText('Horario disponible') as HTMLSelectElement).value).toBe('');
  expect(screen.queryByRole('option', { name: '09:00 – 09:30' })).toBeNull();
  expect((screen.getByRole('button', { name: 'Guardar cita' }) as HTMLButtonElement).disabled).toBe(true);
});
it.each([['Sucursal', 'c2'], ['Especialidad', 's2']])('cambiar %s invalida selección', async (label, value) => {
  render(<App />); const user = await choose(); await user.selectOptions(screen.getByLabelText(label), value);
  expect((screen.getByLabelText('Horario disponible') as HTMLSelectElement).value).toBe(''); expect((screen.getByRole('button', { name: 'Guardar cita' }) as HTMLButtonElement).disabled).toBe(true);
});
it('cambiar fecha vuelve a consultar y descarta respuestas anteriores', async () => {
  render(<App />); const user = await choose(); let release!: () => void;
  availabilityWait = new Promise<void>(resolve => { release = resolve; });
  await user.clear(screen.getByLabelText('Fecha')); await user.type(screen.getByLabelText('Fecha'), '2030-01-08');
  expect((screen.getByLabelText('Horario disponible') as HTMLSelectElement).value).toBe('');
  availabilityWait = null; slots = [{ startTime: '11:00:00', endTime: '11:30:00' }];
  await user.clear(screen.getByLabelText('Fecha')); await user.type(screen.getByLabelText('Fecha'), '2030-01-09');
  await screen.findByRole('option', { name: '11:00 – 11:30' });
  await act(async () => release());
  expect(screen.queryByRole('option', { name: '09:00 – 09:30' })).toBeNull();
  expect(String(availabilityCalls().at(-1)?.[0])).toContain('date=2030-01-09');
});
it('cambiar duración consulta otro intervalo completo', async () => {
  render(<App />); const user = await choose(); slots = [{ startTime: '09:00:00', endTime: '10:00:00' }];
  await user.clear(screen.getByLabelText('Duración (minutos)')); await user.type(screen.getByLabelText('Duración (minutos)'), '60');
  await screen.findByRole('option', { name: '09:00 – 10:00' });
  expect(String(availabilityCalls().at(-1)?.[0])).toContain('durationMinutes=60');
  expect((screen.getByLabelText('Horario disponible') as HTMLSelectElement).value).toBe('');
});
it.each([['appointments.create', 'Nueva cita'], ['appointments.update', 'Editar cita'], ['appointments.cancel', 'Cancelar cita']])(
  'oculta acción sin %s', async (permission, button) => {
    permissions = permissions.filter(p => p !== permission); render(<App />); await userEvent.click(await screen.findByRole('button',{name:/Ana Paciente,/}));
    expect(screen.queryByRole('button', { name: button })).toBeNull();
    if (permission === 'appointments.update') expect(screen.queryByLabelText('Estado de cita de Ana Paciente')).toBeNull();
    if (permission === 'appointments.cancel') expect(within(screen.getByLabelText('Estado de cita de Ana Paciente')).queryByRole('option', { name: 'Cancelada' })).toBeNull();
  });
it('cancela con permiso independiente de update', async () => {
  permissions = permissions.filter(p => p !== 'appointments.update'); render(<App />);
  await userEvent.click(await screen.findByRole('button',{name:/Ana Paciente,/}));
  await userEvent.click(await screen.findByRole('button', { name: 'Cancelar cita' }));
  await waitFor(() => expect(table().getByText('Cancelada')).toBeTruthy());
  expect(JSON.parse(String(writes('PATCH')[0][1]?.body))).toEqual({ status: 'CANCELLED' });
});
it('cambia otro estado con update sin cancel', async () => {
  permissions = permissions.filter(p => p !== 'appointments.cancel'); render(<App />);
  await userEvent.click(await screen.findByRole('button',{name:/Ana Paciente,/}));
  await userEvent.selectOptions(await screen.findByLabelText('Estado de cita de Ana Paciente'), 'CONFIRMED');
  await waitFor(() => expect(table().getByText('Confirmada', { selector: 'span' })).toBeTruthy());
  expect(JSON.parse(String(writes('PATCH')[0][1]?.body))).toEqual({ status: 'CONFIRMED' });
});
it.each([400, 403, 404, 409])('muestra error %s al guardar y conserva formulario', async status => {
  render(<App />); const user = await choose(); failure = { method: 'POST', status, title: status === 409 ? 'El doctor ya tiene una cita en ese intervalo.' : `Error ${status}` };
  await user.click(screen.getByRole('button', { name: 'Guardar cita' }));
  expect((await screen.findByRole('alert')).textContent).toContain(failure.title);
  expect((screen.getByLabelText('Horario disponible') as HTMLSelectElement).value).toBe('');
  expect(screen.getByRole('heading', { name: 'Nueva cita' })).toBeTruthy();
});
it('no envía una reserva si el slot dejó de estar disponible', async () => {
  render(<App />); const user = await choose(); slots = [];
  await user.click(screen.getByRole('button', { name: 'Guardar cita' }));
  expect((await screen.findByRole('alert')).textContent).toContain('ya no está disponible'); expect(writes('POST')).toHaveLength(0);
});
it('traduce todos los estados', async () => {
  appointments = Object.keys(appointmentStates).map((status, index) => ({ ...original, id: String(index), status: status as Appointment['status'] }));
  render(<App />); await screen.findAllByText('Ana Paciente');
  for (const label of Object.values(appointmentStates)) expect(table().getByText(label, { selector: 'span' })).toBeTruthy();
});
it('consulta la agenda por fecha, doctor, sucursal, especialidad y estado', async () => {
  extraPage = true;
  appointments.push({ ...original, id: 'a2', doctorId: 'd2', centerId: 'c2', specialtyId: 's2', appointmentDate: '2030-01-08', status: 'CONFIRMED', reason: 'Segunda' });
  render(<App />); await screen.findByText('Control'); const user = userEvent.setup();
  await user.click(screen.getByText(/Filtros ·/));
  fireEvent.change(screen.getByLabelText('Filtrar fecha'),{target:{value:'2030-01-08'}});
  for (const [label, value] of [['doctor', 'd2'], ['sucursal', 'c2'], ['especialidad', 's2'], ['estado', 'CONFIRMED']])
    await user.selectOptions(screen.getByLabelText(`Filtrar ${label}`), value);
  await waitFor(()=>expect(table().queryByText('Control')).toBeNull()); expect(await screen.findByText('Segunda')).toBeTruthy();
  await user.selectOptions(screen.getByLabelText('Filtrar estado'), 'CANCELLED');
  await waitFor(()=>expect(table().queryByText('Segunda')).toBeNull());expect(screen.getByRole('region',{name:'Horarios por doctor'})).toBeTruthy();
});
it('muestra loading de citas', async () => {
  let release!: () => void; listWait = new Promise<void>(resolve => { release = resolve; }); render(<App />);
  expect(await screen.findByText('Cargando citas…')).toBeTruthy(); await act(async () => release()); await screen.findByText('Control');
});
it('edita datos conservando explícitamente su reserva actual', async () => {
  render(<App />); await screen.findByText('Ana Paciente'); const user = userEvent.setup();
  await user.click(screen.getByRole('button',{name:/Ana Paciente,/}));
  await user.click(screen.getByRole('button', { name: 'Editar cita' }));
  await screen.findByRole('option', { name: '08:00 – 08:30 (reserva actual)' });
  await user.selectOptions(screen.getByLabelText('Horario disponible'), 'current');
  await user.clear(screen.getByLabelText('Paciente')); await user.type(screen.getByLabelText('Paciente'), 'Luis');
  await user.click(await screen.findByRole('option', { name: /Luis Paciente/ }));
  await user.clear(screen.getByLabelText('Motivo')); await user.type(screen.getByLabelText('Motivo'), 'Editada');
  await user.click(screen.getByRole('button', { name: 'Guardar cita' })); await screen.findByText('Editada');
  expect(JSON.parse(String(writes('PUT')[0][1]?.body))).toMatchObject({ patientId: 'p2', startTime: '08:00:00', endTime: '08:30:00', reason: 'Editada' });
});
it('edita horario con un slot disponible', async () => {
  render(<App />); await screen.findByText('Ana Paciente'); const user = userEvent.setup();
  await user.click(screen.getByRole('button',{name:/Ana Paciente,/}));
  await user.click(screen.getByRole('button', { name: 'Editar cita' })); await screen.findByRole('option', { name: '09:00 – 09:30' });
  await user.selectOptions(screen.getByLabelText('Horario disponible'), '09:00:00|09:30:00');
  await user.click(screen.getByRole('button', { name: 'Guardar cita' }));
  await waitFor(() => expect(writes('PUT')).toHaveLength(1));
  expect(JSON.parse(String(writes('PUT')[0][1]?.body))).toMatchObject(available);
});
it('no sobrescribe una cita editada por otro usuario', async () => {
  render(<App />); await screen.findByText('Ana Paciente'); const user = userEvent.setup();
  await user.click(screen.getByRole('button',{name:/Ana Paciente,/}));
  await user.click(screen.getByRole('button', { name: 'Editar cita' })); await screen.findByRole('option', { name: '08:00 – 08:30 (reserva actual)' });
  await user.selectOptions(screen.getByLabelText('Horario disponible'), 'current'); appointments[0].status = 'CANCELLED';
  await user.click(screen.getByRole('button', { name: 'Guardar cita' }));
  expect((await screen.findByRole('alert')).textContent).toContain('La cita cambió'); expect(writes('PUT')).toHaveLength(0);
});
it('sin acceso a catálogos muestra identificadores sin hacer consultas prohibidas', async () => {
  permissions = ['appointments.view', 'appointments.create']; render(<App />);
  expect(await screen.findByText('Ana Paciente')).toBeTruthy(); expect(table().getByText('Marta Doctor',{selector:'strong'})).toBeTruthy(); expect(table().getByText('Central')).toBeTruthy(); expect(screen.queryByText(/ID:/)).toBeNull(); expect((screen.getByRole('button', { name: 'Nueva cita' }) as HTMLButtonElement).disabled).toBe(true);
  expect(fetchMock.mock.calls.some(([url]) => /\/(patients|doctors|centers|specialties)(\?|$)/.test(String(url)))).toBe(false);
});

it('navega por día anterior, siguiente y Hoy sin convertir las horas',async()=>{
 render(<App/>);await screen.findByText('Ana Paciente');const user=userEvent.setup();
 await user.click(screen.getByRole('button',{name:'Día siguiente'}));
 await waitFor(()=>expect((screen.getByLabelText('Filtrar fecha') as HTMLInputElement).value).toBe('2030-01-08'));
 await user.click(screen.getByRole('button',{name:'Día anterior'}));
 expect((screen.getByLabelText('Filtrar fecha') as HTMLInputElement).value).toBe('2030-01-07');
 await user.click(screen.getByRole('button',{name:'Día anterior'}));await user.click(screen.getByRole('button',{name:'Hoy'}));
 expect((screen.getByLabelText('Filtrar fecha') as HTMLInputElement).value).toBe('2030-01-07');
 expect(await screen.findByRole('button',{name:/Ana Paciente, 08:00–08:30/})).toBeTruthy();
});
it('abre y cierra detalle con notas y conserva la agenda',async()=>{
 render(<App/>);await userEvent.click(await screen.findByRole('button',{name:/Ana Paciente,/}));
 const detail=within(screen.getByRole('dialog',{name:'Detalle de cita'}));
 expect(detail.getByText('Nota')).toBeTruthy();expect(detail.getByText('Central')).toBeTruthy();
 await userEvent.click(detail.getByRole('button',{name:'Cerrar detalle'}));
 expect(screen.queryByRole('dialog')).toBeNull();expect(screen.getByRole('region',{name:'Agenda diaria'})).toBeTruthy();
});
