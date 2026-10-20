import PasswordField from '../../../components/PasswordField';
import { useEffect, useId, useRef, useState, type FormEvent } from 'react';
import { createPortal } from 'react-dom';
import { api, ApiError } from '../../../services/api';
import { useAuth } from '../../auth/AuthProvider';
import './protected-delete.css';

type Entity = 'centers' | 'specialties' | 'doctors' | 'schedules' | 'patients' | 'appointments' | 'social-networks' | 'users' | 'roles';
interface Check { canDelete: boolean; message: string; dependencies: { type: string; count: number }[]; protected?: boolean }
interface Props { entity: Entity; id: string; label: string; protected?: boolean; onDeleted: () => void | Promise<void> }
const dependencyLabels: Record<string, string> = {
  DoctorCenters: 'Asignaciones a sucursales', DoctorSchedules: 'Horarios', Appointments: 'Citas',
  DoctorSpecialties: 'Asignaciones de especialidades', DoctorProfiles: 'Perfiles de doctor', PatientProfiles: 'Pacientes',
  UserRoles: 'Asignaciones de roles', RolePermissions: 'Permisos asignados', AuditLogs: 'Registros de auditoría', RefreshTokens: 'Historial de sesiones',
};
function blocked(error: unknown): Check | null {
  if (!(error instanceof ApiError) || error.status !== 409) return null;
  const data = error.details as Check | undefined;
  return data?.canDelete === false && Array.isArray(data.dependencies) ? data : null;
}

export function ProtectedDeleteDialog({ entity, id, label, onDeleted, close }: Props & { close: () => void }) {
  const [check, setCheck] = useState<Check | null>(null), [error, setError] = useState('');
  const [password, setPassword] = useState(''), [busy, setBusy] = useState(false);
  const root = useRef<HTMLDivElement>(null); const title = useId(); const description = useId();
  const path = `/api/admin/deletion/${entity}/${id}`;
  useEffect(() => {
    const previous = document.activeElement as HTMLElement | null;
    root.current?.focus();
    const oldOverflow = document.body.style.overflow; document.body.style.overflow = 'hidden';
    return () => { document.body.style.overflow = oldOverflow; if (previous?.isConnected) previous.focus(); };
  }, []);
  useEffect(() => {
    let active = true;
    api<Check>(`${path}/check`).then(value => { if (active) setCheck(value); }).catch(e => {
      if (active) { const result = blocked(e); if (result) setCheck(result); else setError(e.message); }
    });
    return () => { active = false; };
  }, [path]);
  async function confirm(event: FormEvent) {
    event.preventDefault(); if (!check?.canDelete || !password || busy) return;
    setBusy(true); setError('');
    try {
      await api(path, 'DELETE', { password });
      setPassword('');
      // Close before refreshing: a failed list refresh must never offer a second DELETE.
      close(); await onDeleted();
    } catch (e) {
      const result = blocked(e); if (result) setCheck(result);
      else setError((e as Error).message);
    } finally { setPassword(''); setBusy(false); }
  }
  return createPortal(<div className="protected-delete-overlay">
    <div className="protected-delete-dialog" ref={root} role="dialog" aria-modal="true" aria-labelledby={title} aria-describedby={description} tabIndex={-1}
      onKeyDown={event => {
        if (event.key === 'Escape') { event.preventDefault(); if (!busy) close(); }
        if (event.key === 'Tab') {
          const elements = Array.from(root.current!.querySelectorAll<HTMLElement>('button:not(:disabled), input:not(:disabled)'));
          const first = elements[0], last = elements[elements.length - 1];
          if (!first) { event.preventDefault(); return; }
          if (event.shiftKey && (document.activeElement === first || document.activeElement === root.current)) { event.preventDefault(); last.focus(); }
          else if (!event.shiftKey && (document.activeElement === last || document.activeElement === root.current)) { event.preventDefault(); first.focus(); }
        }
      }}>
      <h2 id={title}>{check?.canDelete ? entity === 'centers' ? 'Eliminar sucursal definitivamente' : 'Eliminar definitivamente' : check ? entity === 'centers' ? 'No se puede eliminar esta sucursal' : 'No se puede eliminar' : 'Verificar eliminación'}</h2>
      <p id={description}>{label}</p>
      {!check && !error && <p role="status">Verificando relaciones…</p>}
      {check && <p>{check.message}</p>}
      {entity === 'centers' && !!check?.dependencies.length && <p>Esta sucursal tiene información vinculada y su eliminación podría afectar datos clínicos o administrativos.</p>}
      {check && check.dependencies.length > 0 && <ul>{check.dependencies.map(d => <li key={d.type}>{dependencyLabels[d.type] ?? d.type}: {d.count}</li>)}</ul>}
      {error && <p className="error" role="alert">{error}</p>}
      {check?.canDelete ? <form onSubmit={event => void confirm(event)}>
        <PasswordField label="Contraseña actual"  autoComplete="current-password" required maxLength={128} value={password} disabled={busy} onChange={event => setPassword(event.target.value)} />
        <div className="actions"><button type="button" className="secondary" disabled={busy} onClick={close}>Cancelar</button>
          <button className="protected-delete-confirm" disabled={busy || !password}>{busy ? 'Eliminando…' : 'Eliminar definitivamente'}</button></div>
      </form> : <button type="button" className="secondary" onClick={close}>{check ? 'Entendido' : 'Cancelar'}</button>}
    </div>
  </div>, document.body);
}

export default function ProtectedDeleteButton(props: Props) {
  const { session } = useAuth(); const [open, setOpen] = useState(false);
  if (!session?.user.roles.includes('ADMINISTRATOR') || props.protected || (props.entity === 'users' && props.id === session.user.id)) return null;
  return <><button type="button" className="secondary protected-delete-trigger" aria-label={`Eliminar ${props.label}`} onClick={() => setOpen(true)}>Eliminar</button>
    {open && <ProtectedDeleteDialog {...props} close={() => setOpen(false)} />}</>;
}
