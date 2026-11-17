import { sectionHref } from './PublicLayout';
import './mobile-bottom-nav.css';

const icons = {
  home: 'M3 10 12 3l9 7M5 9v12h5v-7h4v7h5V9',
  services: 'M12 5C8 2 3 3 4 9c1 5 2 12 5 12 2 0 1-7 3-7s1 7 3 7c3 0 4-7 5-12 1-6-4-7-8-4Z',
  calendar: 'M8 3v4m8-4v4M4 10h16M5 5h14a1 1 0 0 1 1 1v14H4V6a1 1 0 0 1 1-1ZM8 14h2m4 0h2m-8 3h2',
  phone: 'M8 3H5a2 2 0 0 0-2 2c0 9 7 16 16 16a2 2 0 0 0 2-2v-3l-5-2-2 2a15 15 0 0 1-6-6l2-2-2-5Z',
  user: 'M16 7a4 4 0 1 1-8 0 4 4 0 0 1 8 0ZM4 21v-2a8 8 0 0 1 16 0v2',
};

export default function MobileBottomNav({ section }: { section: string | null }) {
  const items = [
    { label: 'Inicio', icon: 'home', href: sectionHref('inicio'), active: !section || section === 'inicio' },
    { label: 'Servicios', icon: 'services', href: sectionHref('servicios'), active: section === 'servicios' },
    { label: 'Agendar', icon: 'calendar', href: sectionHref('contacto'), active: false },
    { label: 'Contacto', icon: 'phone', href: sectionHref('contacto'), active: section === 'contacto' },
    // Workspace already restores the session and resolves the appropriate role dashboard.
    { label: 'Mi perfil', icon: 'user', href: '#/login', active: false },
  ] as const;
  return <nav className="public-bottom-nav" aria-label="Navegación móvil principal">
    {items.map(item => <a key={item.label} href={item.href}
      className={item.icon === 'calendar' ? 'public-bottom-book' : undefined}
      aria-label={item.icon === 'calendar' ? 'Agendar cita' : undefined}
      aria-current={item.active ? 'page' : undefined}>
      <span className="public-bottom-icon"><svg width="22" height="22" viewBox="0 0 24 24" fill="none" aria-hidden="true" focusable="false"><path d={icons[item.icon]} stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" /></svg></span>
      <span>{item.label}</span>
    </a>)}
  </nav>;
}
