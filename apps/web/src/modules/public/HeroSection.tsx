import HeroTooth3D from './HeroTooth3D';
import { sectionHref } from './PublicLayout';

export default function HeroSection() {
  return <section id="inicio" className="public-hero" aria-labelledby="hero-title" tabIndex={-1}><div className="hero-clinic-scene" aria-hidden="true" /><div className="public-container public-hero-grid">
    <div className="public-hero-copy"><p className="public-eyebrow">CLÍNICA DENTAL RAMÍREZ</p>
      <div className="hero-family-badge"><svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 5C5 0 2 6 5 12c2 4 1 9 4 9 2 0 1-7 3-7s1 7 3 7c3 0 2-5 4-9 3-6 0-12-7-7Z"/></svg>Odontología para toda la familia</div>
      <h1 id="hero-title">Cuidamos tu sonrisa <span>en cada etapa</span></h1>
      <p>Atención odontológica profesional, cercana y organizada para ti y tu familia.</p>
      <div className="public-actions"><a className="public-button" href={sectionHref('contacto')}>Reservar cita <span aria-hidden="true">→</span></a><a className="public-button public-button-secondary" href={sectionHref('servicios')}>Conocer servicios <span aria-hidden="true">↗</span></a></div>
      <ul className="hero-care-benefits">
        <li><span aria-hidden="true">✧</span><div>Atención profesional<small>Un equipo para acompañarte</small></div></li>
        <li><span aria-hidden="true">♡</span><div>Cuidado personalizado<small>Escuchamos tus necesidades</small></div></li>
        <li><span aria-hidden="true">✓</span><div>Atención organizada<small>Cuidado en cada etapa</small></div></li>
      </ul>
    </div>
    <HeroTooth3D />
  </div></section>;
}
