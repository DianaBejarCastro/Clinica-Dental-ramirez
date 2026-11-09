import { render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { maps, markers, mockApi, resetMaps } from './googleMapsMock';
import { loadGoogleMaps } from '../components/maps/googleMapsLoader';
vi.mock('../components/maps/googleMapsLoader', () => ({ loadGoogleMaps: vi.fn(), mapFailureEvent: 'maps-failure' }));
beforeEach(() => { resetMaps(); vi.mocked(loadGoogleMaps).mockResolvedValue(mockApi); });
import BranchesSection from '../modules/public/BranchesSection';
const branch = { id: 'a', name: 'Central', address: 'Calle principal', city: 'Llallagua', department: 'Potosí', phone: '12345', latitude: -18.42, longitude: -66.58 };
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals(); });
it('loads one list, renders multiple markers and fits bounds', async () => {
  const fetch = vi.fn().mockResolvedValue(Response.json([branch, { ...branch, id: 'b', name: 'Norte', latitude: -17 }]));
  vi.stubGlobal('fetch', fetch); 
  render(<BranchesSection />); await screen.findByText('Central');
  await waitFor(() => expect(markers).toHaveLength(2));
  expect(maps[0].fitBounds).toHaveBeenCalled(); expect(fetch).toHaveBeenCalledTimes(1);
  const link = screen.getByRole('link', { name: /Cómo llegar a Central/ });
  expect(link.getAttribute('href')).toContain('-18.42,-66.58');
  expect(link.getAttribute('rel')).toBe('noopener noreferrer');
  expect(maps).toHaveLength(1);
});
it('centers a single branch on its persisted coordinates', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(Response.json([branch])));
  render(<BranchesSection />); await screen.findByText('Central');
  await waitFor(() => expect(maps[0].setCenter).toHaveBeenCalledWith({ lat: -18.42, lng: -66.58 }));
});
it('keeps address without inventing coordinates or directions', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(Response.json([{ ...branch, latitude: null, longitude: null }])));
  render(<BranchesSection />); await screen.findByText('Central');
  expect(screen.queryByRole('region', { name: 'Mapa de nuestras sucursales' })).toBeNull();
  expect(screen.queryByRole('link', { name: /Cómo llegar/ })).toBeNull();
});

it('preserves cards and directions when Google cannot load', async () => {
  vi.mocked(loadGoogleMaps).mockRejectedValue(new Error('missing key'));
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(Response.json([branch])));
  render(<BranchesSection />); await screen.findByText(/No se pudo cargar el mapa/);
  expect(screen.getByText('Central')).toBeTruthy();
  expect(screen.getByRole('link', { name: /Cómo llegar/ })).toBeTruthy();
});
