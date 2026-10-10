import { beforeEach, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import App from '../App';
import { setSession } from '../services/api';

const fetchMock = vi.fn(async () => ({ ok: false, status: 401, json: async () => ({ title: 'Sin sesión' }) }) as Response);
beforeEach(() => {
  setSession(null); window.history.replaceState(null, '', '#/inicio');
  fetchMock.mockClear(); vi.stubGlobal('fetch', fetchMock);
});
it('muestra la landing pública y sus secciones sin consultar APIs privadas', () => {
  render(<App />);
  expect(screen.getByRole('heading', { level: 1 }).textContent).toContain('Cuidamos tu sonrisa');
  for (const name of ['Nuestros servicios', 'Nuestro equipo', 'Contacto', 'Agenda tu próxima consulta']) expect(screen.getByRole('heading', { name })).toBeTruthy();
  expect(fetchMock).not.toHaveBeenCalled();
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
  render(<App />); const nav = within(screen.getByRole('navigation', { name: 'Navegación pública' }));
  for (const [name, id] of [['Inicio', 'inicio'], ['Nosotros', 'nosotros'], ['Servicios', 'servicios'], ['Equipo', 'equipo'], ['Contacto', 'contacto']])
    expect(nav.getByRole('link', { name }).getAttribute('href')).toBe(`#/inicio?section=${id}`);
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
  expect(screen.getByLabelText('Abrir menú').getAttribute('aria-expanded')).toBe('false');
});
it('reservar dirige a contacto sin abrir agenda interna', () => {
  render(<App />);
  screen.getAllByRole('link', { name: 'Reservar cita' }).forEach(link => expect(link.getAttribute('href')).toBe('#/inicio?section=contacto'));
  expect(fetchMock).not.toHaveBeenCalled();
});
it('Acceso al sistema abre el login funcional', async () => {
  render(<App />); const u = userEvent.setup();
  await u.click(within(screen.getByRole('navigation', { name: 'Navegación pública' })).getByRole('link', { name: 'Acceso al sistema' }));
  expect(await screen.findByRole('button', { name: 'Ingresar' })).toBeTruthy();
  expect(screen.getByLabelText('Email')).toBeTruthy(); expect(screen.getByLabelText('Contraseña')).toBeTruthy();
});
it('login permite volver al inicio público', async () => {
  window.history.replaceState(null, '', '#/login'); render(<App />); const u = userEvent.setup();
  await u.click(await screen.findByRole('link', { name: '← Volver al inicio' }));
  await waitFor(() => expect(screen.getByRole('heading', { level: 1 }).textContent).toContain('Cuidamos tu sonrisa'));
});
