import HeroTooth3D from './HeroTooth3D';
import { sectionHref } from './PublicLayout';

export default function HeroSection() {
  return <section id="inicio" className="public-hero" aria-labelledby="hero-title" tabIndex={-1}><div className="public-container public-hero-grid">
    <div className="public-hero-copy"><p className="public-eyebrow">CLÍNICA DENTAL RAMÍREZ</p>
      <h1 id="hero-title">Cuidamos tu sonrisa <span>en cada etapa</span></h1>
      <p>Atención odontológica profesional, cercana y organizada para ti y tu familia.</p>
      <div className="public-actions"><a className="public-button" href={sectionHref('contacto')}>Reservar cita</a><a className="public-button public-button-secondary" href={sectionHref('servicios')}>Conocer servicios <span aria-hidden="true">↗</span></a></div>
      <p className="public-hero-note">Cercanía en el trato. Cuidado en cada detalle.</p>
    </div>
    <HeroTooth3D />
  </div></section>;
}
