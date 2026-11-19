import { useEffect, useState } from 'react';
import { apiUrl } from '../../services/api';
import './public-whatsapp.css';

export const whatsappMessage = 'Hola, quisiera más información y consultar sobre una cita.';
export function whatsappHref(value?: string | null) {
  if (!value || !/^\+?[1-9][\d\s().-]*$/.test(value.trim())) return null;
  const digits = value.replace(/\D/g, '');
  if (digits.length < 9 || digits.length > 15) return null;
  return `https://wa.me/${digits}?text=${encodeURIComponent(whatsappMessage)}`;
}
export default function PublicWhatsAppButton() {
  const [number, setNumber] = useState<string | null>(null);
  useEffect(() => {
    const controller = new AbortController();
    fetch(apiUrl('/api/clinic/brand'), { signal: controller.signal })
      .then(response => response.ok ? response.json() : null)
      .then(data => { if (!controller.signal.aborted) setNumber(typeof data?.whatsapp === 'string' ? data.whatsapp : null); })
      .catch(() => {});
    return () => controller.abort();
  }, []);
  const href = whatsappHref(number);
  if (!href) return null;
  return <a className="public-whatsapp" href={href} target="_blank" rel="noopener noreferrer" aria-label="Contactar por WhatsApp" title="Contactar por WhatsApp">
    <svg viewBox="0 0 24 24" aria-hidden="true" fill="currentColor"><path d="M20.52 3.48A11.9 11.9 0 0 0 12.05 0C5.47 0 .12 5.35.12 11.93c0 2.1.55 4.16 1.6 5.97L0 24l6.26-1.64a11.9 11.9 0 0 0 5.79 1.48h.01c6.57 0 11.93-5.35 11.94-11.93a11.85 11.85 0 0 0-3.48-8.43ZM12.06 21.83a9.9 9.9 0 0 1-5.04-1.38l-.36-.21-3.72.97.99-3.62-.23-.37a9.87 9.87 0 0 1-1.51-5.29c0-5.47 4.45-9.92 9.87-9.92a9.86 9.86 0 0 1 7.02 2.91 9.85 9.85 0 0 1 2.9 7c-.01 5.47-4.46 9.91-9.92 9.91Zm5.44-7.42c-.3-.15-1.76-.87-2.03-.97-.27-.1-.47-.15-.67.15-.2.3-.77.97-.94 1.17-.17.2-.35.22-.64.07-.3-.15-1.26-.46-2.4-1.48-.89-.79-1.48-1.77-1.65-2.07-.17-.3-.02-.46.13-.61.13-.13.3-.35.45-.52.15-.17.2-.3.3-.5.1-.2.05-.37-.03-.52-.07-.15-.67-1.61-.92-2.21-.24-.58-.49-.5-.67-.51h-.57c-.2 0-.52.07-.79.37-.27.3-1.04 1.02-1.04 2.48s1.07 2.88 1.21 3.08c.15.2 2.1 3.21 5.08 4.5.71.31 1.27.49 1.7.63.71.23 1.36.2 1.87.12.57-.09 1.76-.72 2.01-1.41.25-.7.25-1.29.17-1.42-.07-.12-.27-.2-.57-.35Z" /></svg>
  </a>;
}
