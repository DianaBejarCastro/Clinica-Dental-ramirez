import { beforeEach, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AuthProvider } from '../modules/auth/AuthProvider';
import ProtectedDeleteButton from '../modules/admin/deletion/ProtectedDeleteButton';
import CentersPage from '../modules/admin/centers/CentersPage';
import { setSession, type Session } from '../services/api';

let session: Session;
let check: { canDelete: boolean; message: string; dependencies: { type: string; count: number }[] };
let removed: boolean, deleteError: number;
let holdCheck: boolean;
const onDeleted = vi.fn();
const response = (body: unknown, status = 200) => ({ ok: status >= 200 && status < 300, status, json: async () => body }) as Response;
const fetchMock = vi.fn(async (url: string | URL | Request, init?: RequestInit) => {
  const path = new URL(String(url)).pathname;
  if (path === '/api/auth/refresh') return response(session);
  if (path === '/api/admin/centers') return response(removed ? [] : [{ id: 'c1', name: 'Sucursal de prueba', code: 'TEST', address: 'Test', isActive: false, latitude: null, longitude: null }]);
  if (path.endsWith('/check')) return holdCheck ? new Promise<Response>(() => {}) : response(check, check.canDelete ? 200 : 409);
  if (init?.method === 'DELETE') {
    if (deleteError === 403) return response({ title: 'Contraseña incorrecta.' }, 403);
    if (deleteError === 409) return response({ canDelete: false, message: 'Apareció información vinculada.', dependencies: [{ type: 'Appointments', count: 1 }] }, 409);
    removed = true; return response(null, 204);
  }
  throw new Error(`Unexpected ${path}`);
});
beforeEach(() => {
  removed = false; deleteError = 0; holdCheck = false; onDeleted.mockReset(); fetchMock.mockClear();
  check = { canDelete: true, message: 'Esta acción no se puede deshacer.', dependencies: [] };
  session = { accessToken: 'test', expiresAt: '', user: { id: 'admin', firstName: 'Admin', lastName: 'Test', email: 'test@example.test', roles: ['ADMINISTRATOR'], permissions: ['centers.view'] } };
  setSession(session); vi.stubGlobal('fetch', fetchMock);
});
function mount(isProtected = false) { render(<AuthProvider><ProtectedDeleteButton entity="centers" id="c1" label="Sucursal de prueba" protected={isProtected} onDeleted={onDeleted} /></AuthProvider>); }
async function open() { const user = userEvent.setup(); await user.click(screen.getByRole('button', { name: 'Eliminar Sucursal de prueba' })); return user; }
const deletes = () => fetchMock.mock.calls.filter(([, init]) => init?.method === 'DELETE');

