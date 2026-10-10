import { beforeEach, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import ClinicSocialNetworks, { type SocialNetwork } from '../modules/admin/clinic/ClinicSocialNetworks';
import { AuthProvider } from '../modules/auth/AuthProvider';
import { setSession, type Session } from '../services/api';

const root = '/api/admin/clinic/social-networks';
const record = (id = '1', name = 'Instagram', order = 0): SocialNetwork => ({ id, name, url: `https://example.com/${id}`, hasLogo: false, logoContentType: null, logoFileName: null, logoSize: null, displayOrder: order, isActive: true, createdAt: '2026-10-08T12:00:00Z', updatedAt: '2026-10-08T12:00:00Z' });
let items: SocialNetwork[], failure: boolean, permissions: string[];
const session = (): Session => ({ accessToken: 'test', expiresAt: '', user: { id: 'u', firstName: 'Ana', lastName: 'Prueba', email: 'test@example.com', roles: [], permissions } });
const fetchMock = vi.fn(async (input: string | URL | Request, init?: RequestInit) => {
  const path = new URL(String(input)).pathname, method = init?.method ?? 'GET';
  if (path === '/api/auth/refresh') return Response.json(session());
  if (failure && method !== 'GET') return Response.json({ title: 'Esta URL ya está registrada.' }, { status: 409 });
  if (path === root && method === 'GET') return Response.json(items);
  const body = init?.body && !(init.body instanceof FormData) ? JSON.parse(String(init.body)) : {};
  if (path === root && method === 'POST') { const item = { ...record(String(items.length + 1)), ...body }; items.push(item); return Response.json(item, { status: 201 }); }
  const item = items.find(x => path.startsWith(`${root}/${x.id}`));
  if (item) {
    if (path.endsWith('/logo')) {
      item.hasLogo = method !== 'DELETE'; item.updatedAt = '2026-10-08T13:00:00Z';
      return method === 'DELETE' ? new Response(null, { status: 204 }) : Response.json(item);
    }
    if (method !== 'GET') Object.assign(item, body, { updatedAt: '2026-10-08T14:00:00Z' });
    return Response.json(item);
  }
  throw new Error(`Petición inesperada ${path}`);
});
const createURL = vi.fn(() => 'blob:social-preview'), revokeURL = vi.fn();
beforeEach(() => {
  items = [record()]; failure = false; permissions = ['clinic.view', 'clinic.update']; setSession(session()); fetchMock.mockClear(); createURL.mockClear(); revokeURL.mockClear();
  vi.stubGlobal('fetch', fetchMock);
  Object.defineProperty(URL, 'createObjectURL', { configurable: true, value: createURL });
  Object.defineProperty(URL, 'revokeObjectURL', { configurable: true, value: revokeURL });
});
function mount() { return render(<AuthProvider><ClinicSocialNetworks /></AuthProvider>); }
async function edit() { await userEvent.click(await screen.findByRole('button', { name: 'Editar Instagram' })); }
async function select() { const file = new File(['png bytes'], 'social.png', { type: 'image/png' }); await userEvent.upload(screen.getByLabelText(/Subir logo|Cambiar logo/), file); return file; }
async function create(name: string, url: string) {
  await userEvent.click(await screen.findByRole('button', { name: '+ Agregar red social' }));
  await userEvent.type(screen.getByLabelText('Nombre de la red social'), name);
  await userEvent.type(screen.getByLabelText('URL'), url);
  await userEvent.click(screen.getByRole('button', { name: 'Guardar red social' }));
  await screen.findByRole('button', { name: `Editar ${name}` });
}
it('lista redes con enlace y estado', async () => { mount(); expect(await screen.findByText('Instagram')).toBeTruthy(); expect(screen.getByRole('link').getAttribute('href')).toBe('https://example.com/1'); expect(screen.getByText('Activa')).toBeTruthy(); });
it('muestra estado vacío', async () => { items = []; mount(); expect(await screen.findByText('Todavía no hay redes sociales registradas.')).toBeTruthy(); });
it('agrega la primera red sin imagen en JSON', async () => {
  items = []; mount(); await screen.findByText('Todavía no hay redes sociales registradas.'); await create('Facebook', 'https://facebook.com/clinic');
  expect(items).toHaveLength(1); expect(screen.getByLabelText('Subir logo')).toBeTruthy();
  const call = fetchMock.mock.calls.find(([url, init]) => String(url).endsWith(root) && init?.method === 'POST')!; expect(JSON.parse(String(call[1]!.body))).toEqual({ name: 'Facebook', url: 'https://facebook.com/clinic', displayOrder: 0, isActive: true });
});
it('permite agregar múltiples redes', async () => { mount(); await screen.findByText('Instagram'); await create('Facebook', 'https://facebook.com/clinic'); await create('YouTube', 'https://youtube.com/clinic'); expect(items).toHaveLength(3); });
it('edita nombre URL y orden', async () => {
  mount(); await edit(); const input = screen.getByLabelText('Nombre de la red social'); await userEvent.clear(input); await userEvent.type(input, 'Instagram oficial');
  await userEvent.clear(screen.getByLabelText('Orden de visualización')); await userEvent.type(screen.getByLabelText('Orden de visualización'), '3');
  await userEvent.click(screen.getByRole('button', { name: 'Guardar red social' })); await screen.findByRole('button', { name: 'Editar Instagram oficial' }); expect(items[0].displayOrder).toBe(3);
});
it('desactiva sin borrar físicamente', async () => { mount(); await userEvent.click(await screen.findByRole('button', { name: 'Desactivar Instagram' })); expect(await screen.findByText('Inactiva')).toBeTruthy(); expect(items).toHaveLength(1); expect(fetchMock.mock.calls.some(([, init]) => init?.method === 'DELETE')).toBe(false); });
it('muestra preview y libera object URL', async () => { const view = mount(); await edit(); const file = await select(); expect(createURL).toHaveBeenCalledWith(file); expect(screen.getByAltText('Vista previa del logo de red social').getAttribute('src')).toBe('blob:social-preview'); view.unmount(); expect(revokeURL).toHaveBeenCalledWith('blob:social-preview'); });
it('sube con FormData sin Content-Type manual', async () => {
  mount(); await edit(); const file = await select(); await userEvent.click(screen.getByRole('button', { name: 'Guardar logo de red social' })); await screen.findByText('Logo guardado.');
  const call = fetchMock.mock.calls.find(([url, init]) => String(url).endsWith('/logo') && init?.method === 'PUT')!;
  expect((call[1]!.body as FormData).get('file')).toBe(file); expect((call[1]!.headers as Record<string, string>)['Content-Type']).toBeUndefined();
});
it('cambia logo existente y actualiza URL de caché', async () => { items[0].hasLogo = true; mount(); await edit(); await select(); await userEvent.click(screen.getByRole('button', { name: 'Guardar logo de red social' })); await screen.findByText('Logo guardado.'); expect(screen.getAllByAltText('Logo de Instagram').every(img => img.getAttribute('src')?.includes('13%3A00'))).toBe(true); });
it('elimina únicamente el logo', async () => { items[0].hasLogo = true; mount(); await edit(); await userEvent.click(screen.getByRole('button', { name: 'Eliminar logo de red social' })); expect(await screen.findByText('Logo eliminado.')).toBeTruthy(); expect(items[0].hasLogo).toBe(false); expect(items).toHaveLength(1); });
it('muestra error backend sin perder formulario', async () => { failure = true; mount(); await edit(); await userEvent.click(screen.getByRole('button', { name: 'Guardar red social' })); expect((await screen.findByRole('alert')).textContent).toContain('Esta URL ya está registrada'); expect(screen.getByDisplayValue('Instagram')).toBeTruthy(); });
it('ordena por orden y luego nombre', async () => { items = [record('1', 'Zeta', 2), record('2', 'Beta', 1), record('3', 'Alfa', 1)]; mount(); await screen.findByText('Zeta'); expect(within(screen.getByRole('list')).getAllByRole('listitem').map(li => li.querySelector('strong')?.textContent)).toEqual(['Alfa', 'Beta', 'Zeta']); });
it('oculta todas las escrituras sin clinic.update', async () => { permissions = ['clinic.view']; setSession(session()); mount(); await screen.findByText('Instagram'); expect(screen.queryByRole('button')).toBeNull(); });
it.each(['image/svg+xml', 'image/png'])('rechaza formato o tamaño inválido: %s', async type => {
  mount(); await edit(); const file = new File([type === 'image/png' ? new Uint8Array(2097153) : '<svg/>'], 'fake.png', { type });
  await userEvent.setup({ applyAccept: false }).upload(screen.getByLabelText('Subir logo'), file);
  await waitFor(() => expect(screen.getByRole('alert')).toBeTruthy()); expect(createURL).not.toHaveBeenCalled();
});
