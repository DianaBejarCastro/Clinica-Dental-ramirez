import { useEffect, useState } from 'react';
import { apiUrl } from '../../services/api';
import './clinic-hero-banner.css';

interface Metadata { focalPointX: number | null; focalPointY: number | null; altText: string; updatedAt: string }
export default function ClinicHeroBanner() {
  const [meta, setMeta] = useState<Metadata | null>(null), [loadedVersion, setLoadedVersion] = useState('');
  useEffect(() => {
    let controller: AbortController | null = null;
    function refresh() {
      controller?.abort(); const request = new AbortController(); controller = request;
      void fetch(apiUrl('/api/clinic/site-images/hero/meta'), { signal: request.signal, cache: 'no-store' })
        .then(async response => response.ok ? await response.json() as Metadata : null)
        .then(value => { if (!request.signal.aborted) setMeta(value); })
        .catch(() => { if (!request.signal.aborted) { setMeta(null); setLoadedVersion(''); } });
    }
    refresh(); window.addEventListener('focus', refresh); window.addEventListener('clinic-cover-changed', refresh);
    return () => { controller?.abort(); window.removeEventListener('focus', refresh); window.removeEventListener('clinic-cover-changed', refresh); };
  }, []);
  if (!meta) return null;
  return <div className="clinic-hero-banner" hidden={loadedVersion !== meta.updatedAt}>
    <img key={meta.updatedAt} src={apiUrl(`/api/clinic/site-images/hero?v=${encodeURIComponent(meta.updatedAt)}`)} width={1916} height={821}
      alt={meta.altText} decoding="async" onLoad={() => setLoadedVersion(meta.updatedAt)} onError={() => { setMeta(null); setLoadedVersion(''); }}
      style={{ objectPosition: `${(meta.focalPointX ?? .5) * 100}% ${(meta.focalPointY ?? .5) * 100}%` }} />
  </div>;
}
