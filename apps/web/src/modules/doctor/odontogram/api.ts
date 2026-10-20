import { api } from '../../../services/api';
export type Dentition = 'PERMANENT' | 'PRIMARY' | 'MIXED';
export interface Tooth { id:number; fdiCode:number; dentitionClass:Exclude<Dentition,'MIXED'>; quadrant:number; toothType:string; displayOrder:number; isActive:boolean }
export interface Surface { id:number; code:string; name:string; displayOrder:number; isActive:boolean }
export interface FindingType { id:number; code:string; name:string; category:string; appliesToSurface:boolean; appliesToWholeTooth:boolean; displayOrder:number; isActive:boolean }
export interface Finding { id?:string; dentalToothId:number; findingTypeId:number; surfaceIds:number[]; observation:string|null }
export interface Exam { id:string; patientProfileId:string; clinicalEncounterId:string|null; clinicalDate:string; dentitionMode:Dentition; status:'DRAFT'|'FINALIZED'; createdByDoctorName:string; previousExamId:string|null; isLatest:boolean; revision:number; generalNotes:string|null; finalizedAt:string|null; finalizedByDoctorProfileId:string|null }
export interface Detail { exam:Exam; findings:Finding[] }
export interface Catalogs { teeth:Tooth[]; surfaces:Surface[]; findings:FindingType[] }
export interface Draft { revision:number; dentitionMode:Dentition; generalNotes:string|null; findings:Finding[] }
const root='/api/doctor';
const path=(id:string)=>`${root}/odontograms/${encodeURIComponent(id)}`;
export const odontogramApi={
  catalogs:async():Promise<Catalogs>=>{const [teeth,surfaces,findings]=await Promise.all([api<Tooth[]>(root+'/dental-catalogs/teeth'),api<Surface[]>(root+'/dental-catalogs/surfaces'),api<FindingType[]>(root+'/dental-catalogs/finding-types')]);return {teeth,surfaces,findings};},
  history:(patient:string)=>api<Exam[]>(`${root}/patients/${encodeURIComponent(patient)}/odontograms`),
  get:(id:string)=>api<Detail>(path(id)),
  create:(patient:string,dentitionMode:Dentition,clinicalEncounterId:string|null)=>api<Detail>(`${root}/patients/${encodeURIComponent(patient)}/odontograms`,'POST',{dentitionMode,clinicalDate:new Date().toISOString(),clinicalEncounterId,generalNotes:null}),
  save:(id:string,draft:Draft)=>api<void>(path(id),'PUT',{...draft,findings:draft.findings.map(({dentalToothId,findingTypeId,surfaceIds,observation})=>({dentalToothId,findingTypeId,surfaceIds,observation}))}),
  version:(id:string,clinicalEncounterId:string|null)=>api<Detail>(path(id)+'/new-version','POST',{clinicalDate:new Date().toISOString(),clinicalEncounterId}),
  finalize:(id:string,revision:number)=>api<void>(path(id)+'/finalize','POST',{revision}),
};
export const clinicalDate=(date:string)=>new Date(date).toLocaleDateString('es-BO',{day:'2-digit',month:'2-digit',year:'numeric'});
