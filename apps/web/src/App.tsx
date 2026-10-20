import { AuthProvider, useAuth } from './modules/auth/AuthProvider';
import { lazy, Suspense, useEffect } from 'react';
const CashierPayments = lazy(() => import('./modules/cashier/CashierPayments'));
import PatientRegisterPage from './modules/auth/PatientRegisterPage';
import PatientDashboard from './modules/patient/PatientDashboard';
import DoctorDashboard from './modules/doctor/DoctorDashboard';
import ClinicalConsultation from './modules/doctor/clinical/ClinicalConsultation';
import { Protected, useRoute } from './router/Router';
import AdminLayout from './layouts/AdminLayout';
import StaffPage from './modules/admin/users/StaffPage';
import UsersPage from './modules/admin/users/UsersPage';
import RolesPage from './modules/admin/roles/RolesPage';
import PermissionsPage from './modules/admin/permissions/PermissionsPage';
import ClinicPage from './modules/admin/clinic/ClinicPage';
import CentersPage from './modules/admin/centers/CentersPage';
import DoctorsPage from './modules/admin/doctors/DoctorsPage';
import SpecialtiesPage from './modules/admin/specialties/SpecialtiesPage';
import SchedulesPage from './modules/admin/schedules/SchedulesPage';
import DashboardPage from './modules/admin/DashboardPage';
import PatientsPage from './modules/admin/patients/PatientsPage';
import AppointmentsPage from './modules/admin/appointments/AppointmentsPage';
import PublicHomePage from './modules/public/PublicHomePage';
import './admin.css';
import './clinic-admin.css';

function Workspace() {
  const route = useRoute(); const [path, query = ''] = route.split('?');
  const { session, loading } = useAuth(); const isPatient = (session?.user.roles.includes('PATIENT') ?? false) && !session?.user.permissions.length;
  const isDoctor = session?.user.roles.includes('DOCTOR') ?? false;
  const isCashier = session?.user.roles.includes('CASHIER') ?? false;
  useEffect(() => { if (!loading && isCashier && !isDoctor && (path === '/' || path === '/login' || path === '/cashier')) window.location.hash = '/cashier/payments'; }, [isCashier, isDoctor, loading, path]);
  useEffect(() => { if (!loading && isDoctor && (path === '/' || path === '/login' || path === '/doctor')) window.location.hash = '/doctor/appointments'; }, [isDoctor, loading, path]);
  useEffect(() => { if (!loading && isPatient && !isDoctor && path !== '/patient' && path !== '/patient/appointments') window.location.hash = '/patient'; }, [isPatient, isDoctor, loading, path]);
  if (path === '/cashier/payments' || path === '/cashier' || !loading && isCashier && !isDoctor && (path === '/' || path === '/login'))
    return <Protected permission="payments.view"><Suspense fallback={<p role="status">Cargando cobros…</p>}><CashierPayments /></Suspense></Protected>;
  if (path === '/doctor/consultation')
    return <Protected permission="clinical_history.view">{isDoctor ? <ClinicalConsultation key={query} appointmentId={new URLSearchParams(query).get('appointment') ?? ''} /> : <section className="card"><h1>Acceso restringido</h1></section>}</Protected>;
  if (path === '/doctor' || path === '/doctor/appointments' || !loading && isDoctor && (path === '/' || path === '/login'))
    return <Protected>{isDoctor ? <DoctorDashboard /> : <section className="card"><h1>Acceso restringido</h1><p>Este portal es exclusivo para doctores.</p></section>}</Protected>;
  if (!loading && (isPatient || session?.user.roles.includes('PATIENT') && (path === '/patient' || path === '/patient/appointments')))
    return <Protected><PatientDashboard appointments={path === '/patient/appointments'} /></Protected>;
  const pages = {
    '/administrators': <Protected permission="users.view"><StaffPage key="administrators" kind="administrators" /></Protected>,
    '/cashiers': <Protected permission="users.view"><StaffPage key="cashiers" kind="cashiers" /></Protected>,
    '/secretaries': <Protected permission="users.view"><StaffPage key="secretaries" kind="secretaries" /></Protected>,
    '/appointments': <Protected permission="appointments.view"><AppointmentsPage /></Protected>,
    '/patients': <Protected permission="patients.view"><PatientsPage /></Protected>,
    '/users': <Protected permission="users.view"><UsersPage key={query} initialUser={new URLSearchParams(query).get('user') ?? ''} /></Protected>, '/roles': <Protected permission="roles.view"><RolesPage /></Protected>, '/permissions': <Protected permission="permissions.view"><PermissionsPage /></Protected>,
    '/clinic': <Protected permission="clinic.view"><ClinicPage /></Protected>, '/centers': <Protected permission="centers.view"><CentersPage /></Protected>, '/doctors': <Protected permission="doctors.view"><DoctorsPage /></Protected>,
    '/specialties': <Protected permission="specialties.view"><SpecialtiesPage /></Protected>, '/schedules': <Protected permission="schedules.view"><SchedulesPage key={query} initialDoctor={new URLSearchParams(query).get('doctor') ?? ''} /></Protected>,
  };
  return <Protected><AdminLayout path={path}>{path in pages ? pages[path as keyof typeof pages] : path === '/' || path === '/login' ? <DashboardPage /> : <section className="card"><h1>Página no encontrada</h1><a href="#/">Volver al resumen</a></section>}</AdminLayout></Protected>;
}

export default function App() {
  const route = useRoute(); const [path, query = ''] = route.split('?');
  if (path === '/inicio') return <PublicHomePage section={new URLSearchParams(query).get('section')} />;
  if (path === '/register') return <PatientRegisterPage />;
  return <AuthProvider><Workspace /></AuthProvider>;
}
