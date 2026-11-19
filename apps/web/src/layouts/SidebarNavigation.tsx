import { useEffect, useId, useState } from 'react';
import { navigationGroups, overview, type NavigationConfiguration, type NavigationGroup, type NavigationItem } from './navigation';
import './sidebar.css';

function GroupIcon({ label }: { label: string }) {
  const paths: Record<string, string> = {
    'Administración': 'M4 7h16M4 12h16M4 17h16M8 5v4M16 10v4M10 15v4',
    'Clínica': 'M5 21V5h14v16M3 21h18M9 9h6M12 6v6M9 21v-5h6v5',
    'Equipo médico': 'M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2M22 21v-2a4 4 0 0 0-3-3.87M9 3a4 4 0 1 0 0 8 4 4 0 0 0 0-8M17 3a4 4 0 0 1 0 8',
    'Atención': 'M8 2v4M16 2v4M3 10h18M5 4h14a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2M8 15h3M8 18h7',
  };
  return <svg className="sidebar-group-icon" width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true">
    <path d={paths[label]} stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
  </svg>;
}

const peopleIcon = 'M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8M22 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75';
const inventoryIcon = 'M21 8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16zM3.3 7l8.7 5 8.7-5M12 22V12';
const calendarIcon = 'M8 2v4M16 2v4M3 10h18M5 4h14a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2zM8 14h.01M12 14h.01M16 14h.01M8 18h.01';
const paymentIcon = 'M3 5h18v14H3zM3 10h18M7 15h3';

const itemIconPaths: Record<string, string> = {
  '/': 'M3 3h7v7H3zM14 3h7v7h-7zM3 14h7v7H3zM14 14h7v7h-7z',
  '/administrators': peopleIcon,
  '/cashiers': 'M3 7h18v13H3zM3 7l2-4h14l2 4M7 12h4',
  '/secretaries': peopleIcon,
  '/users': peopleIcon,
  '/roles': 'M12 22s8-4 8-11V5l-8-3-8 3v6c0 7 8 11 8 11zM9 12l2 2 4-4',
  '/permissions': 'M9 11l3 3L22 4M21 12v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11',
  '/admin/inventory': inventoryIcon,
  '/doctor/inventory': inventoryIcon,
  '/clinic': 'M3 21h18M5 21V5h14v16M9 9h2M13 9h2M9 13h2M13 13h2M10 21v-4h4v4',
  '/centers': 'M20 10c0 5-8 12-8 12S4 15 4 10a8 8 0 1 1 16 0zM12 10a2.5 2.5 0 1 0 0-5 2.5 2.5 0 0 0 0 5z',
  '/specialties': 'M12 3c-4 0-7 3-7 7 0 5 3 11 7 11s7-6 7-11c0-4-3-7-7-7zM8 11c0 2 1 4 2 5M16 11c0 2-1 4-2 5M10 8c0 1 1 2 2 2s2-1 2-2',
  '/doctors': 'M20 21a8 8 0 0 0-16 0M12 13a4 4 0 1 0 0-8 4 4 0 0 0 0 8zM19 8v6M16 11h6',
  '/schedules': 'M8 2v4M16 2v4M3 10h18M5 4h14a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2zM8 14h3M8 18h3',
  '/patients': peopleIcon,
  '/doctor/patients': peopleIcon,
  '/appointments': calendarIcon,
  '/doctor/appointments': calendarIcon,
  '/patient/appointments': calendarIcon,
  '/cashier/payments': paymentIcon,
  '/patient/payments': paymentIcon,
  '/patient': 'M3 10.5 12 3l9 7.5M5 9v12h14V9M9 21v-7h6v7',
};

export function SidebarItemIcon({ route }: { route: string }) {
  return <svg className="sidebar-item-icon" viewBox="0 0 24 24" fill="none" aria-hidden="true" focusable="false">
    <path d={itemIconPaths[route] ?? 'M4 5h16M4 12h16M4 19h16'} stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" />
  </svg>;
}

export function SidebarItem({ item, path }: { item: NavigationItem; path: string }) {
  return <a href={`#${item.route}`} aria-current={path === item.route ? 'page' : undefined}>
    <SidebarItemIcon route={item.route} /><span>{item.label}</span>
  </a>;
}
export function SidebarGroup({ group, path, open, onToggle }: { group: NavigationGroup; path: string; open: boolean; onToggle: () => void }) {
  const id = useId();
  const active = group.items.some(item => item.route === path);
  return <section className={`sidebar-group${active ? ' is-active' : ''}`}>
    <button className="sidebar-group-toggle" type="button" aria-expanded={open} aria-controls={id}
      aria-disabled={active && open || undefined} title={active && open ? 'La sección actual permanece abierta' : undefined}
      onClick={onToggle}>
      <span className="sidebar-group-label"><GroupIcon label={group.label} />{group.label}</span><svg className="sidebar-chevron" width="16" height="16" viewBox="0 0 16 16" fill="none" aria-hidden="true">
        <path d="m6 3 5 5-5 5" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
      </svg>
    </button>
    <div id={id} className="sidebar-group-panel" data-open={open} aria-hidden={!open} inert={!open}>
      <div className="sidebar-group-clip"><ul>{group.items.map(item => <li key={item.route}><SidebarItem item={item} path={path} /></li>)}</ul></div>
    </div>
  </section>;
}
export default function SidebarNavigation({ path, permissions, navigation = { items: [overview], groups: navigationGroups } }: { path: string; permissions: string[]; navigation?: NavigationConfiguration }) {
  const groups = navigation.groups.map(group => ({ ...group,
    items: group.items.filter(item => !item.permission || permissions.includes(item.permission)),
  })).filter(group => group.items.length > 0);
  const activeGroup = groups.find(group => group.items.some(item => item.route === path))?.label ?? null;
  const [openGroup, setOpenGroup] = useState<string | null>(activeGroup);
  useEffect(() => { setOpenGroup(activeGroup); }, [path, activeGroup]);
  return <nav className="sidebar-navigation" aria-label="Navegación principal">
    {navigation.items.filter(item => !item.permission || permissions.includes(item.permission)).map(item => <SidebarItem key={item.route} item={item} path={path} />)}
    {groups.map(group => <SidebarGroup key={group.label} group={group} path={path} open={openGroup === group.label}
      onToggle={() => setOpenGroup(current => current === group.label && activeGroup !== group.label ? null : group.label)} />)}
  </nav>;
}
