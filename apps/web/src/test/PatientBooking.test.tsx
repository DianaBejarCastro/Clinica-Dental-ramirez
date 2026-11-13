import { beforeEach, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import PatientBooking from '../modules/patient/PatientBooking';
import TeamSection from '../modules/public/TeamSection';
import App from '../App';
import { getSession, setSession } from '../services/api';
import { localToday } from '../modules/patient/PatientFields';
const doctor='12345678-1234-1234-1234-123456789abc';
let blocked=false,conflict=false,empty=false;const requests:{url:string;body?:Record<string,unknown>}[]=[];
const day=()=>localToday().slice(0,7)+'-28';
const slot=()=>({date:day(),startTime:'08:00:00',endTime:'08:45:00',centerId:'center',centerName:'Central'});
const appointment=()=>({id:'appointment',appointmentDate:day(),startTime:'08:00:00',endTime:'08:45:00',doctor:'Ana Pérez',center:'Central',status:'SCHEDULED'});
const mock=vi.fn(async(input:RequestInfo|URL,init?:RequestInit)=>{
 const url=String(input);requests.push({url,body:init?.body?JSON.parse(String(init.body)):undefined});
 if(url.includes('/refresh'))return getSession()?Response.json(getSession()):Response.json({}, {status:401});
 if(url.includes('/login')){const value=session('PATIENT');return Response.json(value);}
 if(url.includes('/api/clinic/team'))return Response.json([{id:doctor,firstName:'Ana',lastName:'Pérez',specialties:['Odontología'],photoVersion:null}]);
 if(url.includes('/availability'))return Response.json({doctor:{featuredDoctorId:doctor,name:'Ana Pérez',specialty:'Odontología',photoVersion:null},existingAppointment:blocked?appointment():null,slots:blocked||empty?[]:[slot()]});
 if(url.endsWith('/api/patient/appointments')&&init?.method==='POST')return conflict?Response.json({title:'Ese horario ya no está disponible.'},{status:409}):Response.json(appointment(),{status:201});
 if(url.includes('/dashboard'))return Response.json({clinicName:'Clínica',account:{firstName:'Paciente',lastName:'Prueba'},finance:{totalEstimated:0,totalPaid:0,pendingBalance:0,pendingTreatments:0},nextAppointment:null,recentPayments:[],recentAppointments:[]});
 return Response.json({});
});
function session(role:string){return {accessToken:'token',expiresAt:'2099-01-01',user:{id:'user',firstName:'Paciente',lastName:'Prueba',email:'p@example.test',roles:[role],permissions:[]}};}
beforeEach(()=>{blocked=false;conflict=false;empty=false;requests.length=0;setSession(session('PATIENT'));vi.stubGlobal('fetch',mock);window.location.hash='/patient/book?doctor='+doctor;});
async function choose(){await screen.findByRole('button',{name:day()});fireEvent.click(screen.getByRole('button',{name:day()}));fireEvent.click(screen.getByRole('button',{name:/08:00–08:45/}));}
it('calendar enables only backend dates and offers fixed slots, doctor and duration',async()=>{
 render(<PatientBooking featuredId={doctor}/>);await choose();expect(screen.getByRole('button',{name:localToday().slice(0,7)+'-01'}).hasAttribute('disabled')).toBe(true);
 expect(document.querySelector('input[type="time"],input[type="date"],select')).toBeNull();expect(screen.getByText('45 minutos')).toBeTruthy();expect(screen.getAllByText('Ana Pérez').length).toBeGreaterThan(0);expect(screen.getByLabelText('3. Motivo de consulta')).toBeTruthy();expect(screen.queryByRole('button',{name:/09:00/})).toBeNull();
});
it('confirms minimal request and displays success',async()=>{
 render(<PatientBooking featuredId={doctor}/>);await choose();fireEvent.change(screen.getByLabelText('3. Motivo de consulta'),{target:{value:'Revisión'}});fireEvent.click(screen.getByRole('button',{name:'Confirmar cita'}));await screen.findByText('Cita reservada correctamente.');const body=requests.find(x=>x.body)?.body;expect(body).toEqual({featuredDoctorId:doctor,date:day(),startTime:'08:00:00',centerId:'center',reason:'Revisión'});expect(screen.getByRole('link',{name:'Ir a Mis citas'})).toBeTruthy();
});
it('existing appointment blocks form and shows summary',async()=>{blocked=true;render(<PatientBooking featuredId={doctor}/>);await screen.findByText('Ya tienes una cita reservada.');expect(screen.getByText('Programada')).toBeTruthy();expect(screen.getByRole('link',{name:'Ver mi cita'})).toBeTruthy();expect(screen.queryByRole('button',{name:'Confirmar cita'})).toBeNull();});
it('conflict refreshes availability and clears stale choice',async()=>{conflict=true;render(<PatientBooking featuredId={doctor}/>);await choose();fireEvent.click(screen.getByRole('button',{name:'Confirmar cita'}));await screen.findByRole('alert');await waitFor(()=>expect(requests.filter(x=>x.url.includes('/availability'))).toHaveLength(2));expect(screen.getByRole('button',{name:'Confirmar cita'}).hasAttribute('disabled')).toBe(true);});
it('empty month has no selectable day',async()=>{empty=true;render(<PatientBooking featuredId={doctor}/>);await screen.findByRole('group',{name:'Días disponibles'});expect([...screen.getByRole('group').querySelectorAll('button')].every(b=>b.disabled)).toBe(true);});
it('anonymous card opens login preserving selected doctor',async()=>{setSession(null);render(<TeamSection/>);fireEvent.click(await screen.findByRole('button',{name:'Reservar cita con Ana Pérez'}));await waitFor(()=>expect(window.location.hash).toBe('#/login?bookingDoctor='+doctor));});
it('patient card opens selected doctor booking',async()=>{render(<TeamSection/>);fireEvent.click(await screen.findByRole('button',{name:'Reservar cita con Ana Pérez'}));await waitFor(()=>expect(window.location.hash).toBe('#/patient/book?doctor='+doctor));});
it('other role gets explanation without logout',async()=>{setSession(session('DOCTOR'));render(<TeamSection/>);fireEvent.click(await screen.findByRole('button',{name:'Reservar cita con Ana Pérez'}));expect((await screen.findByRole('alert')).textContent).toContain('solo para pacientes');expect(getSession()?.user.roles).toEqual(['DOCTOR']);});
it('patient login intent resumes booking without accepting an external return URL',async()=>{window.location.hash='/login?bookingDoctor='+doctor;render(<App/>);await screen.findByRole('heading',{name:'Reservar cita'});await waitFor(()=>expect(window.location.hash).toBe('#/patient/book?doctor='+doctor));});

it('anonymous user logs in and continues with the same doctor',async()=>{
 setSession(null);window.location.hash='/login?bookingDoctor='+doctor;render(<App/>);
 const email=await screen.findByLabelText('Email');fireEvent.change(email,{target:{value:'p@example.test'}});fireEvent.change(screen.getByLabelText('Contraseña'),{target:{value:'SafePassword123!'}});fireEvent.submit(email.closest('form')!);
 await screen.findByRole('heading',{name:'Reservar cita'});await waitFor(()=>expect(requests.some(r=>r.url.includes('featuredDoctorId='+doctor))).toBe(true));
});
