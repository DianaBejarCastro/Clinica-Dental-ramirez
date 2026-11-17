import HeroTooth3D from './HeroTooth3D';
import { sectionHref } from './PublicLayout';

const toothPath = 'M12 5C5 0 2 6 5 12c2 4 1 9 4 9 2 0 1-7 3-7s1 7 3 7c3 0 2-5 4-9 3-6 0-12-7-7Z';
const benefits = [
  ['Atención segura', 'M12 3 3 7v5c0 5 9 9 9 9s9-4 9-9V7l-9-4ZM8 12l3 3 5-6'],
  ['Profesionales calificados', 'M15 7a3 3 0 1 1-6 0 3 3 0 0 1 6 0ZM5 21v-3a7 7 0 0 1 14 0v3M4 7a2 2 0 1 0 0 4m16-4a2 2 0 1 1 0 4M2 19v-3m20 3v-3'],
  ['Tecnología moderna', toothPath],
] as const;

export default function HeroSection() {
  return <section id="inicio" className="public-hero" aria-labelledby="hero-title" tabIndex={-1}>
    <div className="hero-clinic-scene" aria-hidden="true">
      <svg className="hero-clinic-room" viewBox="0 0 600 550" fill="none">
        <path d="M30 0v550M300 0v550M590 0v550M0 180h600" stroke="#fff" strokeWidth="12"/>
        <path d="M440 40v90h-120v80" stroke="#88b9ba" strokeWidth="12" strokeLinejoin="round"/>
        <ellipse cx="320" cy="215" rx="65" ry="27" fill="#fff" stroke="#a9d1d1" strokeWidth="12"/>
        <path d="M300 340 360 245" stroke="#52bcb8" strokeWidth="80" strokeLinecap="round"/>
        <path d="m300 340 130 65h80" stroke="#68c9c3" strokeWidth="62" strokeLinecap="round"/>
        <path d="M380 410v70m-65 20h140" stroke="#b7cece" strokeWidth="24" strokeLinecap="round"/>
        <path d="M520 340v125m-35-130h70" stroke="#a0bfc1" strokeWidth="14"/>
      </svg>
    </div>
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
      <div className="public-hero-visual">
        <HeroTooth3D />
        <svg className="public-hero-shield" viewBox="0 0 140 165" aria-hidden="true">
          <defs><linearGradient id="hero-shield-fill" x2="1" y2="1"><stop stopColor="#53e9de"/><stop offset="1" stopColor="#008c86"/></linearGradient></defs>
          <path d="M70 8 128 30v47c0 36-58 77-58 77S12 113 12 77V30Z" fill="url(#hero-shield-fill)" stroke="#c2fffa" strokeWidth="5"/>
          <path d="M70 21 116 39v38c0 26-46 62-46 62S24 103 24 77V39Z" fill="none" stroke="#008a83" strokeWidth="2"/>
          <path d="M59 48h22v21h21v22H81v21H59V91H38V69h21Z" fill="white"/>
        </svg>
        <svg className="public-hero-mirror" viewBox="0 0 100 310" aria-hidden="true">
          <defs><linearGradient id="hero-mirror-metal"><stop stopColor="#789598"/><stop offset=".4" stopColor="#f8ffff"/><stop offset=".7" stopColor="#b6cdcf"/><stop offset="1" stopColor="#718c90"/></linearGradient></defs>
          <rect x="42" y="94" width="16" height="200" rx="8" fill="url(#hero-mirror-metal)" stroke="#789598"/>
          <ellipse cx="50" cy="52" rx="34" ry="46" fill="url(#hero-mirror-metal)" stroke="#789598" strokeWidth="3"/>
          <ellipse cx="50" cy="52" rx="27" ry="38" fill="#effafa" stroke="white" strokeWidth="2"/>
        </svg>
      </div>
    </div>
  </section>;
}
