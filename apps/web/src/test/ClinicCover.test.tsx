import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, expect, it, vi } from 'vitest';
import { AuthProvider } from '../modules/auth/AuthProvider';
import { setSession } from '../services/api';
import ClinicCoverUploader from '../modules/admin/clinic/ClinicCoverUploader';
import { coverCrop, renderCover } from '../modules/admin/clinic/coverCrop';
import ClinicHeroBanner from '../modules/public/ClinicHeroBanner';
import PublicHomePage from '../modules/public/PublicHomePage';

const metadata = { contentType: 'image/png', fileName: 'portada.png', width: 1916, height: 821, size: 1234, focalPointX: .3, focalPointY: .6, altText: 'Instalaciones', updatedAt: '2026-10-15T12:00:00Z' };
let exists = false, fail = false, permission = true;
const drawImage = vi.fn(), encode = vi.fn();
const fetchMock = vi.fn(async (input: unknown, init?: RequestInit): Promise<Response> => {
  const path = new URL(String(input)).pathname;
  if (path === '/api/auth/refresh') return Response.json({ accessToken: 'test', expiresAt: '', user: { id: 'u', firstName: 'Admin', lastName: '', email: '', roles: [], permissions: permission ? ['clinic.view', 'clinic.update'] : ['clinic.view'] } });
  if (path.endsWith('/meta')) return exists ? Response.json(metadata) : new Response(null, { status: 404 });
  if (path === '/api/admin/clinic/site-images/hero') {
    if (fail) return Response.json({ title: 'Imagen inválida.' }, { status: 400 });
    if (init?.method === 'DELETE') { exists = false; return new Response(null, { status: 204 }); }
    if (init?.method === 'PUT') { exists = true; return Response.json(metadata); }
    return exists ? Response.json(metadata) : new Response(null, { status: 404 });
  }
  return new Response(null, { status: 404 });
});
beforeEach(() => {
  exists = false; fail = false; permission = true; vi.clearAllMocks(); vi.stubGlobal('fetch', fetchMock);
  setSession({ accessToken: 'test', expiresAt: '', user: { id: 'u', firstName: 'Admin', lastName: '', email: '', roles: [], permissions: ['clinic.view', 'clinic.update'] } });
  Object.defineProperty(URL, 'createObjectURL', { configurable: true, value: vi.fn(() => 'blob:cover') });
  Object.defineProperty(URL, 'revokeObjectURL', { configurable: true, value: vi.fn() });
  HTMLDialogElement.prototype.showModal = function () { this.setAttribute('open', ''); };
  HTMLElement.prototype.setPointerCapture = vi.fn();
  vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue({ drawImage, imageSmoothingEnabled: false, imageSmoothingQuality: 'low' } as unknown as CanvasRenderingContext2D);
  vi.spyOn(HTMLCanvasElement.prototype, 'toBlob').mockImplementation(function (this: HTMLCanvasElement, callback, type) { encode(this.width, this.height); callback(new Blob(['pixels'], { type })); });
});
function mount() {
  if (!permission) setSession({ accessToken: 'test', expiresAt: '', user: { id: 'u', firstName: '', lastName: '', email: '', roles: [], permissions: ['clinic.view'] } });
  return render(<AuthProvider><ClinicCoverUploader /></AuthProvider>);
}
function loadCrop(width = 1920, height = 1080) {
  const image = screen.getByAltText('Imagen para recortar');
  Object.defineProperties(image, { naturalWidth: { configurable: true, value: width }, naturalHeight: { configurable: true, value: height } }); fireEvent.load(image);
}
async function select() {
  await screen.findByRole('button', { name: /Subir portada|Cambiar portada/ });
  await userEvent.upload(screen.getByLabelText('Archivo de portada'), new File(['pixels'], 'photo.png', { type: 'image/png' })); loadCrop();
}
it('muestra sección, carga y ausencia de portada', async () => {
  mount(); expect(screen.getByRole('status').textContent).toContain('Cargando portada');
  expect(await screen.findByRole('button', { name: 'Subir portada' })).toBeTruthy(); expect(screen.queryByAltText('Vista previa de portada')).toBeNull();
});
it('abre editor, conserva ratio y exporta 1916 × 821 antes de guardar con FormData', async () => {
  mount(); await select(); expect(screen.getByRole('dialog', { name: 'Editar foto de portada' })).toBeTruthy();
  await userEvent.click(screen.getByRole('button', { name: 'Aplicar recorte' }));
  expect(encode).toHaveBeenCalledWith(1916, 821); expect(await screen.findByAltText('Vista previa de portada')).toBeTruthy();
  expect(fetchMock.mock.calls.some(([, init]) => init?.method === 'PUT')).toBe(false);
  await userEvent.type(screen.getByLabelText('Texto alternativo'), 'Clínica');
  await userEvent.click(screen.getByRole('button', { name: 'Guardar portada' }));
  await screen.findByText('Portada guardada.'); const call = fetchMock.mock.calls.find(([, init]) => init?.method === 'PUT')!;
  expect(call[1]!.body).toBeInstanceOf(FormData); expect((call[1]!.body as FormData).get('altText')).toBe('Clínica');
  expect((call[1]!.headers as Record<string, string>)['Content-Type']).toBeUndefined();
});
it('zoom más/menos, slider, teclado y reset cambian el encuadre', async () => {
  mount(); await select(); const slider = screen.getByLabelText('Zoom') as HTMLInputElement;
  await userEvent.click(screen.getByLabelText('Acercar')); expect(Number(slider.value)).toBeCloseTo(1.1);
  await userEvent.click(screen.getByLabelText('Alejar')); expect(slider.value).toBe('1');
  fireEvent.change(slider, { target: { value: '2' } }); expect(slider.value).toBe('2');
  const image = screen.getByAltText('Imagen para recortar'); const before = image.style.left;
  fireEvent.keyDown(screen.getByRole('group', { name: 'Encuadre de portada' }), { key: 'ArrowRight' }); expect(image.style.left).not.toBe(before);
  await userEvent.click(screen.getByRole('button', { name: 'Restablecer' })); expect(slider.value).toBe('1');
});
it('arrastrar mueve la imagen sin salir del encuadre', async () => {
  mount(); await select(); fireEvent.change(screen.getByLabelText('Zoom'), { target: { value: '2' } });
  const frame = screen.getByRole('group', { name: 'Encuadre de portada' });
  vi.spyOn(frame, 'getBoundingClientRect').mockReturnValue({ width: 600, height: 257 } as DOMRect);
  // PointerEvent is not available in jsdom; dispatch coordinate-bearing events.
  const pointer = (type: string, x: number) => { const event = new Event(type, { bubbles: true }); Object.defineProperties(event, { clientX: { value: x }, clientY: { value: 100 }, pointerId: { value: 1 } }); fireEvent(frame, event); };
  const before = screen.getByAltText('Imagen para recortar').style.left;
  pointer('pointerdown', 100); pointer('pointermove', 150); pointer('pointerup', 150);
  expect(screen.getByAltText('Imagen para recortar').style.left).not.toBe(before);
});
it('cancelar no sube ni cambia la portada anterior', async () => {
  exists = true; mount(); await select(); await userEvent.click(screen.getByRole('button', { name: 'Cancelar' }));
  expect(screen.queryByRole('dialog')).toBeNull(); expect(screen.getByAltText('Instalaciones').getAttribute('src')).toContain('/api/clinic/site-images/hero?v=');
  expect(fetchMock.mock.calls.some(([, init]) => init?.method === 'PUT')).toBe(false);
});
it('cambia portada existente sin crear otra sección', async () => {
  exists = true; mount(); await select(); await userEvent.click(screen.getByRole('button', { name: 'Aplicar recorte' }));
  await userEvent.click(await screen.findByRole('button', { name: 'Guardar portada' })); await screen.findByText('Portada guardada.');
  expect(screen.getAllByRole('region', { name: 'Foto de portada del inicio' })).toHaveLength(1);
});
it('requiere confirmar eliminación y permite cancelarla', async () => {
  exists = true; mount(); await userEvent.click(await screen.findByRole('button', { name: 'Eliminar portada' }));
  expect(fetchMock.mock.calls.some(([, init]) => init?.method === 'DELETE')).toBe(false);
  await userEvent.click(screen.getByRole('button', { name: 'Cancelar eliminación' }));
  await userEvent.click(screen.getByRole('button', { name: 'Eliminar portada' }));
  await userEvent.click(screen.getByRole('button', { name: 'Confirmar eliminación' }));
  await screen.findByText('Portada eliminada.'); expect(screen.queryByAltText('Instalaciones')).toBeNull();
});
it('sin permiso oculta acciones', async () => { permission = false; mount(); await waitFor(() => expect(screen.queryByRole('status')).toBeNull()); expect(screen.queryByRole('button')).toBeNull(); });
it.each(['image/svg+xml', 'text/plain'])('rechaza formato %s', async type => {
  mount(); await screen.findByRole('button', { name: 'Subir portada' });
  fireEvent.change(screen.getByLabelText('Archivo de portada'), { target: { files: [new File(['x'], 'fake.png', { type })] } });
  expect(screen.getByRole('alert').textContent).toContain('PNG'); expect(screen.queryByRole('dialog')).toBeNull();
});
it('rechaza más de 10 MB', async () => {
  mount(); await screen.findByRole('button', { name: 'Subir portada' });
  fireEvent.change(screen.getByLabelText('Archivo de portada'), { target: { files: [new File([new Uint8Array(10 * 1024 * 1024 + 1)], 'big.png', { type: 'image/png' })] } });
  expect(screen.getByRole('alert').textContent).toContain('10 MB');
});
it('error de guardado conserva preview para reintentar', async () => {
  mount(); await select(); await userEvent.click(screen.getByRole('button', { name: 'Aplicar recorte' })); fail = true;
  await userEvent.click(await screen.findByRole('button', { name: 'Guardar portada' })); expect((await screen.findByRole('alert')).textContent).toContain('Imagen inválida');
  expect(screen.getByAltText('Vista previa de portada')).toBeTruthy();
});
it.each([[1920, 1080], [3000, 2000], [1000, 1000], [4000, 1000]])('recorte %s × %s mantiene ratio y límites', (w, h) => {
  for (const zoom of [1, 2, 3]) for (const x of [0, .5, 1]) for (const y of [0, .5, 1]) {
    const crop = coverCrop(w, h, zoom, x, y); expect(crop.width / crop.height).toBeCloseTo(1916 / 821);
    expect(crop.x).toBeGreaterThanOrEqual(0); expect(crop.y).toBeGreaterThanOrEqual(0);
    expect(crop.x + crop.width).toBeLessThanOrEqual(w + .001); expect(crop.y + crop.height).toBeLessThanOrEqual(h + .001);
  }
});
it('Canvas usa recorte y resolución exacta', async () => {
  const image = new Image(); Object.defineProperties(image, { naturalWidth: { value: 1920 }, naturalHeight: { value: 1080 } });
  await renderCover(image, 2, .4, .3, 'image/webp'); expect(encode).toHaveBeenCalledWith(1916, 821);
  expect(drawImage.mock.calls[0].slice(-4)).toEqual([0, 0, 1916, 821]);
});
it('portada configurable después del equipo y antes de la llamada a reservar', async () => {
  exists = true; const { container } = render(<PublicHomePage section={null} />);
  const image = await screen.findByAltText('Instalaciones'); fireEvent.load(image);
  const banner = container.querySelector('.clinic-hero-banner')!;
  expect(banner.hasAttribute('hidden')).toBe(false); expect(image.style.objectPosition).toBe('30% 60%');
  expect(container.querySelector('main')!.firstElementChild).not.toBe(banner);
  expect(banner.previousElementSibling).toBe(container.querySelector('#equipo'));
  expect(banner.nextElementSibling).toBe(container.querySelector('.public-cta'));
  expect(image.getAttribute('src')).toContain('/api/clinic/site-images/hero?v=');
  expect(container.querySelectorAll('.clinic-hero-banner')).toHaveLength(1);
  expect(container.querySelector('header')!.compareDocumentPosition(banner) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  expect(screen.getByRole('heading', { level: 1 }).textContent).toContain('Cuidamos tu sonrisa');
  expect(container.querySelector('.hero-tooth')).toBeTruthy();
});
it('sin portada no hay wrapper, placeholder ni hueco y conserva hero', async () => {
  const { container } = render(<PublicHomePage section={null} />); await act(async () => {});
  expect(container.querySelector('.clinic-hero-banner')).toBeNull(); expect(container.querySelector('.hero-tooth')).toBeTruthy();
  expect(screen.getByRole('heading', { level: 1 }).textContent).toContain('Cuidamos tu sonrisa');
});
it('fallo de imagen no deja hueco y cambio de versión invalida caché', async () => {
  exists = true; const { container } = render(<ClinicHeroBanner />); const image = await screen.findByAltText('Instalaciones'); fireEvent.error(image);
  expect(container.innerHTML).toBe(''); exists = false; fireEvent(window, new Event('clinic-cover-changed')); await act(async () => {}); expect(container.innerHTML).toBe('');
});
