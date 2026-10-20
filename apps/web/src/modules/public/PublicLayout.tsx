import { useEffect, useRef, useState, type ReactNode } from 'react';
import ClinicLogo from '../admin/clinic/ClinicLogo';

export const publicLinks = [['inicio', 'Inicio'], ['nosotros', 'Nosotros'], ['servicios', 'Servicios'], ['equipo', 'Equipo'], ['contacto', 'Contacto']] as const;
export const sectionHref = (section: string) => `#/inicio?section=${section}`;
export function PublicHeader() {
  const [open, setOpen] = useState(false); const toggle = useRef<HTMLButtonElement>(null);
  return <header className="public-header"><div className="public-container public-header-inner">
    <a className="public-brand" href="#/inicio" aria-label="Clínica Dental RAMÍREZ, inicio"><ClinicLogo /></a>
    <button ref={toggle} className="public-menu-toggle" type="button" aria-controls="public-menu" aria-expanded={open}
      aria-label={open ? 'Cerrar menú' : 'Abrir menú'} onClick={() => setOpen(value => !value)}>
      <svg width="22" height="22" viewBox="0 0 24 24" fill="none" aria-hidden="true"><path d={open ? 'M6 6l12 12M18 6 6 18' : 'M4 6h16M4 12h16M4 18h16'} stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" /></svg>
    </button>
    <nav id="public-menu" className={`public-menu${open ? ' is-open' : ''}`} aria-label="Navegación pública"
      onKeyDown={event => { if (event.key === 'Escape') { setOpen(false); toggle.current?.focus(); } }}>
      {publicLinks.map(([id, label]) => <a key={id} href={sectionHref(id)} onClick={() => setOpen(false)}>{label}</a>)}
      <a className="public-access" href="#/login" onClick={() => setOpen(false)}>Acceso al sistema</a>
      <a className="public-button" href={sectionHref('contacto')} onClick={() => setOpen(false)}>Reservar cita</a>
    </nav>
  </div></header>;
}
export function PublicFooter() {
  return <footer className="public-footer"><div className="public-container public-footer-inner">
    <div><div className="public-brand"><ClinicLogo /><span>CLÍNICA DENTAL<strong>RAMÍREZ</strong></span></div><p>Un espacio para cuidar tu sonrisa.</p></div>
    <nav aria-label="Navegación del pie">{publicLinks.map(([id, label]) => <a key={id} href={sectionHref(id)}>{label}</a>)}<a href="#/login">Acceso al sistema</a></nav>
  </div></footer>;
}
export default function PublicLayout({ children, section }: { children: ReactNode; section: string | null }) {
  useEffect(() => {
    if (!section) return;
    if (!publicLinks.some(([id]) => id === section)) return;
    const target = document.getElementById(section);
    target?.scrollIntoView?.({ behavior: 'auto', block: 'start' }); target?.focus({ preventScroll: true });
  }, [section]);
  return <div className="public-site"><a className="public-skip" href={sectionHref('inicio')}>Ir al contenido</a><PublicHeader /><main>{children}</main><PublicFooter /></div>;
}
