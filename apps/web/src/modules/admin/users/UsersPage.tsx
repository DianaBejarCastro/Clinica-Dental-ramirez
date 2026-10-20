import ProtectedDeleteButton from '../deletion/ProtectedDeleteButton';
import { useEffect, useState, type FormEvent } from 'react';
import { api, type Role, type User } from '../../../services/api';
import { usePermissions, Feedback, FormActions } from '../shared';
import { localToday } from '../../patient/PatientFields';
import StaffIdentityFields from './StaffIdentityFields';
import DomainRoleForm, { domainRoles, type DomainRole } from './DomainRoleForm';

export default function UsersPage({ initialUser = '' }: { initialUser?: string }) {
  const can = usePermissions();
  const [items, setItems] = useState<User[]>([]); const [total, setTotal] = useState(0); const [page, setPage] = useState(1);
  const [query, setQuery] = useState(''); const [search, setSearch] = useState('');
  const [error, setError] = useState(''); const [loading, setLoading] = useState(true); const [busy, setBusy] = useState(false);
  const [editing, setEditing] = useState<User | null>(null); const [assigning, setAssigning] = useState<User | null>(null);
  const [domain, setDomain] = useState<{ user: User; role: DomainRole } | null>(null);
  const [roles, setRoles] = useState<Role[]>([]); const [selected, setSelected] = useState<string[]>([]);
  async function load() {
    setLoading(true);
    try {
      if (initialUser && !search) { const user = await api<User>(`/api/admin/users/${initialUser}`); setItems([user]); setTotal(1); }
      else { const data = await api<{ items: User[]; total: number }>(`/api/admin/users?page=${page}&pageSize=10&q=${encodeURIComponent(search)}`); setItems(data.items); setTotal(data.total); }
    } catch (e) { setError((e as Error).message); } finally { setLoading(false); }
  }
  useEffect(() => { void load(); }, [page, search, initialUser]);
  async function action(work: () => Promise<unknown>) { setError(''); setBusy(true); try { await work(); await load(); } catch (e) { setError((e as Error).message); } finally { setBusy(false); } }
  async function save(e: FormEvent<HTMLFormElement>) {
    e.preventDefault(); const data = Object.fromEntries(new FormData(e.currentTarget));
    if (String(data.birthDate) > localToday()) { setError('La fecha de nacimiento no puede ser futura.'); return; }
    await action(async () => { await api(`/api/admin/users/${editing!.id}`, 'PUT', { ...data, email: data.email || null }); setEditing(null); });
  }
  const simple = (role: { code?: string }) => !domainRoles.some(d => d.code === role.code);
  async function assign(user: User) {
    const all = await api<Role[]>('/api/admin/roles');
    setRoles(all.filter(simple)); setSelected(user.roles.filter(r => all.some(a => a.id === r.id && a.isActive && simple(a))).map(r => r.id));
    setAssigning(user); setEditing(null); setDomain(null);
  }
  return <><div className="title-row"><div><h1>Usuarios</h1><p className="muted">Identidad, acceso y múltiples roles. Crea personas desde su módulo correspondiente.</p></div></div><Feedback error={error} />
    <form className="actions" onSubmit={e => { e.preventDefault(); setPage(1); setSearch(query); }}><label>Buscar por nombre, CI o email<input value={query} onChange={e => setQuery(e.target.value)} maxLength={200} /></label><button className="secondary">Buscar</button></form>
    {editing && <section className="card"><h2>Editar usuario</h2><form key={editing.id} onSubmit={save} className="editor">
      <StaffIdentityFields value={editing} canLogin={editing.canLogin !== false} /><FormActions busy={busy} label="Guardar usuario" cancel={() => setEditing(null)} />
    </form></section>}
    {assigning && <section className="card"><h2>Roles de {assigning.firstName}</h2><p>Roles actuales: {assigning.roles.map(r => r.name).join(', ') || 'Sin roles'}</p><p>Los roles de paciente, doctor y asistente dental se conservan y se gestionan desde sus módulos.</p>
      <form onSubmit={e => { e.preventDefault(); void action(async () => { await api(`/api/admin/users/${assigning.id}/roles`, 'PUT', { roleIds: selected }); setAssigning(null); }); }}>
        <div className="checks">{roles.filter(r => r.isActive).map(r => <label key={r.id}><input type="checkbox" checked={selected.includes(r.id)} onChange={e => setSelected(e.target.checked ? [...selected, r.id] : selected.filter(id => id !== r.id))} />{r.name}</label>)}</div>
        <FormActions busy={busy} label="Guardar roles" cancel={() => setAssigning(null)} />
      </form></section>}
    {domain && <DomainRoleForm key={`${domain.user.id}-${domain.role.code}`} {...domain} cancel={() => setDomain(null)} done={async () => { setDomain(null); await load(); }} />}
    <section className="card table-card">{loading ? <p role="status">Cargando usuarios…</p> : <><table><thead><tr><th>Usuario</th><th>CI</th><th>Roles</th><th>Acceso</th><th>Estado</th><th>Acciones</th></tr></thead><tbody>{items.map(user => <tr key={user.id}>
      <td><strong>{user.firstName} {user.lastName}</strong><small>{user.email}</small></td><td>{user.ci ?? '—'}</td><td>{user.roles.map(r => r.name).join(', ') || 'Sin roles'}</td><td>{user.canLogin === false ? 'Sin cuenta de acceso' : 'Cuenta habilitada'}</td><td><span className={`badge ${user.isActive ? '' : 'inactive'}`}>{user.isActive ? 'Activo' : 'Inactivo'}</span></td>
      <td><div className="actions">
        {can('users.update') && <button className="secondary" disabled={busy} onClick={() => { setEditing(user); setAssigning(null); setDomain(null); }}>Editar</button>}
        {can('users.disable') && <button className="secondary" disabled={busy} onClick={() => void action(() => api(`/api/admin/users/${user.id}/status`, 'PATCH', { isActive: !user.isActive }))}>{user.isActive ? 'Desactivar' : 'Activar'}</button>}
        {can('users.update') && can('roles.assign_permissions') && <>
          {can('roles.view') && <button className="secondary" disabled={busy} onClick={() => void action(() => assign(user))}>Asignar roles</button>}
          {domainRoles.filter(role => can(role.permission) && !(user[role.profileKey] ?? user.roles.some(r => r.code === role.code))).map(role => <button key={role.code} className="secondary" disabled={busy} onClick={() => { setDomain({ user, role }); setEditing(null); setAssigning(null); }}>{user.roles.some(r => r.code === role.code) ? 'Completar perfil' : 'Añadir rol'} {role.label}</button>)}
        </>}
        <ProtectedDeleteButton entity="users" id={user.id} label={`${user.firstName} ${user.lastName}`} onDeleted={async () => { setEditing(null); setAssigning(null); setDomain(null); await load(); }} />
      </div></td></tr>)}</tbody></table>{!items.length && <p>No hay usuarios.</p>}</>}
      <div className="pagination"><span>{total} usuarios · Página {page}</span><button className="secondary" disabled={page === 1 || loading} onClick={() => setPage(p => p - 1)}>Anterior</button><button className="secondary" disabled={page * 10 >= total || loading} onClick={() => setPage(p => p + 1)}>Siguiente</button></div>
    </section></>;
}
