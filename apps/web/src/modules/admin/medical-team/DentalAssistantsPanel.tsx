import PasswordField from '../../../components/PasswordField';
import { useEffect, useRef, useState, type FormEvent } from 'react';
import { api } from '../../../services/api';
import { localToday } from '../../patient/PatientFields';
import { Feedback, Fields, FormActions, readFields, StateBadge, useAction, usePermissions } from '../shared';
import { personalFields, accountFields } from './fields';

interface Assistant {
  id: string; userId: string; firstName: string; lastName: string; ci: string | null;
  birthDate: string | null; gender: string | null; phone: string | null; address: string | null;
  email: string | null; isActive: boolean; userIsActive: boolean; canLogin: boolean;
}
interface AssistantList { items: Assistant[]; total: number; page: number; pageSize: number }
const endpoint = '/api/admin/dental-assistants';

export default function DentalAssistantsPanel({ creating, closeCreate }: { creating: boolean; closeCreate: () => void }) {
  const can = usePermissions(); const action = useAction();
  const [list, setList] = useState<AssistantList | null>(null);
  const [page, setPage] = useState(1); const [revision, setRevision] = useState(0);
  const [editing, setEditing] = useState<Assistant | null>(null);
  const [loading, setLoading] = useState(true);
  const formRef = useRef<HTMLFormElement>(null);
  useEffect(() => {
    let active = true; setLoading(true);
    api<AssistantList>(`${endpoint}?page=${page}&pageSize=10`)
      .then(value => { if (active) setList(value); })
      .catch(error => { if (active) action.setError(error.message); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [page, revision]);
  useEffect(() => { if (creating) setEditing(null); }, [creating]);
  useEffect(() => {
    if (creating || editing) formRef.current?.querySelector<HTMLInputElement>('input')?.focus();
  }, [creating, editing]);
  const edit = creating ? null : editing;
  const cancel = () => { setEditing(null); closeCreate(); };
  function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (action.busy || !can(edit ? 'doctors.update' : 'doctors.create')) return;
    const form = new FormData(event.currentTarget);
    if (String(form.get('birthDate')) > localToday()) { action.setError('La fecha de nacimiento no puede ser futura.'); return; }
    if (!edit && form.get('password') !== form.get('confirmPassword')) { action.setError('Las contraseñas no coinciden.'); return; }
    const body = { ...readFields(event, personalFields), ...(!edit ? {
      ...readFields(event, accountFields), password: String(form.get('password')), confirmPassword: String(form.get('confirmPassword')),
    } : {}) };
    void action.run(async () => {
      await api(edit ? `${endpoint}/${edit.id}` : endpoint, edit ? 'PUT' : 'POST', body);
      cancel(); setRevision(value => value + 1);
    }, edit ? 'Asistente dental actualizado.' : 'Asistente dental creado.');
  }
  return <section aria-labelledby="assistants-title">
    <h2 id="assistants-title">Asistentes dentales</h2>
    <Feedback {...action} />
    {(creating && can('doctors.create') || edit && can('doctors.update')) && <section className="card">
      <h3>{edit ? 'Editar asistente dental' : 'Nuevo asistente dental'}</h3>
      <form ref={formRef} className="editor" key={edit?.id ?? 'new-assistant'} onSubmit={save}>
        <h4 className="full-width">Datos personales</h4>
        <Fields fields={personalFields} value={edit ?? {}} disabled={action.busy} />
        {!edit && <><h4 className="full-width">Cuenta de acceso</h4>
          <Fields fields={accountFields} disabled={action.busy} />
          <PasswordField label="Contraseña" name="password"  autoComplete="new-password" required minLength={12} maxLength={128} disabled={action.busy} />
          <PasswordField label="Confirmar contraseña" name="confirmPassword"  autoComplete="new-password" required maxLength={128} disabled={action.busy} />
        </>}
        <FormActions busy={action.busy} label="Guardar asistente dental" cancel={cancel} />
      </form>
    </section>}
    <section className="card table-card">
      {loading ? <p role="status">Cargando asistentes dentales…</p> : list && <>
        {list.items.length === 0 ? <p>No hay asistentes dentales en esta página.</p> : <table aria-label="Asistentes dentales">
          <thead><tr><th>Nombre</th><th>CI</th><th>Email</th><th>Teléfono</th><th>Estado del perfil</th><th>Estado de cuenta</th><th>Acciones</th></tr></thead>
          <tbody>{list.items.map(assistant => <tr key={assistant.id}>
            <td>{assistant.firstName} {assistant.lastName}</td><td>{assistant.ci ?? '—'}</td><td>{assistant.email ?? '—'}</td><td>{assistant.phone ?? '—'}</td>
            <td><StateBadge active={assistant.isActive} /></td><td><StateBadge active={assistant.userIsActive && assistant.canLogin} /></td>
            <td>{can('doctors.update') && <button className="secondary" disabled={action.busy} onClick={() => void action.run(async () => {
              const current = await api<Assistant>(`${endpoint}/${assistant.id}`); closeCreate(); setEditing(current);
            }, '')}>Editar asistente</button>}</td>
          </tr>)}</tbody>
        </table>}
        <div className="pagination"><span>{list.total} asistentes dentales · Página {page}</span>
          <button className="secondary" disabled={page === 1 || action.busy} onClick={() => { cancel(); setPage(value => value - 1); }}>Anterior</button>
          <button className="secondary" disabled={page * 10 >= list.total || action.busy} onClick={() => { cancel(); setPage(value => value + 1); }}>Siguiente</button>
        </div>
      </>}
    </section>
  </section>;
}
