
import { ProfileAvatar } from '../../../components/ProfilePhoto';
import { appointmentStates, timeLabel, type Appointment } from './types';
import './daily-agenda.css';
export interface AgendaDoctor { id:string; userId:string; firstName:string; lastName:string; specialties:string[] }
export interface WorkingInterval { doctorId:string; centerId:string; centerName:string; startTime:string; endTime:string; breakStart:string|null; breakEnd:string|null }
export interface Agenda { date:string; slotMinutes:number; doctors:AgendaDoctor[]; workingIntervals:WorkingInterval[]; appointments:Appointment[]; occupiedAppointments?:Appointment[] }
export const clinicToday=()=>new Intl.DateTimeFormat('en-CA',{timeZone:'America/La_Paz',year:'numeric',month:'2-digit',day:'2-digit'}).format(new Date());
export function shiftDate(date:string,days:number){const d=new Date(date+'T12:00:00Z');d.setUTCDate(d.getUTCDate()+days);return d.toISOString().slice(0,10);}
export const minutes=(time:string)=>{const [h,m,s=0]=time.split(':').map(Number);return h*60+m+s/60;};
const label=(m:number)=>`${String(Math.floor(m/60)).padStart(2,'0')}:${String(Math.floor(m%60)).padStart(2,'0')}`;

export function slotAvailable(agenda:Agenda,start:number,end:number){
 const occupied=agenda.occupiedAppointments??agenda.appointments;
 return agenda.workingIntervals.some(w=>minutes(w.startTime)<=start&&minutes(w.endTime)>=end
   &&(!w.breakStart||!w.breakEnd||end<=minutes(w.breakStart)||start>=minutes(w.breakEnd))
   &&!occupied.some(a=>a.doctorId===w.doctorId&&!['CANCELLED','NO_SHOW'].includes(a.status)&&minutes(a.startTime)<end&&minutes(a.endTime)>start));
}
export function AgendaCalendarIcon(){return <svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" strokeWidth="1.7" aria-hidden="true"><rect x="4" y="5" width="16" height="16" rx="2"/><path d="M8 3v5m8-5v5M4 11h16m-12 4h3m2 0h3m-8 3h3"/></svg>;}
export default function DailyAgenda({agenda,onSelect}:{agenda:Agenda;onSelect:(a:Appointment)=>void}){
 const step=agenda.slotMinutes||30;
 const allTimes=[...agenda.workingIntervals.flatMap(w=>[minutes(w.startTime),minutes(w.endTime)]),...agenda.appointments.flatMap(a=>[minutes(a.startTime),minutes(a.endTime)])];
 const start=allTimes.length?Math.floor(Math.min(...allTimes)/step)*step:0;
 const end=allTimes.length?Math.ceil(Math.max(...allTimes)/step)*step:0;
 const ticks=[...new Set([...Array.from({length:Math.ceil((end-start)/step)},(_,i)=>start+i*step),...agenda.appointments.map(a=>minutes(a.startTime))])].sort((a,b)=>a-b);
 return <section className="daily-agenda" aria-label="Agenda diaria">
  <div className="agenda-overview"><div className="agenda-legend" aria-label="Estados de citas">
   {Object.entries(appointmentStates).map(([status,name])=><span className={`agenda-status status-${status.toLowerCase()}`} key={status}><i aria-hidden="true"/>{name}</span>)}
  </div><div className="agenda-count"><span aria-hidden="true">▦</span><div><strong>{agenda.appointments.length}</strong><small>Citas del día</small></div></div></div>
  <div className="agenda-timeline" role="region" aria-label="Citas por hora">
  {ticks.map((time,index)=>{
    const entries=agenda.appointments.filter(a=>minutes(a.startTime)===time).sort((a,b)=>(a.doctorName??'').localeCompare(b.doctorName??'')||a.id.localeCompare(b.id));
    const available=slotAvailable(agenda,time,Math.min(time+step,ticks[index+1]??end));
    const ongoing=(agenda.occupiedAppointments??agenda.appointments).some(a=>!['CANCELLED','NO_SHOW'].includes(a.status)&&agenda.doctors.some(d=>d.id===a.doctorId)&&minutes(a.startTime)<time&&minutes(a.endTime)>time);
    return <section className="agenda-time-row" key={time} aria-label={`Hora ${label(time)}`}>
      <h3 className="agenda-time-label"><time>{label(time)}</time><span aria-hidden="true"/></h3>
      <div className="agenda-row-cards">
      {entries.map(a=>{
        const doctor=agenda.doctors.find(d=>d.id===a.doctorId);
        return <button key={a.id} type="button" className={`agenda-appointment status-${a.status.toLowerCase()}`} aria-label={`${a.patientName||'Paciente no disponible'}, ${timeLabel(a.startTime)}–${timeLabel(a.endTime)}, ${appointmentStates[a.status]}, ${a.doctorName||'Doctor no disponible'}`} onClick={()=>onSelect(a)}>
          <span className="agenda-card-heading"><span className="agenda-status"><i aria-hidden="true"/>{appointmentStates[a.status]}</span><span className="agenda-card-menu" aria-hidden="true">···</span></span>
          <span className="agenda-card-people"><span className="agenda-card-patient"><span className="agenda-patient-avatar" aria-hidden="true">{(a.patientName||'P').trim()[0].toLocaleUpperCase()}</span><span><small className="agenda-card-time">{timeLabel(a.startTime)} – {timeLabel(a.endTime)}</small><strong>{a.patientName||'Paciente no disponible'}</strong><small>{a.reason||'Consulta'}</small></span></span>
          <span className="agenda-card-doctor">{doctor&&<ProfileAvatar userId={doctor.userId} firstName={doctor.firstName} lastName={doctor.lastName}/>}<span><strong>{a.doctorName||(doctor?`${doctor.firstName} ${doctor.lastName}`:'Doctor no disponible')}</strong>{doctor?.specialties[0]&&<small>{doctor.specialties[0]}</small>}</span></span></span>
          <small className="agenda-card-center">{a.centerName||'Sucursal no disponible'}</small>
        </button>;
      })}
      {!entries.length&&<div className={`agenda-empty-slot ${available?'available':'unavailable'}`}><span aria-hidden="true">▦</span><div><strong>{ongoing?'Citas en curso':'Sin citas programadas'}</strong><small>{available?'Horario disponible':'Sin disponibilidad'}</small></div></div>}
      </div>
    </section>;
  })}
  {!ticks.length&&<p className="agenda-no-hours">No hay horarios laborales configurados ni citas en esta fecha.</p>}
  </div>
 </section>;
}
