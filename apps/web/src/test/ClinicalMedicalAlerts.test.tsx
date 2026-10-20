import { afterEach, expect, it, vi } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import ClinicalMedicalAlerts from '../modules/doctor/clinical/ClinicalMedicalAlerts';
import ClinicalPatientHeader from '../modules/doctor/clinical/ClinicalPatientHeader';
import type { Review } from '../modules/doctor/clinical/types';
afterEach(cleanup);
const current:Review={id:'new',stateReviewId:null,reviewedAt:'2026-10-15T12:00:00Z',noKnownAllergies:false,notes:null,allergies:[{name:'Penicilina',reaction:'Urticaria',severity:null,isCurrentlyActive:true}],conditions:[{conditionName:'Asma',details:null,isCurrentlyActive:true}],medications:[{medicationName:'Losartán',dose:'10 mg',frequency:null,reason:null,isCurrentlyActive:true}]};
const old:Review={...current,id:'old',reviewedAt:'2024-01-01T12:00:00Z',noKnownAllergies:true,allergies:[],conditions:[],medications:[]};
const load=()=>Promise.resolve({medicalReviews:[old,current],episodes:[],events:[]});
it('shows current categories and keeps them visible while sorting historical versions',async()=>{
 render(<ClinicalMedicalAlerts review={current} loadHistory={load}/>);
 for(const name of ['Penicilina','Asma','Losartán'])expect(screen.getByText(name)).toBeTruthy();
 await userEvent.click(screen.getByRole('button',{name:/Ver historial/}));
 const history=await screen.findByRole('region',{name:'Historial de alergias'});
 expect(history.querySelectorAll('time')[0].dateTime).toBe(current.reviewedAt);
 expect(within(history).getByText(/confirmado por el paciente/)).toBeTruthy();
 expect(within(screen.getByRole('region',{name:'Alergias actuales'})).getByText('Penicilina')).toBeTruthy();
 await userEvent.click(screen.getByRole('button',{name:/Ocultar historial/}));
 expect(screen.queryByRole('region',{name:'Historial de alergias'})).toBeNull();
});
it('does not invent absence and supports empty history',async()=>{
 render(<ClinicalMedicalAlerts review={null} loadHistory={async()=>({medicalReviews:[],episodes:[],events:[]})}/>);
 expect(screen.getByText(/ausencia no confirmada/)).toBeTruthy();
 expect(screen.getByText('Sin enfermedades registradas.')).toBeTruthy();
 expect(screen.getByText('Sin medicamentos registrados.')).toBeTruthy();
 await userEvent.click(screen.getByRole('button',{name:/Ver historial/}));
 expect(await screen.findByText('No hay revisiones médicas registradas.')).toBeTruthy();
});
it('shows inactive historical entries without presenting them as current',async()=>{
 const inactive={...current,allergies:[{...current.allergies[0],isCurrentlyActive:false}]};
 render(<ClinicalMedicalAlerts review={inactive} loadHistory={async()=>({medicalReviews:[inactive],episodes:[],events:[]})}/>);
 expect(screen.queryByText('Penicilina')).toBeNull();
 await userEvent.click(screen.getByRole('button',{name:/Ver historial/}));
 expect(await screen.findByText('Penicilina')).toBeTruthy();
 expect(screen.getByText(/Inactivo/)).toBeTruthy();
});
it('handles loading and error without losing summary',async()=>{
 let reject!:(e:Error)=>void;
 render(<ClinicalMedicalAlerts review={current} loadHistory={()=>new Promise((_,r)=>{reject=r;})}/>);
 await userEvent.click(screen.getByRole('button',{name:/Ver historial/}));
 expect(screen.getByRole('status')).toBeTruthy();reject(new Error('network'));
 expect(await screen.findByRole('alert')).toBeTruthy();expect(screen.getByText('Penicilina')).toBeTruthy();
});
it('delegates updates to the existing workflow',async()=>{
 const update=vi.fn();render(<ClinicalMedicalAlerts review={null} loadHistory={load} onUpdate={update}/>);
 await userEvent.click(screen.getByRole('button',{name:'Revisar / agregar antecedentes'}));expect(update).toHaveBeenCalledOnce();
});
it.each(['O+','UNKNOWN',null])('shows patient identity and blood type %s',bloodType=>{
 render(<ClinicalPatientHeader patient={{id:'p',firstName:'Ana',lastName:'Prueba',age:30,gender:'F',ci:'123',phone:'700',email:null,bloodType}} appointment={{id:'a',date:'2026-10-15',startTime:'09:00',endTime:'10:00',reason:'Control',center:'Central',status:'CONFIRMED'}}/>);
 expect(screen.getByText('Ana Prueba')).toBeTruthy();expect(screen.getByText('Grupo sanguíneo')).toBeTruthy();
 expect(screen.getAllByText(bloodType==='UNKNOWN'?'Desconocido':bloodType||'No registrado').length).toBeGreaterThan(0);
 expect(screen.getByText('Control')).toBeTruthy();
});
