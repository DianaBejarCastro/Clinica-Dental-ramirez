import type { ReactElement } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AuthProvider } from '../modules/auth/AuthProvider';
import ClinicPage from '../modules/admin/clinic/ClinicPage';
import CentersPage from '../modules/admin/centers/CentersPage';
import SpecialtiesPage from '../modules/admin/specialties/SpecialtiesPage';
import type { Center, Clinic, Specialty } from '../modules/admin/types';
import { setSession, type Session } from '../services/api';

const timestamp = { createdAt: '2026-09-30T12:00:00Z', updatedAt: '2026-09-30T12:00:00Z' };
let clinic: Clinic;
let centers: Center[];
let specialties: Specialty[];
let session: Session;
let failure: { path: string; method: string; message: string } | null;
function response(data: unknown, status = 200) {
  return { ok: status >= 200 && status < 300, status, json: async () => data } as Response;
}
const fetchMock = vi.fn(async (input: string | URL | Request, init?: RequestInit) => {
  const path = new URL(String(input)).pathname;
  const method = init?.method ?? 'GET';
  const body = init?.body ? JSON.parse(String(init.body)) as Record<string, unknown> : {};
  if (path === '/api/auth/refresh') return response(session);
  if (path === '/api/admin/clinic/site-images/hero') return Response.json({ title: 'No hay portada.' }, { status: 404 });
  if (path === '/api/admin/clinic/social-networks') return response([]);
  if (path === '/api/admin/clinic/social-networks') return response([]);
  if (failure?.path === path && failure.method === method) return response({ title: failure.message }, 400);
  if (path === '/api/admin/clinic') {
    if (method === 'PUT') clinic = { ...clinic, ...body };
    return response(clinic);
  }
  if (path === '/api/admin/centers') {
    if (method === 'POST') {
      const created = { ...centers[0], ...body, id: 'center-new' } as Center;
      centers = [...centers, created];
      return response(created, 201);
    }
    return response(centers);
  }
  if (path === '/api/admin/centers/center-1' || path === '/api/admin/centers/center-1/status') {
    centers[0] = { ...centers[0], ...body };
    return response(centers[0], method === 'PATCH' ? 204 : 200);
  }
  if (path === '/api/admin/specialties') {
    if (method === 'POST') {
      const created = { ...specialties[0], ...body, id: 'specialty-new', doctorCount: 0 } as Specialty;
      specialties = [...specialties, created];
      return response(created, 201);
    }
    return response(specialties);
  }
  if (path === '/api/admin/specialties/specialty-1' || path === '/api/admin/specialties/specialty-1/status') {
    specialties[0] = { ...specialties[0], ...body };
    return response(specialties[0], method === 'PATCH' ? 204 : 200);
  }
  throw new Error(`Solicitud inesperada: ${method} ${path}`);
});
function mount(page: ReactElement, permissions: string[]) {
  session = {
    accessToken: 'test-access', expiresAt: '',
    user: { id: 'user-1', firstName: 'Ana', lastName: 'Prueba', email: 'ana@example.com', roles: [], permissions },
  };
  setSession(session);
  render(<AuthProvider>{page}</AuthProvider>);
}
function requestBody(path: string, method: string) {
  const call = fetchMock.mock.calls.find(([url, init]) => String(url).endsWith(path) && init?.method === method);
  expect(call).toBeDefined();
  return JSON.parse(String(call![1]!.body)) as Record<string, unknown>;
}
beforeEach(() => {
  setSession(null);
  failure = null;
  clinic = { ...timestamp, id: 'clinic-1', name: 'Clínica RAMÍREZ', legalName: 'Ramírez SRL', description: null, logoUrl: null, phone: '22223333', whatsapp: null, email: 'clinic@example.com', taxId: null, isActive: true };
  centers = [{ ...timestamp, id: 'center-1', clinicId: clinic.id, name: 'Sucursal Central', code: 'CENTRAL', description: null, address: 'Calle 1', city: 'La Paz', department: 'La Paz', country: 'Bolivia', phone: null, whatsapp: null, email: null, latitude: -16.5, longitude: -68.15, isMain: true, isActive: true }];
  specialties = [{ ...timestamp, id: 'specialty-1', name: 'Ortodoncia', code: 'ORTHODONTICS', description: 'Atención especializada', isActive: true, doctorCount: 2 }];
  fetchMock.mockClear();
  vi.stubGlobal('fetch', fetchMock);
});

