import { useEffect, useState } from 'react';
import { logoUrl } from '../modules/admin/clinic/logoResource';

export default function ClinicFavicon() {
 const [change, setChange] = useState(0);
 useEffect(() => {
  const update = () => setChange(value => value + 1);
  window.addEventListener('clinic-logo-changed', update);
  return () => window.removeEventListener('clinic-logo-changed', update);
 }, []);
 useEffect(() => {
  const controller = new AbortController();
  const timeout = window.setTimeout(() => controller.abort(), 10000);
  const existing = document.head.querySelector<HTMLLinkElement>('link[rel="icon"]');
  const previous = existing?.cloneNode(true) as HTMLLinkElement | undefined;
  let icon: HTMLLinkElement | null = null;
  const restore = () => {
   if (!icon) return;
   icon.onerror = null;
   if (existing && previous) {
    for (const attribute of [...existing.attributes]) existing.removeAttribute(attribute.name);
    for (const attribute of [...previous.attributes]) existing.setAttribute(attribute.name, attribute.value);
   } else icon.remove();
   icon = null;
  };
  void fetch(logoUrl(), { signal: controller.signal, cache: 'no-cache' }).then(async response => {
   const mime = response.headers.get('Content-Type')?.split(';')[0];
   const version = response.headers.get('Last-Modified') || undefined;
   await response.body?.cancel();
   if (controller.signal.aborted || !response.ok || !mime || !['image/png', 'image/jpeg', 'image/webp'].includes(mime)) return;
   icon = existing || document.createElement('link');
   icon.rel = 'icon';
   icon.type = mime;
   icon.removeAttribute('sizes');
   icon.href = logoUrl(version);
   icon.onerror = restore;
   if (!existing) document.head.appendChild(icon);
  }).catch(() => {}).finally(() => window.clearTimeout(timeout));
  return () => { window.clearTimeout(timeout); controller.abort(); restore(); };
 }, [change]);
 return null;
}
