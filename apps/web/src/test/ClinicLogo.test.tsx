import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, expect, it, vi } from 'vitest';
import { AuthProvider } from '../modules/auth/AuthProvider';
import ClinicLogoUploader from '../modules/admin/clinic/ClinicLogoUploader';
import ClinicLogo from '../modules/admin/clinic/ClinicLogo';
import ClinicPage from '../modules/admin/clinic/ClinicPage';
import { notifyLogoChanged, type ClinicLogoMetadata } from '../modules/admin/clinic/logoResource';
import { setSession, type Session } from '../services/api';

const metadata: ClinicLogoMetadata = { contentType: 'image/png', fileName: 'logo.png', size: 1200, updatedAt: '2026-10-08T10:00:00Z' };
let failure = false, expire = false, permissions: string[];
const session = (): Session => ({ accessToken: 'token', expiresAt: '', user: {
  id: 'u1', firstName: 'Ana', lastName: 'Prueba', email: 'test@example.com', roles: [], permissions,
} });
const onChanged = vi.fn(); const createURL = vi.fn(() => 'blob:logo-preview'); const revokeURL = vi.fn();
const fetchMock = vi.fn(async (input: string | URL | Request, init?: RequestInit) => {
  const path = new URL(String(input)).pathname;
  if (path === '/api/auth/refresh') return Response.json(session());
  if (path === '/api/admin/clinic/site-images/hero') return Response.json({ title: 'No hay portada.' }, { status: 404 });
  if (path === '/api/admin/clinic/social-networks') return Response.json([]);
  if (path === '/api/admin/clinic/social-networks') return Response.json([]);
  if (path === '/api/admin/clinic/logo') {
    if (expire) { expire = false; return Response.json({ title: 'Sesión expirada' }, { status: 401 }); }
    if (failure) return Response.json({ title: 'Contenido inválido.' }, { status: 400 });
    return init?.method === 'DELETE' ? new Response(null, { status: 204 }) : Response.json(metadata);
  }
  if (path === '/api/admin/clinic') return Response.json({ id: 'clinic', name: 'Clínica', isActive: true, logo: null });
  throw new Error(`Petición inesperada: ${path}`);
});
beforeEach(() => {
  permissions = ['clinic.view', 'clinic.update']; failure = false; expire = false;
  setSession(session()); onChanged.mockClear(); fetchMock.mockClear(); createURL.mockClear(); revokeURL.mockClear();
  vi.stubGlobal('fetch', fetchMock);
  Object.defineProperty(URL, 'createObjectURL', { configurable: true, writable: true, value: createURL });
  Object.defineProperty(URL, 'revokeObjectURL', { configurable: true, writable: true, value: revokeURL });
});
function mount(logo: ClinicLogoMetadata | null = null, sidebar = false) {
  return render(<AuthProvider>{sidebar && <ClinicLogo />}<ClinicLogoUploader logo={logo} onChanged={onChanged} /></AuthProvider>);
}
async function select(file = new File(['original bytes'], 'logo.png', { type: 'image/png' })) {
  const input = await screen.findByLabelText('Archivo del logo');
  await userEvent.setup({ applyAccept: false }).upload(input, file); return file;
}
const uploadCalls = () => fetchMock.mock.calls.filter(([url, init]) => String(url).endsWith('/clinic/logo') && init?.method === 'PUT');

