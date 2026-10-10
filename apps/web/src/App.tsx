import { AuthProvider, useAuth } from './modules/auth/AuthProvider';
import { useEffect } from 'react';
import PatientRegisterPage from './modules/auth/PatientRegisterPage';
import PatientDashboard from './modules/patient/PatientDashboard';
import { Protected, useRoute } from './router/Router';
import AdminLayout from './layouts/AdminLayout';
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
  const { session, loading } = useAuth(); const isPatient = session?.user.roles.includes('PATIENT') ?? false;
  useEffect(() => { if (!loading && isPatient && path !== '/patient' && path !== '/patient/appointments') window.location.hash = '/patient'; }, [isPatient, loading, path]);
  if (!loading && isPatient) return <Protected><PatientDashboard appointments={path === '/patient/appointments'} /></Protected>;
  const pages = {
    '/appointments': <Protected permission="appointments.view"><AppointmentsPage /></Protected>,
    '/patients': <Protected permission="patients.view"><PatientsPage /></Protected>,
    '/users': <Protected permission="users.view"><UsersPage /></Protected>, '/roles': <Protected permission="roles.view"><RolesPage /></Protected>, '/permissions': <Protected permission="permissions.view"><PermissionsPage /></Protected>,
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
