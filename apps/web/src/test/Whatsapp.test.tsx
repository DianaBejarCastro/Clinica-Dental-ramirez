import { afterEach, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import PublicWhatsAppButton, { whatsappHref, whatsappMessage } from '../modules/public/PublicWhatsAppButton';
import ClinicWhatsapp from '../modules/admin/clinic/ClinicWhatsapp';
import { api } from '../services/api';
import type { Clinic } from '../modules/admin/types';
vi.mock('../services/api', () => ({ api: vi.fn(), apiUrl: (path: string) => path }));
afterEach(() => vi.unstubAllGlobals());
it('normalizes formatted numbers and safely encodes the message', () => {
  expect(whatsappHref('+591 7123-4567')).toBe(`https://wa.me/59171234567?text=${encodeURIComponent(whatsappMessage)}`);
  for (const invalid of ['', 'https://evil.test/59171234567', 'abc59171234567', '123', '+0123456789']) expect(whatsappHref(invalid)).toBeNull();
});
it('loads the existing public brand endpoint and renders a safe external link', async () => {
  const fetcher = vi.fn().mockResolvedValue({ ok: true, json: async () => ({ name: 'Clinic', whatsapp: '+59171234567' }) });
  vi.stubGlobal('fetch', fetcher);
  render(<PublicWhatsAppButton />);
  const link = await screen.findByRole('link', { name: 'Contactar por WhatsApp' });
  expect(link.getAttribute('href')).toBe(whatsappHref('+59171234567'));
  expect(link.getAttribute('target')).toBe('_blank');
  expect(link.getAttribute('rel')).toBe('noopener noreferrer');
  expect(fetcher.mock.calls[0][0]).toBe('/api/clinic/brand');
});
it.each([null, '', 'invalid', '+123'])('hides the button for %s', async whatsapp => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: true, json: async () => ({ whatsapp }) }));
  const { container } = render(<PublicWhatsAppButton />);
  await waitFor(() => expect(fetch).toHaveBeenCalled());
  expect(container.querySelector('a')).toBeNull();
});
it('admin reads, validates and saves the existing whatsapp property', async () => {
  const clinic = { id: 'clinic', name: 'Clinic', whatsapp: '+59171234567', isActive: true } as Clinic;
  const saved = { ...clinic, whatsapp: '+59176543210' };
  vi.mocked(api).mockResolvedValue(saved);
  const onSaved = vi.fn();
  const view = render(<ClinicWhatsapp clinic={clinic} onSaved={onSaved} disabled={false} />);
  const input = screen.getByLabelText('Número de WhatsApp') as HTMLInputElement;
  expect(input.value).toBe('71234567');
  fireEvent.change(input, { target: { value: '123' } });
  fireEvent.click(screen.getByRole('button', { name: 'Guardar WhatsApp' }));
  expect(await screen.findByRole('alert')).toHaveProperty('textContent', expect.stringContaining('8 dígitos'));
  fireEvent.change(input, { target: { value: '76543210' } });
  fireEvent.click(screen.getByRole('button', { name: 'Guardar WhatsApp' }));
  await waitFor(() => expect(onSaved).toHaveBeenCalledWith(saved));
  expect(api).toHaveBeenCalledWith('/api/admin/clinic', 'PUT', saved);
  view.unmount();
  render(<ClinicWhatsapp clinic={saved} onSaved={onSaved} disabled={false} />);
  expect((screen.getByLabelText('Número de WhatsApp') as HTMLInputElement).value).toBe('76543210');
});
