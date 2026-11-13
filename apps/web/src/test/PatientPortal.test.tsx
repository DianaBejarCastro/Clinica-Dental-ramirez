import { beforeEach, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import PatientDashboard from '../modules/patient/PatientDashboard';
import { getSession, setSession } from '../services/api';
vi.mock('../modules/auth/AuthProvider',()=>({useAuth:()=>({session:getSession(),loading:false})}));
const appointment={id:'internal-appointment-id',appointmentDate:'2027-01-10',startTime:'09:00:00',endTime:'09:30:00',status:'CONFIRMED',doctor:'Doctor Uno',center:'Central',specialty:'Odontología',reason:'Revisión',createdAt:'2026-11-10T10:00:00Z'};
const payment={id:'internal-payment-id',concept:'Ortodoncia',amount:300,paidAt:'2026-11-10T10:00:00Z',method:'QR',reference:'REC-1',status:'PAID'};
const finance={totalEstimated:1000,totalPaid:300,pendingBalance:700,pendingTreatments:1};
let empty=false,fail=false,pending=false;const requests:string[]=[];
const fetchMock=vi.fn(async(input:RequestInfo|URL)=>{
 const url=new URL(String(input));requests.push(url.pathname+url.search);
 if(pending)return new Promise<Response>(()=>{});if(fail)return Response.json({title:'Acceso denegado.'},{status:403});
 const page=Number(url.searchParams.get('page')??1);const base={clinicName:'Clínica de prueba',page,pageSize:20,total:empty?0:25};
 if(url.pathname.endsWith('/dashboard'))return Response.json({clinicName:base.clinicName,account:{firstName:'Diana',lastName:'Bejar'},finance:empty?{totalEstimated:0,totalPaid:0,pendingBalance:0,pendingTreatments:0}:finance,nextAppointment:empty?null:appointment,recentAppointments:empty?[]:[appointment],recentPayments:empty?[]:[payment]});
 if(url.pathname.endsWith('/payments'))return Response.json({clinicName:base.clinicName,summary:empty?{...finance,pendingBalance:0}:finance,payments:{...base,items:empty?[]:[payment]},treatments:{...base,page:Number(url.searchParams.get('treatmentPage')??1),items:empty?[]:[{id:'internal-treatment-id',title:'Ortodoncia',date:payment.paidAt,total:1000,paid:300,balance:700,status:'PENDING'}]}});
 return Response.json({...base,items:empty?[]:[appointment]});
});
beforeEach(()=>{empty=false;fail=false;pending=false;requests.length=0;vi.stubGlobal('fetch',fetchMock);setSession({accessToken:'token',expiresAt:'2099-01-01',user:{id:'patient',firstName:'Diana',lastName:'Bejar',email:'p@test.example',roles:['PATIENT'],permissions:[]}});});
it('summary exposes only patient navigation and real financial totals',async()=>{
 render(<PatientDashboard/>);await screen.findByRole('heading',{name:'Diana Bejar'});
 expect(within(screen.getByRole('navigation',{name:'Portal del paciente'})).getAllByRole('link').map(x=>x.textContent)).toEqual(['Resumen','Mis citas','Mis pagos']);
 expect(screen.getByText('Clínica de prueba')).toBeTruthy();expect(screen.getByText('Total pagado')).toBeTruthy();expect(screen.getByText('Tratamientos con saldo pendiente')).toBeTruthy();expect(screen.getAllByText('Doctor Uno')).toHaveLength(2);expect(screen.getByText('REC-1',{exact:false})).toBeTruthy();
 expect(document.body.textContent).not.toContain('internal-');expect(screen.queryByText('Administración')).toBeNull();expect(requests).toEqual(['/api/patient/dashboard']);
});
it('summary has explicit empty states',async()=>{empty=true;render(<PatientDashboard/>);expect(await screen.findByText('No tienes próximas citas.')).toBeTruthy();expect(screen.getByText('Todavía no tienes pagos registrados.')).toBeTruthy();expect(screen.getByText('Todavía no tienes citas anteriores.')).toBeTruthy();});
it('appointments filter and pagination request server pages',async()=>{
 render(<PatientDashboard view="appointments"/>);await screen.findByText('Doctor Uno');expect(screen.getByText('Confirmada')).toBeTruthy();expect(screen.getByText(/Registrada el/)).toBeTruthy();
 fireEvent.click(screen.getByRole('button',{name:'Siguiente'}));await waitFor(()=>expect(requests.at(-1)).toContain('page=2'));
 fireEvent.change(screen.getByLabelText('Mostrar citas'),{target:{value:'history'}});await waitFor(()=>expect(requests.at(-1)).toContain('filter=history&page=1'));
 expect(screen.queryByRole('button',{name:/Cancelar cita|Editar/})).toBeNull();
});
it('payments show treatment balances and history without write controls',async()=>{
 render(<PatientDashboard view="payments"/>);await screen.findByRole('heading',{name:'Historial de pagos'});expect(screen.getAllByText('Ortodoncia')).toHaveLength(2);expect(screen.getByText('Pendiente')).toBeTruthy();expect(screen.getAllByText('Pagado')).toHaveLength(2);expect(screen.queryByRole('textbox')).toBeNull();expect(screen.queryByRole('button',{name:/Registrar pago|Eliminar|Anular/})).toBeNull();
 fireEvent.click(within(screen.getByRole('navigation',{name:'Paginación de pagos'})).getByRole('button',{name:'Siguiente'}));await waitFor(()=>expect(requests.at(-1)).toContain('page=2&treatmentPage=1'));
});
it('empty payments preserve zero-balance feedback',async()=>{empty=true;render(<PatientDashboard view="payments"/>);expect(await screen.findByText('No tienes saldos pendientes.')).toBeTruthy();expect(screen.getByText('Todavía no tienes pagos registrados.')).toBeTruthy();});
it('loading and denied access do not show stale financial data',async()=>{fail=true;render(<PatientDashboard/>);expect(await screen.findByRole('alert')).toBeTruthy();expect(screen.queryByText('Total pagado')).toBeNull();});
it('loading is accessible',()=>{pending=true;render(<PatientDashboard/>);expect(screen.getByRole('status').textContent).toContain('Cargando');});
