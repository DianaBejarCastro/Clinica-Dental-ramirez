import { PublicCard, PublicSection } from './PublicSection';
const services = ['Odontología general', 'Ortodoncia', 'Endodoncia', 'Periodoncia', 'Odontopediatría', 'Implantología', 'Estética dental'];
export default function ServicesSection() {
  return <PublicSection id="servicios" eyebrow="CUIDADO ODONTOLÓGICO" title="Nuestros servicios">
    <p className="public-intro">Conoce las áreas de atención y consulta con la clínica la disponibilidad para tu caso.</p>
    <div className="public-services">{services.map((name, index) => <PublicCard key={name}><span className="public-service-number" aria-hidden="true">{String(index + 1).padStart(2, '0')}</span><h3>{name}</h3></PublicCard>)}</div>
  </PublicSection>;
}
