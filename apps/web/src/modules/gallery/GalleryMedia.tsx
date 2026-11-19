import { useEffect, useState } from 'react';
import { apiUrl, getSession } from '../../services/api';
import GalleryVideo from './GalleryVideo';
import type { MediaItem } from './media';
export default function GalleryMedia({ item, admin = false, full = false, previewPaused = false }: { item: MediaItem; admin?: boolean; full?: boolean; previewPaused?: boolean }) {
 const path = `/api/${admin ? 'admin/' : ''}clinic/media/${item.id}/content${full ? '' : '?thumbnail=true'}`;
 const [preview, setPreview] = useState('');
 const [failed, setFailed] = useState(false);
 useEffect(() => {
  if (!admin || item.mediaType !== 'IMAGE') return;
  const controller = new AbortController(); let url = '';
  fetch(apiUrl(path), { signal: controller.signal, headers: { Authorization: `Bearer ${getSession()?.accessToken ?? ''}` } })
   .then(r => { if (!r.ok) throw new Error(); return r.blob(); }).then(blob => { if (!controller.signal.aborted) { url = URL.createObjectURL(blob); setPreview(url); } }).catch(() => {});
  return () => { controller.abort(); if (url) URL.revokeObjectURL(url); };
 }, [path, admin, item.mediaType]);
 if (item.mediaType === 'IMAGE') return (!admin || preview) && !failed ? <img onError={() => setFailed(true)} src={admin ? preview : apiUrl(path)} alt={item.altText || item.title || 'Imagen de la clínica'} width={item.width || 640} height={item.height || 400} loading="lazy" /> : <span className="media-placeholder"><span aria-hidden="true">▧</span>{item.altText || item.title || 'Foto de la clínica'}</span>;
 if (item.mediaType === 'VIDEO_UPLOAD' && !admin) return <GalleryVideo item={item} full={full} paused={previewPaused} />;
 if (item.mediaType === 'TIKTOK' && !admin) return <span className="media-tiktok-preview"><span className="media-tiktok-badge">TikTok</span><span className="media-play-icon" aria-hidden="true">&#9654;</span><strong>{item.title || 'Video en TikTok'}</strong><small>Ver en TikTok &#8599;</small></span>;
 return <span className="media-placeholder"><span aria-hidden="true">&#9655;</span>{item.mediaType === 'TIKTOK' ? 'TikTok' : 'Video'}<small>{item.title || 'Ver contenido'}</small></span>;
}
