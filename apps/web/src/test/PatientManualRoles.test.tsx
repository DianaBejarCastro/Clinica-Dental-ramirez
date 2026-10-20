import { beforeEach, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import App from '../App';
import { setSession } from '../services/api';

const fetchMock = vi.fn(async (input: string | URL | Request) => {
  const path = new URL(String(input)).pathname;
  if (path.endsWith('/refresh')) return Response.json({ accessToken: 'token', user: { id: 'admin', firstName: 'Admin', lastName: 'Test', email: 'admin@example.test', roles: [], permissions: ['users.view', 'users.create', 'users.update', 'roles.view', 'roles.assign_permissions'] } });
  if (path === '/api/admin/users') return Response.json({ items: [{ id: 'account', firstName: 'Ana', lastName: 'Test', email: 'ana@example.test', isActive: true, roles: [{ id: 'patient', name: 'Paciente' }] }], total: 1 });
  if (path === '/api/admin/roles') return Response.json([
    { id: 'patient', code: 'PATIENT', name: 'Paciente', isActive: true },
    { id: 'assistant', code: 'DENTAL_ASSISTANT', name: 'Asistente dental', isActive: true },
    { id: 'doctor', code: 'DOCTOR', name: 'Doctor', isActive: true },
    { id: 'secretary', code: 'SECRETARY', name: 'Secretaría', isActive: true },
  ]);
  return new Response(null, { status: 204 });
});
beforeEach(() => { setSession(null); window.location.hash = '/users'; fetchMock.mockClear(); vi.stubGlobal('fetch', fetchMock); });

it('Usuarios deja el alta a los modulos correspondientes', async () => {
  render(<App />); await screen.findByText('Ana Test');
  expect(screen.queryByRole('button', { name: 'Crear usuario' })).toBeNull();
  expect(screen.queryByLabelText('Contraseña inicial')).toBeNull();
});

it('asignación manual omite PATIENT existente y no lo envía al guardar', async () => {
  render(<App />); await userEvent.click(await screen.findByRole('button', { name: 'Asignar roles' }));
  await screen.findByRole('checkbox', { name: 'Secretaría' });
  expect(screen.queryByRole('checkbox', { name: 'Paciente' })).toBeNull();
  expect(screen.queryByRole('checkbox', { name: 'Doctor' })).toBeNull();
  expect(screen.queryByRole('checkbox', { name: 'Asistente dental' })).toBeNull();
  await userEvent.click(screen.getByRole('button', { name: 'Guardar roles' }));
  const calls = vi.mocked(fetch).mock.calls;
  const write = calls.find(([, init]) => init?.method === 'PUT');
  expect(JSON.parse(String(write?.[1]?.body))).toEqual({ roleIds: [] });
});