it('shows privileged delete action and centered accessible dialog', async () => {
  mount(); await open(); const dialog = await screen.findByRole('dialog');
  expect(dialog.getAttribute('aria-modal')).toBe('true'); expect(dialog.parentElement?.className).toBe('protected-delete-overlay');
  expect(dialog.className).toBe('protected-delete-dialog'); expect(dialog.getAttribute('aria-labelledby')).toBeTruthy();
});
it('shows checking state without password', async () => {
  holdCheck = true; mount(); await open(); expect(screen.getByRole('status').textContent).toContain('Verificando relaciones');
  expect(screen.queryByLabelText('Contraseña actual')).toBeNull();
});
it('shows dependencies without password or delete confirmation', async () => {
  check = { canDelete: false, message: 'Este registro tiene información vinculada.', dependencies: [{ type: 'DoctorSchedules', count: 4 }, { type: 'Appointments', count: 12 }] };
  mount(); await open(); expect(await screen.findByText('Horarios: 4')).toBeTruthy(); expect(screen.getByText('Citas: 12')).toBeTruthy();
  expect(screen.queryByLabelText('Contraseña actual')).toBeNull(); expect(screen.queryByRole('button', { name: 'Eliminar definitivamente' })).toBeNull();
  await userEvent.click(screen.getByRole('button', { name: 'Entendido' })); expect(screen.queryByRole('dialog')).toBeNull(); expect(deletes()).toHaveLength(0);
});
it('asks current password only for an allowed record', async () => {
  mount(); await open(); expect(await screen.findByLabelText('Contraseña actual')).toBeTruthy();
  expect(screen.queryByLabelText(/email/i)).toBeNull(); expect((screen.getByRole('button', { name: 'Eliminar definitivamente' }) as HTMLButtonElement).disabled).toBe(true);
});
it('wrong password remains actionable and clears sensitive input', async () => {
  deleteError = 403; mount(); const user = await open(); await user.type(await screen.findByLabelText('Contraseña actual'), 'wrong-password');
  await user.click(screen.getByRole('button', { name: 'Eliminar definitivamente' }));
  expect((await screen.findByRole('alert')).textContent).toBe('Contraseña incorrecta.');
  expect((screen.getByLabelText('Contraseña actual') as HTMLInputElement).value).toBe(''); expect(onDeleted).not.toHaveBeenCalled();
});
it('successful confirmation removes actual row from Centers list', async () => {
  render(<AuthProvider><CentersPage /></AuthProvider>); await screen.findByText('Sucursal de prueba');
  const user = await open(); await user.type(await screen.findByLabelText('Contraseña actual'), 'current-password');
  await user.click(screen.getByRole('button', { name: 'Eliminar definitivamente' }));
  await screen.findByText('No hay sucursales. Crea la primera sede de atención.'); expect(screen.queryByText('Sucursal de prueba')).toBeNull();
  expect(deletes()).toHaveLength(1); expect(String(deletes()[0][0])).not.toContain('password');
  expect(JSON.parse(String(deletes()[0][1]?.body))).toEqual({ password: 'current-password' });
});
it('cancel never deletes and restores trigger focus', async () => {
  mount(); const user = await open(); await screen.findByLabelText('Contraseña actual');
  await user.click(screen.getByRole('button', { name: 'Cancelar' })); expect(screen.queryByRole('dialog')).toBeNull(); expect(deletes()).toHaveLength(0);
  expect(document.activeElement).toBe(screen.getByRole('button', { name: 'Eliminar Sucursal de prueba' }));
});
it('Escape closes and reopening never retains password', async () => {
  mount(); const user = await open(); await user.type(await screen.findByLabelText('Contraseña actual'), 'temporary'); await user.keyboard('{Escape}');
  expect(screen.queryByRole('dialog')).toBeNull(); await open(); expect((await screen.findByLabelText('Contraseña actual') as HTMLInputElement).value).toBe('');
});
it('keyboard focus stays inside modal', async () => {
  mount(); const user = await open(); await screen.findByLabelText('Contraseña actual'); await user.tab();
  expect(document.activeElement).toBe(screen.getByLabelText('Contraseña actual')); await user.tab();
  expect(document.activeElement).toBe(screen.getByRole('button', { name: 'Mostrar contraseña' })); await user.tab();
  expect(document.activeElement).toBe(screen.getByRole('button', { name: 'Cancelar' })); await user.tab();
  expect(document.activeElement).toBe(screen.getByLabelText('Contraseña actual'));
});
it('non administrator sees no delete action even with edit and disable permissions', async () => {
  session.user.roles = ['SECRETARY']; session.user.permissions.push('centers.update', 'centers.disable'); mount();
  await waitFor(() => expect(fetchMock).toHaveBeenCalled()); expect(screen.queryByRole('button')).toBeNull();
});
it('protected records show no delete action', () => { mount(true); expect(screen.queryByRole('button')).toBeNull(); });
it('authenticated user cannot see self delete', () => {
  render(<AuthProvider><ProtectedDeleteButton entity="users" id="admin" label="Admin" onDeleted={onDeleted} /></AuthProvider>);
  expect(screen.queryByRole('button')).toBeNull();
});
it('dependency appearing after check switches to blocked state', async () => {
  deleteError = 409; mount(); const user = await open(); await user.type(await screen.findByLabelText('Contraseña actual'), 'current');
  await user.click(screen.getByRole('button', { name: 'Eliminar definitivamente' })); await screen.findByText('Citas: 1');
  expect(screen.queryByLabelText('Contraseña actual')).toBeNull(); expect(onDeleted).not.toHaveBeenCalled();
});
it('failed relation lookup fails closed', async () => {
  fetchMock.mockImplementationOnce(async () => response(session));
  mount(); fetchMock.mockImplementationOnce(async () => response({ title: 'Servicio no disponible' }, 500));
  await open(); expect((await screen.findByRole('alert')).textContent).toBe('Servicio no disponible');
  expect(within(screen.getByRole('dialog')).queryByLabelText('Contraseña actual')).toBeNull();
});
