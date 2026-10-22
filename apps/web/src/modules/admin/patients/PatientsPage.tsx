import { documentLabel } from '../../../components/IdentityDocumentFields';
import { PatientFields, PatientAccountFields, patientData, patientError, accountData } from '../../patient/PatientFields';
import ProtectedDeleteButton from '../deletion/ProtectedDeleteButton';
import { useEffect, useState, type FormEvent } from 'react';
import { api } from '../../../services/api';
import { Feedback, FormActions, PageTitle, StateBadge, useAction, usePermissions } from '../shared';
import type { Patient, PatientList } from './types';

const accountLabel = (canLogin: boolean) => canLogin ? 'Cuenta vinculada' : 'Paciente clínico sin cuenta';

export default function PatientsPage() {
  const can = usePermissions(); const action = useAction();
  const [list, setList] = useState<PatientList | null>(null);
  const [page, setPage] = useState(1); const [revision, setRevision] = useState(0);
  const [loading, setLoading] = useState(true);
  const [editing, setEditing] = useState<Patient | 'new' | null>(null);
  const [createAccount, setCreateAccount] = useState(false);
  const [accountPatient, setAccountPatient] = useState<Patient | null>(null);
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
    const error = patientError(data); if (error) { action.setError(error); return; }
    let account;
    try { account = !edit && createAccount ? accountData(data) : null; } catch (e) { action.setError((e as Error).message); return; }
    const body = { ...patientData(data, true), ...(!edit ? { account } : {}),
      isActive: can('patients.disable') ? data.has('isActive') : edit?.isActive ?? true };
    void action.run(async () => {
      await api(edit ? `/api/admin/patients/${edit.id}` : '/api/admin/patients', edit ? 'PUT' : 'POST', body);
      setEditing(null); setRevision(value => value + 1);
    }, 'Paciente guardado.');
  }
  function open(patient: Patient) {
    void action.run(async () => {
      const detail = await api<Patient>(`/api/admin/patients/${patient.id}`);
      setEditing(detail); setAccountPatient(null);
    }, '');
  }
  return <>
    <PageTitle title="Pacientes" description="Datos del paciente clínico. La cuenta de acceso es opcional.">
      {can('patients.create') && <button disabled={action.busy} onClick={() => { setEditing('new'); setCreateAccount(false); setAccountPatient(null); }}>Crear paciente</button>}
    </PageTitle>
    <Feedback {...action} />
    {editing && maySave && <section className="card">
      <h2>{edit ? 'Editar paciente' : 'Nuevo paciente'}</h2>
      <form className="editor" key={edit?.id ?? 'new'} onSubmit={save}>
        <PatientFields value={edit ?? {}} admin disabled={action.busy} />
        {!edit && <fieldset className="full-width"><legend>Cuenta de acceso</legend>
          <label className="check"><input type="checkbox" checked={createAccount} onChange={e => setCreateAccount(e.target.checked)} disabled={action.busy} />Crear cuenta de acceso para este paciente</label>
          {createAccount && <div className="editor"><PatientAccountFields disabled={action.busy} /></div>}
        </fieldset>}
        {edit && <p className="muted">{accountLabel(edit.canLogin)}. Las credenciales se gestionan por separado.</p>}
        <label className="check"><input type="checkbox" name="isActive" defaultChecked={edit?.isActive ?? true}
          disabled={action.busy || !can('patients.disable')} />Paciente activo</label>
        <FormActions busy={action.busy} label="Guardar paciente" cancel={() => setEditing(null)} />
      </form>
    </section>}
    {accountPatient && can('patients.update') && <section className="card"><h2>Crear cuenta de acceso</h2><p>{accountPatient.firstName} {accountPatient.lastName}</p>
      <form className="editor" onSubmit={event => { event.preventDefault(); const form = new FormData(event.currentTarget); void action.run(async () => {
        await api(`/api/admin/patients/${accountPatient.id}/account`, 'POST', accountData(form)); setAccountPatient(null); setRevision(v => v + 1);
      }, 'Cuenta vinculada.'); }}><PatientAccountFields disabled={action.busy} /><FormActions busy={action.busy} label="Crear cuenta" cancel={() => setAccountPatient(null)} /></form>
    </section>}
    <section className="card table-card">
      {loading && <p role="status">Cargando pacientes…</p>}
      {!loading && list && <>
        <table><thead><tr><th>Nombre completo</th><th>Documento</th><th>Fecha de nacimiento</th><th>Teléfono</th><th>Email</th><th>Cuenta</th><th>Estado</th><th>Acciones</th></tr></thead>
          <tbody>{list.items.map(patient => <tr key={patient.id}>
            <td>{patient.firstName} {patient.lastName}</td><td>{documentLabel(patient)}</td><td>{patient.birthDate ?? '—'}</td>
            <td>{patient.phone ?? '—'}</td><td>{patient.email ?? '—'}</td><td>{accountLabel(patient.canLogin)}</td><td><StateBadge active={patient.isActive} /></td>
            <td><div className="actions">
              {can('patients.update') && <button className="secondary" disabled={action.busy} onClick={() => open(patient)}>Editar</button>}
              {can('patients.update') && !patient.canLogin && <button className="secondary" disabled={action.busy} onClick={() => { setEditing(null); setAccountPatient(patient); }}>Crear cuenta de acceso</button>}
              {can('patients.disable') && <button className="secondary" disabled={action.busy} onClick={() => void action.run(async () => {
                await api(`/api/admin/patients/${patient.id}/status`, 'PATCH', { isActive: !patient.isActive });
                setEditing(null); setRevision(value => value + 1);
              }, 'Estado del paciente actualizado.')}>{patient.isActive ? 'Desactivar' : 'Activar'}</button>}
              <ProtectedDeleteButton entity="patients" id={patient.id} label={`${patient.firstName} ${patient.lastName}`} onDeleted={() => { setEditing(null); setRevision(v => v + 1); }} />
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
