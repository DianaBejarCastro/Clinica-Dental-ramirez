import PublicWhatsAppButton from './PublicWhatsAppButton';
import { useEffect, useRef, useState, type ReactNode } from 'react';
import MobileBottomNav from './MobileBottomNav';
import PublicSocialLinks from './PublicSocialLinks';
import DecorativeAquaWaves from '../../components/DecorativeAquaWaves';
import ClinicLogo from '../admin/clinic/ClinicLogo';

export const publicLinks = [['inicio', 'Inicio'], ['nosotros', 'Nosotros'], ['servicios', 'Servicios'], ['equipo', 'Equipo'], ['contacto', 'Contacto']] as const;
export const sectionHref = (section: string) => `#/inicio?section=${section}`;
function PublicNavigationItems({ section, onNavigate }: { section?: string | null; onNavigate?: () => void }) {
  return <>
    {publicLinks.map(([id, label]) => <a key={id} href={sectionHref(id)}
      aria-current={(section || 'inicio') === id ? 'location' : undefined} onClick={onNavigate}>{label}</a>)}
    <a className="public-access" href="#/login" onClick={onNavigate}>Acceso al sistema <span aria-hidden="true">→</span></a>
    <a className="public-button" href={sectionHref('contacto')} onClick={onNavigate}>
      <svg width="20" height="20" viewBox="0 0 24 24" fill="none" aria-hidden="true">
        <path d="M8 3v4m8-4v4M4 10h16M5 5h14a1 1 0 0 1 1 1v14H4V6a1 1 0 0 1 1-1Z" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round" />
        <path d="M8 14h2m4 0h2m-8 3h2" stroke="currentColor" strokeWidth="1.7" />
      </svg>Reservar cita
    </a>
  </>;
}

export function PublicHeader({ section }: { section?: string | null }) {
  const [open, setOpen] = useState(false); const toggle = useRef<HTMLButtonElement>(null);
  return <header className="public-header"><div className="public-header-surface"><div className="public-header-inner">
    <a className="public-brand" href="#/inicio" aria-label="Clínica Dental RAMÍREZ, inicio"><ClinicLogo /></a>
    <button ref={toggle} className="public-menu-toggle" type="button" aria-controls="public-menu" aria-expanded={open}
      aria-label={open ? 'Cerrar menú' : 'Abrir menú'} onClick={() => setOpen(value => !value)}>
      <svg width="22" height="22" viewBox="0 0 24 24" fill="none" aria-hidden="true"><path d={open ? 'M6 6l12 12M18 6 6 18' : 'M4 6h16M4 12h16M4 18h16'} stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" /></svg>
    </button>
    <nav id="public-menu" className={`public-menu public-navigation${open ? ' is-open' : ''}`} aria-label="Navegación pública"
      onKeyDown={event => { if (event.key === 'Escape') { setOpen(false); toggle.current?.focus(); } }}>
      <PublicNavigationItems section={section} onNavigate={() => setOpen(false)} />
    </nav>
  </div></div></header>;
}
export function PublicFooter({ section }: { section?: string | null }) {
  const currentYear = new Date().getFullYear();

  return <footer className="public-footer">
    <DecorativeAquaWaves className="public-footer-wave public-footer-wave--top" />
    <div className="public-container public-footer-inner">
      <div className="public-footer-brand">
        <div className="public-brand"><ClinicLogo /></div>
      </div>
      <nav className="public-navigation public-footer-nav" aria-label="Navegación del pie">
        <PublicNavigationItems section={section} />
      </nav>
      <div className="public-footer-actions"><PublicSocialLinks /></div>
    </div>
    <div className="public-footer-bottom">
      <svg className="public-footer-wave public-footer-wave--bottom" viewBox="0 0 1600 156" preserveAspectRatio="none" aria-hidden="true" focusable="false">
        <defs>
          <linearGradient id="footer-wave-band" x1="0%" y1="0%" x2="100%" y2="0%">
            <stop offset="0%" stopColor="var(--public-color-main-turquoise)" />
            <stop offset="50%" stopColor="var(--public-color-band-midpoint)" />
            <stop offset="100%" stopColor="var(--public-color-deep-turquoise)" />
          </linearGradient>
          <linearGradient id="footer-wave-aqua" x1="0%" y1="0%" x2="100%" y2="100%">
            <stop offset="0%" stopColor="var(--public-color-light-aqua)" />
            <stop offset="62%" stopColor="var(--public-color-bright-aqua)" />
            <stop offset="100%" stopColor="var(--public-color-soft-turquoise)" />
          </linearGradient>
          <linearGradient id="footer-wave-cap" x1="0%" y1="0%" x2="100%" y2="100%">
            <stop offset="0%" stopColor="var(--public-color-white-blue)" />
            <stop offset="45%" stopColor="var(--public-color-white-blue)" />
            <stop offset="100%" stopColor="var(--public-color-light-aqua)" />
          </linearGradient>
        </defs>
        <path fill="url(#footer-wave-band)" d="M0 104C180 93 390 96 610 105 830 114 1018 107 1195 92 1372 77 1505 78 1600 84V0H0Z" />
        <path fill="url(#footer-wave-aqua)" fillOpacity=".95" d="M0 78C180 70 390 95 615 104 825 111 1010 103 1185 84 1365 66 1505 66 1600 76V0H0Z" />
        <path fill="url(#footer-wave-cap)" d="M0 34C150 27 380 118 620 101 820 111 980 108 1130 94 1320 77 1485 25 1600 0V0H0Z" />
      </svg>
      <div className="public-footer-bottom-content">
        <p className="public-footer-bottom-item">© <time dateTime={String(currentYear)}>{currentYear}</time> Clínica Dental Ramírez. Todos los derechos reservados.</p>
        <span className="public-footer-bottom-separator" aria-hidden="true" />
        <span className="public-footer-bottom-item">Política de Privacidad</span>
        <span className="public-footer-bottom-separator" aria-hidden="true" />
        <span className="public-footer-bottom-item">Términos de Uso</span>
      </div>
    </div>
  </footer>;
}
export default function PublicLayout({ children, section }: { children: ReactNode; section: string | null }) {
  useEffect(() => {
    if (!section) return;
    if (!publicLinks.some(([id]) => id === section)) return;
    const target = document.getElementById(section);
    target?.scrollIntoView?.({ behavior: 'auto', block: 'start' }); target?.focus({ preventScroll: true });
  }, [section]);
  return <div className="public-site"><a className="public-skip" href={sectionHref('inicio')}>Ir al contenido</a><PublicHeader section={section} /><main>{children}</main><PublicFooter section={section} /><MobileBottomNav section={section} /><PublicWhatsAppButton /></div>;
}
