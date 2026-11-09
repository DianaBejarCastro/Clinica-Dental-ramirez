import { vi } from 'vitest';
export const maps: MockMap[] = [];
export const markers: MockMarker[] = [];
class Events {
  handlers: Record<string, (event: unknown) => void> = {};
  addListener(name: string, fn: (event: unknown) => void) {
    this.handlers[name] = fn;
    return { remove: () => { delete this.handlers[name]; } };
  }
  fire(name: string, lat: number, lng: number) { this.handlers[name]?.({ latLng: { lat: () => lat, lng: () => lng } }); }
}
export class MockMap extends Events {
  setCenter = vi.fn(); setZoom = vi.fn(); fitBounds = vi.fn(); unbindAll = vi.fn();
  constructor(_node: HTMLElement, public options: unknown) { super(); maps.push(this); }
}
export class MockMarker extends Events {
  map: unknown; position: unknown; gmpDraggable: boolean;
  constructor(options: { map: unknown; position: unknown; gmpDraggable: boolean }) {
    super(); Object.assign(this, options); this.gmpDraggable = options.gmpDraggable; markers.push(this);
  }
}
export class MockBounds { extend = vi.fn(); }
export const mockApi = { Map: MockMap, marker: { AdvancedMarkerElement: MockMarker }, LatLngBounds: MockBounds } as unknown as typeof google.maps;
export function resetMaps() { maps.length = 0; markers.length = 0; }
