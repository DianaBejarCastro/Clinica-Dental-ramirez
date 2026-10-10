import { useEffect, useId, useRef, useState, type ChangeEvent } from 'react';
import { api } from '../../../services/api';
import { Feedback, useAction, usePermissions } from '../shared';
import { logoUrl, notifyLogoChanged, type ClinicLogoMetadata } from './logoResource';
import './clinic-logo.css';

export default function ClinicLogoUploader({ logo: initialLogo, onChanged }: {
  logo: ClinicLogoMetadata | null; onChanged: (logo: ClinicLogoMetadata | null) => void;
}) {
  const can = usePermissions(); const action = useAction(); const input = useRef<HTMLInputElement>(null);
  const id = useId(); const [logo, setLogo] = useState(initialLogo); const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState(''); const [imageFailed, setImageFailed] = useState(false);
  useEffect(() => { setLogo(initialLogo); setImageFailed(false); }, [initialLogo]);
  useEffect(() => {
    if (!file) { setPreview(''); return; }
    const url = URL.createObjectURL(file); setPreview(url); setImageFailed(false);
    return () => URL.revokeObjectURL(url);
  }, [file]);
  function clear() { setFile(null); setImageFailed(false); if (input.current) input.current.value = ''; }
  function choose(event: ChangeEvent<HTMLInputElement>) {
    const selected = event.target.files?.[0];
    clear(); action.setError('');
    if (!selected) return;
    if (!['image/png', 'image/jpeg', 'image/webp'].includes(selected.type)) { action.setError('Selecciona una imagen PNG, JPG o WebP.'); return; }
    if (!selected.size) { action.setError('El archivo está vacío.'); return; }
    if (selected.size > 5 * 1024 * 1024) { action.setError('El logo no puede superar 5 MB.'); return; }
    setFile(selected);
  }
  function updated(value: ClinicLogoMetadata | null) { setLogo(value); clear(); onChanged(value); notifyLogoChanged(); }
  const src = preview || (logo ? logoUrl(logo.updatedAt) : '');
  return <section className="card" aria-labelledby={`${id}-title`}>
    <h2 id={`${id}-title`}>Logo de la clínica</h2><Feedback {...action} />
    <div className="clinic-logo-editor">
      <div className="clinic-logo-preview">
        {src && !imageFailed ? <img src={src} alt={file ? 'Vista previa del nuevo logo' : 'Logo actual de la clínica'}
          onError={() => { setImageFailed(true); if (file) action.setError('No se pudo mostrar la imagen seleccionada.'); }} />
          : <span className="muted">{imageFailed ? 'Vista previa no disponible' : 'Sin logo'}</span>}
      </div>
      <div className="clinic-logo-details">
        <p id={`${id}-help`} className="muted">PNG, JPG o WebP. Máximo 5 MB.</p>
        {(file || logo) && <p className="clinic-logo-metadata">{file?.name ?? logo?.fileName}<br />
          <small>{file?.type ?? logo?.contentType} · {((file?.size ?? logo?.size ?? 0) / 1024).toFixed(1)} KB</small></p>}
        {file && <p className="muted">Vista previa. Pulsa Guardar logo para confirmar el cambio.</p>}
        {can('clinic.update') && <>
          <input ref={input} id={id} type="file" accept="image/png,image/jpeg,image/webp" hidden
            aria-label="Archivo del logo" aria-describedby={`${id}-help`} disabled={action.busy} onChange={choose} />
          <div className="actions">
            <button type="button" className="secondary" disabled={action.busy} onClick={() => input.current?.click()}>{logo ? 'Cambiar logo' : 'Subir logo'}</button>
            {file && <><button type="button" disabled={action.busy || imageFailed} onClick={() => {
              if (!file || !can('clinic.update')) return;
              void action.run(async () => { const data = new FormData(); data.append('file', file); updated(await api<ClinicLogoMetadata>('/api/admin/clinic/logo', 'PUT', data)); }, 'Logo guardado.');
            }}>{action.busy ? 'Guardando…' : 'Guardar logo'}</button>
            <button type="button" className="secondary" disabled={action.busy} onClick={clear}>Descartar selección</button></>}
            {logo && <button type="button" className="secondary" disabled={action.busy} onClick={() => {
              if (!can('clinic.update')) return;
              void action.run(async () => { await api('/api/admin/clinic/logo', 'DELETE'); updated(null); }, 'Logo eliminado.');
            }}>Eliminar logo</button>}
          </div>
        </>}
      </div>
    </div>
  </section>;
}
