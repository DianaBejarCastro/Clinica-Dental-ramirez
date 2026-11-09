/// <reference types="google.maps" />
type MapsWindow = Window & { google?: typeof google; __clinicMapsReady?: () => void; gm_authFailure?: () => void };
const host = window as MapsWindow;
let pending: Promise<typeof google.maps> | undefined;
export const mapFailureEvent = 'clinic-google-maps-error';
export function loadGoogleMaps(): Promise<typeof google.maps> {
  if (pending) return pending;
  const key = import.meta.env.VITE_GOOGLE_MAPS_API_KEY?.trim();
  if (!key) return Promise.reject(new Error('Map configuration unavailable'));
  if (host.google?.maps?.marker?.AdvancedMarkerElement) return Promise.resolve(host.google.maps);
  pending = new Promise((resolve, reject) => {
    let settled = false;
    const fail = () => {
      host.dispatchEvent(new Event(mapFailureEvent));
      if (!settled) { settled = true; clearTimeout(timer); reject(new Error('Map unavailable')); }
    };
    const timer = window.setTimeout(fail, 15000);
    host.gm_authFailure = fail;
    host.__clinicMapsReady = () => {
      if (settled) return;
      if (!host.google?.maps?.marker?.AdvancedMarkerElement) { fail(); return; }
      settled = true; clearTimeout(timer); resolve(host.google.maps);
    };
    const script = document.createElement('script');
    script.id = 'clinic-google-maps';
    const query = new URLSearchParams({ key, callback: '__clinicMapsReady', loading: 'async', libraries: 'marker', v: 'weekly', language: 'es' });
    script.src = `https://maps.googleapis.com/maps/api/js?${query}`;
    script.async = true; script.onerror = fail;
    document.head.appendChild(script);
  });
  return pending;
}
