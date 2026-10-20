import ProtectedDeleteButton from '../deletion/ProtectedDeleteButton';
import { useEffect, useId, useState, type FormEvent } from 'react';
import { api, apiUrl } from '../../../services/api';
import { Feedback, useAction, usePermissions } from '../shared';
import './social-networks.css';

export interface SocialNetwork {
  id: string; name: string; url: string; hasLogo: boolean; logoContentType: string | null;
  logoFileName: string | null; logoSize: number | null; displayOrder: number; isActive: boolean;
  createdAt: string; updatedAt: string;
}
const root = '/api/admin/clinic/social-networks';
const logoUrl = (item: SocialNetwork) => apiUrl(`/api/clinic/social-networks/${item.id}/logo?v=${encodeURIComponent(item.updatedAt)}`);

function SocialLogoEditor({ item, onSaved }: { item: SocialNetwork; onSaved: (item: SocialNetwork) => void }) {
  const [file, setFile] = useState<File | null>(null); const [preview, setPreview] = useState(''); const action = useAction();
  useEffect(() => {
    if (!file) { setPreview(''); return; }
    const url = URL.createObjectURL(file); setPreview(url); return () => URL.revokeObjectURL(url);
  }, [file]);
  return <div className="social-logo-editor"><h3>Logo de {item.name}</h3><Feedback {...action} />
    <div className="clinic-logo-preview">{preview || item.hasLogo ? <img src={preview || logoUrl(item)} alt={preview ? 'Vista previa del logo de red social' : `Logo de ${item.name}`} /> : <span>Sin logo</span>}</div>
    <p className="muted">PNG, JPG o WebP. Máximo 2 MB.</p>
    <label>{item.hasLogo ? 'Cambiar logo' : 'Subir logo'}<input type="file" accept="image/png,image/jpeg,image/webp" disabled={action.busy} onChange={event => {
      const chosen = event.target.files?.[0]; event.target.value = ''; setFile(null); action.setError('');
      if (!chosen) return;
      if (!['image/png', 'image/jpeg', 'image/webp'].includes(chosen.type)) { action.setError('Selecciona PNG, JPG o WebP.'); return; }
      if (!chosen.size || chosen.size > 2 * 1024 * 1024) { action.setError('El archivo debe contener una imagen de máximo 2 MB.'); return; }
      setFile(chosen);
    }} /></label>
    {file && <p>{file.name} · {file.type} · {(file.size / 1024).toFixed(1)} KB</p>}
    <div className="actions">{file && <><button type="button" disabled={action.busy} onClick={() => void action.run(async () => {
      const form = new FormData(); form.append('file', file); onSaved(await api<SocialNetwork>(`${root}/${item.id}/logo`, 'PUT', form)); setFile(null);
    }, 'Logo guardado.')}>Guardar logo de red social</button><button type="button" className="secondary" disabled={action.busy} onClick={() => setFile(null)}>Descartar imagen</button></>}
    {item.hasLogo && <button type="button" className="secondary" disabled={action.busy} onClick={() => void action.run(async () => {
      await api(`${root}/${item.id}/logo`, 'DELETE'); onSaved(await api<SocialNetwork>(`${root}/${item.id}`)); setFile(null);
    }, 'Logo eliminado.')}>Eliminar logo de red social</button>}</div>
  </div>;
}

