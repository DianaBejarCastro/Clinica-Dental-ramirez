import { useEffect, useState } from 'react';
import { apiUrl } from '../../services/api';

interface SocialAccount { id: string; name: string; url: string; hasLogo: boolean }
function safeUrl(value: string) {
  try { const url = new URL(value); return ['http:', 'https:'].includes(url.protocol) && !url.username && !url.password; }
  catch { return false; }
}
export default function PublicSocialLinks() {
  const [accounts, setAccounts] = useState<SocialAccount[]>([]);
  useEffect(() => {
    let controller: AbortController | undefined;
    const refresh = () => {
      controller?.abort();
      const request = new AbortController(); controller = request;
      void fetch(apiUrl('/api/clinic/social-networks'), { signal: request.signal, cache: 'no-store' })
        .then(async response => response.ok ? await response.json() as SocialAccount[] : [])
        .then(items => { if (!request.signal.aborted) setAccounts(items.filter(item => safeUrl(item.url))); })
        .catch(() => { if (!request.signal.aborted) setAccounts([]); });
    };
    refresh();
    const timer = window.setInterval(refresh, 60000);
    window.addEventListener('focus', refresh);
    return () => { controller?.abort(); clearInterval(timer); window.removeEventListener('focus', refresh); };
  }, []);
  if (!accounts.length) return null;
  return <div className="public-social"><h3>Síguenos</h3><ul>{accounts.map(account =>
    <li key={account.id}><a href={account.url} target="_blank" rel="noopener noreferrer"
      aria-label={`Visitar ${account.name} de Clínica Dental RAMÍREZ`}>
      {account.hasLogo ? <img src={apiUrl(`/api/clinic/social-networks/${account.id}/logo`)} alt={account.name} width={36} height={36} /> : <span>{account.name}</span>}
    </a></li>)}</ul></div>;
}
