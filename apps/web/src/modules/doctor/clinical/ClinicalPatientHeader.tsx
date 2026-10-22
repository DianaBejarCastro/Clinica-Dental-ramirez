import { documentLabel } from '../../../components/IdentityDocumentFields';
import type { Context } from './types';

const icons = {
  identity: <><rect x="3" y="5" width="18" height="14" rx="2"/><circle cx="8" cy="10" r="2"/><path d="M5 16c0-3 6-3 6 0m3-6h4m-4 4h4"/></>,
  phone: <path d="m7 3 3 5-2 2a14 14 0 0 0 6 6l2-2 5 3-1 4C10 22 2 14 3 4Z"/>,
  email: <><rect x="3" y="5" width="18" height="14" rx="2"/><path d="m3 6 9 7 9-7"/></>,
  calendar: <><rect x="3" y="5" width="18" height="16" rx="2"/><path d="M7 3v4m10-4v4M3 11h18m-13 4h2m4 0h2"/></>,
  location: <><path d="M19 10c0 5-7 11-7 11S5 15 5 10a7 7 0 1 1 14 0Z"/><circle cx="12" cy="10" r="2"/></>,
};
function Icon({name}:{name:keyof typeof icons}){
  return <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" focusable="false">{icons[name]}</svg>;
}

export default function ClinicalPatientHeader({patient,appointment}:{patient:Context['patient'];appointment:Context['appointment']}){
  const initials = [patient.firstName,patient.lastName].map(value=>value.trim().charAt(0)).join('').toLocaleUpperCase('es') || 'P';
  return <section className="card clinical-patient" aria-label="Resumen del paciente y cita actual">
    <div className="clinical-patient-profile">
      <div className="clinical-patient-heading">
        <span className="clinical-patient-avatar" aria-hidden="true">{initials}</span>
        <div className="clinical-patient-identity"><span className="clinical-patient-kicker">Información del paciente</span>
          <h2>{`${patient.firstName} ${patient.lastName}`.trim() || 'Nombre no registrado'}</h2>
          <dl className="clinical-patient-demographics"><div><dt>Edad</dt><dd>{patient.age==null?'No registrada':`${patient.age} años`}</dd></div><div><dt>Género</dt><dd>{patient.gender||'No registrado'}</dd></div><div><dt>Grupo sanguíneo</dt><dd className="clinical-blood-type">{patient.bloodType==='UNKNOWN'?'Desconocido':patient.bloodType||'No registrado'}</dd></div></dl>
        </div>
      </div>
      <dl className="clinical-patient-contacts">
        {([{label:'Documento',value:documentLabel(patient),icon:'identity'},{label:'Teléfono',value:patient.phone,icon:'phone'},{label:'Email',value:patient.email,icon:'email'}] as const).map(({label,value,icon})=><div className="clinical-patient-contact" key={label}>
          <span className="clinical-patient-icon"><Icon name={icon}/></span><div><dt>{label}</dt><dd>{value||'No registrado'}</dd></div>
        </div>)}
      </dl>
    </div>
    <section className="clinical-patient-appointment" aria-label="Cita actual">
      <div className="clinical-patient-appointment-title"><Icon name="calendar"/><h3>Cita actual</h3><span className="clinical-patient-tag">Consulta</span></div>
      <div className="clinical-patient-time"><time dateTime={`${appointment.date}T${appointment.startTime}`}>{appointment.startTime.slice(0,5)}<span> – </span>{appointment.endTime.slice(0,5)}</time><time className="clinical-patient-date" dateTime={appointment.date}>{appointment.date}</time></div>
      <p className="clinical-patient-branch"><Icon name="location"/><span><span className="clinical-patient-label">Sucursal</span>{appointment.center||'No registrada'}</span></p>
      <div className="clinical-patient-reason"><span className="clinical-patient-label">Motivo de la cita</span><p>{appointment.reason||'Sin motivo registrado'}</p></div>
    </section>
  </section>;
}
