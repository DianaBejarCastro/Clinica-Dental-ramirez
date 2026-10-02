import { useEffect, useState, type FormEvent } from 'react';
import { api } from '../../../services/api';
import { Feedback, Fields, FormActions, PageTitle, readFields, StateBadge, useAction, usePermissions, type FieldSpec } from '../shared';
import type { Patient, PatientList } from './types';

const fields: FieldSpec[] = [
  { name: 'firstName', label: 'Nombre', required: true, maxLength: 100 },
  { name: 'lastName', label: 'Apellido', required: true, maxLength: 100 },
  { name: 'ci', label: 'CI', maxLength: 50 },
  { name: 'birthDate', label: 'Fecha de nacimiento', type: 'date' },
  { name: 'gender', label: 'Género', maxLength: 50 },
  { name: 'phone', label: 'Teléfono', maxLength: 30 },
  { name: 'email', label: 'Email', type: 'email', maxLength: 254 },
  { name: 'address', label: 'Dirección', maxLength: 500 },
  { name: 'emergencyContactName', label: 'Contacto de emergencia', maxLength: 200 },
  { name: 'emergencyContactPhone', label: 'Teléfono de emergencia', maxLength: 30 },
  { name: 'bloodType', label: 'Grupo sanguíneo', maxLength: 10 },
  { name: 'notes', label: 'Notas', type: 'textarea', maxLength: 4000 },
];
const accountLabel = (userId: string | null) => userId ? 'Cuenta vinculada' : 'Paciente clínico sin cuenta';

export default function PatientsPage() {
  const can = usePermissions(); const action = useAction();
  const [list, setList] = useState<PatientList | null>(null);
  const [page, setPage] = useState(1); const [revision, setRevision] = useState(0);
  const [loading, setLoading] = useState(true);
  const [editing, setEditing] = useState<Patient | 'new' | null>(null);
  const [userId, setUserId] = useState('');
  const edit = editing && editing !== 'new' ? editing : null;
  const maySave = can(edit ? 'patients.update' : 'patients.create');
  useEffect(() => {
    let active = true;
    setLoading(true); setList(null); action.setError('');
    api<PatientList>(`/api/admin/patients?page=${page}&pageSize=10`)
      .then(value => { if (active) setList(value); })
      .catch(error => { if (active) action.setError(error.message); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [page, revision]);
  function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!maySave || action.busy) return;
    const data = new FormData(event.currentTarget);
    const body = { ...readFields(event, fields), userId: userId.trim() || null,
      isActive: can('patients.disable') ? data.has('isActive') : edit?.isActive ?? true };
    void action.run(async () => {
      await api(edit ? `/api/admin/patients/${edit.id}` : '/api/admin/patients', edit ? 'PUT' : 'POST', body);
      setEditing(null); setRevision(value => value + 1);
    }, 'Paciente guardado.');
  }
  function open(patient: Patient) {
    void action.run(async () => {
      const detail = await api<Patient>(`/api/admin/patients/${patient.id}`);
      setEditing(detail); setUserId(detail.userId ?? '');
    }, '');
  }
  return <>
    <PageTitle title="Pacientes" description="Datos del paciente clínico. La cuenta de acceso es opcional.">
      {can('patients.create') && <button disabled={action.busy} onClick={() => { setEditing('new'); setUserId(''); }}>Crear paciente</button>}
    </PageTitle>
    <Feedback {...action} />
    {editing && maySave && <section className="card">
      <h2>{edit ? 'Editar paciente' : 'Nuevo paciente'}</h2>
      <form className="editor" key={edit?.id ?? 'new'} onSubmit={save}>
        <Fields fields={fields} value={edit ?? {}} disabled={action.busy} />
        <label>Usuario vinculado (ID opcional)
          <input value={userId} onChange={event => setUserId(event.target.value)} disabled={action.busy}
            pattern="[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}" />
        </label>
        <p className="muted">{accountLabel(userId.trim() || null)}. Si vinculas una cuenta, debe existir y tener el rol PATIENT. El email clínico no cambia el email de acceso.</p>
        <label className="check"><input type="checkbox" name="isActive" defaultChecked={edit?.isActive ?? true}
          disabled={action.busy || !can('patients.disable')} />Paciente activo</label>
        <FormActions busy={action.busy} label="Guardar paciente" cancel={() => setEditing(null)} />
      </form>
    </section>}
    <section className="card table-card">
      {loading && <p role="status">Cargando pacientes…</p>}
      {!loading && list && <>
        <table><thead><tr><th>Nombre completo</th><th>CI</th><th>Fecha de nacimiento</th><th>Teléfono</th><th>Email</th><th>Cuenta</th><th>Estado</th><th>Acciones</th></tr></thead>
          <tbody>{list.items.map(patient => <tr key={patient.id}>
            <td>{patient.firstName} {patient.lastName}</td><td>{patient.ci ?? '—'}</td><td>{patient.birthDate ?? '—'}</td>
            <td>{patient.phone ?? '—'}</td><td>{patient.email ?? '—'}</td><td>{accountLabel(patient.userId)}</td><td><StateBadge active={patient.isActive} /></td>
            <td><div className="actions">
              {can('patients.update') && <button className="secondary" disabled={action.busy} onClick={() => open(patient)}>Editar</button>}
              {can('patients.disable') && <button className="secondary" disabled={action.busy} onClick={() => void action.run(async () => {
                await api(`/api/admin/patients/${patient.id}/status`, 'PATCH', { isActive: !patient.isActive });
                setEditing(null); setRevision(value => value + 1);
              }, 'Estado del paciente actualizado.')}>{patient.isActive ? 'Desactivar' : 'Activar'}</button>}
            </div></td>
          </tr>)}</tbody>
        </table>
        {list.items.length === 0 && <p>No hay pacientes en esta página.</p>}
      </>}
      <div className="pagination"><span>{list ? `${list.total} pacientes · Página ${page}` : `Página ${page}`}</span>
        <button className="secondary" disabled={page === 1 || loading || action.busy} onClick={() => { setEditing(null); setPage(value => value - 1); }}>Anterior</button>
        <button className="secondary" disabled={!list || page * 10 >= list.total || loading || action.busy} onClick={() => { setEditing(null); setPage(value => value + 1); }}>Siguiente</button>
      </div>
    </section>
  </>;
}
