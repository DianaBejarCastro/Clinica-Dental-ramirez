import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import DoctorPatients from '../modules/doctor/DoctorPatients';
import { getSession, setSession } from '../services/api';
vi.mock('../modules/auth/AuthProvider',()=>({useAuth:()=>({session:getSession(),loading:false})}));
const patient={id:'7f8e3352-02db-46f5-8bcc-f325b680800a',firstName:'Diana',lastName:'Bejar',documentType:'PASSPORT',documentNumber:'AB123456',documentCountryCode:'BO',phone:'65700345'};
let total=1,empty=false,fail=false,pending=false;
const mock=vi.fn(async(input:RequestInfo|URL)=>{
 if(!String(input).includes('/api/doctor/me/patients'))return new Response('{}');
 if(pending)return new Promise<Response>(()=>{});
 if(fail)return new Response(JSON.stringify({title:'Paciente no disponible.'}),{status:403});
 return new Response(JSON.stringify({items:empty?[]:[patient],total:empty?0:total,page:1,pageSize:20}));
});
beforeEach(()=>{total=1;empty=false;fail=false;pending=false;mock.mockClear();vi.stubGlobal('fetch',mock);setSession({accessToken:'token',expiresAt:'2099-01-01',user:{id:'doctor',firstName:'Juan',lastName:'Ramírez',email:'d@test.example',roles:['DOCTOR'],permissions:['patients.view','clinical_history.view','inventory.view']}});});
afterEach(()=>{cleanup();setSession(null);vi.unstubAllGlobals();});
it('shows sidebar in order, initials, document, phone and a scoped history link without visible UUIDs',async()=>{
 render(<DoctorPatients/>);await screen.findByRole('heading',{name:'Diana Bejar'});
 const nav=screen.getByRole('navigation',{name:'Navegación principal'});
 expect(within(nav).getAllByRole('link').map(x=>x.textContent)).toEqual(['Mis citas','Mis pacientes','Inventario']);
 expect(screen.getByText('DB')).toBeTruthy();expect(screen.getByText(/AB123456/).textContent).toMatch(/Pasaporte/i);expect(screen.getByText('65700345')).toBeTruthy();
 expect(screen.getByRole('link',{name:'Ver historial de Diana Bejar'}).getAttribute('href')).toContain(patient.id);
 expect(document.body.textContent).not.toContain(patient.id);
});
it.each(['Diana','Bejar','123456','65700345'])('debounces remote partial search for %s',async(term)=>{
 render(<DoctorPatients/>);await screen.findByText('65700345');const before=mock.mock.calls.length;
 fireEvent.change(screen.getByRole('searchbox'),{target:{value:term}});
 expect(mock.mock.calls).toHaveLength(before);
 await waitFor(()=>expect(mock.mock.calls.some(([url])=>String(url).includes('q='+term))).toBe(true));
 expect(mock.mock.calls).toHaveLength(before+1);
});
it('paginates remotely and resets page after searching',async()=>{
 total=45;render(<DoctorPatients/>);await screen.findByText('65700345');await userEvent.click(screen.getByRole('button',{name:'Siguiente'}));
 await waitFor(()=>expect(mock.mock.calls.some(([url])=>String(url).includes('page=2'))).toBe(true));
 fireEvent.change(screen.getByRole('searchbox'),{target:{value:'Ana'}});
 await waitFor(()=>expect(mock.mock.calls.some(([url])=>String(url).includes('q=Ana&page=1'))).toBe(true));
});
it('distinguishes empty related patients from no search results',async()=>{
 empty=true;render(<DoctorPatients/>);expect(await screen.findByText('Aún no tienes pacientes relacionados')).toBeTruthy();
 fireEvent.change(screen.getByRole('searchbox'),{target:{value:'missing'}});expect(await screen.findByText('No se encontraron pacientes')).toBeTruthy();
});
it('shows loading without old results',()=>{pending=true;render(<DoctorPatients/>);expect(screen.getByRole('status').textContent).toBe('Buscando pacientes…');expect(screen.queryByText('Diana Bejar')).toBeNull();});
it('handles backend rejection and retries',async()=>{fail=true;render(<DoctorPatients/>);expect(await screen.findByRole('alert')).toBeTruthy();expect(screen.queryByText('Diana Bejar')).toBeNull();fail=false;await userEvent.click(screen.getByRole('button',{name:'Reintentar'}));expect(await screen.findByText('Diana Bejar')).toBeTruthy();});
it('does not offer clinical access without existing permission',async()=>{getSession()!.user.permissions=['patients.view'];render(<DoctorPatients/>);await screen.findByText('Diana Bejar');expect(screen.queryByRole('link',{name:/Ver historial/})).toBeNull();});
