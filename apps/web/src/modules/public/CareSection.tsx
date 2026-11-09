import { useEffect, useState } from 'react';
import { apiUrl } from '../../services/api';
import { sectionHref } from './PublicLayout';
import './care-section.css';

interface Metadata { focalPointX: number | null; focalPointY: number | null; altText: string; updatedAt: string }
const benefits = [
  { title: 'Atención organizada', description: 'Un equipo que coordina tu atención de forma clara y cercana.', icon: 'organized' },
  { title: 'Profesionales por especialidad', description: 'Profesionales en distintas áreas de la odontología para acompañar tu cuidado.', icon: 'team' },
  { title: 'Agenda planificada', description: 'Coordinamos tus citas para organizar cada paso de tu atención.', icon: 'calendar' },
  { title: 'Seguimiento del paciente', description: 'Te acompañamos durante tu tratamiento, con control y orientación personalizada.', icon: 'tooth' },
];
function CareIcon({ kind }: { kind: string }) {
  return <svg viewBox="0 0 48 48" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
    {kind === 'team' ? <><circle cx="24" cy="13" r="6" /><circle cx="9" cy="20" r="4" /><circle cx="39" cy="20" r="4" /><path d="M14 40v-6a10 10 0 0 1 20 0v6" fill="#b7f3eb" /><path d="M2 39v-5a7 7 0 0 1 11-6M46 39v-5a7 7 0 0 0-11-6" /></>
      : kind === 'tooth' ? <><path d="M24 9C13 2 4 9 7 21c2 7 4 20 8 20 4 0 3-13 9-13s5 13 9 13c4 0 6-13 8-20C44 9 35 2 24 9Z" /><circle cx="35" cy="34" r="10" fill="#31e4d3" /><path d="m30 34 3 3 6-6" /></>
      : <><rect x="7" y="9" width="33" height="31" rx="4" /><path d="M15 5v9M32 5v9M7 20h33" />{kind === 'organized' ? <><circle cx="35" cy="34" r="10" fill="#31e4d3" /><path d="m30 34 3 3 6-6" /></> : <path d="M15 27h3m6 0h3m6 0h1M15 33h3m6 0h3" />}</>}
  </svg>;
}
export default function CareSection() {
  const [meta, setMeta] = useState<Metadata | null>(null);
  useEffect(() => {
    let controller: AbortController | null = null;
    function refresh() {
      controller?.abort(); const request = new AbortController(); controller = request;
      void fetch(apiUrl('/api/clinic/site-images/care/meta'), { signal: request.signal, cache: 'no-store' })
        .then(async response => response.ok ? await response.json() as Metadata : null)
        .then(value => { if (!request.signal.aborted) setMeta(value); })
        .catch(() => { if (!request.signal.aborted) setMeta(null); });
    }
    refresh(); window.addEventListener('focus', refresh); window.addEventListener('clinic-care-changed', refresh);
    return () => { controller?.abort(); window.removeEventListener('focus', refresh); window.removeEventListener('clinic-care-changed', refresh); };
  }, []);
  return <section id="nosotros" className="care-section" aria-labelledby="nosotros-title" tabIndex={-1}>
    <div className="care-layout public-container">
      <div className="care-visual">
        {meta ? <img key={meta.updatedAt} src={apiUrl(`/api/clinic/site-images/care?v=${encodeURIComponent(meta.updatedAt)}`)}
          width={1200} height={1350} alt={meta.altText} loading="lazy" decoding="async" onError={() => setMeta(null)}
          style={{ objectPosition: `${(meta.focalPointX ?? .5) * 100}% ${(meta.focalPointY ?? .5) * 100}%` }} />
          : <div className="care-placeholder"><CareIcon kind="tooth" /><span>Tu sonrisa,<br /><strong>nuestro compromiso.</strong></span><p>Cuidado cercano en cada etapa.</p></div>}
      </div>
      <div className="care-copy">
        <p className="public-eyebrow">CERCA DE TI</p>
        <h2 id="nosotros-title">Una atención pensada en las personas</h2>
        <p className="care-intro">En Clínica Dental RAMÍREZ, el cuidado de tu sonrisa empieza por escucharte y organizar cada paso de tu atención.</p>
        <ul className="care-benefits">{benefits.map(benefit => <li key={benefit.icon}><span className="care-icon"><CareIcon kind={benefit.icon} /></span><div><h3>{benefit.title}</h3><p>{benefit.description}</p></div></li>)}</ul>
        <a className="public-button care-cta" href={sectionHref('contacto')}>Agendar consulta <span aria-hidden="true">→</span></a>
      </div>
    </div>
  </section>;
}
