import { AuthProvider } from './modules/auth/AuthProvider';
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
import './admin.css';
import './clinic-admin.css';

function Workspace() {
  const route = useRoute(); const [path, query = ''] = route.split('?');
  const pages = {
    '/patients': <Protected permission="patients.view"><PatientsPage /></Protected>,
    '/users': <Protected permission="users.view"><UsersPage /></Protected>, '/roles': <Protected permission="roles.view"><RolesPage /></Protected>, '/permissions': <Protected permission="permissions.view"><PermissionsPage /></Protected>,
    '/clinic': <Protected permission="clinic.view"><ClinicPage /></Protected>, '/centers': <Protected permission="centers.view"><CentersPage /></Protected>, '/doctors': <Protected permission="doctors.view"><DoctorsPage /></Protected>,
    '/specialties': <Protected permission="specialties.view"><SpecialtiesPage /></Protected>, '/schedules': <Protected permission="schedules.view"><SchedulesPage key={query} initialDoctor={new URLSearchParams(query).get('doctor') ?? ''} /></Protected>,
  };
  return <Protected><AdminLayout path={path}>{path in pages ? pages[path as keyof typeof pages] : path === '/' || path === '/login' ? <DashboardPage /> : <section className="card"><h1>Página no encontrada</h1><a href="#/">Volver al resumen</a></section>}</AdminLayout></Protected>;
}

export default function App() {
  return <AuthProvider><Workspace /></AuthProvider>;
}
