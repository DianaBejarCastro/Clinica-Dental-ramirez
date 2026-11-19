import { useEffect, useState, type FormEvent } from 'react';
import { api } from '../../../services/api';
import { usePermissions } from '../shared';
import GalleryMedia from '../../gallery/GalleryMedia';
import MediaDialog from '../../gallery/MediaDialog';
import { mediaFilters, type MediaItem, type MediaPage } from '../../gallery/media';
import '../../gallery/gallery.css';
export default function ClinicGallery() {
 const can = usePermissions(), writable = can('clinic.update');
 const [type, setType] = useState(''), [page, setPage] = useState(1), [version, setVersion] = useState(0);
 const [data, setData] = useState<MediaPage | null>(null), [error, setError] = useState(''), [busy, setBusy] = useState(false);
 const [editing, setEditing] = useState<MediaItem | 'new' | null>(null), [kind, setKind] = useState('IMAGE'), [deleting, setDeleting] = useState<MediaItem | null>(null);
 useEffect(() => { let live = true; setData(null); api<MediaPage>(`/api/admin/clinic/media?type=${type}&page=${page}&pageSize=8`).then(d => { if(live) setData(d); }).catch(e => { if(live) setError(e.message); }); return () => {live=false;}; }, [type,page,version]);
 async function run(work: () => Promise<unknown>) { setBusy(true); setError(''); try { await work(); setVersion(v=>v+1); return true; } catch(e) {setError((e as Error).message);return false;} finally {setBusy(false);} }
 async function save(e: FormEvent<HTMLFormElement>) {
  e.preventDefault(); const form = new FormData(e.currentTarget); const metadata = { title: String(form.get('title')||''), caption: String(form.get('caption')||''), altText: String(form.get('altText')||''), sortOrder: Number(form.get('sortOrder')), isActive: form.get('isActive') === 'on' };
  if(editing === 'new') {
   const file = form.get('file') as File | null;
   if(kind !== 'TIKTOK' && (!file?.size || file.size > (kind === 'IMAGE' ? 10 : 50)*1024*1024)) {setError('Selecciona un archivo válido: fotos hasta 10 MB; videos hasta 50 MB.');return;}
   if(kind === 'TIKTOK' && !/^https:\/\/(www\.)?tiktok\.com\/@[\w.]+\/video\/\d{10,25}(?:[/?#].*)?$/.test(String(form.get('externalUrl')))) {setError('Introduce el enlace completo HTTPS de TikTok.');return;}
   form.set('mediaType',kind);form.set('isActive',String(metadata.isActive));
   if(await run(()=>api('/api/admin/clinic/media','POST',form)))setEditing(null);
  } else if(editing && await run(()=>api(`/api/admin/clinic/media/${editing.id}`,'PUT',metadata)))setEditing(null);
 }
 return <section className="card clinic-gallery" aria-label="Galería Multimedia"><header className="gallery-heading"><span className="gallery-icon" aria-hidden="true"><svg viewBox="0 0 24 24" width="28" height="28" fill="none" stroke="currentColor" strokeWidth="1.7" aria-hidden="true"><rect x="3" y="4" width="18" height="16" rx="3"/><circle cx="8" cy="9" r="1.5"/><path d="m4 17 5-5 4 4 3-3 4 4"/></svg></span><div><h2>Galería Multimedia</h2><p>Fotos, videos y contenido de nuestras redes sociales.</p></div></header>
 <div className="gallery-toolbar"><div className="gallery-filters" aria-label="Filtrar contenido">{[...mediaFilters.slice(1),['','Todos']].map(([value,label])=><button key={value} type="button" aria-pressed={type===value} onClick={()=>{setType(value);setPage(1);}}>{label}</button>)}</div>{writable&&<button className="gallery-primary" onClick={()=>{setEditing('new');setKind('IMAGE');setError('');}}>↑ Subir contenido</button>}</div>
 {error&&!editing&&<p role="alert">{error}</p>}{!data&&!error&&<p role="status">Cargando galería…</p>}
 <div className="admin-media-grid">{data?.items.map(item=><article key={item.id} className="media-card"><GalleryMedia item={item} admin /><div className="media-card-body"><h3>{item.title||item.altText||item.mediaType}</h3><p>{item.isActive?'Visible':'Oculto'} · Orden {item.sortOrder}</p>{writable&&<div className="media-actions"><button onClick={()=>{setEditing(item);setKind(item.mediaType);setError('');}}>Editar / ordenar</button><button disabled={busy} onClick={()=>void run(()=>api(`/api/admin/clinic/media/${item.id}`,'PUT',{...item,isActive:!item.isActive}))}>{item.isActive?'Ocultar':'Mostrar'}</button><button onClick={()=>setDeleting(item)}>Eliminar</button></div>}</div></article>)}</div>
 {data?.total===0&&<p>No hay contenido para este filtro.</p>}
 {data&&data.total>8&&<nav className="gallery-pages" aria-label="Páginas de galería"><button disabled={page===1} onClick={()=>setPage(p=>p-1)}>← Anterior</button><span>{page} / {Math.ceil(data.total/8)}</span><button disabled={page*8>=data.total} onClick={()=>setPage(p=>p+1)}>Siguiente →</button></nav>}
 {editing&&<MediaDialog title={editing==='new'?'Subir contenido':'Editar contenido'} onClose={()=>!busy&&setEditing(null)}><form onSubmit={save} className="media-editor">{error&&<p role="alert">{error}</p>}{editing==='new'&&<label>Tipo de contenido<select value={kind} onChange={e=>setKind(e.target.value)}><option value="IMAGE">Foto</option><option value="VIDEO_UPLOAD">Video</option><option value="TIKTOK">TikTok</option></select></label>}
 {editing==='new'&&(kind==='TIKTOK'?<label>Enlace de TikTok<input name="externalUrl" type="url" required /></label>:<label>Archivo<input key={kind} name="file" type="file" accept={kind==='IMAGE'?'image/png,image/jpeg,image/webp':'video/mp4'} required /></label>)}
 {kind==='VIDEO_UPLOAD'&&editing==='new'&&!data?.videoUploadAvailable&&<p role="alert">La carga de videos requiere configurar almacenamiento durable. No se guardar?n en el disco del servidor.</p>}
 <label>Título<input name="title" maxLength={200} defaultValue={editing==='new'?'':editing.title||''} /></label><label>Descripción<textarea name="caption" maxLength={1000} defaultValue={editing==='new'?'':editing.caption||''} /></label>
 {kind==='IMAGE'&&<label>Texto alternativo<input name="altText" required maxLength={300} defaultValue={editing==='new'?'':editing.altText||''} /></label>}
 <label>Orden<input name="sortOrder" type="number" min="0" max="100000" required defaultValue={editing==='new'?(data?.total||0):editing.sortOrder} /></label><label><input name="isActive" type="checkbox" defaultChecked={editing==='new'||editing.isActive} /> Visible</label><button className="gallery-primary" disabled={busy||(kind==='VIDEO_UPLOAD'&&editing==='new'&&!data?.videoUploadAvailable)}>{busy?'Guardando…':'Guardar contenido'}</button></form></MediaDialog>}
 {deleting&&<MediaDialog title="Eliminar contenido" onClose={()=>!busy&&setDeleting(null)}>{error&&<p role="alert">{error}</p>}<p>¿Eliminar este contenido de la galería… Esta acción no se puede deshacer.</p><button disabled={busy} onClick={async()=>{if(await run(()=>api(`/api/admin/clinic/media/${deleting.id}`,'DELETE')))setDeleting(null);}}>Confirmar eliminación</button><button onClick={()=>setDeleting(null)}>Cancelar</button></MediaDialog>}
 </section>;
}
