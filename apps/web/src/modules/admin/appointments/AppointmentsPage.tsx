import { useEffect, useState } from 'react';
import { api } from '../../../services/api';
import { Feedback, PageTitle, useAction, usePermissions } from '../shared';
import type { Doctor } from '../types';
import type { Patient } from '../patients/types';
import AppointmentForm from './AppointmentForm';
import { allPages, appointmentStates, timeLabel, type Appointment, type AppointmentStatus, type Option } from './types';

export default function AppointmentsPage() {
  const can = usePermissions(); const action = useAction();
  const [appointments, setAppointments] = useState<Appointment[]>([]); const [loading, setLoading] = useState(true);
  const [revision, setRevision] = useState(0); const [editing, setEditing] = useState<Appointment | 'new' | null>(null);
  const [patients, setPatients] = useState<Option[]>([]); const [doctors, setDoctors] = useState<Doctor[]>([]);
  const [centers, setCenters] = useState<Option[]>([]); const [specialties, setSpecialties] = useState<Option[]>([]);
  const [catalogLoading, setCatalogLoading] = useState(true); const [catalogError, setCatalogError] = useState('');
  const [filters, setFilters] = useState({ date: '', doctorId: '', centerId: '', specialtyId: '', status: '' });
  const patientsView = can('patients.view'), doctorsView = can('doctors.view'), centersView = can('centers.view'), specialtiesView = can('specialties.view');
  useEffect(() => {
    let active = true; setLoading(true); action.setError('');
    allPages<Appointment>('/api/admin/appointments').then(items => { if (active) setAppointments(items); })
      .catch(error => { if (active) { setAppointments([]); action.setError(error.message); } })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [revision]);
  useEffect(() => {
    let active = true; setCatalogLoading(true); setCatalogError('');
    setPatients([]); setDoctors([]); setCenters([]); setSpecialties([]);
    Promise.all([
      patientsView ? allPages<Patient>('/api/admin/patients') : Promise.resolve([]),
      doctorsView ? allPages<Doctor>('/api/admin/doctors') : Promise.resolve([]),
      centersView ? api<Option[]>('/api/admin/centers') : Promise.resolve([]),
      specialtiesView ? api<Option[]>('/api/admin/specialties') : Promise.resolve([]),
    ]).then(([p, d, c, s]) => { if (active) {
      setPatients(p.map(item => ({ id: item.id, name: `${item.firstName} ${item.lastName}`, isActive: item.isActive })));
      setDoctors(d); setCenters(c); setSpecialties(s);
    } }).catch(error => { if (active) setCatalogError(error.message); })
      .finally(() => { if (active) setCatalogLoading(false); });
    return () => { active = false; };
  }, [patientsView, doctorsView, centersView, specialtiesView]);
  const catalogsReady = patientsView && doctorsView && centersView && specialtiesView && !catalogLoading && !catalogError;
  const doctorNames = doctors.map(d => ({ id: d.id, name: `${d.user.firstName} ${d.user.lastName}`, isActive: d.isActive }));
  const name = (options: Option[], id: string) => options.find(o => o.id === id)?.name ?? `ID: ${id}`;
  const optionsFor = (options: Option[], field: 'doctorId' | 'centerId' | 'specialtyId') =>
    [...new Set([...options.map(o => o.id), ...appointments.map(a => a[field])])].map(id => ({ id, name: name(options, id) }));
  const visible = appointments.filter(a => (!filters.date || a.appointmentDate === filters.date)
    && (!filters.doctorId || a.doctorId === filters.doctorId) && (!filters.centerId || a.centerId === filters.centerId)
    && (!filters.specialtyId || a.specialtyId === filters.specialtyId) && (!filters.status || a.status === filters.status))
    .sort((a, b) => `${a.appointmentDate} ${a.startTime}`.localeCompare(`${b.appointmentDate} ${b.startTime}`));
  function changeStatus(a: Appointment, status: AppointmentStatus) {
    if (!can(status === 'CANCELLED' ? 'appointments.cancel' : 'appointments.update') || action.busy) return;
    void action.run(async () => { await api(`/api/admin/appointments/${a.id}/status`, 'PATCH', { status }); setEditing(null); setRevision(v => v + 1); }, 'Estado de la cita actualizado.');
  }
  return <>
    <PageTitle title="Agenda / Citas" description="Consulta las citas y reserva horarios disponibles por doctor y sucursal.">
      {can('appointments.create') && <button disabled={action.busy || !catalogsReady} onClick={() => setEditing('new')}>Nueva cita</button>}
    </PageTitle>
    <Feedback {...action} /><Feedback error={catalogError} />
    {catalogLoading && <p role="status">Cargando catálogos…</p>}
    {(!patientsView || !doctorsView || !centersView || !specialtiesView) && <p className="muted">Los nombres requieren acceso a sus catálogos. Para crear o editar citas necesitas permisos de consulta de pacientes, doctores, sucursales y especialidades.</p>}
    <section className="card"><h2>Filtros de agenda</h2><div className="editor">
      <label>Filtrar fecha<input type="date" value={filters.date} onChange={e => setFilters({ ...filters, date: e.target.value })} /></label>
      {([['doctorId', 'doctor', doctorNames], ['centerId', 'sucursal', centers], ['specialtyId', 'especialidad', specialties]] as const).map(([field, label, options]) =>
        <label key={field}>Filtrar {label}<select value={filters[field]} onChange={e => setFilters({ ...filters, [field]: e.target.value })}>
          <option value="">Todos</option>{optionsFor(options, field).map(o => <option key={o.id} value={o.id}>{o.name}</option>)}
        </select></label>)}
      <label>Filtrar estado<select value={filters.status} onChange={e => setFilters({ ...filters, status: e.target.value })}><option value="">Todos</option>
        {Object.entries(appointmentStates).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
      </select></label>
    </div></section>
    {editing && catalogsReady && can(editing === 'new' ? 'appointments.create' : 'appointments.update') &&
      <AppointmentForm key={editing === 'new' ? 'new' : editing.id} edit={editing === 'new' ? null : editing}
        patients={patients} doctors={doctors} centers={centers} specialties={specialties}
        saved={() => { setEditing(null); setRevision(v => v + 1); }} close={() => setEditing(null)} />}
    <section className="card table-card">
      {loading ? <p role="status">Cargando citas…</p> : <>
        <table><thead><tr><th>Fecha / Hora</th><th>Paciente</th><th>Doctor</th><th>Especialidad</th><th>Sucursal</th><th>Estado</th><th>Motivo</th><th>Acciones</th></tr></thead>
          <tbody>{visible.map(a => <tr key={a.id}>
            <td>{a.appointmentDate}<br />{timeLabel(a.startTime)} – {timeLabel(a.endTime)}</td>
            <td>{name(patients, a.patientId)}</td><td>{name(doctorNames, a.doctorId)}</td><td>{name(specialties, a.specialtyId)}</td><td>{name(centers, a.centerId)}</td>
            <td><span className={`badge ${['CANCELLED', 'NO_SHOW'].includes(a.status) ? 'inactive' : ''}`}>{appointmentStates[a.status]}</span></td><td>{a.reason ?? '—'}</td>
            <td><div className="actions">
              {can('appointments.update') && <><button className="secondary" disabled={action.busy || !catalogsReady} onClick={() => void action.run(async () => {
                setEditing(await api<Appointment>(`/api/admin/appointments/${a.id}`));
              }, '')}>Editar cita</button>
              <label>Estado de cita de {name(patients, a.patientId)}<select value={a.status} disabled={action.busy} onChange={e => changeStatus(a, e.target.value as AppointmentStatus)}>
                {a.status === 'CANCELLED' && <option value="CANCELLED" disabled>Cancelada</option>}
                {Object.entries(appointmentStates).filter(([s]) => s !== 'CANCELLED').map(([value, label]) => <option key={value} value={value}>{label}</option>)}
              </select></label></>}
              {can('appointments.cancel') && a.status !== 'CANCELLED' && <button className="secondary" disabled={action.busy} onClick={() => changeStatus(a, 'CANCELLED')}>Cancelar cita</button>}
            </div></td>
          </tr>)}</tbody>
        </table>
        {!visible.length && <p>No hay citas para los filtros seleccionados.</p>}
      </>}
    </section>
  </>;
}
