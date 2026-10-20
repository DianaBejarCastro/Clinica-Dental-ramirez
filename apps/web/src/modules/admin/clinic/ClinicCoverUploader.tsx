import { useEffect, useRef, useState } from 'react';
import { api, ApiError, apiUrl } from '../../../services/api';
import { Feedback, useAction, usePermissions } from '../shared';
import CoverCropEditor from './CoverCropEditor';
import { COVER_MAX_BYTES } from './coverCrop';
import './clinic-cover.css';

export interface CoverMetadata { contentType: string; fileName: string | null; width: number; height: number; size: number; focalPointX: number | null; focalPointY: number | null; altText: string | null; updatedAt: string }
const path = '/api/admin/clinic/site-images/hero';
export default function ClinicCoverUploader() {
  const can = usePermissions(), action = useAction(), input = useRef<HTMLInputElement>(null);
  const [current, setCurrent] = useState<CoverMetadata | null>(null), [loading, setLoading] = useState(true);
  const [source, setSource] = useState<File | null>(null), [final, setFinal] = useState<Blob | null>(null);
  const [preview, setPreview] = useState(''), [alt, setAlt] = useState('');
  const [focal, setFocal] = useState({ x: .5, y: .5 });
  const [confirmDelete, setConfirmDelete] = useState(false);
  useEffect(() => { let active = true;
    api<CoverMetadata>(path).then(data => { if (active) { setCurrent(data); setAlt(data.altText ?? ''); setFocal({ x: data.focalPointX ?? .5, y: data.focalPointY ?? .5 }); } })
      .catch(e => { if (active && !(e instanceof ApiError && e.status === 404)) action.setError(e.message); })
      .finally(() => { if (active) setLoading(false); }); return () => { active = false; };
  }, []);
  useEffect(() => { if (!final) { setPreview(''); return; } const url = URL.createObjectURL(final); setPreview(url); return () => URL.revokeObjectURL(url); }, [final]);
  const src = preview || (current ? apiUrl(`/api/clinic/site-images/hero?v=${encodeURIComponent(current.updatedAt)}`) : '');
  const changed = () => window.dispatchEvent(new Event('clinic-cover-changed'));
  return <section className="card clinic-cover" aria-label="Foto de portada del inicio"><h2>Foto de portada del inicio</h2>
    <p className="muted">Formato panorámico 1916 × 821 px (21:9). Puedes mover, ampliar y recortar la imagen antes de guardarla. PNG, JPG o WebP. Máximo 10 MB.</p>
    <Feedback {...action} />{loading && <p role="status">Cargando portada…</p>}
    {src && <><h3>Vista previa de portada</h3><img className="cover-preview" src={src} alt={alt || 'Vista previa de portada'} style={{ objectPosition: `${focal.x * 100}% ${focal.y * 100}%` }} />
      <p>1916 × 821 px · ≈21:9 · {final?.type ?? current?.contentType} · {((final?.size ?? current?.size ?? 0) / 1024).toFixed(1)} KB</p>
      {!final && current?.altText && <p>Texto alternativo: {current.altText}</p>}</>}
    {can('clinic.update') && !loading && <>
      <input ref={input} type="file" hidden aria-label="Archivo de portada" accept="image/png,image/jpeg,image/webp" disabled={action.busy} onChange={event => {
        const file = event.target.files?.[0]; event.target.value = ''; action.setError('');
        if (!file) return;
        if (!['image/png', 'image/jpeg', 'image/webp'].includes(file.type) || !file.size) { action.setError('Selecciona una imagen PNG, JPG o WebP válida.'); return; }
        if (file.size > COVER_MAX_BYTES) { action.setError('La imagen no puede superar 10 MB.'); return; }
        setSource(file);
      }} />
      {final && <div className="cover-settings"><label>Texto alternativo<input maxLength={300} value={alt} disabled={action.busy} onChange={e => setAlt(e.target.value)} /></label>
        <p className="muted">Punto focal para pantallas que recortan la imagen. El centro es el valor inicial.</p>
        <label>Punto focal horizontal<input type="range" min="0" max="1" step="0.01" value={focal.x} disabled={action.busy} onChange={e => setFocal(f => ({ ...f, x: Number(e.target.value) }))} /></label>
        <label>Punto focal vertical<input type="range" min="0" max="1" step="0.01" value={focal.y} disabled={action.busy} onChange={e => setFocal(f => ({ ...f, y: Number(e.target.value) }))} /></label></div>}
      <div className="actions"><button type="button" className="secondary" disabled={action.busy} onClick={() => input.current?.click()}>{current ? 'Cambiar portada' : 'Subir portada'}</button>
        {final && <><button type="button" disabled={action.busy} onClick={() => void action.run(async () => {
          const form = new FormData(); form.append('file', final, `portada.${final.type === 'image/jpeg' ? 'jpg' : final.type.split('/')[1]}`);
          form.append('altText', alt); form.append('focalPointX', String(focal.x)); form.append('focalPointY', String(focal.y));
          setCurrent(await api<CoverMetadata>(path, 'PUT', form)); setFinal(null); changed();
        }, 'Portada guardada.')}>{action.busy ? 'Guardando…' : 'Guardar portada'}</button>
          <button type="button" className="secondary" disabled={action.busy} onClick={() => { setFinal(null); setAlt(current?.altText ?? ''); setFocal({ x: current?.focalPointX ?? .5, y: current?.focalPointY ?? .5 }); }}>Descartar recorte</button></>}
        {current && <button type="button" className="secondary" disabled={action.busy} onClick={() => setConfirmDelete(true)}>Eliminar portada</button>}
      </div>
      {confirmDelete && <div className="cover-delete" role="group" aria-label="Confirmar eliminación de portada"><p>¿Eliminar la portada? El logo y los demás datos se conservarán.</p>
        <button type="button" disabled={action.busy} onClick={() => void action.run(async () => { await api(path, 'DELETE'); setCurrent(null); setFinal(null); setAlt(''); setConfirmDelete(false); changed(); }, 'Portada eliminada.')}>Confirmar eliminación</button>
        <button type="button" className="secondary" disabled={action.busy} onClick={() => setConfirmDelete(false)}>Cancelar eliminación</button></div>}
    </>}
    {source && <CoverCropEditor file={source} onCancel={() => setSource(null)} onApply={blob => { setFinal(blob); setFocal({ x: .5, y: .5 }); setSource(null); }} />}
  </section>;
}
