import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, expect, it, vi } from 'vitest';
import App from '../App';
import { setSession, type Session } from '../services/api';
import type { Dashboard } from '../modules/admin/types';

const viewPermissions = ['users.view', 'clinic.view', 'centers.view', 'doctors.view', 'specialties.view', 'schedules.view'];
let permissions: string[];
let summary: Dashboard;
let dashboardStatus: number;
const session = (): Session => ({
  accessToken: 'frontend-test-token', expiresAt: '',
  user: { id: 'user-1', firstName: 'Ana', lastName: 'Prueba', email: 'ana@example.com', roles: ['ADMINISTRATOR'], permissions },
});
const fetchMock = vi.fn(async (input: string | URL | Request, init?: RequestInit) => {
  const path = new URL(String(input)).pathname;
  if (path === '/api/auth/refresh') return Response.json(session());
  if (path === '/api/admin/dashboard') return Response.json(
    dashboardStatus === 200 ? summary : { title: 'No se pudo cargar el resumen.' }, { status: dashboardStatus });
  if (path === '/api/admin/clinic' && init?.method === 'GET') return Response.json({
    id: 'clinic-1', name: 'Clínica Dental RAMÍREZ', isActive: true,
    legalName: null, description: null, logoUrl: null, phone: null, whatsapp: null,
    email: null, website: null, taxId: null, createdAt: '2026-09-30T00:00:00Z', updatedAt: null,
  });
  throw new Error(`Solicitud inesperada: ${init?.method} ${path}`);
});

beforeEach(() => {
  setSession(null);
  window.history.replaceState(null, '', '#/');
  permissions = [...viewPermissions];
  summary = { totalUsers: 12, totalDoctors: 5, activeDoctors: 4, totalCenters: 3, activeCenters: 2, totalSpecialties: 12 };
  dashboardStatus = 200;
  fetchMock.mockClear();
  vi.stubGlobal('fetch', fetchMock);
});

it('muestra los seis contadores del dashboard y sus destinos', async () => {
  render(<App />);
  for (const [label, value, href] of [
    ['Total usuarios', '12', '#/users'], ['Total doctores', '5', '#/doctors'],
    ['Doctores activos', '4', '#/doctors'], ['Total sucursales', '3', '#/centers'],
    ['Sucursales activas', '2', '#/centers'], ['Total especialidades', '12', '#/specialties'],
  ]) {
    const metric = await screen.findByRole('link', { name: new RegExp(label) });
    expect(within(metric).getByText(value)).toBeTruthy();
    expect(metric.getAttribute('href')).toBe(href);
  }
});

it('respeta contadores restringidos y muestra el valor cero', async () => {
  permissions = ['centers.view'];
  summary = { totalUsers: null, totalDoctors: null, activeDoctors: null, totalCenters: 0, activeCenters: 0, totalSpecialties: null };
  render(<App />);
  const centers = await screen.findByRole('link', { name: /Total sucursales/ });
  expect(within(centers).getByText('0')).toBeTruthy();
  expect(screen.queryByText('Total usuarios')).toBeNull();
  expect(screen.queryByText('Total doctores')).toBeNull();
  expect(screen.queryByText('Total especialidades')).toBeNull();
  expect(within(screen.getByRole('navigation')).queryByRole('link', { name: 'Doctores' })).toBeNull();
});

it('no solicita dashboard sin permisos para consultar contadores', async () => {
  permissions = ['clinic.view'];
  render(<App />);
  expect(await screen.findByText('No hay contadores disponibles para tus permisos actuales.')).toBeTruthy();
  expect(fetchMock.mock.calls.some(([url]) => String(url).endsWith('/api/admin/dashboard'))).toBe(false);
});

it('muestra errores de carga del dashboard', async () => {
  dashboardStatus = 503;
  render(<App />);
  expect((await screen.findByRole('alert')).textContent).toBe('No se pudo cargar el resumen.');
  expect(screen.queryByText('Cargando resumen…')).toBeNull();
});

it('retira contadores y vuelve a consultar al cambiar permisos de la sesión', async () => {
  render(<App />);
  await screen.findByText('Total usuarios');
  permissions = ['centers.view'];
  summary = { totalUsers: null, totalDoctors: null, activeDoctors: null, totalCenters: 7, activeCenters: 6, totalSpecialties: null };
  act(() => setSession(session()));
  await waitFor(() => expect(fetchMock.mock.calls.filter(([url]) => String(url).endsWith('/api/admin/dashboard'))).toHaveLength(2));
  const centers = await screen.findByRole('link', { name: /Total sucursales/ });
  await within(centers).findByText('7');
  expect(screen.queryByText('Total usuarios')).toBeNull();
  expect(screen.queryByText('Total doctores')).toBeNull();
  expect(screen.queryByText('Total especialidades')).toBeNull();
});

it('incluye las seis vistas v0.3.0 en la navegación autorizada', async () => {
  render(<App />);
  const nav = within(await screen.findByRole('navigation'));
  for (const [name, route] of [
    ['Resumen', '/'], ['Clínica', '/clinic'], ['Sucursales', '/centers'],
    ['Doctores', '/doctors'], ['Especialidades', '/specialties'], ['Horarios', '/schedules'],
  ]) expect(nav.getByRole('link', { name }).getAttribute('href')).toBe(`#${route}`);
  expect(nav.getByRole('link', { name: 'Resumen' }).getAttribute('aria-current')).toBe('page');
});

it('navega entre resumen y clínica marcando la ruta activa', async () => {
  render(<App />);
  const user = userEvent.setup();
  const nav = within(await screen.findByRole('navigation'));
  await user.click(nav.getByRole('link', { name: 'Clínica' }));
  expect(await screen.findByDisplayValue('Clínica Dental RAMÍREZ')).toBeTruthy();
  expect(nav.getByRole('link', { name: 'Clínica' }).getAttribute('aria-current')).toBe('page');
  await user.click(nav.getByRole('link', { name: 'Resumen' }));
  expect(await screen.findByRole('heading', { name: 'Hola, Ana.' })).toBeTruthy();
  expect(nav.getByRole('link', { name: 'Resumen' }).getAttribute('aria-current')).toBe('page');
});

it.each([
  ['/clinic', 'Clínica'], ['/centers', 'Sucursales'], ['/doctors', 'Doctores'],
  ['/specialties', 'Especialidades'], ['/schedules', 'Horarios'],
])('restringe acceso directo a %s y oculta su navegación sin permiso', async (route, name) => {
  permissions = [];
  window.history.replaceState(null, '', `#${route}`);
  render(<App />);
  expect(await screen.findByRole('heading', { name: 'Acceso restringido' })).toBeTruthy();
  expect(within(screen.getByRole('navigation')).queryByRole('link', { name })).toBeNull();
  expect(fetchMock.mock.calls.some(([url]) => String(url).includes('/api/admin/'))).toBe(false);
});
