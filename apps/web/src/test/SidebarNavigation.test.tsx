import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { expect, it } from 'vitest';
import SidebarNavigation from '../layouts/SidebarNavigation';
import { navigationGroups } from '../layouts/navigation';

const permissions = navigationGroups.flatMap(group => group.items.map(item => item.permission!));

it('abrir otro grupo cierra el anterior incluso si contiene la ruta activa', async () => {
  render(<SidebarNavigation path="/clinic" permissions={permissions} />);
  const user = userEvent.setup();
  for (const label of ['Equipo médico', 'Atención', 'Administración', 'Clínica']) {
    await user.click(screen.getByRole('button', { name: label }));
    expect(screen.getAllByRole('button', { expanded: true })).toHaveLength(1);
    expect(screen.getByRole('button', { expanded: true }).textContent).toBe(label);
  }
});
it('navegar a un hijo abre su grupo y reemplaza la apertura manual', async () => {
  const { rerender } = render(<SidebarNavigation path="/clinic" permissions={permissions} />);
  await userEvent.click(screen.getByRole('button', { name: 'Atención' }));
  rerender(<SidebarNavigation path="/doctors" permissions={permissions} />);
  expect(screen.getAllByRole('button', { expanded: true })).toHaveLength(1);
  expect(screen.getByRole('button', { expanded: true }).textContent).toBe('Equipo médico');
  expect(screen.getByRole('link', { name: 'Doctores' }).getAttribute('aria-current')).toBe('page');
});
it.each(['/', '/dashboard'])('volver a %s cierra todos los grupos', async path => {
  const { rerender } = render(<SidebarNavigation path="/clinic" permissions={permissions} />);
  await userEvent.click(screen.getByRole('button', { name: 'Equipo médico' }));
  rerender(<SidebarNavigation path={path} permissions={permissions} />);
  expect(screen.queryAllByRole('button', { expanded: true })).toHaveLength(0);
});

it('muestra Resumen y los cuatro grupos en el orden solicitado', () => {
  render(<SidebarNavigation path="/" permissions={permissions} />);
  expect(screen.getAllByRole('button').map(button => button.textContent)).toEqual(['Administración', 'Clínica', 'Equipo médico', 'Atención']);
  expect(screen.getAllByRole('link').map(link => link.textContent)).toEqual(['Resumen']);
  expect(screen.getByRole('navigation').getAttribute('aria-label')).toBe('Navegación principal');
});
it('expande y contrae cada grupo con controles accesibles', async () => {
  render(<SidebarNavigation path="/" permissions={permissions} />); const user = userEvent.setup();
  for (const group of navigationGroups) {
    const toggle = screen.getByRole('button', { name: group.label });
    expect(toggle.getAttribute('aria-expanded')).toBe('false');
    await user.click(toggle);
    expect(toggle.getAttribute('aria-expanded')).toBe('true');
    const panel = document.getElementById(toggle.getAttribute('aria-controls')!)!;
    expect(panel.hasAttribute('inert')).toBe(false);
    expect(within(panel).getAllByRole('link').map(link => link.textContent)).toEqual(group.items.map(item => item.label));
    await user.click(toggle);
    expect(toggle.getAttribute('aria-expanded')).toBe('false'); expect(panel.hasAttribute('inert')).toBe(true);
    expect(within(panel).queryAllByRole('link')).toHaveLength(0);
  }
});
it.each(navigationGroups.flatMap(group => group.items.map(item => [group.label, item.label, item.route])))('abre %s para la ruta activa %s', (group, label, path) => {
  render(<SidebarNavigation path={path} permissions={permissions} />);
  expect(screen.getByRole('button', { name: group }).getAttribute('aria-expanded')).toBe('true');
  expect(screen.getByRole('link', { name: label }).getAttribute('aria-current')).toBe('page');
  expect(screen.getByRole('link', { name: label }).getAttribute('href')).toBe(`#${path}`);
  expect(screen.getByRole('link', { name: 'Resumen' }).hasAttribute('aria-current')).toBe(false);
});
it('mantiene abierto el grupo activo y abre otro al cambiar ruta externamente', async () => {
  const { rerender } = render(<SidebarNavigation path="/patients" permissions={permissions} />);
  const toggle = screen.getByRole('button', { name: 'Atención' });
  await userEvent.click(toggle); expect(toggle.getAttribute('aria-expanded')).toBe('true');
  expect(toggle.getAttribute('aria-disabled')).toBe('true');
  rerender(<SidebarNavigation path="/users" permissions={permissions} />);
  expect(screen.getByRole('button', { name: 'Administración' }).getAttribute('aria-expanded')).toBe('true');
  expect(screen.getByRole('link', { name: 'Usuarios' }).getAttribute('aria-current')).toBe('page');
  expect(toggle.getAttribute('aria-expanded')).toBe('false');
});
it('oculta items sin permiso aunque su grupo esté abierto', () => {
  render(<SidebarNavigation path="/roles" permissions={['roles.view']} />);
  expect(screen.getByRole('link', { name: 'Roles' })).toBeTruthy();
  expect(screen.queryByRole('link', { name: 'Usuarios', hidden: true })).toBeNull();
  expect(screen.queryByRole('link', { name: 'Permisos', hidden: true })).toBeNull();
  expect(screen.getAllByRole('button')).toHaveLength(1);
});
it('no renderiza grupos vacíos y reacciona a cambios de permisos', () => {
  const { rerender } = render(<SidebarNavigation path="/patients" permissions={['patients.view']} />);
  expect(screen.getAllByRole('button').map(button => button.textContent)).toEqual(['Atención']);
  rerender(<SidebarNavigation path="/patients" permissions={[]} />);
  expect(screen.queryAllByRole('button')).toHaveLength(0);
  expect(screen.queryByRole('link', { name: 'Pacientes', hidden: true })).toBeNull();
  expect(screen.getByRole('link', { name: 'Resumen' })).toBeTruthy();
});
it('permite operar con Tab, Enter y Espacio y conserva enlaces nativos', async () => {
  render(<SidebarNavigation path="/" permissions={['users.view', 'roles.view']} />); const user = userEvent.setup();
  await user.tab(); expect(document.activeElement).toBe(screen.getByRole('link', { name: 'Resumen' }));
  await user.tab(); const toggle = screen.getByRole('button', { name: 'Administración' }); expect(document.activeElement).toBe(toggle);
  await user.keyboard('{Enter}'); expect(toggle.getAttribute('aria-expanded')).toBe('true');
  await user.tab(); expect(document.activeElement).toBe(screen.getByRole('link', { name: 'Usuarios' }));
  expect((document.activeElement as HTMLAnchorElement).getAttribute('href')).toBe('#/users');
  await user.tab({ shift: true }); await user.keyboard(' '); expect(toggle.getAttribute('aria-expanded')).toBe('false');
});
