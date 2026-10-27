import { localToday } from '../modules/patient/PatientFields';
import { beforeEach, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import App from '../App';
import { setSession } from '../services/api';
import { clearRegistrationNotice } from '../modules/auth/PatientRegisterPage';

const user = { id: 'patient-user', firstName: 'Ana', lastName: 'Paciente', email: 'ana@example.com', roles: ['PATIENT'], permissions: [] };
const session = { accessToken: 'token', expiresAt: '', user };
let logged: boolean; let errorStatus: number; let errorTitle: string;
const fetchMock = vi.fn(async (input: string | URL | Request, init?: RequestInit) => {
  const path = new URL(String(input)).pathname;
  if (path === '/api/auth/refresh') return logged ? Response.json(session) : Response.json({}, { status: 401 });
  if (path === '/api/auth/register/patient') return errorStatus ? Response.json({ title: errorTitle }, { status: errorStatus }) : Response.json({ message: 'OK' }, { status: 201 });
  if (path === '/api/auth/login') { logged = true; return Response.json(session); }
  if (path === '/api/patient/me') return Response.json({ firstName: 'Ana', lastName: 'Paciente', email: user.email, phone: '123', documentNumber: null, birthDate: null, gender: null });
  if (path === '/api/patient/appointments') return Response.json([]);
  throw new Error(`Solicitud inesperada ${init?.method} ${path}`);
});
beforeEach(() => {
  logged = false; errorStatus = 0; errorTitle = ''; setSession(null); clearRegistrationNotice();
  window.history.replaceState(null, '', '#/register'); fetchMock.mockClear(); vi.stubGlobal('fetch', fetchMock);
});
async function fill(confirmation = 'SafePassword123!') {
  for (const [name, value] of Object.entries({ documentNumber: 'CI-TEST', birthDate: '2000-01-01', phone: '70000000', emergencyContactName: 'Contacto', emergencyContactPhone: '71111111', bloodType: 'UNKNOWN' })) fireEvent.change(document.querySelector(`[name="${name}"]`)!, { target: { value } });
  await userEvent.type(screen.getByLabelText('Nombre'), 'Ana'); await userEvent.type(screen.getByLabelText('Apellido'), 'Paciente');
  await userEvent.type(screen.getByLabelText('Email'), user.email); await userEvent.type(screen.getByLabelText('Contraseña', { exact: true }), 'SafePassword123!');
  await userEvent.type(screen.getByLabelText('Confirmar contraseña'), confirmation);
}
it('login muestra Regístrate y abre formulario público', async () => {
  window.history.replaceState(null, '', '#/login'); render(<App />);
  await userEvent.click(await screen.findByRole('link', { name: 'Regístrate' })); expect(await screen.findByRole('heading', { name: 'Crear cuenta de paciente' })).toBeTruthy();
});
it('formulario sin rol ni estado ni cuenta administrativa', () => {
  render(<App />); expect(screen.getByLabelText('Número de documento')).toBeTruthy(); expect(screen.getByLabelText('Fecha de nacimiento')).toBeTruthy();
  expect(screen.getByRole('combobox', { name: 'Grupo sanguíneo' })).toBeTruthy(); expect(screen.queryByLabelText(/Rol|UserId|Estado|Permisos/)).toBeNull();
  expect(fetchMock).not.toHaveBeenCalled(); expect(screen.getByAltText('Logo de la clínica')).toBeTruthy();
  expect(screen.getByRole('main').classList.contains('premium-login')).toBe(true);
  expect(screen.getByText('REGISTRO DE PACIENTE')).toBeTruthy();
  expect(screen.getByRole('link', { name: 'Volver al inicio' }).getAttribute('href')).toBe('#/inicio');
});
it('validación de confirmación evita envío', async () => {
  render(<App />); await fill('DifferentPassword123!'); await userEvent.click(screen.getByRole('button', { name: 'Crear cuenta' }));
  expect(screen.getByRole('alert').textContent).toContain('no coinciden'); expect(fetchMock).not.toHaveBeenCalled();
});
it('password inválido impide registro', async () => {
  render(<App />); await fill(); fireEvent.change(screen.getByLabelText('Contraseña', { exact: true }), { target: { value: 'short' } });
  fireEvent.submit(screen.getByRole('button', { name: 'Crear cuenta' }).closest('form')!);
  expect(screen.getByRole('alert').textContent).toContain('12 y 128'); expect(fetchMock).not.toHaveBeenCalled();
});
it.each(['Ya existe una cuenta registrada con este correo electrónico.', 'CI ya registrado.'])('muestra conflicto: %s', async message => {
  errorStatus = 409; errorTitle = message; render(<App />); await fill(); await userEvent.click(screen.getByRole('button', { name: 'Crear cuenta' }));
  expect((await screen.findByRole('alert')).textContent).toBe(message);
});
it('registro exitoso dirige a login con mensaje y sin auto-login', async () => {
  render(<App />); await fill(); await userEvent.click(screen.getByRole('button', { name: 'Crear cuenta' }));
  expect(await screen.findByText('Cuenta creada correctamente. Ya puedes iniciar sesión.')).toBeTruthy();
  expect(window.location.hash).toBe('#/login'); expect(screen.getByRole('button', { name: 'Ingresar' })).toBeTruthy();
  const call = fetchMock.mock.calls.find(([url]) => String(url).includes('/register/patient'))!;
  const body = JSON.parse(String(call[1]!.body)); expect(body.email).toBe(user.email); expect(body).not.toHaveProperty('roles'); expect(body).not.toHaveProperty('userId');
  expect(fetchMock.mock.calls.some(([url]) => String(url).endsWith('/login'))).toBe(false);
});
it('loading evita doble envío', async () => {
  fetchMock.mockReturnValueOnce(new Promise<Response>(() => {})); render(<App />); await fill(); await userEvent.click(screen.getByRole('button', { name: 'Crear cuenta' }));
  expect((screen.getByRole('button', { name: 'Creando cuenta…' }) as HTMLButtonElement).disabled).toBe(true); expect(fetchMock).toHaveBeenCalledTimes(1);
});
it('error general no expone detalles técnicos', async () => {
  errorStatus = 500; errorTitle = 'Sensitive SQL failure'; render(<App />); await fill(); await userEvent.click(screen.getByRole('button', { name: 'Crear cuenta' }));
  expect((await screen.findByRole('alert')).textContent).toContain('No se pudo crear'); expect(screen.queryByText(errorTitle)).toBeNull();
});
it('login paciente dirige a Mi cuenta sin sidebar administrativo', async () => {
  window.history.replaceState(null, '', '#/login'); render(<App />);
  await userEvent.type(await screen.findByLabelText('Email'), user.email); await userEvent.type(screen.getByLabelText('Contraseña'), 'SafePassword123!');
  await userEvent.click(screen.getByRole('button', { name: 'Ingresar' }));
  expect(await screen.findByRole('heading', { name: 'Mi cuenta' })).toBeTruthy(); expect(await screen.findByText('Ana Paciente')).toBeTruthy();
  await waitFor(() => expect(window.location.hash).toBe('#/patient')); expect(screen.queryByText('Usuarios')).toBeNull(); expect(document.querySelector('.sidebar')).toBeNull();
  expect(fetchMock.mock.calls.some(([url]) => String(url).includes('/api/admin/'))).toBe(false);
});
it('paciente con ruta admin vuelve a portal y Mis citas usa endpoint propio', async () => {
  logged = true; window.history.replaceState(null, '', '#/patients'); render(<App />); await screen.findByText('Ana Paciente');
  await userEvent.click(screen.getByRole('link', { name: 'Mis citas' })); expect(await screen.findByText('No tienes citas registradas.')).toBeTruthy();
  expect(fetchMock.mock.calls.some(([url]) => String(url).endsWith('/api/patient/appointments'))).toBe(true);
  expect(fetchMock.mock.calls.some(([url]) => String(url).includes('/api/admin/'))).toBe(false);
});

it('exige datos clínicos y bloquea nacimiento futuro incluso al enviar directamente', async () => {
  render(<App />); await fill();
  const birth = screen.getByLabelText('Fecha de nacimiento') as HTMLInputElement; expect(birth.max).toBe(localToday());
  for (const label of ['Número de documento', 'Fecha de nacimiento', 'Teléfono', 'Contacto de emergencia', 'Teléfono de emergencia', 'Grupo sanguíneo', 'Email']) expect((screen.getByLabelText(label) as HTMLInputElement).required).toBe(true);
  expect((screen.getByLabelText('Género') as HTMLInputElement).required).toBe(false);
  fireEvent.change(birth, { target: { value: '2999-01-01' } }); fireEvent.submit(screen.getByRole('button', { name: 'Crear cuenta' }).closest('form')!);
  expect(screen.getByRole('alert').textContent).toContain('futura'); expect(fetchMock).not.toHaveBeenCalled();
  expect(screen.queryByLabelText('Notas')).toBeNull();
});
