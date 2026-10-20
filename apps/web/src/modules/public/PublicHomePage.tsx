import PublicLayout, { sectionHref } from './PublicLayout';
import ClinicHeroBanner from './ClinicHeroBanner';
import HeroSection from './HeroSection';
import ServicesSection from './ServicesSection';
import TeamSection from './TeamSection';
import ContactSection from './ContactSection';
import { PublicCard, PublicSection } from './PublicSection';
import './public.css';

export default function PublicHomePage({ section }: { section: string | null }) {
  return <PublicLayout section={section}><ClinicHeroBanner /><HeroSection />
    <PublicSection id="nosotros" eyebrow="CERCA DE TI" title="Una atención pensada en las personas">
      <p className="public-intro">En Clínica Dental RAMÍREZ, el cuidado de tu sonrisa empieza por escucharte y organizar cada paso de tu atención.</p>
      <div className="public-benefits">{['Atención organizada', 'Profesionales por especialidad', 'Agenda planificada', 'Seguimiento del paciente'].map(label => <PublicCard key={label}><span className="public-benefit-mark" aria-hidden="true" /><h3>{label}</h3></PublicCard>)}</div>
    </PublicSection>
    <ServicesSection /><TeamSection />
    <section className="public-cta"><div className="public-container"><div><p className="public-eyebrow">EL SIGUIENTE PASO</p><h2>Agenda tu próxima consulta</h2><p>Conversemos sobre el cuidado de tu sonrisa.</p></div><a className="public-button" href={sectionHref('contacto')}>Reservar cita</a></div></section>
    <ContactSection />
  </PublicLayout>;
}
