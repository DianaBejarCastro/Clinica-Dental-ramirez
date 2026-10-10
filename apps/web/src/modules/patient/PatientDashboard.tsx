import { useEffect, useState } from 'react';
import { api, logout } from '../../services/api';
import ClinicLogo from '../admin/clinic/ClinicLogo';
import '../auth/patient-register.css';
interface Account { firstName: string; lastName: string; ci: string | null; birthDate: string | null; gender: string | null; phone: string | null; email: string | null }
interface Appointment { id: string; appointmentDate: string; startTime: string; endTime: string; status: string; doctor: string; center: string; specialty: string }
const statuses: Record<string, string> = { SCHEDULED: 'Programada', CONFIRMED: 'Confirmada', IN_PROGRESS: 'En atención', COMPLETED: 'Completada', CANCELLED: 'Cancelada', NO_SHOW: 'No asistió' };
export default function PatientDashboard({ appointments = false }: { appointments?: boolean }) {
  const [account, setAccount] = useState<Account | null>(null); const [items, setItems] = useState<Appointment[]>([]);
  const [loading, setLoading] = useState(true); const [error, setError] = useState('');
  useEffect(() => { let live = true; setError(''); setLoading(true);
    const request = appointments ? api<Appointment[]>('/api/patient/appointments').then(data => { if (live) setItems(data); }) : api<Account>('/api/patient/me').then(data => { if (live) setAccount(data); });
    request.catch(e => { if (live) setError(e.message); }).finally(() => { if (live) setLoading(false); }); return () => { live = false; };
  }, [appointments]);
  return <main className="patient-portal"><header><ClinicLogo /><nav aria-label="Portal del paciente"><a href="#/patient">Mi cuenta</a> · <a href="#/patient/appointments">Mis citas</a></nav>
    <button className="secondary" onClick={() => void logout().then(() => { window.location.hash = '/login'; }).catch(() => setError('No se pudo cerrar sesión. Inténtalo nuevamente.'))}>Cerrar sesión</button></header>
    <section className="card"><h1>{appointments ? 'Mis citas' : 'Mi cuenta'}</h1>{loading && <p role="status">Cargando…</p>}{error && <p role="alert">{error}</p>}
      {!loading && !error && !appointments && account && <><h2>{account.firstName} {account.lastName}</h2><dl>{[['Email', account.email], ['CI', account.ci], ['Fecha de nacimiento', account.birthDate], ['Género', account.gender], ['Teléfono', account.phone]].map(([label, value]) => <div key={label}><dt>{label}</dt><dd>{value || '—'}</dd></div>)}</dl></>}
      {!loading && !error && appointments && (items.length ? <div className="table-card"><table><thead><tr><th>Fecha</th><th>Hora</th><th>Doctor</th><th>Especialidad</th><th>Sucursal</th><th>Estado</th></tr></thead><tbody>{items.map(a => <tr key={a.id}><td>{a.appointmentDate}</td><td>{a.startTime.slice(0, 5)}–{a.endTime.slice(0, 5)}</td><td>{a.doctor}</td><td>{a.specialty}</td><td>{a.center}</td><td>{statuses[a.status] ?? a.status}</td></tr>)}</tbody></table></div> : <p>No tienes citas registradas.</p>)}
    </section></main>;
}