export default function ClinicSocialNetworks() {
  const [items, setItems] = useState<SocialNetwork[]>([]); const [loading, setLoading] = useState(true);
  const [editor, setEditor] = useState<SocialNetwork | 'new' | null>(null); const action = useAction(); const can = usePermissions(); const title = useId();
  useEffect(() => { let live = true; api<SocialNetwork[]>(root).then(data => { if (live) setItems(data); }).catch(e => { if (live) action.setError(e.message); }).finally(() => { if (live) setLoading(false); }); return () => { live = false; }; }, []);
  const ordered = [...items].sort((a, b) => a.displayOrder - b.displayOrder || a.name.localeCompare(b.name));
  function saved(value: SocialNetwork) { setItems(current => [...current.filter(x => x.id !== value.id), value]); setEditor(value); }
  function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (!can('clinic.update')) return;
    const form = new FormData(event.currentTarget); const url = String(form.get('url')).trim();
    try { const parsed = new URL(url); if (!['http:', 'https:'].includes(parsed.protocol) || parsed.username || parsed.password || /\s/.test(url)) throw new Error(); }
    catch { action.setError('Utiliza una URL http:// o https:// válida.'); return; }
    const body = { name: String(form.get('name')).trim(), url, displayOrder: Number(form.get('displayOrder')), isActive: form.get('isActive') === 'on' };
    void action.run(async () => saved(await api<SocialNetwork>(editor === 'new' ? root : `${root}/${editor!.id}`, editor === 'new' ? 'POST' : 'PUT', body)), 'Red social guardada.');
  }
  return <section className="card" aria-labelledby={title}><div className="page-title"><h2 id={title}>Redes sociales</h2>
    {can('clinic.update') && <button type="button" disabled={action.busy || loading} onClick={() => setEditor('new')}>+ Agregar red social</button>}</div>
    <Feedback {...action} />{loading ? <p role="status">Cargando redes sociales…</p> : <>
      {!items.length && !action.error && <p className="muted">Todavía no hay redes sociales registradas.</p>}
      <ul className="social-network-list">{ordered.map(item => <li key={item.id}>
        <div className="social-network-logo">{item.hasLogo ? <img src={logoUrl(item)} alt={`Logo de ${item.name}`} /> : <span aria-hidden="true">{item.name.slice(0, 1)}</span>}</div>
        <div className="social-network-info"><strong>{item.name}</strong><a href={item.url} target="_blank" rel="noopener noreferrer">{item.url}</a><span className={`badge ${item.isActive ? 'success' : ''}`}>{item.isActive ? 'Activa' : 'Inactiva'}</span></div>
        {can('clinic.update') && <div className="actions"><button className="secondary" type="button" disabled={action.busy} onClick={() => setEditor(item)} aria-label={`Editar ${item.name}`}>Editar</button>
          <button className="secondary" type="button" disabled={action.busy} aria-label={`${item.isActive ? 'Desactivar' : 'Activar'} ${item.name}`} onClick={() => void action.run(async () => {
            const value = await api<SocialNetwork>(`${root}/${item.id}/status`, 'PATCH', { isActive: !item.isActive });
            setItems(current => current.map(x => x.id === value.id ? value : x)); if (editor !== 'new' && editor?.id === value.id) setEditor(value);
          }, 'Estado actualizado.')}>{item.isActive ? 'Desactivar' : 'Activar'}</button></div>}
        <ProtectedDeleteButton entity="social-networks" id={item.id} label={item.name} onDeleted={() => { setItems(current => current.filter(x => x.id !== item.id)); setEditor(null); }} />
      </li>)}</ul>
    </>}
    {editor && can('clinic.update') && <div className="social-network-editor">
      <h3>{editor === 'new' ? 'Nueva red social' : `Editar ${editor.name}`}</h3>
      <form key={editor === 'new' ? 'new' : `${editor.id}-${editor.updatedAt}`} className="editor" onSubmit={save}>
        <div className="fields"><label>Nombre de la red social<input name="name" required maxLength={100} defaultValue={editor === 'new' ? '' : editor.name} disabled={action.busy} /></label>
        <label>URL<input name="url" type="url" required maxLength={1000} defaultValue={editor === 'new' ? '' : editor.url} disabled={action.busy} /></label>
        <label>Orden de visualización<input name="displayOrder" type="number" min={0} step={1} required defaultValue={editor === 'new' ? 0 : editor.displayOrder} disabled={action.busy} /></label>
        <label><input name="isActive" type="checkbox" defaultChecked={editor === 'new' || editor.isActive} disabled={action.busy} />Activa</label></div>
        <div className="actions"><button disabled={action.busy}>Guardar red social</button><button type="button" className="secondary" disabled={action.busy} onClick={() => setEditor(null)}>Cerrar edición</button></div>
      </form>
      {editor === 'new' ? <p className="muted">Guarda los datos básicos para agregar un logo opcional.</p> : <SocialLogoEditor key={editor.id} item={editor} onSaved={saved} />}
    </div>}
  </section>;
}
