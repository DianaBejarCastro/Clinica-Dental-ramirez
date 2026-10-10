import { beforeEach, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import App from '../App';
import { setSession } from '../services/api';
const user = { id: '1', firstName: 'Ana', lastName: 'Prueba', email: 'ana@example.com', roles: ['ADMINISTRATOR'], permissions: ['users.view', 'roles.view', 'permissions.view'] };
const session = { accessToken: 'test-access', expiresAt: '', user };
it('login muestra logo público sin bloquear el formulario durante la carga', async () => {
  render(<App />); const submit = await screen.findByRole('button', { name: 'Ingresar' });
  expect((submit as HTMLButtonElement).disabled).toBe(false);
  const image = screen.getByAltText('Logo de la clínica') as HTMLImageElement;
  expect(image.getAttribute('src')).toContain('/api/clinic/logo');
  fireEvent.load(image);
  expect(image.hidden).toBe(false); expect(screen.queryByLabelText('Monograma de la clínica')).toBeNull();
});
it('login conserva R al fallar el logo y permite iniciar sesión', async () => {
  render(<App />); const u = userEvent.setup(); await screen.findByRole('button', { name: 'Ingresar' });
  fireEvent.error(screen.getByAltText('Logo de la clínica'));
  expect(screen.getByLabelText('Monograma de la clínica').textContent).toBe('R');
  expect(screen.queryByAltText('Logo de la clínica')).toBeNull();
  await u.type(screen.getByLabelText('Email'), user.email);
  await u.type(screen.getByLabelText('Contraseña'), 'valid-test-password');
  await u.click(screen.getByRole('button', { name: 'Ingresar' }));
  expect(await screen.findByText('Hola, Ana.')).toBeTruthy();
});
let authenticated = false;
let loginFails = false;
const fetchMock = vi.fn(async (input: string | URL | Request, init?: RequestInit) => {
  const path = String(input); let status = 200; let data: unknown = {};
  if (path.endsWith('/refresh')) { status = authenticated ? 200 : 401; data = authenticated ? session : { title: 'Sesión inválida.' }; }
  else if (path.endsWith('/login')) { status = loginFails ? 401 : 200; data = loginFails ? { title: 'Credenciales incorrectas.' } : session; }
  else if (path.includes('/users')) data = { items: [{ ...user, isActive: true, phone: null, roles: [] }], total: 1 };
  else if (path.endsWith('/roles')) data = [{ id: 'r', code: 'ADMINISTRATOR', name: 'Administrador', isActive: true, isSystem: true, permissionIds: [] }];
  return { ok: status === 200, status, json: async () => data } as Response;
});
beforeEach(() => { setSession(null); authenticated = false; loginFails = false; window.location.hash = '/'; fetchMock.mockClear(); vi.stubGlobal('fetch', fetchMock); });
it('renderiza login sin sesión', async () => { render(<App />); expect(await screen.findByRole('button', { name: 'Ingresar' })).toBeTruthy(); expect(screen.getByLabelText('Email')).toBeTruthy(); });
it('muestra error de login', async () => { loginFails = true; render(<App />); const u = userEvent.setup(); await u.type(await screen.findByLabelText('Email'), user.email); await u.type(screen.getByLabelText('Contraseña'), 'incorrecta'); await u.click(screen.getByRole('button', { name: 'Ingresar' })); expect((await screen.findByRole('alert')).textContent).toContain('Credenciales incorrectas'); });
it('login correcto abre resumen', async () => { render(<App />); const u = userEvent.setup(); await u.type(await screen.findByLabelText('Email'), user.email); await u.type(screen.getByLabelText('Contraseña'), 'valid-test-password'); await u.click(screen.getByRole('button', { name: 'Ingresar' })); expect(await screen.findByText('Hola, Ana.')).toBeTruthy(); expect(localStorage.length).toBe(0); });
it('protege ruta de usuarios sin sesión', async () => { window.location.hash = '/users'; render(<App />); expect(await screen.findByRole('button', { name: 'Ingresar' })).toBeTruthy(); expect(fetchMock.mock.calls.some(([url]) => String(url).includes('/admin/users'))).toBe(false); });
it('lista usuarios con sesión restaurada', async () => { authenticated = true; window.location.hash = '/users'; render(<App />); expect(await screen.findByText('Ana Prueba')).toBeTruthy(); expect(screen.getByText('1 usuarios · Página 1')).toBeTruthy(); });
it('lista roles', async () => { authenticated = true; window.location.hash = '/roles'; render(<App />); expect(await screen.findByText('Administrador')).toBeTruthy(); });
it('restringe ruta por permiso', async () => { authenticated = true; window.location.hash = '/users'; render(<App />); await screen.findByText('Ana Prueba'); setSession({ ...session, user: { ...user, permissions: [] } }); await waitFor(() => expect(screen.getByText('Acceso restringido')).toBeTruthy()); });
