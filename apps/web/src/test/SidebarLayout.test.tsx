import { expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import AdminLayout from '../layouts/AdminLayout';
const { logoutMock } = vi.hoisted(() => ({ logoutMock: vi.fn() }));
vi.mock('../modules/auth/AuthProvider',()=>({useAuth:()=>({session:{user:{firstName:'Usuario',lastName:'Prueba',email:'visual@example.test',roles:['ADMINISTRATOR'],permissions:['users.view','clinic.view','appointments.view','payments.view']}}})}));
vi.mock('../modules/admin/clinic/ClinicLogo',()=>({default:()=> <span>Logo</span>}));
vi.mock('../services/api', async importOriginal => ({ ...await importOriginal<typeof import('../services/api')>(), logout: logoutMock }));
it.each(['Admin','Doctor','Cashier'])('%s renders exactly one sidebar account and logout',role=>{
 const {container}=render(<AdminLayout path={role==='Doctor'?'/doctor/appointments':role==='Cashier'?'/cashier/payments':'/clinic'} doctorPortal={role==='Doctor'} cashierPortal={role==='Cashier'}><p>Contenido</p></AdminLayout>);
 const sidebar=container.querySelector('aside')!;
 expect(sidebar.querySelectorAll('.account')).toHaveLength(1);
 expect(within(sidebar).getAllByText('visual@example.test')).toHaveLength(1);
 expect(within(sidebar).getAllByRole('button',{name:'Cerrar sesión'})).toHaveLength(1);
 expect(sidebar.querySelector('.brand')?.parentElement).toBe(sidebar);
 expect(sidebar.querySelector('.account')?.parentElement).toBe(sidebar);
 expect(sidebar.querySelector('nav.sidebar-navigation')).toBeTruthy();
 expect(sidebar.querySelector('.dashboard-sidebar-waves')?.getAttribute('aria-hidden')).toBe('true');
 const activeLink=sidebar.querySelector('nav a[aria-current="page"]');
 expect(activeLink?.getAttribute('href')).toBe(`#${role==='Doctor'?'/doctor/appointments':role==='Cashier'?'/cashier/payments':'/clinic'}`);
 expect(activeLink?.querySelector('.sidebar-item-icon')).toBeTruthy();
 expect(screen.getByText('Contenido')).toBeTruthy();
});

it('preserves the working logout action and returns its button to idle',async()=>{
 logoutMock.mockReset().mockResolvedValue(undefined);
 render(<AdminLayout path="/clinic"><p>Contenido</p></AdminLayout>);
 const button=screen.getByRole('button',{name:'Cerrar sesión'});
 await userEvent.click(button);
 await waitFor(()=>expect(logoutMock).toHaveBeenCalledOnce());
 await waitFor(()=>expect((button as HTMLButtonElement).disabled).toBe(false));
 expect(screen.queryByRole('alert')).toBeNull();
});
