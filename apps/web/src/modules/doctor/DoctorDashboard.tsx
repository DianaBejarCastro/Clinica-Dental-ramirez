import { useEffect, useState } from 'react';
import { api } from '../../services/api';
import AdminLayout from '../../layouts/AdminLayout';
import { appointmentStates, type AppointmentStatus } from '../admin/appointments/types';
import './doctor-portal.css';
import { usePermissions } from '../admin/shared';

type View = 'day' | 'week' | 'month';
interface Appointment { appointmentId: string; date: string; startTime: string; endTime: string; patient: { patientProfileId: string; firstName: string; lastName: string }; specialty: { id: string; name: string }; center: { id: string; name: string }; reason: string | null; status: AppointmentStatus }
interface Agenda { from: string; to: string; items: Appointment[] }
const localDate = (date: Date) => `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
const parse = (date: string) => new Date(`${date}T12:00:00`);
const addDays = (date: string, days: number) => { const next = parse(date); next.setDate(next.getDate() + days); return localDate(next); };
const label = (date: string) => parse(date).toLocaleDateString('es-BO', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' });
function monthDays(date: string) { const current = parse(date); const first = new Date(current.getFullYear(), current.getMonth(), 1); return { offset: (first.getDay() + 6) % 7, days: Array.from({ length: new Date(current.getFullYear(), current.getMonth() + 1, 0).getDate() }, (_, i) => localDate(new Date(current.getFullYear(), current.getMonth(), i + 1))) }; }

export default function DoctorDashboard() {
  const can = usePermissions();
  const [date, setDate] = useState(() => localDate(new Date())); const [view, setView] = useState<View>('day');
  const [month, setMonth] = useState(date); const [agenda, setAgenda] = useState<Agenda | null>(null);
  const [loading, setLoading] = useState(true); const [error, setError] = useState(''); const [detail, setDetail] = useState<Appointment | null>(null);
  useEffect(() => { let live = true; setLoading(true); setError(''); setAgenda(null); setDetail(null);
    api<Agenda>(`/api/doctor/me/appointments?date=${date}&view=${view}`).then(data => { if (live) setAgenda(data); })
      .catch(e => { if (live) setError(e.message); }).finally(() => { if (live) setLoading(false); });
    return () => { live = false; };
  }, [date, view]);
  function choose(value: string) { setDate(value); setMonth(value); setView('day'); }
  function move(direction: number) { const next = parse(date); if (view === 'month') { next.setDate(1); next.setMonth(next.getMonth() + direction); } else next.setDate(next.getDate() + direction * (view === 'week' ? 7 : 1)); setDate(localDate(next)); setMonth(localDate(next)); }
  function calendar(value: string, compact: boolean) { const { days, offset } = monthDays(value); return <div className={`doctor-calendar ${compact ? 'compact' : ''}`} aria-label={compact ? 'Calendario compacto' : 'Calendario mensual'}>
    {['L', 'M', 'X', 'J', 'V', 'S', 'D'].map((day, i) => <span key={i} aria-hidden="true">{day}</span>)}
    {Array.from({ length: offset }, (_, i) => <span key={`blank-${i}`} />)}
    {days.map(day => { const count = agenda?.items.filter(a => a.date === day).length ?? 0; return <button key={day} aria-label={label(day)} aria-pressed={date === day} className={date === day ? 'selected' : 'secondary'} onClick={() => choose(day)}>{parse(day).getDate()}{!compact && <small>{count} citas</small>}</button>; })}
  </div>; }
  function list(items: Appointment[]) { return items.length ? <div className="doctor-appointment-list">{items.map(a => <article className="doctor-appointment" key={a.appointmentId}>
    <div className="doctor-appointment-time"><small>Hora</small><strong>{a.startTime.slice(0, 5)}–{a.endTime.slice(0, 5)}</strong></div>
    <div className="doctor-appointment-patient"><small>Paciente</small><strong>{a.patient.firstName} {a.patient.lastName}</strong><p>{a.reason || 'Sin motivo registrado'}</p></div>
    <div className="doctor-appointment-meta"><small>Especialidad</small><span>{a.specialty.name}</span><small>Sucursal</small><span>{a.center.name}</span></div>
    <div className="doctor-appointment-actions"><span className={`badge appointment-status-${a.status.toLowerCase()}`}>{appointmentStates[a.status]}</span><button className="secondary" onClick={() => setDetail(a)}>Ver cita</button>{can('clinical_history.view') && can('clinical_history.create') && ['SCHEDULED','CONFIRMED','IN_PROGRESS'].includes(a.status) && <a className="doctor-consultation-button" href={`#/doctor/consultation?appointment=${a.appointmentId}`}>Iniciar consulta</a>}</div>
  </article>)}</div> : <div className="doctor-agenda-empty"><span aria-hidden="true">▦</span><p>No tienes citas para esta fecha.</p></div>; }
  const weekDays = agenda ? Array.from({ length: 7 }, (_, i) => addDays(agenda.from, i)) : [];
  return <AdminLayout path="/doctor/appointments" doctorPortal>
      <header className="doctor-agenda-heading"><h1>Mis citas</h1><p>Consulta tu agenda y acompaña a tus pacientes.</p></header>
      <section className="card doctor-toolbar" aria-label="Controles de agenda"><button className="secondary" aria-label="Anterior" onClick={() => move(-1)}>‹</button><button className="secondary" onClick={() => choose(localDate(new Date()))}>Hoy</button><button className="secondary" aria-label="Siguiente" onClick={() => move(1)}>›</button>
        <label>Fecha<input type="date" value={date} min="1900-01-01" max="9998-12-31" onChange={e => { if (e.target.value) { setDate(e.target.value); setMonth(e.target.value); } }} /></label>
        <div role="group" aria-label="Vista">{(['day','week','month'] as View[]).map((v,i) => <button key={v} className={view === v ? '' : 'secondary'} aria-pressed={view === v} onClick={() => setView(v)}>{['Día','Semana','Mes'][i]}</button>)}</div>
      </section>
      {loading && <p role="status">Cargando citas…</p>}{error && <p role="alert">{error}</p>}
      {!loading && !error && agenda && <div className="doctor-agenda-layout"><section className="card doctor-agenda-main">
        {view === 'day' && <><h2>{label(date)}</h2>{list(agenda.items)}</>}
        {view === 'week' && weekDays.map(day => <section key={day}><h2>{label(day)}</h2>{list(agenda.items.filter(a => a.date === day))}</section>)}
        {view === 'month' && <><h2>{parse(date).toLocaleDateString('es-BO', { month: 'long', year: 'numeric' })}</h2>{calendar(date, false)}</>}
        {detail && <section className="card" aria-label="Detalle de cita"><h2>Detalle de cita</h2><p>{label(detail.date)} · {detail.startTime.slice(0,5)}–{detail.endTime.slice(0,5)}</p><p>{detail.patient.firstName} {detail.patient.lastName}</p><p>{detail.reason || 'Sin motivo registrado'}</p><p>{detail.specialty.name} · {detail.center.name} · {appointmentStates[detail.status]}</p><button className="secondary" onClick={() => setDetail(null)}>Cerrar detalle</button></section>}
      </section><div className="doctor-agenda-side"><section className="card"><div className="doctor-month-controls"><button className="secondary" aria-label="Mes anterior" onClick={() => { const d=parse(month); d.setDate(1); d.setMonth(d.getMonth()-1); setMonth(localDate(d)); }}>‹</button><strong>{parse(month).toLocaleDateString('es-BO', { month:'long', year:'numeric' })}</strong><button className="secondary" aria-label="Mes siguiente" onClick={() => { const d=parse(month); d.setDate(1); d.setMonth(d.getMonth()+1); setMonth(localDate(d)); }}>›</button></div>{calendar(month, true)}</section>
        {view === 'day' && <section className="card"><h2>Resumen diario</h2><dl>{Object.entries(appointmentStates).map(([status,text]) => <div className="doctor-summary-row" key={status}><dt>{text}</dt><dd>{agenda.items.filter(a => a.status === status).length}</dd></div>)}</dl></section>}
      </div></div>}
    </AdminLayout>;
}
