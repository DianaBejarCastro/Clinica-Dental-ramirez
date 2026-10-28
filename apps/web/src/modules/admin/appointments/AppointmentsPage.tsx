import ProtectedDeleteButton from '../deletion/ProtectedDeleteButton';
import { useEffect, useRef, useState } from 'react';
import { api } from '../../../services/api';
import { Feedback, PageTitle, useAction, usePermissions } from '../shared';
import type { Doctor } from '../types';
import DailyAgenda, { clinicToday, shiftDate, type Agenda } from './DailyAgenda';
import AppointmentForm from './AppointmentForm';
import { allPages, appointmentStates, timeLabel, type Appointment, type AppointmentStatus, type Option } from './types';

export default function AppointmentsPage() {
  const can = usePermissions(); const action = useAction();
  const [appointments, setAppointments] = useState<Appointment[]>([]); const [loading, setLoading] = useState(true);
  const [revision, setRevision] = useState(0); const [editing, setEditing] = useState<Appointment | 'new' | null>(null);
  const [doctors, setDoctors] = useState<Doctor[]>([]);
  const [centers, setCenters] = useState<Option[]>([]); const [specialties, setSpecialties] = useState<Option[]>([]);
  const [catalogLoading, setCatalogLoading] = useState(true); const [catalogError, setCatalogError] = useState('');
  const [filters, setFilters] = useState({ date: clinicToday(), doctorId: '', centerId: '', specialtyId: '', status: '' });
  const [agenda,setAgenda]=useState<Agenda|null>(null),[selected,setSelected]=useState<Appointment|null>(null);const dialog=useRef<HTMLDialogElement>(null);
  useEffect(()=>{if(selected)dialog.current?.showModal();},[selected]);
  const patientsView = can('patients.view'), doctorsView = can('doctors.view'), centersView = can('centers.view'), specialtiesView = can('specialties.view');
  useEffect(() => {
    let active = true; setLoading(true); action.setError('');
    api<Agenda>('/api/admin/appointments/agenda?'+new URLSearchParams(Object.entries(filters).filter(([,v])=>!!v))).then(data => { if (active) {setAgenda(data);setAppointments(data.appointments);} })
      .catch(error => { if (active) { setAgenda(null);setAppointments([]); action.setError(error.message); } })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [revision,filters]);
  useEffect(() => {
    let active = true; setCatalogLoading(true); setCatalogError('');
    setDoctors([]); setCenters([]); setSpecialties([]);
    Promise.all([
      doctorsView ? allPages<Doctor>('/api/admin/doctors') : Promise.resolve([]),
      centersView ? api<Option[]>('/api/admin/centers') : Promise.resolve([]),
      specialtiesView ? api<Option[]>('/api/admin/specialties') : Promise.resolve([]),
    ]).then(([d, c, s]) => { if (active) {
      setDoctors(d); setCenters(c); setSpecialties(s);
    } }).catch(error => { if (active) setCatalogError(error.message); })
      .finally(() => { if (active) setCatalogLoading(false); });
    return () => { active = false; };
  }, [patientsView, doctorsView, centersView, specialtiesView]);
  const catalogsReady = patientsView && doctorsView && centersView && specialtiesView && !catalogLoading && !catalogError;
  const doctorNames = doctors.length ? doctors.map(d => ({ id: d.id, name: `${d.user.firstName} ${d.user.lastName}`, isActive: d.isActive })) : (agenda?.doctors??[]).map(d=>({id:d.id,name:`${d.firstName} ${d.lastName}`,isActive:true}));
  const filterName = (options: Option[], id: string, field: 'doctorId' | 'centerId' | 'specialtyId') => {
    const appointment = appointments.find(a => a[field] === id);
    return options.find(o => o.id === id)?.name || (field === 'doctorId' ? appointment?.doctorName : field === 'centerId' ? appointment?.centerName : null) || 'No disponible';
  };
  const optionsFor = (options: Option[], field: 'doctorId' | 'centerId' | 'specialtyId') =>
    [...new Set([...options.map(o => o.id), ...appointments.map(a => a[field])])].map(id => ({ id, name: filterName(options, id, field) }));
  function changeStatus(a: Appointment, status: AppointmentStatus) {
    if (!can(status === 'CANCELLED' ? 'appointments.cancel' : 'appointments.update') || action.busy) return;
    void action.run(async () => { await api(`/api/admin/appointments/${a.id}/status`, 'PATCH', { status }); setSelected(null);setEditing(null); setRevision(v => v + 1); }, 'Estado de la cita actualizado.');
  }
  return <>
    <PageTitle title="Agenda / Citas" description="Consulta las citas y reserva horarios disponibles por doctor y sucursal.">
      {can('appointments.create') && <button disabled={action.busy || !catalogsReady} onClick={() => setEditing('new')}>Nueva cita</button>}
    </PageTitle>
    <Feedback {...action} /><Feedback error={catalogError} />
    {catalogLoading && <p role="status">Cargando catálogos…</p>}
    {(!patientsView || !doctorsView || !centersView || !specialtiesView) && <p className="muted">Para crear o editar citas necesitas permisos de consulta de pacientes, doctores, sucursales y especialidades.</p>}
    <div className="agenda-toolbar" aria-label="Navegación de fecha">
      <button className="secondary" aria-label="Día anterior" onClick={()=>setFilters({...filters,date:shiftDate(filters.date,-1)})}>←</button>
      <label>Filtrar fecha<input type="date" required value={filters.date} onChange={e=>{if(e.target.value)setFilters({...filters,date:e.target.value});}}/></label>
      <strong>{new Date(filters.date+'T12:00:00Z').toLocaleDateString('es-BO',{timeZone:'America/La_Paz',weekday:'long',year:'numeric',month:'long',day:'numeric'})}</strong>
      <button className="secondary" onClick={()=>setFilters({...filters,date:clinicToday()})}>Hoy</button>
      <button className="secondary" aria-label="Día siguiente" onClick={()=>setFilters({...filters,date:shiftDate(filters.date,1)})}>→</button>
    </div>
    <details className="card agenda-filters"><summary>Filtros · {filters.centerId?filterName(centers,filters.centerId,'centerId'):'Todas las sucursales'}</summary><div className="editor">
      {([['doctorId', 'doctor', doctorNames], ['centerId', 'sucursal', centers], ['specialtyId', 'especialidad', specialties]] as const).map(([field, label, options]) =>
        <label key={field}>Filtrar {label}<select value={filters[field]} onChange={e => setFilters({ ...filters, [field]: e.target.value })}>
          <option value="">Todos</option>{optionsFor(options, field).map(o => <option key={o.id} value={o.id}>{o.name}</option>)}
        </select></label>)}
      <label>Filtrar estado<select value={filters.status} onChange={e => setFilters({ ...filters, status: e.target.value })}><option value="">Todos</option>
        {Object.entries(appointmentStates).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
      </select></label>
    </div></details>
    {editing && catalogsReady && can(editing === 'new' ? 'appointments.create' : 'appointments.update') &&
      <AppointmentForm key={editing === 'new' ? 'new' : editing.id} edit={editing === 'new' ? null : editing}
        doctors={doctors} centers={centers} specialties={specialties}
        saved={() => { setEditing(null); setRevision(v => v + 1); }} close={() => setEditing(null)} />}
    {filters.status&&<p className="muted">El filtro de estado puede ocultar reservas. La disponibilidad se confirma al crear o editar la cita.</p>}
    {loading?<p role="status">Cargando citas…</p>:agenda&&<DailyAgenda agenda={agenda} onSelect={setSelected}/>}
    {selected&&<dialog className="agenda-detail" ref={dialog} aria-label="Detalle de cita" onCancel={()=>setSelected(null)}>
      <h2>Detalle de cita</h2><dl>
        <dt>Paciente</dt><dd>{selected.patientName||'Paciente no disponible'}</dd>
        <dt>Doctor</dt><dd>{selected.doctorName||'Doctor no disponible'}</dd>
        <dt>Fecha / Hora</dt><dd>{selected.appointmentDate} · {timeLabel(selected.startTime)}–{timeLabel(selected.endTime)}</dd>
        <dt>Sucursal</dt><dd>{selected.centerName||'Sucursal no disponible'}</dd>
        <dt>Estado</dt><dd>{appointmentStates[selected.status]}</dd>
        <dt>Motivo</dt><dd>{selected.reason||'—'}</dd><dt>Notas</dt><dd>{selected.notes||'—'}</dd>
      </dl>
<div className="actions">
              {can('appointments.update') && <><button className="secondary" disabled={action.busy || !catalogsReady} onClick={() => void action.run(async () => {
                setSelected(null);setEditing(await api<Appointment>(`/api/admin/appointments/${selected.id}`));
              }, '')}>Editar cita</button>
              <label>Estado de cita de {(selected.patientName || 'Paciente no disponible')}<select value={selected.status} disabled={action.busy} onChange={e => changeStatus(selected, e.target.value as AppointmentStatus)}>
                {selected.status === 'CANCELLED' && <option value="CANCELLED" disabled>Cancelada</option>}
                {Object.entries(appointmentStates).filter(([s]) => s !== 'CANCELLED').map(([value, label]) => <option key={value} value={value}>{label}</option>)}
              </select></label></>}
              {can('appointments.cancel') && selected.status !== 'CANCELLED' && <button className="secondary" disabled={action.busy} onClick={() => changeStatus(selected, 'CANCELLED')}>Cancelar cita</button>}
              <ProtectedDeleteButton entity="appointments" id={selected.id} label={`cita ${selected.appointmentDate} ${timeLabel(selected.startTime)}`} onDeleted={() => { setSelected(null);setEditing(null); setRevision(v => v + 1); }} />
            </div>
      <button type="button" className="secondary" onClick={()=>setSelected(null)}>Cerrar detalle</button>
    </dialog>}
  </>;
}
