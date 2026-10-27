import { afterEach, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import PublicSocialLinks from '../modules/public/PublicSocialLinks';
afterEach(() => vi.unstubAllGlobals());
const account = { id: 'configured', name: 'Red configurada', url: 'https://example.org/account?ref=clinic', hasLogo: true };
it('uses the public active-only endpoint, configured order, logo and exact secure link', async () => {
  const fetcher = vi.fn().mockResolvedValue(Response.json([account, { ...account, id: 'second', name: 'Otra red', hasLogo: false }]));
  vi.stubGlobal('fetch', fetcher);
  render(<PublicSocialLinks />);
  const link = await screen.findByRole('link', { name: 'Visitar Red configurada de Clínica Dental RAMÍREZ' });
  expect(link.getAttribute('href')).toBe(account.url);
  expect(link.getAttribute('target')).toBe('_blank');
  expect(link.getAttribute('rel')).toBe('noopener noreferrer');
  expect(screen.getByAltText(account.name).getAttribute('src')).toContain('/api/clinic/social-networks/configured/logo');
  expect(screen.getAllByRole('link')[0]).toBe(link);
  expect(fetcher.mock.calls[0][0]).toContain('/api/clinic/social-networks');
  expect(fetcher.mock.calls[0][1].cache).toBe('no-store');
});
it('hides the entire block for no active accounts and refreshes deactivation on focus', async () => {
  const fetcher = vi.fn().mockImplementation(async () => Response.json([account]));
  vi.stubGlobal('fetch', fetcher);
  const view = render(<PublicSocialLinks />);
  await screen.findByText('Síguenos');
  fetcher.mockImplementation(async () => Response.json([]));
  fireEvent(window, new Event('focus'));
  await waitFor(() => expect(view.container.innerHTML).toBe(''));
});
it('never renders unsafe or credential-bearing URLs', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(Response.json([
    ...['javascript:alert(1)', 'data:text/html,test', 'https://user:pass@example.org', 'not a URL'].map((url, id) => ({ ...account, id: String(id), url })),
    account,
  ])));
  render(<PublicSocialLinks />);
  await screen.findByText('Síguenos');
  expect(screen.getAllByRole('link')).toHaveLength(1);
});
it('has no empty heading or spacing after a failed public request', async () => {
  vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new Error('offline')));
  const view = render(<PublicSocialLinks />);
  await waitFor(() => expect(view.container.innerHTML).toBe(''));
});
