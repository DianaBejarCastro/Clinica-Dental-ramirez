import { useEffect, useRef } from 'react';
import type { Dentition, Exam } from './api';
import { clinicalDate } from './api';
export function DentitionTabs({value,onChange,disabled=false}:{value:Dentition;onChange:(v:Dentition)=>void;disabled?:boolean}) {
  return <fieldset className="odo-dentition" disabled={disabled}><legend>Dentición</legend>{([['PERMANENT','Permanente'],['PRIMARY','Temporal'],['MIXED','Mixta']] as const).map(([mode,name])=><button key={mode} type="button" className={value===mode?'':'secondary'} aria-pressed={value===mode} onClick={()=>onChange(mode)}>{name}</button>)}</fieldset>;
}
export function ExamHistory({exams,id,disabled,onChange}:{exams:Exam[];id:string;disabled:boolean;onChange:(id:string)=>void}){return <label>Historial de odontogramas<select value={id} disabled={disabled} onChange={e=>onChange(e.target.value)}>{[...exams].sort((a,b)=>b.clinicalDate.localeCompare(a.clinicalDate)).map(e=><option key={e.id} value={e.id}>{clinicalDate(e.clinicalDate)} · {e.status==='DRAFT'?'Borrador':'Finalizado'} · {e.createdByDoctorName}</option>)}</select></label>;}
export function ExamStatus({exam,dirty}:{exam:Exam;dirty:boolean}){return <div className="odo-status"><strong>{exam.status==='DRAFT'?'DRAFT · Borrador':'FINALIZED · Finalizado'}</strong><span>Fecha clínica: {clinicalDate(exam.clinicalDate)}</span><span>Doctor: {exam.createdByDoctorName}</span>{dirty&&<span>● Cambios sin guardar</span>}</div>;}
export function FinalizeDialog({busy,onCancel,onConfirm}:{busy:boolean;onCancel:()=>void;onConfirm:()=>void}){
  const ref=useRef<HTMLDialogElement>(null);
  useEffect(()=>{const dialog=ref.current!;dialog.showModal();return()=>dialog.close();},[]);
  return <dialog ref={ref} className="odo-dialog" aria-labelledby="odo-finalize-title" onCancel={e=>{e.preventDefault();if(!busy)onCancel();}}><h2 id="odo-finalize-title">Finalizar odontograma</h2><p>Una vez finalizado, este odontograma será un registro clínico histórico y no podrá editarse. Los cambios posteriores deberán registrarse en una nueva actualización.</p><div className="actions"><button type="button" className="secondary" disabled={busy} onClick={onCancel} autoFocus>Cancelar</button><button type="button" disabled={busy} onClick={onConfirm}>{busy?'Finalizando…':'Confirmar finalización'}</button></div></dialog>;
}
