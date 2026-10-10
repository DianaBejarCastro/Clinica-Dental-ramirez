import { sectionHref } from './PublicLayout';

export default function HeroSection() {
  return <section id="inicio" className="public-hero" aria-labelledby="hero-title" tabIndex={-1}><div className="public-container public-hero-grid">
    <div className="public-hero-copy"><p className="public-eyebrow">CLÍNICA DENTAL RAMÍREZ</p>
      <h1 id="hero-title">Cuidamos tu sonrisa <span>en cada etapa</span></h1>
      <p>Atención odontológica profesional, cercana y organizada para ti y tu familia.</p>
      <div className="public-actions"><a className="public-button" href={sectionHref('contacto')}>Reservar cita</a><a className="public-button public-button-secondary" href={sectionHref('servicios')}>Conocer servicios <span aria-hidden="true">↗</span></a></div>
      <p className="public-hero-note">Cercanía en el trato. Cuidado en cada detalle.</p>
    </div>
    <div className="public-hero-art" aria-hidden="true"><div className="public-orbit" /><svg viewBox="0 0 240 280" fill="none">
      <path d="M120 48C88 25 47 32 39 70c-7 35 18 65 24 99 5 30 12 75 28 72 14-3 13-77 29-77s15 74 29 77c16 3 23-42 28-72 6-34 31-64 24-99-8-38-49-45-81-22Z" fill="currentColor" />
      <path d="M120 48c14 9 25 12 39 10M65 81c-3 15 3 27 8 38" stroke="var(--public-accent)" strokeWidth="4" strokeLinecap="round" />
    </svg><span className="public-art-caption">Tu salud oral,<br /><strong>en el centro.</strong></span></div>
  </div></section>;
}
