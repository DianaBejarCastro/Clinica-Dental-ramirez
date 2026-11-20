import { StrictMode } from 'react';
import { act, cleanup, render, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import ClinicFavicon from '../components/ClinicFavicon';
import { notifyLogoChanged } from '../modules/admin/clinic/logoResource';
vi.mock('../services/api', () => ({ apiUrl: (path: string) => path }));
afterEach(() => { cleanup(); document.head.querySelectorAll('link[rel="icon"]').forEach(x => x.remove()); vi.unstubAllGlobals(); });
function response(version = 'Thu, 19 Nov 2026 12:00:00 GMT', mime = 'image/png') {
 return new Response(null, { headers: { 'Content-Type': mime, 'Last-Modified': version } });
}
it('reuses the logo URL with stable version and keeps one favicon across routes and renders', async () => {
 vi.stubGlobal('fetch', vi.fn(async () => response()));
 const title = document.title;
 const { rerender } = render(<StrictMode><ClinicFavicon /></StrictMode>);
 await waitFor(() => expect(document.head.querySelector('link[rel="icon"]')).not.toBeNull());
 const icon = document.head.querySelector<HTMLLinkElement>('link[rel="icon"]')!;
 expect(icon.getAttribute('href')).toBe('/api/clinic/logo?v=Thu%2C%2019%20Nov%202026%2012%3A00%3A00%20GMT');
 for (const route of ['/inicio', '/login', '/clinic', '/doctor/appointments', '/patient', '/cashier/payments']) {
  window.history.replaceState(null, '', `#${route}`);rerender(<StrictMode><ClinicFavicon /></StrictMode>);
  expect(document.head.querySelectorAll('link[rel="icon"]')).toHaveLength(1);
  expect(document.head.querySelector('link[rel="icon"]')).toBe(icon);
 }
 expect(document.title).toBe(title);
});
it('updates version and MIME on logo changes and reloads', async () => {
 const fetcher = vi.fn(async () => response());vi.stubGlobal('fetch', fetcher);
 const view = render(<ClinicFavicon />);
 await waitFor(() => expect(document.head.querySelector('link[rel="icon"]')).not.toBeNull());
 fetcher.mockImplementation(async () => response('Fri, 20 Nov 2026 12:00:00 GMT', 'image/webp'));
 act(() => notifyLogoChanged());
 await waitFor(() => expect(document.head.querySelector('link[rel="icon"]')?.getAttribute('type')).toBe('image/webp'));
 const href = document.head.querySelector('link[rel="icon"]')?.getAttribute('href');
 view.unmount();render(<ClinicFavicon />);
 await waitFor(() => expect(document.head.querySelector('link[rel="icon"]')?.getAttribute('href')).toBe(href));
});
it.each([false, true])('preserves an existing fallback on missing or failed requests (%s)', async failure => {
 const fallback = document.createElement('link');fallback.rel='icon';fallback.href='/favicon.ico';document.head.append(fallback);
 vi.stubGlobal('fetch', vi.fn(async () => { if(failure) throw Error('offline');return new Response(null,{status:404}); }));
 render(<ClinicFavicon />);await act(async()=>{});
 expect(fallback.getAttribute('href')).toBe('/favicon.ico');expect(document.head.querySelectorAll('link[rel="icon"]')).toHaveLength(1);
});
it('restores fallback if the icon fails to load', async () => {
 vi.stubGlobal('fetch',vi.fn(async()=>response()));render(<ClinicFavicon />);
 await waitFor(()=>expect(document.head.querySelector('link[rel="icon"]')).not.toBeNull());
 document.head.querySelector('link[rel="icon"]')!.dispatchEvent(new Event('error'));
 expect(document.head.querySelector('link[rel="icon"]')).toBeNull();
});
it('updates the existing node and restores its attributes on unmount', async () => {
 const icon=document.createElement('link');icon.rel='icon';icon.href='/favicon.ico';icon.type='image/x-icon';icon.setAttribute('sizes','32x32');document.head.append(icon);
 vi.stubGlobal('fetch',vi.fn(async()=>response()));const view=render(<ClinicFavicon />);
 await waitFor(()=>expect(icon.type).toBe('image/png'));
 expect(document.head.querySelectorAll('link[rel="icon"]')).toHaveLength(1);
 expect(document.head.querySelector('link[rel="icon"]')).toBe(icon);
 view.unmount();expect(icon.getAttribute('href')).toBe('/favicon.ico');expect(icon.type).toBe('image/x-icon');expect(icon.getAttribute('sizes')).toBe('32x32');
});
it('ignores an unusable response', async () => {
 vi.stubGlobal('fetch',vi.fn(async()=>response(undefined,'text/html')));render(<ClinicFavicon />);await act(async()=>{});
 expect(document.head.querySelector('link[rel="icon"]')).toBeNull();
});
it('aborts a stalled request without changing fallback', async () => {
 vi.useFakeTimers();
 try {
  let signal:AbortSignal|undefined;
  vi.stubGlobal('fetch',vi.fn((_url:string,options:RequestInit)=>{signal=options.signal as AbortSignal;return new Promise(()=>{});}));
  const view=render(<ClinicFavicon />);act(()=>vi.advanceTimersByTime(10000));
  expect(signal?.aborted).toBe(true);expect(document.head.querySelector('link[rel="icon"]')).toBeNull();view.unmount();
 } finally {vi.useRealTimers();}
});
