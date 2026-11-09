import PublicLayout, { sectionHref } from './PublicLayout';
import ClinicHeroBanner from './ClinicHeroBanner';
import HeroSection from './HeroSection';
import ServicesSection from './ServicesSection';
import TeamSection from './TeamSection';
import ContactSection from './ContactSection';
import CareSection from './CareSection';
import BranchesSection from './BranchesSection';
import './public.css';

export default function PublicHomePage({ section }: { section: string | null }) {
  return <PublicLayout section={section}><HeroSection />
    <CareSection />
    <ServicesSection /><TeamSection /><ClinicHeroBanner />
    <section className="public-cta"><div className="public-container"><div><p className="public-eyebrow">EL SIGUIENTE PASO</p><h2>Agenda tu próxima consulta</h2><p>Conversemos sobre el cuidado de tu sonrisa.</p></div><a className="public-button" href={sectionHref('contacto')}>Reservar cita</a></div></section>
    <BranchesSection /><ContactSection />
  </PublicLayout>;
}
