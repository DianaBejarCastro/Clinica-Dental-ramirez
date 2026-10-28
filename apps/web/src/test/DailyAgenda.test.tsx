import { render,screen,fireEvent,cleanup } from '@testing-library/react';
import { afterEach,expect,it,vi } from 'vitest';
import DailyAgenda,{type Agenda,shiftDate} from '../modules/admin/appointments/DailyAgenda';
import type {Appointment} from '../modules/admin/appointments/types';
import {readFileSync} from 'node:fs';
vi.mock('../components/ProfilePhoto',()=>({ProfileAvatar:({firstName,lastName}:{firstName:string;lastName:string})=><span aria-label="Avatar">{firstName[0]}{lastName[0]}</span>}));
afterEach(cleanup);
const doctor=(id:string)=>({id,userId:id,firstName:'Juan '+id,lastName:'Ramirez',specialties:['General']});
const appointment=(id:string,doctorId:string,startTime='08:00:00',endTime='08:30:00'):Appointment=>({id,doctorId,patientId:id,patientName:'Paciente '+id,centerId:'c',centerName:'Central',specialtyId:'s',appointmentDate:'2030-01-07',startTime,endTime,status:'CONFIRMED',reason:'Control',notes:null,createdByUserId:null,createdAt:'',updatedAt:''});
const agenda=():Agenda=>({date:'2030-01-07',slotMinutes:30,doctors:[doctor('a'),doctor('b')],workingIntervals:[{doctorId:'a',centerId:'c',centerName:'Central',startTime:'07:00:00',endTime:'19:00:00',breakStart:'12:00:00',breakEnd:'13:00:00'}],appointments:[]});
it('renders empty working hours and doctors even without appointments',()=>{
 render(<DailyAgenda agenda={agenda()} onSelect={vi.fn()}/>);
 expect(screen.getByText('07:00')).toBeTruthy();expect(screen.getByText('18:30')).toBeTruthy();
 expect(document.querySelectorAll('.agenda-doctor')).toHaveLength(2);
 expect(document.querySelectorAll('.agenda-appointment')).toHaveLength(0);
 expect(document.querySelectorAll('.agenda-working')).toHaveLength(2);
 expect(screen.getByLabelText('Horario de Juan b Ramirez').querySelector('.agenda-working')).toBeNull();
 expect(screen.getAllByLabelText('Avatar')).toHaveLength(2);
});
it('positions simultaneous appointments and proportional duration by doctor',()=>{
 const data=agenda();data.appointments=[appointment('one','a'),appointment('two','b','08:00:00','09:00:00')];
 const select=vi.fn();render(<DailyAgenda agenda={data} onSelect={select}/>);
 const one=screen.getByRole('button',{name:/Paciente one/}),two=screen.getByRole('button',{name:/Paciente two/});
 expect(one.style.top).toBe('144px');expect(one.style.height).toBe('72px');expect(two.style.height).toBe('144px');
 expect(one.closest('.agenda-day-column')?.getAttribute('aria-label')).toContain('Juan a');
 expect(two.closest('.agenda-day-column')?.getAttribute('aria-label')).toContain('Juan b');
 fireEvent.click(one);expect(select).toHaveBeenCalledWith(data.appointments[0]);
});
it('overlapping records stay separately accessible',()=>{
 const data=agenda();data.appointments=[appointment('one','a'),appointment('two','a')];
 render(<DailyAgenda agenda={data} onSelect={vi.fn()}/>);
 expect(screen.getByRole('button',{name:/Paciente one/}).style.left).not.toBe(screen.getByRole('button',{name:/Paciente two/}).style.left);
});
it('mobile selector chooses a single doctor and supports ten doctors',()=>{
 const data=agenda();data.doctors=Array.from({length:10},(_,i)=>doctor(String(i)));
 render(<DailyAgenda agenda={data} onSelect={vi.fn()}/>);
 fireEvent.change(screen.getByLabelText('Doctor de la agenda'),{target:{value:'9'}});
 expect(document.querySelector('.agenda-doctor.mobile-selected')?.textContent).toContain('Juan 9');
 expect(document.querySelectorAll('.agenda-doctor')).toHaveLength(10);
});
it('keeps scrolling local and headers/time sticky on tablet and mobile',()=>{
 const css=readFileSync('src/modules/admin/appointments/daily-agenda.css','utf8');
 expect(css).toContain('overflow:auto');expect(css).toContain('position:sticky');expect(css).toContain('contain:inline-size');
 expect(css).toContain('.agenda-doctor:not(.mobile-selected)');expect(css).toContain('grid-template-columns:52px minmax(0,1fr)');
});
it('date navigation handles month and year boundaries without timezone shifts',()=>{
 expect(shiftDate('2030-01-01',-1)).toBe('2029-12-31');expect(shiftDate('2030-01-31',1)).toBe('2030-02-01');
});
