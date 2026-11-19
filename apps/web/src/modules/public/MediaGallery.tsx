import { useEffect, useState } from 'react';
import { apiUrl } from '../../services/api';
import DecorativeAquaWaves from '../../components/DecorativeAquaWaves';
import GalleryMedia from '../gallery/GalleryMedia';
import MediaDialog from '../gallery/MediaDialog';
import { mediaFilters, type MediaItem, type MediaPage } from '../gallery/media';
import '../gallery/gallery.css';
export default function MediaGallery() {
 const [type,setType]=useState(''),[page,setPage]=useState(1),[data,setData]=useState<MediaPage|null>(null),[selected,setSelected]=useState<MediaItem|null>(null),[failed,setFailed]=useState(false);
 useEffect(()=>{const controller=new AbortController();setFailed(false);setData(null);fetch(apiUrl(`/api/clinic/media?type=${type}&page=${page}&pageSize=7`),{signal:controller.signal}).then(r=>{if(!r.ok)throw new Error();return r.json();}).then(d=>{if(!controller.signal.aborted)setData(d);}).catch(()=>{if(!controller.signal.aborted)setFailed(true);});return()=>controller.abort();},[type,page]);
 if(failed||data?.total===0&&type==='')return null;
 return <section className="public-gallery" aria-labelledby="gallery-title"><DecorativeAquaWaves className="gallery-wave" /><div className="gallery-content"><span className="gallery-icon" aria-hidden="true"><svg viewBox="0 0 24 24" width="28" height="28" fill="none" stroke="currentColor" strokeWidth="1.7" aria-hidden="true"><rect x="3" y="4" width="18" height="16" rx="3"/><circle cx="8" cy="9" r="1.5"/><path d="m4 17 5-5 4 4 3-3 4 4"/></svg></span><h2 id="gallery-title">Nuestra <span>Galería</span></h2><p>Conoce nuestras instalaciones, equipo y algunos de nuestros tratamientos.</p>
 <div className="gallery-filters" aria-label="Filtrar galería">{mediaFilters.map(([value,label])=><button key={value} aria-pressed={type===value} onClick={()=>{setType(value);setPage(1);}}>{label}</button>)}</div>
 {!data&&<p role="status">Cargando galería…</p>}{data?.total===0&&<p>No hay contenido para este filtro.</p>}
 <div className="public-media-grid">{data?.items.map((item,index)=><article className={`media-card ${index===0?'media-featured':''}`} key={item.id}>{item.mediaType==='TIKTOK'?<a href={item.externalUrl!} target="_blank" rel="noopener noreferrer" aria-label={`Ver ${item.title||'video'} en TikTok`}><GalleryMedia item={item} previewPaused={!!selected}/></a>:<button className="media-open" onClick={()=>setSelected(item)} aria-label={`Abrir ${item.title||item.altText||'video'}`}><GalleryMedia item={item} previewPaused={!!selected}/></button>}{item.title&&<h3>{item.title}</h3>}</article>)}</div>
 {data&&data.total>7&&<nav className="gallery-pages" aria-label="Navegación de galería"><button aria-label="Página anterior" disabled={page===1} onClick={()=>setPage(p=>p-1)}>←</button><span>Página {page} de {Math.ceil(data.total/7)}</span>{Array.from({length:Math.min(5,Math.ceil(data.total/7))},(_,i)=>{const n=Math.max(1,Math.min(page-2,Math.ceil(data.total/7)-4))+i;return <button key={n} aria-label={`Página ${n}`} aria-current={page===n?'page':undefined} onClick={()=>setPage(n)}>●</button>;})}<button aria-label="Página siguiente" disabled={page*7>=data.total} onClick={()=>setPage(p=>p+1)}>→</button></nav>}
 </div>{selected&&<MediaDialog title={selected.title||'Contenido de la clínica'} onClose={()=>setSelected(null)}><GalleryMedia item={selected} full />{selected.caption&&<p>{selected.caption}</p>}</MediaDialog>}</section>;
}