it('muestra placeholder y ayuda sin logo', async () => {
  mount(); expect(await screen.findByText('Sin logo')).toBeTruthy();
  expect(screen.getByText('PNG, JPG o WebP. Máximo 5 MB.')).toBeTruthy(); expect(screen.getByRole('button', { name: 'Subir logo' })).toBeTruthy();
  expect(screen.queryByRole('button', { name: 'Eliminar logo' })).toBeNull();
});
it('muestra preview local y metadata antes de enviar, liberando object URL', async () => {
  const { unmount } = mount(); const file = await select();
  expect(createURL).toHaveBeenCalledWith(file); expect(screen.getByAltText('Vista previa del nuevo logo').getAttribute('src')).toBe('blob:logo-preview');
  expect(screen.getByText('logo.png')).toBeTruthy(); expect(screen.getByText(/image\/png/)).toBeTruthy(); expect(uploadCalls()).toHaveLength(0);
  unmount(); expect(revokeURL).toHaveBeenCalledWith('blob:logo-preview');
});
it.each(['image/svg+xml', 'text/plain'])('rechaza %s', async type => {
  mount(); await select(new File(['not image'], 'fake.png', { type }));
  expect((await screen.findByRole('alert')).textContent).toContain('PNG, JPG o WebP'); expect(createURL).not.toHaveBeenCalled();
});
it('rechaza archivos mayores de 5 MB', async () => {
  mount(); await select(new File([new Uint8Array(5 * 1024 * 1024 + 1)], 'large.png', { type: 'image/png' }));
  expect((await screen.findByRole('alert')).textContent).toContain('5 MB'); expect(uploadCalls()).toHaveLength(0);
});
it('rechaza archivos vacíos', async () => {
  mount(); await select(new File([], 'empty.png', { type: 'image/png' }));
  expect((await screen.findByRole('alert')).textContent).toContain('vacío');
});
it('sube FormData sin fijar boundary y refresca preview con versión', async () => {
  mount(); const file = await select(); await userEvent.click(screen.getByRole('button', { name: 'Guardar logo' }));
  await screen.findByText('Logo guardado.'); expect(onChanged).toHaveBeenCalledWith(metadata);
  const init = uploadCalls()[0][1]!; expect(init.body instanceof FormData).toBe(true);
  expect((init.body as FormData).get('file')).toBe(file); expect(new Headers(init.headers).has('Content-Type')).toBe(false);
  expect(new Headers(init.headers).get('Authorization')).toBe('Bearer token');
  expect(screen.getByAltText('Logo actual de la clínica').getAttribute('src')).toContain('/api/clinic/logo?v=2026-10-08T10%3A00%3A00Z');
  expect(revokeURL).toHaveBeenCalledWith('blob:logo-preview');
});
it('reemplaza un logo guardado', async () => {
  mount(metadata); expect(await screen.findByRole('button', { name: 'Cambiar logo' })).toBeTruthy();
  await select(new File(['webp bytes'], 'new.webp', { type: 'image/webp' })); await userEvent.click(screen.getByRole('button', { name: 'Guardar logo' }));
  await screen.findByText('Logo guardado.'); expect(uploadCalls()).toHaveLength(1);
});
it('elimina logo y regresa al placeholder', async () => {
  mount(metadata); await userEvent.click(await screen.findByRole('button', { name: 'Eliminar logo' }));
  await screen.findByText('Logo eliminado.'); expect(screen.getByText('Sin logo')).toBeTruthy(); expect(onChanged).toHaveBeenCalledWith(null);
  expect(fetchMock.mock.calls.some(([url, init]) => String(url).endsWith('/clinic/logo') && init?.method === 'DELETE')).toBe(true);
});
it('muestra fallo backend sin perder la selección ni el logo guardado', async () => {
  mount(metadata); await select(); failure = true; await userEvent.click(screen.getByRole('button', { name: 'Guardar logo' }));
  expect((await screen.findByRole('alert')).textContent).toBe('Contenido inválido.'); expect(onChanged).not.toHaveBeenCalled();
  expect(screen.getByAltText('Vista previa del nuevo logo')).toBeTruthy(); expect(screen.getByRole('button', { name: 'Eliminar logo' })).toBeTruthy();
});
it('respeta clinic.update para upload y delete', async () => {
  permissions = ['clinic.view']; setSession(session()); mount(metadata);
  await screen.findByText('Logo de la clínica'); expect(screen.queryByRole('button')).toBeNull();
});
it('sidebar usa fallback R si GET falla', () => {
  render(<ClinicLogo />); const image = screen.getByAltText('Logo de la clínica'); fireEvent.error(image);
  expect(screen.getByLabelText('Monograma de la clínica').textContent).toBe('R'); expect(screen.queryByAltText('Logo de la clínica')).toBeNull();
});
it('sidebar muestra imagen al cargar y se actualiza al subir o eliminar', () => {
  render(<ClinicLogo />); const image = screen.getByAltText('Logo de la clínica'); fireEvent.load(image);
  expect((image as HTMLImageElement).hidden).toBe(false); expect(screen.queryByText('R')).toBeNull();
  act(notifyLogoChanged); const updated = screen.getByAltText('Logo de la clínica'); expect(updated.getAttribute('src')).toContain('?v=');
  fireEvent.error(updated); expect(screen.getByText('R')).toBeTruthy();
});
it('subir desde el uploader actualiza también el sidebar montado', async () => {
  mount(null, true); fireEvent.error(screen.getByAltText('Logo de la clínica'));
  await select(); await userEvent.click(screen.getByRole('button', { name: 'Guardar logo' })); await screen.findByText('Logo guardado.');
  const image = screen.getByAltText('Logo de la clínica'); expect(image.getAttribute('src')).toContain('?v='); fireEvent.load(image);
  expect(screen.queryByText('R')).toBeNull();
});
it('renueva sesión y reintenta multipart sin convertir el archivo a JSON', async () => {
  mount(); const file = await select(); expire = true; await userEvent.click(screen.getByRole('button', { name: 'Guardar logo' }));
  await screen.findByText('Logo guardado.'); expect(uploadCalls()).toHaveLength(2);
  for (const [, init] of uploadCalls()) expect((init!.body as FormData).get('file')).toBe(file);
});
it('Datos de la clínica reemplaza Logo URL por el uploader', async () => {
  render(<AuthProvider><ClinicPage /></AuthProvider>); await screen.findByText('Logo de la clínica');
  expect(screen.queryByLabelText('Logo URL')).toBeNull(); await waitFor(() => expect(screen.getByRole('button', { name: 'Subir logo' })).toBeTruthy());
});
