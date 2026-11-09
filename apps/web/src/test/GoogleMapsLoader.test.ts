import { afterEach, expect, it, vi } from 'vitest';
import { mockApi } from './googleMapsMock';
type TestWindow = Window & { google?: { maps: typeof google.maps }; __clinicMapsReady?: () => void; gm_authFailure?: () => void };
const host = window as TestWindow;
afterEach(() => {
  document.querySelector('#clinic-google-maps')?.remove();
  delete host.google; delete host.__clinicMapsReady; delete host.gm_authFailure;
  vi.unstubAllEnvs(); vi.useRealTimers(); vi.resetModules();
});
it('missing key inserts no external script', async () => {
  vi.stubEnv('VITE_GOOGLE_MAPS_API_KEY', '');
  const { loadGoogleMaps } = await import('../components/maps/googleMapsLoader');
  await expect(loadGoogleMaps()).rejects.toThrow();
  expect(document.querySelector('#clinic-google-maps')).toBeNull();
});
it('concurrent consumers share exactly one script and load only marker library', async () => {
  vi.stubEnv('VITE_GOOGLE_MAPS_API_KEY', 'test-placeholder');
  const { loadGoogleMaps } = await import('../components/maps/googleMapsLoader');
  const first = loadGoogleMaps(), second = loadGoogleMaps();
  expect(first).toBe(second);
  const scripts = document.querySelectorAll<HTMLScriptElement>('#clinic-google-maps');
  expect(scripts).toHaveLength(1);
  expect(new URL(scripts[0].src).searchParams.get('libraries')).toBe('marker');
  host.google = { maps: mockApi }; host.__clinicMapsReady!();
  expect(await first).toBe(mockApi);
});
it('network failure rejects safely without inserting repeated scripts', async () => {
  vi.stubEnv('VITE_GOOGLE_MAPS_API_KEY', 'test-placeholder');
  const { loadGoogleMaps } = await import('../components/maps/googleMapsLoader');
  const promise = loadGoogleMaps(); const rejected = expect(promise).rejects.toThrow();
  document.querySelector('#clinic-google-maps')!.dispatchEvent(new Event('error')); await rejected;
  expect(loadGoogleMaps()).toBe(promise);
});
it('authorization failure after loading notifies mounted maps', async () => {
  vi.stubEnv('VITE_GOOGLE_MAPS_API_KEY', 'test-placeholder');
  const { loadGoogleMaps, mapFailureEvent } = await import('../components/maps/googleMapsLoader');
  const promise = loadGoogleMaps(); host.google = { maps: mockApi }; host.__clinicMapsReady!(); await promise;
  const listener = vi.fn(); window.addEventListener(mapFailureEvent, listener);
  host.gm_authFailure!(); expect(listener).toHaveBeenCalledTimes(1); window.removeEventListener(mapFailureEvent, listener);
});
it('a stalled script times out', async () => {
  vi.useFakeTimers(); vi.stubEnv('VITE_GOOGLE_MAPS_API_KEY', 'test-placeholder');
  const { loadGoogleMaps } = await import('../components/maps/googleMapsLoader');
  const rejected = expect(loadGoogleMaps()).rejects.toThrow();
  await vi.advanceTimersByTimeAsync(15000); await rejected;
});
