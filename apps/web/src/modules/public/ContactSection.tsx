import { PublicSection } from './PublicSection';
export interface PublicContact { phone?: string; whatsapp?: string; email?: string; address?: string }
export default function ContactSection({ contact = {} }: { contact?: PublicContact }) {
  const hasContact = Object.values(contact).some(Boolean);
  const whatsapp = contact.whatsapp?.replace(/\D/g, '');
  return <PublicSection id="contacto" eyebrow="ESTAMOS PARA ORIENTARTE" title="Contacto">
    <p className="public-intro">Para coordinar una consulta, confirma con la clínica el profesional, la sucursal y el horario de atención.</p>
    {hasContact ? <address className="public-contact-list">
      {contact.phone && <a href={`tel:${contact.phone.replace(/[^+\d]/g, '')}`}>Teléfono: {contact.phone}</a>}
      {whatsapp && <a href={`https://wa.me/${whatsapp}`} target="_blank" rel="noreferrer">WhatsApp</a>}
      {contact.email && <a href={`mailto:${contact.email}`}>{contact.email}</a>}
      {contact.address && <p>{contact.address}</p>}
    </address> : <p className="public-contact-note">Los canales de contacto se publicarán aquí próximamente. La reserva en línea aún no está habilitada.</p>}
  </PublicSection>;
}
