import { beforeEach, afterEach, expect, it, vi } from 'vitest';
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import ClinicServices from '../modules/admin/clinic/ClinicServices';
import ServicesSection, { ServicesMarquee } from '../modules/public/ServicesSection';
import type { ClinicService } from '../modules/clinicServices';

const api = vi.hoisted(() => vi.fn());
const permission = vi.hoisted(() => ({ edit: true }));
vi.mock('../services/api', () => ({ api, apiUrl: (path: string) => path }));
vi.mock('../modules/admin/shared', async importOriginal => ({ ...await importOriginal<object>(), usePermissions: () => () => permission.edit }));
vi.mock('../modules/admin/deletion/ProtectedDeleteButton', () => ({ default: () => <button>Eliminar protegido</button> }));
const item: ClinicService = { id: 'one', name: 'Atención de prueba', shortDescription: 'Descripción breve', visualVariant: 'TEAL', displayOrder: 1, isActive: true, isPublic: true, specialtyId: null };
let rows: ClinicService[], reduced: boolean, frames: Map<number, FrameRequestCallback>, next: number;
beforeEach(() => {
  rows = [{ ...item }]; reduced = false; permission.edit = true; frames = new Map(); next = 0;
  api.mockReset().mockImplementation(async (path: string, method = 'GET', body?: Partial<ClinicService>) => {
    if (path.endsWith('/specialties')) return [];
    if (method === 'POST') { const created = { ...item, ...body, id: 'new' }; rows.push(created); return created; }
    if (method === 'PUT') { rows[0] = { ...rows[0], ...body }; return rows[0]; }
    if (method === 'PATCH') { rows[0] = { ...rows[0], ...body }; return; }
    return [...rows];
  });
  vi.stubGlobal('fetch', vi.fn(async () => ({ ok: true, json: async () => rows })));
  vi.stubGlobal('ResizeObserver', class { observe() {} disconnect() {} });
  vi.stubGlobal('requestAnimationFrame', (fn: FrameRequestCallback) => { frames.set(++next, fn); return next; });
  vi.stubGlobal('cancelAnimationFrame', (id: number) => frames.delete(id));
  window.matchMedia = vi.fn(() => ({ matches: reduced, addEventListener() {}, removeEventListener() {} }) as unknown as MediaQueryList);
  vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockReturnValue({ width: 700 } as DOMRect);
  HTMLElement.prototype.setPointerCapture = vi.fn();
  vi.stubGlobal('PointerEvent', class extends MouseEvent { pointerId = 1; isPrimary = true; });
});
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals(); });
function tick(time: number) { act(() => { const callbacks = [...frames.values()]; frames.clear(); callbacks.forEach(fn => fn(time)); }); }
it('lists services, order and controlled visual variants', async () => {
  render(<ClinicServices />); expect(await screen.findByText(item.name)).toBeTruthy(); expect(screen.getByText(/Orden: 1/)).toBeTruthy();
  await userEvent.click(screen.getByRole('button', { name: 'Editar' })); expect(screen.getByLabelText('Color / estilo').querySelectorAll('option')).toHaveLength(6);
});
it('creates with public, active, order and optional specialty', async () => {
  rows = []; render(<ClinicServices />); await screen.findByText('No hay servicios configurados.');
  const u = userEvent.setup(); await u.click(screen.getByText('+ Agregar servicio')); await u.type(screen.getByLabelText('Nombre'), 'Nuevo servicio');
  await u.selectOptions(screen.getByLabelText('Color / estilo'), 'NAVY'); await u.click(screen.getByText('Guardar servicio'));
  await waitFor(() => expect(api).toHaveBeenCalledWith('/api/admin/clinic/services', 'POST', expect.objectContaining({ name: 'Nuevo servicio', visualVariant: 'NAVY', isActive: true, isPublic: true, specialtyId: null })));
});
it('edits and deactivates', async () => {
  render(<ClinicServices />); await screen.findByText(item.name); const u = userEvent.setup();
  await u.click(screen.getByText('Editar')); await u.clear(screen.getByLabelText('Nombre')); await u.type(screen.getByLabelText('Nombre'), 'Editado'); await u.click(screen.getByText('Guardar servicio'));
  await screen.findByText('Editado'); await u.click(screen.getByText('Desactivar')); await screen.findByText('Activar');
  expect(api).toHaveBeenCalledWith('/api/admin/clinic/services/one/status', 'PATCH', { isActive: false });
});
it('requires a name and rejects whitespace', async () => {
  render(<ClinicServices />); await screen.findByText(item.name); await userEvent.click(screen.getByText('+ Agregar servicio'));
  expect((screen.getByLabelText('Nombre') as HTMLInputElement).required).toBe(true);
  fireEvent.change(screen.getByLabelText('Nombre'), { target: { value: '   ' } }); fireEvent.submit(screen.getByRole('form'));
  await screen.findByText('Escribe el nombre del servicio.'); expect(api.mock.calls.some(x => x[1] === 'POST')).toBe(false);
});
it('preserves read-only permissions', async () => {
  permission.edit = false; render(<ClinicServices />); await screen.findByText(item.name);
  expect(screen.queryByText('+ Agregar servicio')).toBeNull(); expect(screen.queryByText('Editar')).toBeNull();
});
it('loads public services once and hides an empty section', async () => {
  rows = []; const { container } = render(<ServicesSection />); await waitFor(() => expect(fetch).toHaveBeenCalledTimes(1)); expect(container.innerHTML).toBe('');
});
it('renders dynamic services and hides duplicated accessible content', async () => {
  render(<ServicesSection />); expect(await screen.findByRole('heading', { name: item.name })).toBeTruthy();
  expect(screen.getAllByRole('heading', { name: item.name })).toHaveLength(1);
});
it('moves continuously left and can pause and resume', async () => {
  const { container } = render(<ServicesMarquee services={[item]} />); const track = container.querySelector('.services-track') as HTMLElement;
  tick(100); const first = track.style.transform; tick(116); expect(track.style.transform).not.toBe(first);
  await userEvent.click(screen.getByText('Pausar movimiento')); const stopped = track.style.transform; tick(132); expect(track.style.transform).toBe(stopped);
  await userEvent.click(screen.getByText('Reanudar movimiento')); expect(frames.size).toBe(1);
});
it('pauses while dragging and retains the last meaningful direction', () => {
  const { container } = render(<ServicesMarquee services={[item]} />); const track = container.querySelector('.services-track') as HTMLElement, view = screen.getByRole('region');
  const x = () => Number(track.style.transform.match(/\((-?[\d.]+)/)?.[1]);
  fireEvent.pointerDown(view, { clientX: 100, button: 0 }); fireEvent.pointerMove(view, { clientX: 150 }); const dragged = x();
  tick(100); tick(116); expect(x()).toBe(dragged); fireEvent.pointerUp(view); tick(132); expect(x()).toBeGreaterThan(dragged);
  fireEvent.pointerDown(view, { clientX: 150, button: 0 }); fireEvent.pointerMove(view, { clientX: 130 }); fireEvent.pointerUp(view);
  const left = x(); tick(148); expect(x()).toBeLessThan(left);
});
it('ignores tiny direction changes and supports pointer cancellation', () => {
  const { container } = render(<ServicesMarquee services={[item]} />); const view = screen.getByRole('region');
  fireEvent.pointerDown(view, { clientX: 100, button: 0 }); fireEvent.pointerMove(view, { clientX: 103 }); fireEvent.pointerCancel(view);
  const track = container.querySelector('.services-track') as HTMLElement; tick(100); const before = track.style.transform; tick(116); expect(track.style.transform).not.toBe(before);
});
it('reduced motion disables autoplay but preserves keyboard exploration', () => {
  reduced = true; const { container } = render(<ServicesMarquee services={[item]} />); expect(frames.size).toBe(0);
  const track = container.querySelector('.services-track') as HTMLElement; const before = track.style.transform;
  fireEvent.keyDown(screen.getByRole('region'), { key: 'ArrowRight' }); expect(track.style.transform).not.toBe(before);
});
it('many services retain API order and accessible numbering is not repeated', () => {
  const list = Array.from({ length: 10 }, (_, i) => ({ ...item, id: String(i), name: `Servicio ${i}` }));
  render(<ServicesMarquee services={list} />); expect(screen.getAllByRole('heading').map(x => x.textContent)).toEqual(list.map(x => x.name));
});
