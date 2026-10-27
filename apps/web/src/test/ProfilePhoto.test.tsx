import { beforeEach, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import ProfilePhotoField, { ProfileAvatar, persistProfilePhoto, isStaff } from '../components/ProfilePhoto';
import DoctorsPage from '../modules/admin/doctors/DoctorsPage';
import StaffPage from '../modules/admin/users/StaffPage';
import UsersPage from '../modules/admin/users/UsersPage';
import CoverCropEditor from '../modules/admin/clinic/CoverCropEditor';
import { coverCrop, renderCover } from '../modules/admin/clinic/coverCrop';
const api = vi.hoisted(() => vi.fn());
vi.mock('../services/api', () => ({ api, apiUrl: (p: string) => p, getSession: () => ({ accessToken: 'test' }) }));
vi.mock('../modules/auth/AuthProvider', () => ({ useAuth: () => ({ session: { user: { roles: ['ADMINISTRATOR'], permissions: ['users.view', 'users.update', 'users.create', 'roles.assign_permissions', 'doctors.view', 'doctors.create', 'doctors.update'] } } }) }));
const encode = vi.fn((callback: BlobCallback) => callback(new Blob(['image'], { type: 'image/webp' })));
beforeEach(() => {
  api.mockReset(); vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: false }));
  Object.defineProperty(URL, 'createObjectURL', { configurable: true, value: vi.fn(() => 'blob:photo') });
  Object.defineProperty(URL, 'revokeObjectURL', { configurable: true, value: vi.fn() });
  HTMLDialogElement.prototype.showModal = vi.fn(function(this: HTMLDialogElement) { this.setAttribute('open', ''); });
  HTMLCanvasElement.prototype.getContext = vi.fn(() => ({ drawImage: vi.fn() })) as unknown as typeof HTMLCanvasElement.prototype.getContext;
  HTMLCanvasElement.prototype.toBlob = encode;
});
it.each(['ADMINISTRATOR','SECRETARY','DOCTOR','CASHIER','DENTAL_ASSISTANT'])('allows staff identity %s but not patient-only', role => {
  expect(isStaff([{ code: role }])).toBe(true); expect(isStaff([{ code: 'PATIENT' }])).toBe(false);
});
it('mixed patient and staff retains one eligible user identity', () => expect(isStaff([{ code: 'PATIENT' }, { code: 'DOCTOR' }])).toBe(true));
it('initials fallback and existing photo are accessible', async () => {
  const { rerender } = render(<ProfileAvatar userId="u" firstName="Juan" lastName="Ramirez" />);
  expect(screen.getByText('JR')).toBeTruthy();
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: true, blob: async () => new Blob(['image']) }));
  rerender(<ProfileAvatar userId="other" firstName="Juan" lastName="Ramirez" />);
  await waitFor(() => expect(document.querySelector('.profile-avatar img')?.getAttribute('src')).toBe('blob:photo'));
});
it('existing photo can be removed without deleting the user', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: true, blob: async () => new Blob(['image']) }));
  const change = vi.fn(); render(<ProfilePhotoField userId="u" onChange={change} />);
  await screen.findByAltText('Vista previa de la foto de perfil');
  await userEvent.click(screen.getByRole('button', { name: 'Eliminar foto' }));
  expect(change).toHaveBeenLastCalledWith(null);
});
it('rejects invalid or oversized originals before opening crop', () => {
  render(<ProfilePhotoField onChange={vi.fn()} />);
  fireEvent.change(screen.getByLabelText('Subir foto'), { target: { files: [new File(['bad'], 'bad.svg', { type: 'image/svg+xml' })] } });
  expect(screen.getByRole('alert')).toBeTruthy(); expect(screen.queryByRole('dialog')).toBeNull();
});
it('opens shared portrait crop and cancel leaves draft untouched', async () => {
  const change = vi.fn(); render(<ProfilePhotoField onChange={change} />);
  fireEvent.change(screen.getByLabelText('Subir foto'), { target: { files: [new File(['png'], 'p.png', { type: 'image/png' })] } });
  expect(screen.getByText(/1080 × 1350 px. Arrastra/)).toBeTruthy();
  await userEvent.click(screen.getByRole('button', { name: 'Cancelar' }));
  expect(screen.queryByText('Recortar foto de perfil')).toBeNull(); expect(change).toHaveBeenCalledTimes(1);
});
it.each([[2400,1600],[1600,2400],[1080,1350]])('crop preserves 4:5 without stretching %s×%s', (w,h) => {
  const crop = coverCrop(w,h,2,.3,.8,1080,1350);
  expect(crop.width/crop.height).toBeCloseTo(.8);
  expect(crop.x+crop.width).toBeLessThanOrEqual(w); expect(crop.y+crop.height).toBeLessThanOrEqual(h);
});
it('renders exactly 1080x1350', async () => {
  const image = document.createElement('img'); Object.defineProperties(image,{naturalWidth:{value:2400},naturalHeight:{value:1600}});
  await renderCover(image,1,.5,.5,'image/webp',1080,1350);
  expect(encode.mock.instances.at(-1)).toMatchObject({ width: 1080, height: 1350 });
});
it('zoom, keyboard position and reset retain portrait frame', async () => {
  render(<CoverCropEditor file={new File(['png'],'p.png',{type:'image/png'})} width={1080} height={1350} title="Recortar foto de perfil" onApply={vi.fn()} onCancel={vi.fn()} />);
  const image=screen.getByAltText('Imagen para recortar');
  Object.defineProperties(image,{naturalWidth:{value:2400},naturalHeight:{value:1600}}); fireEvent.load(image);
  const frame=screen.getByRole('group'); expect(frame.style.aspectRatio).toBe('1080 / 1350');
  await userEvent.click(screen.getByLabelText('Acercar')); expect(screen.getByRole('slider').getAttribute('value')).toBe('1.1');
  const before=image.style.left; fireEvent.keyDown(frame,{key:'ArrowRight'}); expect(image.style.left).not.toBe(before);
  await userEvent.click(screen.getByText('Restablecer')); expect(screen.getByRole('slider').getAttribute('value')).toBe('1');
});
it('unchanged photo sends no bytes; replacement and deletion use user endpoint', async () => {
  expect(await persistProfilePhoto('u',undefined)).toBe(''); expect(api).not.toHaveBeenCalled();
  api.mockResolvedValue({}); await persistProfilePhoto('u',new Blob(['image'],{type:'image/webp'}));
  expect(api).toHaveBeenLastCalledWith('/api/admin/users/u/profile-photo','PUT',expect.any(FormData));
  await persistProfilePhoto('u',null); expect(api).toHaveBeenLastCalledWith('/api/admin/users/u/profile-photo','DELETE');
});
it('optional photo failure reports saved person without rollback or recreation', async () => {
  api.mockRejectedValue(new Error('failure'));
  expect(await persistProfilePhoto('u',new Blob(['image']))).toContain('Los datos de la persona se guardaron');
  expect(api).toHaveBeenCalledTimes(1);
});

