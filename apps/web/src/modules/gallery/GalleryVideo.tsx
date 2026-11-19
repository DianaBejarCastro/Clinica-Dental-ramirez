import { useEffect, useRef, useState } from 'react';
import { apiUrl } from '../../services/api';
import type { MediaItem } from './media';

// A bounded pool prevents a page of videos from decoding simultaneously.
const previews = new Map<HTMLVideoElement, boolean>();
function syncPreviews() {
 let playing = 0;
 previews.forEach((eligible, video) => {
  const play = eligible && playing++ < 2;
  video.muted = true;
  video.autoplay = play;
  if (play) void video.play()?.catch(() => {});
  else video.pause();
 });
}

export default function GalleryVideo({ item, full, paused }: { item: MediaItem; full: boolean; paused: boolean }) {
 const ref = useRef<HTMLVideoElement>(null);
 const [visible, setVisible] = useState(false);
 const [reduced, setReduced] = useState(() => window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? true);
 const [failed, setFailed] = useState(false);
 useEffect(() => {
  const query = window.matchMedia?.('(prefers-reduced-motion: reduce)');
  const update = () => setReduced(query?.matches ?? true);
  query?.addEventListener('change', update);
  const video = ref.current;
  const observer = !full && video && typeof IntersectionObserver !== 'undefined' ? new IntersectionObserver(entries => setVisible(entries[0].isIntersecting), { threshold: .25 }) : null;
  if (video) observer?.observe(video);
  return () => { observer?.disconnect(); query?.removeEventListener('change', update); };
 }, [full]);
 useEffect(() => {
  const video = ref.current;
  if (!video) return;
  if (!full) { previews.set(video, visible && !reduced && !paused && !failed); syncPreviews(); }
  return () => { previews.delete(video); video.pause(); syncPreviews(); };
 }, [visible, reduced, paused, full, failed]);
 const source = full || visible && !reduced && !paused && !failed;
 return <span className={full ? 'media-video-full' : 'media-video-preview'}>
  {!failed && <video ref={ref} src={source ? apiUrl(`/api/clinic/media/${item.id}/content`) : undefined} muted={!full} loop={!full && !reduced} controls={full} playsInline preload="metadata" onError={() => setFailed(true)} aria-label={item.title || 'Video de la clínica'} />}
  {(!full || failed) && <span className="media-video-overlay"><span className="media-play-icon" aria-hidden="true">▶</span>{failed && <small>No se pudo cargar el video</small>}</span>}
 </span>;
}
