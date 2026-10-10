import { useEffect, useId, useState } from 'react';
import { navigationGroups, overview, type NavigationGroup, type NavigationItem } from './navigation';
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

export function SidebarItem({ item, path }: { item: NavigationItem; path: string }) {
  return <a href={`#${item.route}`} aria-current={path === item.route ? 'page' : undefined}>{item.label}</a>;
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
export default function SidebarNavigation({ path, permissions }: { path: string; permissions: string[] }) {
  const groups = navigationGroups.map(group => ({ ...group,
    items: group.items.filter(item => !item.permission || permissions.includes(item.permission)),
  })).filter(group => group.items.length > 0);
  const activeGroup = groups.find(group => group.items.some(item => item.route === path))?.label ?? null;
  const [openGroup, setOpenGroup] = useState<string | null>(activeGroup);
  useEffect(() => { setOpenGroup(activeGroup); }, [path, activeGroup]);
  return <nav className="sidebar-navigation" aria-label="Navegación principal">
    <SidebarItem item={overview} path={path} />
    {groups.map(group => <SidebarGroup key={group.label} group={group} path={path} open={openGroup === group.label}
      onToggle={() => setOpenGroup(current => current === group.label && activeGroup !== group.label ? null : group.label)} />)}
  </nav>;
}