describe('Clínica', () => {
  it('carga datos institucionales y contactos', async () => {
    mount(<ClinicPage />, ['clinic.view', 'clinic.update']);
    expect(await screen.findByDisplayValue('Clínica RAMÍREZ')).toBeTruthy();
    expect(screen.getByDisplayValue('clinic@example.com')).toBeTruthy();
    expect(screen.queryByLabelText('Sitio web')).toBeNull();
    expect(screen.getByDisplayValue('Ramírez SRL')).toBeTruthy();
  });
  it('actualiza datos mediante PUT y conserva estado', async () => {
    mount(<ClinicPage />, ['clinic.view', 'clinic.update']);
    const user = userEvent.setup();
    const name = await screen.findByLabelText('Nombre');
    await user.clear(name);
    await user.type(name, 'Clínica Renovada');
    await user.click(screen.getByRole('button', { name: 'Guardar clínica' }));
    expect(await screen.findByText('Datos de la clínica actualizados.')).toBeTruthy();
    expect(requestBody('/api/admin/clinic', 'PUT')).not.toHaveProperty('website');
    expect(requestBody('/api/admin/clinic', 'PUT')).toMatchObject({ name: 'Clínica Renovada', legalName: 'Ramírez SRL', email: 'clinic@example.com', isActive: true });
  });
  it('muestra error al cargar', async () => {
    failure = { path: '/api/admin/clinic', method: 'GET', message: 'Clínica no disponible.' };
    mount(<ClinicPage />, ['clinic.view', 'clinic.update']);
    expect((await screen.findByRole('alert')).textContent).toContain('Clínica no disponible.');
    expect(screen.queryByRole('button', { name: 'Guardar clínica' })).toBeNull();
  });
  it('conserva edición y muestra error del servidor al guardar', async () => {
    failure = { path: '/api/admin/clinic', method: 'PUT', message: 'No se pudo guardar la clínica.' };
    mount(<ClinicPage />, ['clinic.view', 'clinic.update']);
    const user = userEvent.setup();
    await user.type(await screen.findByLabelText('Nombre'), ' actualizada');
    await user.click(screen.getByRole('button', { name: 'Guardar clínica' }));
    expect((await screen.findByRole('alert')).textContent).toContain('No se pudo guardar la clínica.');
    expect(screen.getByDisplayValue('Clínica RAMÍREZ actualizada')).toBeTruthy();
    expect((screen.getByRole('button', { name: 'Guardar clínica' }) as HTMLButtonElement).disabled).toBe(false);
  });
  it('ofrece solo lectura sin clinic.update', async () => {
    mount(<ClinicPage />, ['clinic.view']);
    expect((await screen.findByLabelText('Nombre') as HTMLInputElement).disabled).toBe(true);
    expect(screen.queryByRole('button', { name: 'Guardar clínica' })).toBeNull();
  });
});

const catalogs = [
  { module: 'centers', title: 'Sucursales', page: <CentersPage />, singular: 'sucursal', stateLabel: 'Sucursal activa', name: 'Sucursal Central', newName: 'Sucursal Sur', newCode: 'SUR', id: 'center-1', saved: 'Sucursal guardada.', statusSaved: 'Estado de sucursal actualizado.', isCenter: true },
  { module: 'specialties', title: 'Especialidades', page: <SpecialtiesPage />, singular: 'especialidad', stateLabel: 'Especialidad activa', name: 'Ortodoncia', newName: 'Endodoncia', newCode: 'ENDODONTICS', id: 'specialty-1', saved: 'Especialidad guardada.', statusSaved: 'Estado de especialidad actualizado.', isCenter: false },
];

