import { useEffect, useState } from 'react';
import { api, apiUrl, getSession } from '../services/api';
import { useAuth } from '../modules/auth/AuthProvider';
import CoverCropEditor from '../modules/admin/clinic/CoverCropEditor';
import '../modules/admin/clinic/clinic-cover.css';
import './profile-photo.css';

const staffRoles = ['ADMINISTRATOR', 'SECRETARY', 'DOCTOR', 'CASHIER', 'DENTAL_ASSISTANT'];
export const isStaff = (roles: { code?: string }[]) => roles.some(r => staffRoles.includes(r.code ?? ''));
export function useCanManagePhotos() {
  const { session } = useAuth();
  return !!session?.user.roles.includes('ADMINISTRATOR') && session.user.permissions.includes('users.update') && session.user.permissions.includes('users.view');
}
function usePhoto(userId?: string) {
  const [url, setUrl] = useState('');
  const [version, refresh] = useState(0);
  const { session } = useAuth();
  const allowed = !!session?.user.roles.includes('ADMINISTRATOR') && session.user.permissions.includes('users.view');
  useEffect(() => {
    const listener = () => refresh(n => n + 1);
    window.addEventListener('profile-photo-changed', listener);
    return () => window.removeEventListener('profile-photo-changed', listener);
  }, []);
  useEffect(() => {
    setUrl(''); if (!userId || !allowed) return;
    const controller = new AbortController(); let objectUrl = '';
    void fetch(apiUrl(`/api/admin/users/${userId}/profile-photo/content`), {
      headers: { Authorization: `Bearer ${getSession()?.accessToken}` }, credentials: 'include', signal: controller.signal, cache: 'no-store',
    }).then(async r => {
      if (!r.ok) return;
      const blob = await r.blob();
      if (controller.signal.aborted) return;
      objectUrl = URL.createObjectURL(blob); setUrl(objectUrl);
    }).catch(() => undefined);
    return () => { controller.abort(); if (objectUrl) URL.revokeObjectURL(objectUrl); };
  }, [userId, allowed, version]);
  return url;
}
export function ProfileAvatar({ userId, firstName, lastName }: { userId: string; firstName: string; lastName: string }) {
  const url = usePhoto(userId);
  return <span className="profile-avatar" aria-label={`Foto de ${firstName} ${lastName}`}>
    {url ? <img src={url} alt="" /> : <span>{(firstName[0] ?? '') + (lastName[0] ?? '')}</span>}
  </span>;
}
export async function persistProfilePhoto(userId: string, photo: Blob | null | undefined): Promise<string> {
  if (photo === undefined) return '';
  try {
    if (photo === null) await api(`/api/admin/users/${userId}/profile-photo`, 'DELETE');
    else { const form = new FormData(); form.append('file', photo, 'profile-photo.webp'); await api(`/api/admin/users/${userId}/profile-photo`, 'PUT', form); }
    window.dispatchEvent(new Event('profile-photo-changed')); return '';
  } catch {
    return 'Los datos de la persona se guardaron, pero no se pudo guardar la foto. Vuelve a editar para intentarlo de nuevo.';
  }
}
export default function ProfilePhotoField({ userId, onChange, disabled = false }: {
  userId?: string; onChange: (value: Blob | null | undefined) => void; disabled?: boolean;
}) {
  const current = usePhoto(userId);
  useEffect(() => { onChange(undefined); }, [userId, onChange]);
  const [file, setFile] = useState<File | null>(null);
  const [photo, setPhoto] = useState<Blob | null | undefined>();
  const [preview, setPreview] = useState(''); const [error, setError] = useState('');
  useEffect(() => { if (!photo) { setPreview(''); return; } const url = URL.createObjectURL(photo); setPreview(url); return () => URL.revokeObjectURL(url); }, [photo]);
  const url = photo === undefined ? current : preview;
  return <div className="profile-photo-field full-width"><h3>Foto de perfil</h3><p className="muted">Opcional · 4:5 · 1080 × 1350 px. Se guarda junto con los datos.</p>
    <div className="profile-photo-preview">{url ? <img src={url} alt="Vista previa de la foto de perfil" /> : <span>Sin foto</span>}</div>
    {error && <p role="alert">{error}</p>}
    <label>{url ? 'Cambiar foto' : 'Subir foto'}<input type="file" accept="image/png,image/jpeg,image/webp" disabled={disabled} onChange={event => {
      const selected = event.target.files?.[0]; event.target.value = ''; setError('');
      if (!selected) return;
      if (!['image/png', 'image/jpeg', 'image/webp'].includes(selected.type) || !selected.size || selected.size > 10 * 1024 * 1024) { setError('Selecciona PNG, JPEG o WebP de hasta 10 MB.'); return; }
      setFile(selected);
    }} /></label>
    {url && <button type="button" className="secondary" disabled={disabled} onClick={() => { setPhoto(null); onChange(null); }}>Eliminar foto</button>}
    {file && <CoverCropEditor file={file} width={1080} height={1350} title="Recortar foto de perfil" onCancel={() => setFile(null)} onApply={blob => { setPhoto(blob); onChange(blob); setFile(null); }} />}
  </div>;
}
