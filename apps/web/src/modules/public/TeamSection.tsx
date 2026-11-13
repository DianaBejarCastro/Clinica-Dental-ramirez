import { useEffect, useState } from 'react';
import { apiUrl, getSession, refresh } from '../../services/api';
import { PublicSection } from './PublicSection';
import './team-section.css';
export interface PublicDoctor { id: string; firstName: string; lastName: string; specialties: string[]; photoVersion: string | null }
export function Portrait({ doctor }: { doctor: PublicDoctor }) {
  const [failed, setFailed] = useState(false);
  return <div className="team-portrait">{doctor.photoVersion && !failed ? <img loading="lazy" width={1080} height={1350}
    src={apiUrl(`/api/clinic/team/${doctor.id}/photo?v=${encodeURIComponent(doctor.photoVersion)}`)} alt={`${doctor.firstName} ${doctor.lastName}`} onError={() => setFailed(true)} />
    : <span className="team-initials" aria-label={`Sin foto de ${doctor.firstName} ${doctor.lastName}`}>{doctor.firstName[0]}{doctor.lastName[0]}</span>}</div>;
}
export default function TeamSection() {
  const [message,setMessage]=useState('');const [opening,setOpening]=useState(false);
  async function book(id:string) { setOpening(true);setMessage('');try { const session=getSession()??await refresh().catch(()=>null); if(!session)window.location.hash=`/login?bookingDoctor=${encodeURIComponent(id)}`;else if(session.user.roles.includes('PATIENT'))window.location.hash=`/patient/book?doctor=${encodeURIComponent(id)}`;else setMessage('Esta opción está disponible solo para pacientes.'); } finally {setOpening(false);} }
  const [doctors, setDoctors] = useState<PublicDoctor[]>([]);
  useEffect(() => { const controller = new AbortController();
    fetch(apiUrl('/api/clinic/team'), { signal: controller.signal }).then(r => r.ok ? r.json() : []).then(data => { if (!controller.signal.aborted && Array.isArray(data)) setDoctors(data); }).catch(() => undefined);
    return () => controller.abort();
  }, []);
  return <PublicSection id="equipo" eyebrow="PERSONAS QUE CUIDAN" title="Nuestro equipo" className="public-tinted">
    {message&&<p role="alert">{message}</p>}{doctors.length ? <div className="public-team-grid">{doctors.map(doctor => <article className="public-team-card" key={doctor.id}>
      <Portrait doctor={doctor} /><div className="public-team-caption"><h3>{doctor.firstName} {doctor.lastName}</h3>{doctor.specialties.length > 0 && <p>{doctor.specialties.join(' · ')}</p>}<button disabled={opening} onClick={()=>void book(doctor.id)} aria-label={`Reservar cita con ${doctor.firstName} ${doctor.lastName}`}>Reservar cita</button></div>
    </article>)}</div>
      : <div className="public-team-empty"><span className="public-team-symbol" aria-hidden="true">+</span><div><h3>Conoce a quienes te acompañan</h3><p>Consulta con la clínica qué profesional puede atenderte.</p></div></div>}
  </PublicSection>;
}