describe.each(catalogs)('$title', catalog => {
  const path = `/api/admin/${catalog.module}`;
  function permissions(...actions: string[]) { return ['view', ...actions].map(action => `${catalog.module}.${action}`); }
  function currentItem() { return catalog.isCenter ? centers[0] : specialties[0]; }
  it('lista registros y su estado', async () => {
    mount(catalog.page, permissions());
    expect(await screen.findByText(catalog.name)).toBeTruthy();
    expect(screen.getByText('Activo')).toBeTruthy();
    if (catalog.isCenter) expect((screen.getByRole('link', { name: 'Ver mapa' }) as HTMLAnchorElement).href).toBe('https://www.google.com/maps?q=-16.5,-68.15');
    else {
      expect(screen.getByText('Atención especializada')).toBeTruthy();
      expect(screen.getByRole('columnheader', { name: 'Doctores' })).toBeTruthy();
      expect(screen.getByRole('cell', { name: String(specialties[0].doctorCount) })).toBeTruthy();
    }
  });
  it('crea un registro activo sin permiso de cambio de estado', async () => {
    mount(catalog.page, permissions('create'));
    const user = userEvent.setup();
    await screen.findByText(catalog.name);
    await user.click(screen.getByRole('button', { name: `Crear ${catalog.singular}` }));
    expect(screen.queryByLabelText(catalog.stateLabel)).toBeNull();
    await user.type(screen.getByLabelText('Nombre'), catalog.newName);
    await user.type(screen.getByLabelText('Código'), catalog.newCode);
    if (catalog.isCenter) {
      await user.type(screen.getByLabelText('Dirección'), 'Calle Sur 2');
      expect(screen.queryByLabelText('Google Maps URL')).toBeNull();
      await user.type(screen.getByLabelText('Latitud'), '-16.5');
      await user.type(screen.getByLabelText('Longitud'), '-68.15');
    }
    await user.click(screen.getByRole('button', { name: `Guardar ${catalog.singular}` }));
    expect(await screen.findByText(catalog.saved)).toBeTruthy();
    expect(await screen.findByText(catalog.newName)).toBeTruthy();
    expect(requestBody(path, 'POST')).toMatchObject({ name: catalog.newName, code: catalog.newCode, isActive: true });
    if (catalog.isCenter) {
      expect(requestBody(path, 'POST')).toMatchObject({ latitude: -16.5, longitude: -68.15 });
      expect(requestBody(path, 'POST')).not.toHaveProperty('googleMapsUrl');
    }
  });
  it.each([true, false])('edita conservando isActive=%s sin permiso disable', async isActive => {
    currentItem().isActive = isActive;
    mount(catalog.page, permissions('update'));
    const user = userEvent.setup();
    await screen.findByText(catalog.name);
    expect(screen.queryByRole('button', { name: /^(Activar|Desactivar)$/ })).toBeNull();
    await user.click(screen.getByRole('button', { name: 'Editar' }));
    expect(screen.queryByLabelText(catalog.stateLabel)).toBeNull();
    await user.clear(screen.getByLabelText('Nombre'));
    await user.type(screen.getByLabelText('Nombre'), catalog.newName);
    await user.click(screen.getByRole('button', { name: `Guardar ${catalog.singular}` }));
    expect(await screen.findByText(catalog.saved)).toBeTruthy();
    expect(requestBody(`${path}/${catalog.id}`, 'PUT')).toMatchObject({ name: catalog.newName, isActive });
  });
  it.each([true, false])('cambia estado desde isActive=%s con disable', async isActive => {
    currentItem().isActive = isActive;
    mount(catalog.page, permissions('disable'));
    const user = userEvent.setup();
    await user.click(await screen.findByRole('button', { name: isActive ? 'Desactivar' : 'Activar' }));
    expect(await screen.findByText(catalog.statusSaved)).toBeTruthy();
    expect(requestBody(`${path}/${catalog.id}/status`, 'PATCH')).toEqual({ isActive: !isActive });
    expect(screen.getByText(isActive ? 'Inactivo' : 'Activo')).toBeTruthy();
  });
  it('permite cambiar estado en edición solo con disable', async () => {
    mount(catalog.page, permissions('update', 'disable'));
    const user = userEvent.setup();
    await user.click(await screen.findByRole('button', { name: 'Editar' }));
    expect((screen.getByLabelText(catalog.stateLabel) as HTMLInputElement).checked).toBe(true);
    await user.click(screen.getByLabelText(catalog.stateLabel));
    await user.click(screen.getByRole('button', { name: `Guardar ${catalog.singular}` }));
    await screen.findByText(catalog.saved);
    expect(requestBody(`${path}/${catalog.id}`, 'PUT').isActive).toBe(false);
  });
  it('oculta creación, edición y estado en solo lectura', async () => {
    mount(catalog.page, permissions());
    await screen.findByText(catalog.name);
    expect(screen.queryByRole('button', { name: `Crear ${catalog.singular}` })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Editar' })).toBeNull();
    expect(screen.queryByRole('button', { name: /^(Activar|Desactivar)$/ })).toBeNull();
  });
  it('informa errores de carga', async () => {
    failure = { path, method: 'GET', message: 'No se pudo obtener el listado.' };
    mount(catalog.page, permissions());
    expect((await screen.findByRole('alert')).textContent).toContain('No se pudo obtener el listado.');
  });
  it('mantiene formulario y muestra rechazo de la API', async () => {
    failure = { path: `${path}/${catalog.id}`, method: 'PUT', message: 'El código ya existe.' };
    mount(catalog.page, permissions('update'));
    const user = userEvent.setup();
    await user.click(await screen.findByRole('button', { name: 'Editar' }));
    await user.click(screen.getByRole('button', { name: `Guardar ${catalog.singular}` }));
    expect((await screen.findByRole('alert')).textContent).toContain('El código ya existe.');
    await waitFor(() => expect((screen.getByRole('button', { name: `Guardar ${catalog.singular}` }) as HTMLButtonElement).disabled).toBe(false));
    expect(screen.getByDisplayValue(catalog.name)).toBeTruthy();
  });
});
