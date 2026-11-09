import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, expect, it, vi } from 'vitest';
import CareSection from '../modules/public/CareSection';
beforeEach(() => { vi.restoreAllMocks(); vi.stubGlobal('fetch', vi.fn(async () => new Response(null, { status: 404 }))); });
it('conserva los cuatro beneficios con descripción, CTA y fallback sin foto', async () => {
  render(<CareSection />);
  expect(screen.getByRole('heading', { name: 'Una atención pensada en las personas' })).toBeTruthy();
  expect(screen.getAllByRole('listitem')).toHaveLength(4);
  expect(screen.getByRole('link', { name: 'Agendar consulta' }).getAttribute('href')).toContain('contacto');
  expect(screen.getByText('nuestro compromiso.')).toBeTruthy();
  await waitFor(() => expect(fetch).toHaveBeenCalled());
  expect(screen.queryByRole('img')).toBeNull();
});
it('carga la imagen administrada con versión, alt y proporciones sin deformarla', async () => {
  vi.stubGlobal('fetch', vi.fn(async () => Response.json({ altText: 'Atención cercana', updatedAt: 'v1', focalPointX: .4, focalPointY: .6 })));
  render(<CareSection />);
  const image = await screen.findByAltText('Atención cercana');
  expect(image.getAttribute('src')).toContain('/site-images/care?v=v1');
  expect(image.getAttribute('width')).toBe('1200');
  expect(image.getAttribute('height')).toBe('1350');
  expect(image.style.objectPosition).toBe('40% 60%');
  fireEvent.error(image);
  expect(screen.getByText('nuestro compromiso.')).toBeTruthy();
});
it('actualiza la foto al cambiarla y recupera el fallback al eliminarla', async () => {
  const fetcher = vi.fn().mockResolvedValueOnce(Response.json({ altText: 'Foto anterior', updatedAt: '1' })).mockResolvedValueOnce(Response.json({ altText: 'Foto nueva', updatedAt: '2' })).mockResolvedValue(new Response(null, { status: 404 }));
  vi.stubGlobal('fetch', fetcher);
  render(<CareSection />);
  await screen.findByAltText('Foto anterior');
  fireEvent(window, new Event('clinic-care-changed'));
  await screen.findByAltText('Foto nueva');
  fireEvent(window, new Event('clinic-care-changed'));
  await waitFor(() => expect(screen.queryByRole('img')).toBeNull());
});
