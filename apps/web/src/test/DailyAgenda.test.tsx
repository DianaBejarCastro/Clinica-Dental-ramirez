import { render,screen,fireEvent,cleanup } from '@testing-library/react';
import { afterEach,expect,it,vi } from 'vitest';
import DailyAgenda,{type Agenda,shiftDate,slotAvailable} from '../modules/admin/appointments/DailyAgenda';
import type {Appointment} from '../modules/admin/appointments/types';
import {readFileSync} from 'node:fs';
vi.mock('../components/ProfilePhoto',()=>({ProfileAvatar:({firstName,lastName}:{firstName:string;lastName:string})=><span aria-label="Avatar">{firstName[0]}{lastName[0]}</span>}));
afterEach(cleanup);
const doctor=(id:string)=>({id,userId:id,firstName:'Juan '+id,lastName:'Ramirez',specialties:['General']});
const appointment=(id:string,doctorId:string,startTime='08:00:00',endTime='08:30:00'):Appointment=>({id,doctorId,patientId:id,patientName:'Paciente '+id,centerId:'c',centerName:'Central',specialtyId:'s',appointmentDate:'2030-01-07',startTime,endTime,status:'CONFIRMED',reason:'Control',notes:null,createdByUserId:null,createdAt:'',updatedAt:''});
const agenda=():Agenda=>({date:'2030-01-07',slotMinutes:30,doctors:[doctor('a'),doctor('b')],workingIntervals:[{doctorId:'a',centerId:'c',centerName:'Central',startTime:'07:00:00',endTime:'19:00:00',breakStart:'12:00:00',breakEnd:'13:00:00'}],appointments:[]});

it('renders hours as rows and truthful empty availability including breaks',()=>{
 render(<DailyAgenda agenda={agenda()} onSelect={vi.fn()}/>);
 expect(screen.getByRole('region',{name:'Hora 07:00'})).toBeTruthy();
 expect(screen.getByRole('region',{name:'Hora 07:00'}).textContent).toContain('Horario disponible');
 expect(screen.getByRole('region',{name:'Hora 12:00'}).textContent).toContain('Sin disponibilidad');
 expect(document.querySelectorAll('.agenda-appointment')).toHaveLength(0);
 expect(document.querySelector('.agenda-doctor')).toBeNull();
});
it('groups simultaneous appointments into one row with patient doctor specialty and range',()=>{
 const data=agenda();data.appointments=[appointment('one','a'),appointment('two','b','08:00:00','09:00:00')];
 const select=vi.fn();render(<DailyAgenda agenda={data} onSelect={select}/>);
 const row=screen.getByRole('region',{name:'Hora 08:00'});
 expect(row.querySelectorAll('.agenda-appointment')).toHaveLength(2);
 expect(row.textContent).toContain('Juan a Ramirez');expect(row.textContent).toContain('Juan b Ramirez');
 expect(row.textContent).toContain('General');expect(row.textContent).toContain('08:00 – 09:00');
 expect(screen.getAllByLabelText('Avatar')).toHaveLength(2);
 fireEvent.click(screen.getByRole('button',{name:/Paciente one/}));expect(select).toHaveBeenCalledWith(data.appointments[0]);
});
it('does not report availability during a continuing appointment',()=>{
 const data=agenda();data.appointments=[appointment('one','a','08:00:00','09:30:00')];
 render(<DailyAgenda agenda={data} onSelect={vi.fn()}/>);
 expect(screen.getByRole('region',{name:'Hora 08:30'}).textContent).toContain('Citas en curso');
 expect(screen.getByRole('region',{name:'Hora 08:30'}).textContent).toContain('Sin disponibilidad');
});
it('does not treat reservations hidden by filters as free slots',()=>{
 const data=agenda();data.occupiedAppointments=[appointment('hidden','a','08:00:00','09:00:00')];
 expect(slotAvailable(data,480,510)).toBe(false);
 data.occupiedAppointments[0].status='CANCELLED';expect(slotAvailable(data,480,510)).toBe(true);
 data.occupiedAppointments[0].status='NO_SHOW';expect(slotAvailable(data,480,510)).toBe(true);
});
it('shows availability if at least one scheduled doctor is free',()=>{
 const data=agenda();data.workingIntervals.push({...data.workingIntervals[0],doctorId:'b'});
 data.appointments=[appointment('one','a','08:00:00','09:00:00')];
 expect(slotAvailable(data,480,510)).toBe(true);
});
it('counts only visible appointments and lists actual status names',()=>{
 const data=agenda();data.appointments=[appointment('one','a'),appointment('two','b')];
 render(<DailyAgenda agenda={data} onSelect={vi.fn()}/>);
 expect(document.querySelector('.agenda-count strong')?.textContent).toBe('2');
 expect(screen.getByLabelText('Estados de citas').textContent).toContain('No asistió');
 expect(screen.getByLabelText('Estados de citas').textContent).not.toContain('En espera');
});
it('preserves off-grid start times and stable ordering without overlapping cards',()=>{
 const data=agenda();data.appointments=[{...appointment('two','b','08:15:00','08:45:00'),doctorName:'Zulu'},{...appointment('one','a','08:15:00','09:15:00'),doctorName:'Ana'}];
 render(<DailyAgenda agenda={data} onSelect={vi.fn()}/>);
 const cards=screen.getByRole('region',{name:'Hora 08:15'}).querySelectorAll('button');
 expect(cards).toHaveLength(2);expect(cards[0].textContent).toContain('Ana');
 expect(cards[0].style.position).toBe('');
});
it('uses wrapping responsive cards and page scrolling without an internal scroll container',()=>{
 const css=readFileSync('src/modules/admin/appointments/daily-agenda.css','utf8');
 expect(css).toContain('flex-wrap:wrap');expect(css).toContain('@media(max-width:600px)');
 expect(css.split('.agenda-detail')[0]).not.toContain('overflow:auto');expect(css).not.toContain('75vh');
});
it('date navigation handles month and year boundaries without timezone shifts',()=>{
 expect(shiftDate('2030-01-01',-1)).toBe('2029-12-31');expect(shiftDate('2030-01-31',1)).toBe('2030-02-01');
});
