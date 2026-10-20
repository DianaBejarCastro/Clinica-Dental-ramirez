import SidebarClinicName from './SidebarClinicName';
import SidebarNavigation from './SidebarNavigation';
import ClinicLogo from '../modules/admin/clinic/ClinicLogo';
import { cashierNavigation, doctorNavigation, navigationGroups, overview } from './navigation';
import { useState, type ReactNode } from 'react';
import { useAuth } from '../modules/auth/AuthProvider';
import { logout } from '../services/api';
export default function AdminLayout({ children, path, doctorPortal = false, cashierPortal = false }: { children: ReactNode; path: string; doctorPortal?: boolean; cashierPortal?: boolean }) {
  const { session } = useAuth(); const [error, setError] = useState(''); const [busy, setBusy] = useState(false);
  const navigation = cashierPortal ? cashierNavigation : doctorPortal ? doctorNavigation : { items: [overview], groups: navigationGroups };
  const group = navigation.groups.find(group => group.items.some(item => item.route === path));
  const item = navigation.items.find(item => item.route === path) ?? group?.items.find(item => item.route === path);
  const context = item && (!item.permission || session?.user.permissions.includes(item.permission))
    ? item.label : path === '/' ? overview.label : 'Gestión de la clínica';
  return <div className="shell">
    <aside>
      <div className="brand"><ClinicLogo /><SidebarClinicName /></div>
      <p className="eyebrow">ESPACIO DE TRABAJO</p>
      <SidebarNavigation path={path} permissions={session?.user.permissions ?? []} navigation={navigation} />
      <div className="account"><strong>{doctorPortal ? `${session?.user.firstName ?? ''} ${session?.user.lastName ?? ''}` : session?.user.firstName}</strong><small>{session?.user.email}</small>
        {!doctorPortal && !cashierPortal && session?.user.roles.includes('PATIENT') && <a href="#/patient">Mi portal de paciente</a>}
        {!doctorPortal && !cashierPortal && session?.user.roles.includes('DOCTOR') && <a href="#/doctor/appointments">Mis citas</a>}
        <button className="secondary" disabled={busy} onClick={async () => {
          setBusy(true); setError('');
          try { await logout(); } catch { setError('No se pudo cerrar la sesión. Intenta nuevamente.'); }
          finally { setBusy(false); }
        }}>Cerrar sesión</button>{error && <p role="alert">{error}</p>}
      </div>
    </aside>
    <main className="workspace">
      <header><div className="header-context"><small>Clínica Dental RAMÍREZ</small><strong>{context}</strong></div>
        <div className="header-account"><span>{session?.user.firstName}</span><span className="version">v0.3.0</span></div>
      </header>
      <div className="workspace-content">{children}</div>
    </main>
  </div>;
}
