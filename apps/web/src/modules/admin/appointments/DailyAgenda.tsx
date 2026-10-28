import { useEffect, useState, type CSSProperties } from 'react';
import { ProfileAvatar } from '../../../components/ProfilePhoto';
import { appointmentStates, timeLabel, type Appointment } from './types';
import './daily-agenda.css';
export interface AgendaDoctor { id:string; userId:string; firstName:string; lastName:string; specialties:string[] }
export interface WorkingInterval { doctorId:string; centerId:string; centerName:string; startTime:string; endTime:string; breakStart:string|null; breakEnd:string|null }
export interface Agenda { date:string; slotMinutes:number; doctors:AgendaDoctor[]; workingIntervals:WorkingInterval[]; appointments:Appointment[] }
export const clinicToday=()=>new Intl.DateTimeFormat('en-CA',{timeZone:'America/La_Paz',year:'numeric',month:'2-digit',day:'2-digit'}).format(new Date());
export function shiftDate(date:string,days:number){const d=new Date(date+'T12:00:00Z');d.setUTCDate(d.getUTCDate()+days);return d.toISOString().slice(0,10);}
export const minutes=(time:string)=>{const [h,m,s=0]=time.split(':').map(Number);return h*60+m+s/60;};
const label=(m:number)=>`${String(Math.floor(m/60)).padStart(2,'0')}:${String(Math.floor(m%60)).padStart(2,'0')}`;
export default function DailyAgenda({agenda,onSelect}:{agenda:Agenda;onSelect:(a:Appointment)=>void}){
 const [mobileDoctor,setMobileDoctor]=useState(''),[now,setNow]=useState(new Date());
 useEffect(()=>{const id=window.setInterval(()=>setNow(new Date()),60000);return()=>clearInterval(id);},[]);
 const selected=agenda.doctors.some(d=>d.id===mobileDoctor)?mobileDoctor:agenda.doctors[0]?.id;
 const allTimes=[...agenda.workingIntervals.flatMap(w=>[minutes(w.startTime),minutes(w.endTime)]),...agenda.appointments.flatMap(a=>[minutes(a.startTime),minutes(a.endTime)])];
 const step=agenda.slotMinutes||30,start=allTimes.length?Math.floor(Math.min(...allTimes)/step)*step:0,end=allTimes.length?Math.ceil(Math.max(...allTimes)/step)*step:0;
 const ticks=Array.from({length:Math.ceil((end-start)/step)},(_,i)=>start+i*step);
 const unit=72/step;
 const timeNow=minutes(new Intl.DateTimeFormat('en-GB',{timeZone:'America/La_Paz',hour:'2-digit',minute:'2-digit',hour12:false}).format(now));
 return <section className="daily-agenda card" aria-label="Agenda diaria">
 <label className="agenda-mobile-doctor">Doctor de la agenda<select value={selected??''} onChange={e=>setMobileDoctor(e.target.value)}>{agenda.doctors.map(d=><option value={d.id} key={d.id}>{d.firstName} {d.lastName}</option>)}</select></label>
 <p className="agenda-legend"><span>Disponible: fondo blanco</span><span>Fuera de horario / descanso: fondo gris</span><span>Reservas: tarjetas con estado</span></p>
 <div className="agenda-scroll" role="region" aria-label="Horarios por doctor" tabIndex={0}>
 <div className="agenda-grid" style={{'--doctors':Math.max(1,agenda.doctors.length)} as CSSProperties}>
 <div className="agenda-time-head">Hora</div>
 {agenda.doctors.map(d=><header className={`agenda-doctor ${d.id===selected?'mobile-selected':''}`} key={d.id}><ProfileAvatar userId={d.userId} firstName={d.firstName} lastName={d.lastName}/><div><strong>{d.firstName} {d.lastName}</strong><small>{d.specialties.join(' · ')}</small></div></header>)}
 <div className="agenda-times" style={{height:(end-start)*unit}}>{ticks.map(m=><div key={m} style={{top:(m-start)*unit}}>{label(m)}</div>)}</div>
 {agenda.doctors.map(d=>{
  const entries=agenda.appointments.filter(a=>a.doctorId===d.id).sort((a,b)=>a.startTime.localeCompare(b.startTime));
  const laneEnds:number[]=[];const placed=entries.map(a=>{let lane=laneEnds.findIndex(e=>e<=minutes(a.startTime));if(lane<0)lane=laneEnds.length;laneEnds[lane]=minutes(a.endTime);return {a,lane};});
  return <div key={d.id} className={`agenda-day-column ${d.id===selected?'mobile-selected':''}`} aria-label={`Horario de ${d.firstName} ${d.lastName}`} style={{height:(end-start)*unit}}>
   {ticks.map(m=><div className="agenda-time-rule" key={m} style={{top:(m-start)*unit}}/>)}
   {agenda.workingIntervals.filter(w=>w.doctorId===d.id).flatMap(w=>w.breakStart&&w.breakEnd?[{...w,endTime:w.breakStart},{...w,startTime:w.breakEnd}]:[w]).map((w,i)=><div key={i} className="agenda-working" title={`${w.centerName}: ${timeLabel(w.startTime)}–${timeLabel(w.endTime)}`} aria-label={`Horario laboral ${w.centerName} ${timeLabel(w.startTime)}–${timeLabel(w.endTime)}`} style={{top:(minutes(w.startTime)-start)*unit,height:(minutes(w.endTime)-minutes(w.startTime))*unit}}/>)}
   {placed.map(({a,lane})=><button key={a.id} type="button" className={`agenda-appointment status-${a.status.toLowerCase()}`} aria-label={`${a.patientName||'Paciente no disponible'}, ${timeLabel(a.startTime)}–${timeLabel(a.endTime)}, ${appointmentStates[a.status]}`} onClick={()=>onSelect(a)} style={{top:(minutes(a.startTime)-start)*unit,height:(minutes(a.endTime)-minutes(a.startTime))*unit,width:`calc(${100/Math.max(1,laneEnds.length)}% - 8px)`,left:`calc(${lane*100/Math.max(1,laneEnds.length)}% + 4px)`}}>
    <span>{timeLabel(a.startTime)} – {timeLabel(a.endTime)} <span aria-hidden="true">···</span></span><strong>{a.patientName||'Paciente no disponible'}</strong><small>{a.reason||'Consulta'}</small><span>{appointmentStates[a.status]}</span><small>{a.centerName||'Sucursal no disponible'}</small>
   </button>)}
   {agenda.date===clinicToday()&&timeNow>=start&&timeNow<end&&<div className="agenda-now" aria-label="Hora actual" style={{top:(timeNow-start)*unit}}/>}
  </div>;
 })}
 </div>
 {!agenda.doctors.length&&<p>No hay doctores para estos filtros.</p>}
 {!!agenda.doctors.length&&!allTimes.length&&<p>No hay horarios laborales configurados ni citas en esta fecha.</p>}
 </div></section>;
}
