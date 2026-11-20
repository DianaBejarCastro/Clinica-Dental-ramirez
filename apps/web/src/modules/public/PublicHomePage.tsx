import { useEffect, useState } from 'react';
import { apiUrl } from '../../services/api';
import PublicLayout from './PublicLayout';
import MediaGallery from './MediaGallery';
import HeroSection from './HeroSection';
import ServicesSection from './ServicesSection';
import TeamSection from './TeamSection';
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
    <BranchesSection branches={branches} />
  </PublicLayout>;
}
