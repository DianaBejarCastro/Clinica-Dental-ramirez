import HeroArtwork, { HeroBackground } from './HeroArtwork';
import { sectionHref } from './PublicLayout';

const toothPath = 'M12 5C5 0 2 6 5 12c2 4 1 9 4 9 2 0 1-7 3-7s1 7 3 7c3 0 2-5 4-9 3-6 0-12-7-7Z';
const benefits = [
  ['Atención segura', 'M12 3 3 7v5c0 5 9 9 9 9s9-4 9-9V7l-9-4ZM8 12l3 3 5-6'],
  ['Profesionales calificados', 'M15 7a3 3 0 1 1-6 0 3 3 0 0 1 6 0ZM5 21v-3a7 7 0 0 1 14 0v3M4 7a2 2 0 1 0 0 4m16-4a2 2 0 1 1 0 4M2 19v-3m20 3v-3'],
  ['Tecnología moderna', toothPath],
] as const;

export default function HeroSection() {
  return <section id="inicio" className="public-hero" aria-labelledby="hero-title" tabIndex={-1}>
    <HeroBackground />
    <div className="public-container public-hero-grid">
      <div className="public-hero-copy">
        <p className="public-eyebrow">CLÍNICA DENTAL RAMÍREZ</p>
        <h1 id="hero-title">Tu salud bucal,<span>en buenas manos</span></h1>
        <p>Atención dental integral, moderna y personalizada para toda la familia.</p>
        <div className="public-actions">
          <a className="public-button" href={sectionHref('contacto')}><svg width="20" height="20" viewBox="0 0 24 24" fill="none" aria-hidden="true"><path d="M8 3v4m8-4v4M4 10h16M4 5h16v15H4ZM8 14h2m4 0h2" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round"/></svg>Agendar mi cita</a>
          <a className="public-button public-button-secondary" href={sectionHref('servicios')}>Conocer servicios <span aria-hidden="true">→</span></a>
        </div>
        <ul className="hero-care-benefits">{benefits.map(([label, path]) => <li key={label}><span aria-hidden="true"><svg viewBox="0 0 24 24" fill="none"><path d={path} stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round"/></svg></span><div>{label}</div></li>)}</ul>
      </div>
      <HeroArtwork />
    </div>
  </section>;
}
