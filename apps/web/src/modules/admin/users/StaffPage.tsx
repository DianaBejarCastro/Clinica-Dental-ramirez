import ProfilePhotoField, { ProfileAvatar, persistProfilePhoto, useCanManagePhotos } from '../../../components/ProfilePhoto';
import { documentLabel } from '../../../components/IdentityDocumentFields';
import { useEffect, useState, type FormEvent } from 'react';
import { api, type User } from '../../../services/api';
import { usePermissions, PageTitle, Feedback, FormActions, StateBadge } from '../shared';
import { localToday } from '../../patient/PatientFields';
import StaffIdentityFields from './StaffIdentityFields';

export const staffScreens = {
  administrators: { title: 'Administradores', singular: 'administrador' },
  cashiers: { title: 'Cajeros', singular: 'cajero' },
  secretaries: { title: 'Secretarias', singular: 'secretaria' },
};
export default function StaffPage({ kind }: { kind: keyof typeof staffScreens }) {
  const canManagePhotos = useCanManagePhotos(); const [photo, setPhoto] = useState<Blob | null | undefined>(); const [photoWarning, setPhotoWarning] = useState('');
  const screen = staffScreens[kind]; const can = usePermissions();
  const [items, setItems] = useState<User[]>([]); const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1); const [query, setQuery] = useState(''); const [search, setSearch] = useState('');
  const [creating, setCreating] = useState(false); const [loading, setLoading] = useState(true); const [busy, setBusy] = useState(false);
  const [error, setError] = useState(''); const [revision, setRevision] = useState(0);
  useEffect(() => {
    let current = true; setLoading(true); setError('');
    api<{ items: User[]; total: number }>(`/api/admin/${kind}?page=${page}&pageSize=10&q=${encodeURIComponent(search)}`)
      .then(data => { if (current) { setItems(data.items); setTotal(data.total); } })
      .catch(e => { if (current) setError(e.message); }).finally(() => { if (current) setLoading(false); });
    return () => { current = false; };
  }, [kind, page, search, revision]);
  async function save(e: FormEvent<HTMLFormElement>) {
    e.preventDefault(); const data = Object.fromEntries(new FormData(e.currentTarget)); setError('');
    if (String(data.birthDate) > localToday()) { setError('La fecha de nacimiento no puede ser futura.'); return; }
    if (data.password !== data.confirmPassword) { setError('Las contraseñas no coinciden.'); return; }
    setBusy(true);
    try { const saved = await api<User>(`/api/admin/${kind}`, 'POST', data); if (canManagePhotos && photo !== undefined) setPhotoWarning(await persistProfilePhoto(saved.id, photo)); setCreating(false); setRevision(r => r + 1); }
    catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  }
  return <><PageTitle title={screen.title} description="Identidad y cuenta de acceso. Los roles adicionales se administran desde Usuarios.">
    {can('users.create') && can('roles.assign_permissions') && <button onClick={() => setCreating(true)}>Crear {screen.singular}</button>}
  </PageTitle><Feedback error={error} /><Feedback error={photoWarning} />
    {creating && <section className="card"><h2>Crear {screen.singular}</h2><form className="editor" onSubmit={save}>
      <StaffIdentityFields creation />{canManagePhotos && <ProfilePhotoField onChange={setPhoto} disabled={busy} />}<FormActions busy={busy} label={`Guardar ${screen.singular}`} cancel={() => setCreating(false)} />
    </form></section>}
    <form className="actions" onSubmit={e => { e.preventDefault(); setPage(1); setSearch(query); }}><label>Buscar por nombre, documento o email<input value={query} onChange={e => setQuery(e.target.value)} maxLength={200} /></label><button className="secondary">Buscar</button></form>
    <section className="card table-card">{loading ? <p role="status">Cargando…</p> : <><table><thead><tr><th>Nombre</th><th>Documento</th><th>Email</th><th>Roles</th><th>Estado</th><th>Acciones</th></tr></thead><tbody>{items.map(user => <tr key={user.id}>
      <td><ProfileAvatar userId={user.id} firstName={user.firstName} lastName={user.lastName} />{user.firstName} {user.lastName}</td><td>{documentLabel(user)}</td><td>{user.email ?? '—'}</td><td>{user.roles.map(r => r.name).join(', ')}</td><td><StateBadge active={user.isActive} /></td><td><a href={`#/users?user=${user.id}`}>Administrar usuario</a></td>
    </tr>)}</tbody></table>{!items.length && <p>No hay registros.</p>}</>}
      <div className="pagination"><span>{total} registros · Página {page}</span><button className="secondary" disabled={page === 1 || loading} onClick={() => setPage(p => p - 1)}>Anterior</button><button className="secondary" disabled={page * 10 >= total || loading} onClick={() => setPage(p => p + 1)}>Siguiente</button></div>
    </section></>;
}
