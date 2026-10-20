import { beforeEach, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import PasswordField from '../components/PasswordField';
import LoginPage from '../modules/auth/LoginPage';
const mocks=vi.hoisted(()=>({login:vi.fn()}));
vi.mock('../services/api',()=>({login:mocks.login,getSession:()=>null}));
vi.mock('../modules/admin/clinic/ClinicLogo',()=>({default:()=> <span>R</span>}));
vi.mock('../modules/auth/PatientRegisterPage',()=>({registrationCompleted:()=>false,clearRegistrationNotice:()=>{}}));
beforeEach(()=>{mocks.login.mockReset().mockResolvedValue(undefined);});
it('starts hidden and toggles without submitting or changing the value',async()=>{
 const submit=vi.fn(e=>e.preventDefault());render(<form onSubmit={submit}><PasswordField label="Contraseña" name="password" defaultValue="test-only"/></form>);
 const input=screen.getByLabelText('Contraseña') as HTMLInputElement;
 expect(input.type).toBe('password');await userEvent.click(screen.getByRole('button',{name:'Mostrar contraseña'}));
 expect(input.type).toBe('text');expect(input.value).toBe('test-only');
 await userEvent.click(screen.getByRole('button',{name:'Ocultar contraseña'}));expect(input.type).toBe('password');expect(submit).not.toHaveBeenCalled();
});
it('preserves validation disabled and associated error',()=>{
 render(<PasswordField label="Clave" required disabled minLength={12} maxLength={128} error="Revisa la clave"/>);
 const input=screen.getByLabelText('Clave') as HTMLInputElement;expect(input.required).toBe(true);expect(input.disabled).toBe(true);expect(input.minLength).toBe(12);
 expect(screen.getByRole('button').hasAttribute('disabled')).toBe(true);expect(input.getAttribute('aria-describedby')).toBe(screen.getByRole('alert').id);
});
it('login Enter submits the same credentials after visibility toggling',async()=>{
 render(<LoginPage/>);await userEvent.type(screen.getByLabelText('Email'),'test@example.test');await userEvent.type(screen.getByLabelText('Contraseña'),'test-only-password');
 await userEvent.click(screen.getByRole('button',{name:'Mostrar contraseña'}));expect(mocks.login).not.toHaveBeenCalled();
 await userEvent.click(screen.getByLabelText('Contraseña'));await userEvent.keyboard('{Enter}');expect(mocks.login).toHaveBeenCalledWith('test@example.test','test-only-password');
});
it('keeps home and registration destinations',()=>{
 render(<LoginPage/>);expect(screen.getByRole('link',{name:'Volver al inicio'}).getAttribute('href')).toBe('#/inicio');expect(screen.getByRole('link',{name:'Regístrate'}).getAttribute('href')).toBe('#/register');
});