it.each(['Crear doctor', 'Crear asistente dental'])('shared photo field in %s, no legacy URL', async button => {
  api.mockResolvedValue({ items: [], total: 0 }); render(<DoctorsPage />);
  await userEvent.click(screen.getByRole('button', { name: button }));
  expect(screen.getByLabelText('Subir foto')).toBeTruthy(); expect(screen.queryByLabelText('Foto URL')).toBeNull();
});
it.each([['administrators','administrador'],['secretaries','secretaria'],['cashiers','cajero']] as const)('shared photo field in staff creation %s', async (kind, label) => {
  api.mockResolvedValue({ items: [], total: 0 }); render(<StaffPage kind={kind} />);
  await userEvent.click(screen.getByRole('button', { name: `Crear ${label}` }));
  expect(screen.getByLabelText('Subir foto')).toBeTruthy();
});
it.each(['ADMINISTRATOR','SECRETARY','CASHIER','DOCTOR','DENTAL_ASSISTANT','PATIENT'])('user editing restricts photo field by staff identity %s', async role => {
  api.mockResolvedValue({ items: [{ id:'u', firstName:'Ana', lastName:'Test', email:'a@example.test', isActive:true, roles:[{id:'r',code:role,name:role}] }], total:1 });
  render(<UsersPage />); await userEvent.click(await screen.findByRole('button',{name:'Editar'}));
  expect(!!screen.queryByLabelText('Subir foto')).toBe(role !== 'PATIENT');
});
