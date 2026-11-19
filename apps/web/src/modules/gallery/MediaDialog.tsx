import { useEffect, useRef, type ReactNode } from 'react';
export default function MediaDialog({ title, onClose, children }: { title: string; onClose: () => void; children: ReactNode }) {
 const ref = useRef<HTMLDialogElement>(null);
 useEffect(() => { const previous = document.activeElement as HTMLElement | null; ref.current?.showModal?.(); return () => previous?.focus(); }, []);
 return <dialog ref={ref} className="media-dialog" aria-label={title} onCancel={e => { e.preventDefault(); onClose(); }}><header><h2>{title}</h2><button type="button" onClick={onClose} aria-label="Cerrar">×</button></header>{children}</dialog>;
}
