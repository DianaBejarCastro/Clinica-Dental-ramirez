import { useEffect, useRef, useState, type PointerEvent } from 'react';
import { apiUrl } from '../../services/api';
import { serviceVariants, type PublicClinicService } from '../clinicServices';
import { PublicSection } from './PublicSection';
import './services-section.css';

export function ServicesMarquee({ services }: { services: PublicClinicService[] }) {
  const viewport = useRef<HTMLDivElement>(null), track = useRef<HTMLDivElement>(null), group = useRef<HTMLDivElement>(null);
  const motion = useRef({ x: 0, width: 0, direction: -1, dragging: false, last: 0, anchor: 0, pointer: -1 });
  const [copies, setCopies] = useState(1), [paused, setPaused] = useState(false), [reduced, setReduced] = useState(false);
  const paint = () => {
    const m = motion.current;
    if (m.width > 0) m.x = ((m.x % m.width) + m.width) % m.width - m.width;
    if (track.current) track.current.style.transform = `translate3d(${m.x}px,0,0)`;
  };
  useEffect(() => {
    const query = window.matchMedia('(prefers-reduced-motion: reduce)');
    const update = () => setReduced(query.matches); update(); query.addEventListener('change', update);
    return () => query.removeEventListener('change', update);
  }, []);
  useEffect(() => {
    const measure = () => {
      const view = viewport.current?.clientWidth ?? 0;
      const card = group.current?.firstElementChild?.getBoundingClientRect().width ?? 320;
      setCopies(Math.max(1, Math.ceil(view / ((card + 18) * services.length))));
      motion.current.width = group.current?.getBoundingClientRect().width ?? 0; paint();
    };
    measure();
    const observer = new ResizeObserver(measure);
    if (viewport.current) observer.observe(viewport.current);
    if (group.current) observer.observe(group.current);
    return () => observer.disconnect();
  }, [services.length, copies]);
  useEffect(() => {
    if (paused || reduced) return;
    let frame = 0, previous = 0;
    const tick = (now: number) => {
      const elapsed = previous ? Math.min(now - previous, 50) : 0; previous = now;
      if (!motion.current.dragging) { motion.current.x += motion.current.direction * elapsed * 0.014; paint(); }
      frame = requestAnimationFrame(tick);
    };
    frame = requestAnimationFrame(tick); return () => cancelAnimationFrame(frame);
  }, [paused, reduced]);
  function down(event: PointerEvent<HTMLDivElement>) {
    if (!event.isPrimary || event.button !== 0) return;
    const m = motion.current; m.dragging = true; m.last = event.clientX; m.anchor = event.clientX; m.pointer = event.pointerId;
    event.currentTarget.setPointerCapture(event.pointerId);
  }
  function move(event: PointerEvent<HTMLDivElement>) {
    const m = motion.current; if (!m.dragging || event.pointerId !== m.pointer) return;
    m.x += event.clientX - m.last; m.last = event.clientX;
    const delta = event.clientX - m.anchor;
    if (Math.abs(delta) >= 8) { m.direction = Math.sign(delta); m.anchor = event.clientX; }
    paint();
  }
  function end() { motion.current.dragging = false; motion.current.pointer = -1; }
  return <>
    <div className="services-controls"><span>Arrastra para explorar · Usa las flechas del teclado</span>
      {!reduced && <button type="button" className="secondary" onClick={() => setPaused(value => !value)}>{paused ? 'Reanudar movimiento' : 'Pausar movimiento'}</button>}
    </div>
    <div className="services-viewport" ref={viewport} tabIndex={0} role="region" aria-label="Servicios de la clínica. Arrastra o usa las flechas para explorar."
      onPointerDown={down} onPointerMove={move} onPointerUp={end} onPointerCancel={end} onLostPointerCapture={end}
      onKeyDown={event => { if (event.key === 'ArrowLeft' || event.key === 'ArrowRight') { event.preventDefault(); motion.current.direction = event.key === 'ArrowRight' ? 1 : -1; motion.current.x += motion.current.direction * 80; paint(); } }}>
      <div className="services-track" ref={track}>{[0, 1, 2].map(loop => <div className="services-group" ref={loop === 0 ? group : undefined} key={loop} aria-hidden={loop !== 0 ? true : undefined}>
        {Array.from({ length: copies }, (_, repeat) => services.map((service, index) => <article key={`${repeat}-${service.id}`} aria-hidden={repeat > 0 ? true : undefined}
          className={`service-tile service-tile--${Object.hasOwn(serviceVariants, service.visualVariant) ? service.visualVariant : 'TEAL'}`}>
          <span className="service-tile-number" aria-hidden="true">{String(index + 1).padStart(2, '0')}</span>
          <h3>{service.name}</h3>{service.shortDescription && <p>{service.shortDescription}</p>}
        </article>))}
      </div>)}</div>
    </div>
  </>;
}
export default function ServicesSection() {
  const [services, setServices] = useState<PublicClinicService[]>([]);
  useEffect(() => {
    const controller = new AbortController();
    fetch(apiUrl('/api/clinic/services'), { signal: controller.signal })
      .then(response => response.ok ? response.json() : []).then(data => { if (!controller.signal.aborted && Array.isArray(data)) setServices(data); }).catch(() => undefined);
    return () => controller.abort();
  }, []);
  if (!services.length) return null;
  return <PublicSection id="servicios" eyebrow="CUIDADO ODONTOLÓGICO" title="Nuestros servicios">
    <p className="public-intro">Conoce las áreas de atención y consulta con la clínica la disponibilidad para tu caso.</p>
    <ServicesMarquee services={services} />
  </PublicSection>;
}
