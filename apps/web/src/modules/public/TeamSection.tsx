import { PublicCard, PublicSection } from './PublicSection';
export interface PublicDoctor { name: string; specialty: string }
// Solo información aprobada para publicación; nunca consultar el catálogo administrativo.
export default function TeamSection({ doctors = [] }: { doctors?: PublicDoctor[] }) {
  return <PublicSection id="equipo" eyebrow="PERSONAS QUE CUIDAN" title="Nuestro equipo" className="public-tinted">
    {doctors.length ? <div className="public-services">{doctors.map(doctor => <PublicCard key={`${doctor.name}-${doctor.specialty}`}><h3>{doctor.name}</h3><p>{doctor.specialty}</p></PublicCard>)}</div>
      : <div className="public-team-empty"><span className="public-team-symbol" aria-hidden="true">+</span><div><h3>Conoce a quienes te acompañan</h3><p>Próximamente podrás conocer aquí los perfiles de nuestro equipo. Consulta con la clínica qué profesional puede atenderte.</p></div></div>}
  </PublicSection>;
}
