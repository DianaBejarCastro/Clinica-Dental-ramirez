import { AuthProvider, useAuth } from './modules/auth/AuthProvider';
import { Protected, useRoute } from './router/Router';
import AdminLayout from './layouts/AdminLayout';
import UsersPage from './modules/admin/users/UsersPage';
import RolesPage from './modules/admin/roles/RolesPage';
import PermissionsPage from './modules/admin/permissions/PermissionsPage';
import './admin.css';

function Workspace() {
  const path = useRoute(); const { session } = useAuth();
  const pages = { '/users': <Protected permission="users.view"><UsersPage /></Protected>, '/roles': <Protected permission="roles.view"><RolesPage /></Protected>, '/permissions': <Protected permission="permissions.view"><PermissionsPage /></Protected> };
  return <Protected><AdminLayout path={path}>{path in pages ? pages[path as keyof typeof pages] : path === '/' || path === '/login' ? <><p className="eyebrow">RESUMEN</p><h1>Hola, {session?.user.firstName}.</h1><p className="muted">Bienvenido a la administración de Clínica Dental RAMÍREZ.</p><section className="card"><h2>Tu espacio de trabajo</h2><p>Usa el menú para gestionar las cuentas y el acceso del equipo.</p><p><strong>Roles:</strong> {session?.user.roles.join(', ') || 'Sin roles asignados'}</p></section></> : <section className="card"><h1>Página no encontrada</h1><a href="#/">Volver al resumen</a></section>}</AdminLayout></Protected>;
}

export default function App() {
  return <AuthProvider><Workspace /></AuthProvider>;
}
