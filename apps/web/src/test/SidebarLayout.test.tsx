import { expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import AdminLayout from '../layouts/AdminLayout';
vi.mock('../modules/auth/AuthProvider',()=>({useAuth:()=>({session:{user:{firstName:'Usuario',lastName:'Prueba',email:'visual@example.test',roles:['ADMINISTRATOR'],permissions:['users.view','clinic.view','appointments.view','payments.view']}}})}));
vi.mock('../modules/admin/clinic/ClinicLogo',()=>({default:()=> <span>Logo</span>}));
it.each(['Admin','Doctor','Cashier'])('%s renders exactly one sidebar account and logout',role=>{
 const {container}=render(<AdminLayout path={role==='Doctor'?'/doctor/appointments':role==='Cashier'?'/cashier/payments':'/clinic'} doctorPortal={role==='Doctor'} cashierPortal={role==='Cashier'}><p>Contenido</p></AdminLayout>);
 const sidebar=container.querySelector('aside')!;
 expect(sidebar.querySelectorAll('.account')).toHaveLength(1);
 expect(within(sidebar).getAllByText('visual@example.test')).toHaveLength(1);
 expect(within(sidebar).getAllByRole('button',{name:'Cerrar sesión'})).toHaveLength(1);
 expect(sidebar.querySelector('.brand')?.parentElement).toBe(sidebar);
 expect(sidebar.querySelector('.account')?.parentElement).toBe(sidebar);
 expect(sidebar.querySelector('nav.sidebar-navigation')).toBeTruthy();
 expect(screen.getByText('Contenido')).toBeTruthy();
});
