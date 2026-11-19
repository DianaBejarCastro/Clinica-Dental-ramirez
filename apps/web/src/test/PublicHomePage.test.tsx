import { beforeEach, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import App from '../App';
import { setSession } from '../services/api';

const branches = [{
  id: 'branch-1', name: 'Sucursal Centro', address: 'Av. Principal 123', city: 'Cochabamba',
  department: 'Cochabamba', phone: '+591 4 1234567', latitude: null, longitude: null,
}];
const fetchMock = vi.fn(async (_input: unknown) => {
  const path = String(_input);
  if (path.endsWith('/api/clinic/services'))
    return { ok: true, json: async () => [{ id: 's1', name: 'Servicio de prueba', shortDescription: '', visualVariant: 'TEAL' }] } as Response;
  if (path.endsWith('/api/clinic/branches'))
    return { ok: true, json: async () => branches } as Response;
  return { ok: false, status: 401, json: async () => ({ title: 'Sin sesión' }) } as Response;
});
beforeEach(() => {
  setSession(null); window.history.replaceState(null, '', '#/inicio');
  vi.stubGlobal('ResizeObserver', class { observe() {} disconnect() {} });
  window.matchMedia = vi.fn(() => ({ matches: true, addEventListener() {}, removeEventListener() {} }) as unknown as MediaQueryList);
  fetchMock.mockClear(); vi.stubGlobal('fetch', fetchMock);
});
it('muestra la landing pública y sus secciones sin consultar APIs privadas', async () => {
  render(<App />);
  expect(screen.getByRole('heading', { level: 1 }).textContent).toContain('Tu salud bucal,');
  await screen.findByRole('heading', { name: 'Nuestros servicios' });
  const main = within(screen.getByRole('main'));
  for (const name of ['Nuestros servicios', 'Nuestro equipo', 'Contacto', 'Agenda tu próxima consulta']) expect(main.getByRole('heading', { name })).toBeTruthy();
  expect(fetchMock.mock.calls.every(call => ['/api/clinic/media', '/api/clinic/brand', '/api/clinic/branches', '/api/clinic/team', '/api/clinic/services', '/api/clinic/site-images/care/meta', '/api/clinic/site-images/hero/meta', '/api/clinic/social-networks'].some(path => String(call[0]).split('?')[0].endsWith(path)))).toBe(true);
});
it('simplifica el footer y conserva los datos de sucursales solo en su sección pública', async () => {
  render(<App />);
  const main = within(screen.getByRole('main'));
  expect(await main.findByText('Sucursal Centro')).toBeTruthy();
  expect(main.getByText(/Av. Principal 123/)).toBeTruthy();
  expect(main.getByRole('link', { name: '+591 4 1234567' }).getAttribute('href')).toBe('tel:+59141234567');

  const footerElement = screen.getByRole('contentinfo');
  const footer = within(footerElement);
  expect(footerElement.querySelector('.public-footer-inner')?.children).toHaveLength(3);
  expect(footerElement.querySelector('.public-footer-brand .public-brand > span:not(.clinic-brand-logo)')).toBeNull();
  expect(footer.queryByRole('heading', { name: 'Contacto' })).toBeNull();
  for (const value of ['Sucursal Centro', 'Av. Principal 123', '+591 4 1234567']) expect(footer.queryByText(value)).toBeNull();
  expect(fetchMock.mock.calls.filter(([url]) => String(url).endsWith('/api/clinic/branches'))).toHaveLength(1);
  expect(footer.getByRole('link', { name: 'Inicio' }).getAttribute('href')).toBe('#/inicio?section=inicio');
  expect(footer.getByRole('link', { name: 'Reservar cita' }).getAttribute('href')).toBe('#/inicio?section=contacto');
  expect(footer.getByRole('link', { name: 'Acceso al sistema' }).getAttribute('href')).toBe('#/login');
  const year = String(new Date().getFullYear());
  expect(footer.getByText(year, { selector: 'time' }).getAttribute('dateTime')).toBe(year);
  expect(footer.getByText((_, element) => element?.tagName === 'P' && element.textContent === `© ${year} Clínica Dental Ramírez. Todos los derechos reservados.`)).toBeTruthy();
  expect(footer.getByText('Política de Privacidad')).toBeTruthy();
  expect(footer.getByText('Términos de Uso')).toBeTruthy();
  expect(footer.queryByRole('link', { name: 'Política de Privacidad' })).toBeNull();
  expect(footer.queryByRole('link', { name: 'Términos de Uso' })).toBeNull();
  expect(footerElement.querySelectorAll('.public-footer-bottom-separator[aria-hidden="true"]')).toHaveLength(2);
  expect(footerElement.querySelectorAll('.public-footer-wave[aria-hidden="true"]')).toHaveLength(2);
});
it('reutiliza el logo público y muestra la imagen al cargar', () => {
  render(<App />);
  for (const image of screen.getAllByAltText('Logo de la clínica')) {
    expect(image.getAttribute('src')).toContain('/api/clinic/logo'); fireEvent.load(image);
    expect((image as HTMLImageElement).hidden).toBe(false);
  }
  expect(screen.queryByLabelText('Monograma de la clínica')).toBeNull();
});
it('mantiene el monograma si el logo falla', () => {
  render(<App />); screen.getAllByAltText('Logo de la clínica').forEach(image => fireEvent.error(image));
  expect(screen.getAllByLabelText('Monograma de la clínica').every(item => item.textContent === 'R')).toBe(true);
});
it('el menú público contiene enlaces a las cinco secciones', () => {
  render(<App />);
  const nav = within(screen.getByRole('navigation', { name: 'Navegación pública' }));
  const footerNav = within(screen.getByRole('navigation', { name: 'Navegación del pie' }));
  for (const [name, id] of [['Inicio', 'inicio'], ['Nosotros', 'nosotros'], ['Servicios', 'servicios'], ['Equipo', 'equipo'], ['Contacto', 'contacto']]) {
    const headerLink = nav.getByRole('link', { name });
    const footerLink = footerNav.getByRole('link', { name });
    expect(headerLink.getAttribute('href')).toBe(`#/inicio?section=${id}`);
    expect(footerLink.getAttribute('href')).toBe(headerLink.getAttribute('href'));
    expect(footerLink.getAttribute('aria-current')).toBe(headerLink.getAttribute('aria-current'));
  }
  expect(footerNav.getByRole('link', { name: 'Acceso al sistema' }).getAttribute('href')).toBe('#/login');
  expect(footerNav.getByRole('link', { name: 'Reservar cita' }).getAttribute('href')).toBe('#/inicio?section=contacto');
});
it('abre y cierra el menú y Escape devuelve foco al botón', async () => {
  render(<App />); const u = userEvent.setup(); const toggle = screen.getByLabelText('Abrir menú');
  expect(toggle.getAttribute('aria-expanded')).toBe('false'); await u.click(toggle);
  expect(toggle.getAttribute('aria-expanded')).toBe('true');
  fireEvent.keyDown(screen.getByRole('navigation', { name: 'Navegación pública' }), { key: 'Escape' });
  expect(toggle.getAttribute('aria-expanded')).toBe('false'); expect(document.activeElement).toBe(toggle);
});
it('navegar a una sección cierra el menú y enfoca el destino', async () => {
  render(<App />); const u = userEvent.setup(); await u.click(screen.getByLabelText('Abrir menú'));
  await u.click(within(screen.getByRole('navigation', { name: 'Navegación pública' })).getByRole('link', { name: 'Servicios' }));
  await waitFor(() => expect(document.activeElement?.id).toBe('servicios'));
  expect(within(screen.getByRole('navigation', { name: 'Navegación pública' })).getByRole('link', { name: 'Servicios' }).getAttribute('aria-current')).toBe('location');
  expect(within(screen.getByRole('navigation', { name: 'Navegación del pie' })).getByRole('link', { name: 'Servicios' }).getAttribute('aria-current')).toBe('location');
  expect(screen.getByLabelText('Abrir menú').getAttribute('aria-expanded')).toBe('false');
});
it('reservar dirige a contacto sin abrir agenda interna', () => {
  render(<App />);
  screen.getAllByRole('link', { name: 'Reservar cita' }).forEach(link => expect(link.getAttribute('href')).toBe('#/inicio?section=contacto'));
  expect(fetchMock.mock.calls.every(call => ['/api/clinic/media', '/api/clinic/brand', '/api/clinic/branches', '/api/clinic/team', '/api/clinic/services', '/api/clinic/site-images/care/meta', '/api/clinic/site-images/hero/meta', '/api/clinic/social-networks'].some(path => String(call[0]).split('?')[0].endsWith(path)))).toBe(true);
});
it('Acceso al sistema abre el login funcional', async () => {
  render(<App />); const u = userEvent.setup();
  const accessLink = within(screen.getByRole('navigation', { name: 'Navegación pública' })).getByRole('link', { name: 'Acceso al sistema' });
  expect(accessLink.querySelector('span[aria-hidden="true"]')?.textContent).toBe('→');
  await u.click(accessLink);
  expect(await screen.findByRole('button', { name: 'Iniciar sesión' })).toBeTruthy();
  expect(screen.getByLabelText('Email')).toBeTruthy(); expect(screen.getByLabelText('Contraseña')).toBeTruthy();
});
it('login permite volver al inicio público', async () => {
  window.history.replaceState(null, '', '#/login'); render(<App />); const u = userEvent.setup();
  await u.click(await screen.findByRole('link', { name: 'Volver al inicio' }));
  await waitFor(() => expect(screen.getByRole('heading', { level: 1 }).textContent).toContain('Tu salud bucal,'));
});

it('conserva el diente interactivo y añade beneficios sin enlaces ajenos', async () => {
  const { container } = render(<App />);
  expect(container.querySelector('.hero-tooth-stage')).toBeTruthy();
  expect(screen.getByRole('link', { name: 'Agendar mi cita' }).getAttribute('href')).toBe('#/inicio?section=contacto');
  expect(container.querySelector('.public-hero-shield')).toBeTruthy();
  await screen.findByRole('heading', { name: 'Nuestros servicios' });
  expect(container.querySelector('#inicio')?.nextElementSibling?.id).toBe('servicios');
  expect(container.querySelectorAll('.hero-care-benefits li')).toHaveLength(3);
  expect(container.querySelector('.hero-clinic-scene')?.getAttribute('aria-hidden')).toBe('true');
  expect(screen.queryByRole('link', { name: 'Galería' })).toBeNull();
  expect(screen.queryByRole('link', { name: 'Blog' })).toBeNull();
});

it('reutiliza en el hero las ondas decorativas del footer', () => {
  const { container } = render(<App />);
  const heroWaves = container.querySelector('.public-hero-waves');
  const footerWaves = container.querySelector('.public-footer .public-footer-wave--top');
  expect(heroWaves?.getAttribute('aria-hidden')).toBe('true');
  expect(heroWaves?.getAttribute('viewBox')).toBe(footerWaves?.getAttribute('viewBox'));

  const design = (svg: Element | null) => ({
    paths: [...(svg?.querySelectorAll('path') ?? [])].map(path => path.getAttribute('d')),
    gradients: [...(svg?.querySelectorAll('linearGradient') ?? [])].map(gradient => ({
      direction: ['x1', 'y1', 'x2', 'y2'].map(attribute => gradient.getAttribute(attribute)),
      stops: [...gradient.querySelectorAll('stop')].map(stop =>
        ['offset', 'stop-color', 'stop-opacity'].map(attribute => stop.getAttribute(attribute))),
    })),
  });
  expect(design(heroWaves)).toEqual(design(footerWaves));

  const ids = [...(container.querySelectorAll('.public-hero-waves linearGradient, .public-footer .public-footer-wave--top linearGradient'))]
    .map(gradient => gradient.id);
  expect(new Set(ids).size).toBe(ids.length);
});

it('la navegación móvil conserva cinco destinos y la acción pública de reserva', () => {
  render(<App />);
  const nav = within(screen.getByRole('navigation', { name: 'Navegación móvil principal' }));
  expect(nav.getAllByRole('link')).toHaveLength(5);
  for (const [label, href] of [['Inicio', '#/inicio?section=inicio'], ['Servicios', '#/inicio?section=servicios'], ['Agendar cita', '#/inicio?section=contacto'], ['Contacto', '#/inicio?section=contacto'], ['Mi perfil', '#/login']]) {
    expect(nav.getByRole('link', { name: label }).getAttribute('href')).toBe(href);
  }
  expect(nav.getByRole('link', { name: 'Inicio' }).getAttribute('aria-current')).toBe('page');
});

it('Mi perfil sin sesión reutiliza el acceso existente', async () => {
  render(<App />);
  await userEvent.setup().click(within(screen.getByRole('navigation', { name: 'Navegación móvil principal' })).getByRole('link', { name: 'Mi perfil' }));
  expect(await screen.findByRole('button', { name: 'Iniciar sesión' })).toBeTruthy();
});

it.each([['DOCTOR', '/doctor/appointments'], ['PATIENT', '/patient'], ['CASHIER', '/cashier/payments']])('Mi perfil reutiliza el panel de %s', async (role, destination) => {
  const session = { accessToken: 'test', expiresAt: '2099-01-01', user: { id: 'test', firstName: 'Test', lastName: 'User', email: 'test@example.test', roles: [role], permissions: [] } };
  vi.stubGlobal('fetch', vi.fn(async (input: unknown) => {
    const url = String(input);
    if (url.endsWith('/refresh')) return new Response(JSON.stringify(session), { status: 200, headers: { 'Content-Type': 'application/json' } });
    if (url.endsWith('/api/clinic/branches')) return Response.json([]);
    return new Response('{}', { status: 404, headers: { 'Content-Type': 'application/json' } });
  }));
  render(<App />);
  await userEvent.setup().click(within(screen.getByRole('navigation', { name: 'Navegación móvil principal' })).getByRole('link', { name: 'Mi perfil' }));
  await waitFor(() => expect(window.location.hash).toBe('#' + destination));
});


it.each(['/', '/inicio'])('renders the public home for anonymous visitors at %s', async (url) => {
  window.history.replaceState(null, '', url);
  render(<App />);
  expect(screen.getByRole('heading', { level: 1 }).textContent).toContain('Tu salud bucal,');
  await screen.findByRole('heading', { name: 'Nuestros servicios' });
  expect(screen.queryByRole('button', { name: 'Iniciar sesión' })).toBeNull();
  expect(window.location.pathname + window.location.hash).toBe(url);
  expect(fetchMock.mock.calls.some(call => String(call[0]).includes('/auth/'))).toBe(false);
});

it.each(['/login', '/users', '/doctor/appointments', '/cashier/payments', '/patient', '/unknown-route'])('preserves login and protected fallback at %s', async (url) => {
  window.history.replaceState(null, '', url);
  render(<App />);
  expect(await screen.findByRole('button', { name: 'Iniciar sesión' })).toBeTruthy();
  expect(screen.queryByRole('heading', { name: /Tu salud bucal/ })).toBeNull();
});

it.each(['ADMINISTRATOR', 'DOCTOR', 'PATIENT', 'CASHIER'])('keeps the public root accessible with a %s session', async (role) => {
  setSession({ accessToken: 'test', expiresAt: '2099-01-01', user: { id: 'test', firstName: 'Test', lastName: 'User', email: 'test@example.test', roles: [role], permissions: [] } });
  window.history.replaceState(null, '', '/');
  render(<App />);
  await screen.findByRole('heading', { name: 'Nuestros servicios' });
  expect(screen.getByRole('heading', { level: 1 }).textContent).toContain('Tu salud bucal,');
  expect(window.location.hash).toBe('');
});
