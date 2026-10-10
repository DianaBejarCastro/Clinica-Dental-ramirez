import type { ReactNode } from 'react';

export function PublicSection({ id, eyebrow, title, children, className = '' }: {
  id: string; eyebrow: string; title: string; children: ReactNode; className?: string;
}) {
  return <section id={id} className={`public-section ${className}`} aria-labelledby={`${id}-title`} tabIndex={-1}>
    <div className="public-container"><p className="public-eyebrow">{eyebrow}</p><h2 id={`${id}-title`}>{title}</h2>{children}</div>
  </section>;
}
export function PublicCard({ children }: { children: ReactNode }) { return <article className="public-card">{children}</article>; }
