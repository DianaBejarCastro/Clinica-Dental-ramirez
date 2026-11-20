import { useEffect, useState } from 'react';
import { apiUrl } from '../../services/api';
import PublicLayout, { sectionHref } from './PublicLayout';
import MediaGallery from './MediaGallery';
import HeroSection from './HeroSection';
import ServicesSection from './ServicesSection';
import TeamSection from './TeamSection';
import ContactSection from './ContactSection';
import CareSection from './CareSection';
import BranchesSection from './BranchesSection';
import type { PublicBranch } from './BranchesSection';
import './public.css';
import './public-header.css';
import './public-hero.css';

export default function PublicHomePage({ section }: { section: string | null }) {
  const [branches, setBranches] = useState<PublicBranch[] | null>(null);
  useEffect(() => {
    const controller = new AbortController();
    fetch(apiUrl('/api/clinic/branches'), { signal: controller.signal }).then(r => r.ok ? r.json() : [])
      .then(data => { if (!controller.signal.aborted) setBranches(Array.isArray(data) ? data : []); })
      .catch(() => { if (!controller.signal.aborted) setBranches([]); });
    return () => controller.abort();
  }, []);
  return <PublicLayout section={section}><HeroSection />
    <ServicesSection />
    <CareSection /><TeamSection branches={branches} /><MediaGallery />
    <section className="public-cta"><div className="public-container"><div><p className="public-eyebrow">EL SIGUIENTE PASO</p><h2>Agenda tu próxima consulta</h2><p>Conversemos sobre el cuidado de tu sonrisa.</p></div><a className="public-button" href={sectionHref('contacto')}>Reservar cita</a></div></section>
    <BranchesSection branches={branches} /><ContactSection />
  </PublicLayout>;
}
