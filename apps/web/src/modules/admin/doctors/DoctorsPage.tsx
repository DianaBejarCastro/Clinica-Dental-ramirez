import PasswordField from '../../../components/PasswordField';
import DentalAssistantsPanel from '../medical-team/DentalAssistantsPanel';
import { personalFields, accountFields } from '../medical-team/fields';
import { localToday } from '../../patient/PatientFields';
import ProtectedDeleteButton from '../deletion/ProtectedDeleteButton';
import { useEffect, useState, type FormEvent } from 'react';
import { api } from '../../../services/api';
import { Feedback, Fields, FormActions, PageTitle, readFields, StateBadge, toggleId, useAction, usePermissions, type FieldSpec } from '../shared';
import type { Assignment, Doctor, DoctorPage, DoctorSchedule } from '../types';
import { days, timeLabel } from '../schedules/scheduleValidation';

const profileFields: FieldSpec[] = [
  { name: 'professionalLicense', label: 'Matrícula profesional', required: true, maxLength: 100 },
  { name: 'photoUrl', label: 'Foto URL', type: 'url', maxLength: 2048 },
  { name: 'bio', label: 'Biografía', type: 'textarea', maxLength: 4000 },
];

type AssignmentKind = 'specialties' | 'centers';
export default function DoctorsPage() {
  const can = usePermissions(); const action = useAction(); const [items, setItems] = useState<Doctor[]>([]); const [total, setTotal] = useState(0); const [page, setPage] = useState(1); const [loaded, setLoaded] = useState(false);
  const [creatingAssistant, setCreatingAssistant] = useState(false);
  const [editing, setEditing] = useState<Doctor | 'new' | null>(null);
  const [detail, setDetail] = useState<Doctor | null>(null); const [schedules, setSchedules] = useState<DoctorSchedule[]>([]);
  const [assignment, setAssignment] = useState<{ doctor: Doctor; kind: AssignmentKind } | null>(null); const [choices, setChoices] = useState<Assignment[]>([]); const [selected, setSelected] = useState<string[]>([]);
  async function load() { const result = await api<DoctorPage>(`/api/admin/doctors?page=${page}&pageSize=10`); setItems(result.items); setTotal(result.total); setLoaded(true); }
  useEffect(() => { load().catch(e => action.setError(e.message)); }, [page]);
  const edit = editing && editing !== 'new' ? editing : null;
  function create() { setCreatingAssistant(false); setEditing('new'); setAssignment(null); setDetail(null); }
  function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); const data = new FormData(event.currentTarget);
    if (String(data.get('birthDate')) > localToday()) { action.setError('La fecha de nacimiento no puede ser futura.'); return; }
    if (!edit && data.get('password') !== data.get('confirmPassword')) { action.setError('Las contraseñas no coinciden.'); return; }
    const body = { ...readFields(event, personalFields), ...readFields(event, profileFields),
      isActive: edit ? can('doctors.disable') ? data.has('isActive') : edit.isActive : true,
      ...(!edit ? { ...readFields(event, accountFields), password: String(data.get('password')), confirmPassword: String(data.get('confirmPassword')) } : {}) };
    void action.run(async () => { await api(edit ? `/api/admin/doctors/${edit.id}` : '/api/admin/doctors', edit ? 'PUT' : 'POST', body); setEditing(null); await load(); }, edit ? 'Perfil profesional actualizado.' : 'Doctor creado. Se aseguró el rol DOCTOR en su cuenta.');
  }
  async function openAssignment(doctor: Doctor, kind: AssignmentKind) {
    await action.run(async () => { const [options, current] = await Promise.all([api<Assignment[]>(`/api/admin/${kind}`), api<Assignment[]>(`/api/admin/doctors/${doctor.id}/${kind}`)]); setChoices(options.filter(option => option.isActive)); setSelected(current.filter(option => option.isActive).map(option => option.id)); setAssignment({ doctor, kind }); setEditing(null); setDetail(null); }, '');
  }
  async function showDetail(doctor: Doctor) {
    await action.run(async () => { const full = await api<Doctor>(`/api/admin/doctors/${doctor.id}`); const blocks = can('schedules.view') ? await api<DoctorSchedule[]>(`/api/admin/doctors/${doctor.id}/schedules`) : []; setDetail(full); setSchedules(blocks); setAssignment(null); setEditing(null); }, '');
  }
  return <><PageTitle title="Doctores" description="Perfiles profesionales, especialidades y sedes de trabajo.">{can('doctors.create') && <div className="actions"><button onClick={create}>Crear doctor</button><button className="secondary" onClick={() => { setEditing(null); setDetail(null); setAssignment(null); setCreatingAssistant(true); }}>Crear asistente dental</button></div>}</PageTitle><Feedback {...action} />
    {editing && <section className="card"><h2>{edit ? `Editar perfil de ${edit.user.firstName}` : 'Nuevo doctor'}</h2><form className="editor" key={edit?.id ?? 'new'} onSubmit={save}>
      <h3 className="full-width">Datos personales</h3><Fields fields={personalFields} value={edit ? { ...edit, firstName: edit.user.firstName, lastName: edit.user.lastName } : {}} />
      {!edit && <><h3 className="full-width">Cuenta de acceso</h3><Fields fields={accountFields} />
        <PasswordField label="Contraseña" name="password"  autoComplete="new-password" required minLength={12} maxLength={128} />
        <PasswordField label="Confirmar contraseña" name="confirmPassword"  autoComplete="new-password" required maxLength={128} /></>}
      <h3 className="full-width">Datos profesionales</h3><Fields fields={profileFields} value={edit ?? {}} />
      {edit && <label className="check"><input type="checkbox" name="isActive" defaultChecked={edit.isActive} disabled={!can('doctors.disable')} />Perfil profesional activo</label>}
      <p className="muted full-width">El estado profesional y el acceso de la cuenta son independientes. Desactivar este perfil conserva el usuario; sus datos de acceso se administran en Usuarios.</p><FormActions busy={action.busy} label="Guardar doctor" cancel={() => setEditing(null)} /></form></section>}
    {assignment && <section className="card"><h2>{assignment.kind === 'specialties' ? 'Especialidades' : 'Sucursales'} de {assignment.doctor.user.firstName} {assignment.doctor.user.lastName}</h2><p className="muted">Puedes seleccionar varias opciones activas.</p><form onSubmit={event => { event.preventDefault(); void action.run(async () => { await api(`/api/admin/doctors/${assignment.doctor.id}/${assignment.kind}`, 'PUT', assignment.kind === 'specialties' ? { specialtyIds: selected } : { centerIds: selected }); setAssignment(null); await load(); }, 'Asignaciones actualizadas.'); }}><div className="checks">{choices.map(choice => <label key={choice.id}><input type="checkbox" checked={selected.includes(choice.id)} onChange={event => setSelected(toggleId(selected, choice.id, event.target.checked))} />{choice.name}</label>)}</div>{choices.length === 0 && <p>No hay opciones activas disponibles.</p>}<FormActions busy={action.busy} label={assignment.kind === 'specialties' ? 'Guardar especialidades' : 'Guardar sucursales'} cancel={() => setAssignment(null)} /></form></section>}
    {detail && <section className="card"><div className="title-row"><h2>{detail.user.firstName} {detail.user.lastName}</h2><button className="secondary" onClick={() => setDetail(null)}>Cerrar detalle</button></div><dl className="detail-grid"><div><dt>Email de acceso</dt><dd>{detail.user.email}</dd></div><div><dt>Estado de la cuenta</dt><dd><StateBadge active={detail.user.isActive} /></dd></div><div><dt>Estado profesional</dt><dd><StateBadge active={detail.isActive} /></dd></div>{[...personalFields.filter(f => !['firstName', 'lastName'].includes(f.name)), ...profileFields].map(field => <div key={field.name}><dt>{field.label}</dt><dd>{String(detail[field.name as keyof Doctor] ?? '—')}</dd></div>)}<div><dt>Especialidades</dt><dd>{detail.specialties.map(s => `${s.name}${s.isActive ? '' : ' (inactiva)'}`).join(', ') || 'Sin asignaciones'}</dd></div><div><dt>Sucursales</dt><dd>{detail.centers.map(c => `${c.name}${c.isActive ? '' : ' (inactiva)'}`).join(', ') || 'Sin asignaciones'}</dd></div></dl>{can('schedules.view') && <><h3>Horarios por sucursal</h3>{schedules.length ? <ul className="schedule-summary">{schedules.map(schedule => <li key={schedule.id}>{days[schedule.dayOfWeek - 1]} · {schedule.centerName} · {timeLabel(schedule.startTime)}–{timeLabel(schedule.endTime)}{schedule.hasBreak && ` · Descanso ${timeLabel(schedule.breakStart)}–${timeLabel(schedule.breakEnd)}`} {!schedule.isActive && '(Inactivo)'}</li>)}</ul> : <p>Sin horarios registrados.</p>}<a href={`#/schedules?doctor=${detail.id}`}>Ver semana del doctor</a></>}</section>}
    <section className="card table-card">{!loaded ? !action.error && <p role="status">Cargando doctores…</p> : <><table><thead><tr><th>Nombre</th><th>Matrícula</th><th>Especialidades</th><th>Sucursales</th><th>Estado profesional</th><th>Acciones</th></tr></thead><tbody>{items.map(doctor => <tr key={doctor.id}><td><strong>{doctor.user.firstName} {doctor.user.lastName}</strong><small>{doctor.user.email}</small></td><td>{doctor.professionalLicense ?? '—'}</td><td>{doctor.specialties.map(s => s.name).join(', ') || 'Sin asignaciones'}</td><td>{doctor.centers.map(c => `${c.name}${c.isActive ? '' : ' (Inactiva)'}`).join(', ') || 'Sin asignaciones'}</td><td><StateBadge active={doctor.isActive} /></td><td><div className="actions"><button className="secondary" disabled={action.busy} onClick={() => void showDetail(doctor)}>Detalle</button>{can('doctors.update') && <button className="secondary" onClick={() => { setEditing(doctor); setDetail(null); setAssignment(null); }}>Editar</button>}{can('doctors.update') && can('specialties.view') && <button className="secondary" disabled={action.busy || !doctor.isActive} onClick={() => void openAssignment(doctor, 'specialties')}>Especialidades</button>}{can('doctors.update') && can('centers.view') && <button className="secondary" disabled={action.busy || !doctor.isActive} onClick={() => void openAssignment(doctor, 'centers')}>Sucursales</button>}{can('doctors.disable') && <button className="secondary" disabled={action.busy} onClick={() => void action.run(async () => { await api(`/api/admin/doctors/${doctor.id}/status`, 'PATCH', { isActive: !doctor.isActive }); if (detail?.id === doctor.id) setDetail(null); await load(); }, 'Estado profesional actualizado.')}>{doctor.isActive ? 'Desactivar' : 'Activar'}</button>}<ProtectedDeleteButton entity="doctors" id={doctor.id} label={`${doctor.user.firstName} ${doctor.user.lastName}`} onDeleted={() => action.run(async () => { setEditing(null); setDetail(null); setAssignment(null); await load(); })} /></div></td></tr>)}</tbody></table>{items.length === 0 && <p>No hay doctores registrados.</p>}</>}<div className="pagination"><span>{total} doctores · Página {page}</span><button className="secondary" disabled={page === 1 || action.busy} onClick={() => setPage(value => value - 1)}>Anterior</button><button className="secondary" disabled={page * 10 >= total || action.busy} onClick={() => setPage(value => value + 1)}>Siguiente</button></div></section><DentalAssistantsPanel creating={creatingAssistant} closeCreate={() => setCreatingAssistant(false)} /></>;
}
