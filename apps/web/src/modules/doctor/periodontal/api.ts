import { api } from '../../../services/api';
import type { Dentition, Exam, Tooth, FindingType, Detail as OdontogramDetail } from '../odontogram/api';
export type { Dentition, Exam, Tooth };
export interface Site { id:number; code:string; name:string; surfaceGroup:string; displayOrder:number; isActive:boolean }
export interface Measurement { dentalToothId:number; periodontalSiteId:number; probingDepthMm:number|null; gingivalMarginMm:number|null; bleedingOnProbing:boolean; plaque:boolean; suppuration:boolean; observation:string|null }
export interface ToothAssessment { dentalToothId:number; mobilityGrade:number|null; observation:string|null }
export const furcationLocations = [['BUCCAL','Vestibular'],['LINGUAL_PALATAL','Lingual / palatina'],['MESIAL','Mesial'],['DISTAL','Distal']] as const;
export type FurcationLocation = typeof furcationLocations[number][0];
export interface Furcation { dentalToothId:number; location:FurcationLocation; grade:number; observation:string|null }
export interface Detail { exam:Exam; measurements:Measurement[]; toothAssessments:ToothAssessment[]; furcations:Furcation[] }
export interface Catalogs { teeth:Tooth[]; sites:Site[] }
export interface Draft { revision:number; dentitionMode:Dentition; generalNotes:string|null; measurements:Measurement[]; toothAssessments:ToothAssessment[]; furcations:Furcation[] }
// Mirrors the established API limits; the server remains authoritative.
export const ranges={depth:{min:0,max:15},margin:{min:-10,max:20},grade:{min:0,max:3}};
export function cal(m:Measurement|undefined){return m?.probingDepthMm!=null&&m.gingivalMarginMm!=null?Math.round((m.probingDepthMm+m.gingivalMarginMm)*10)/10:null;}
export function measurement(tooth:number,site:number):Measurement{return {dentalToothId:tooth,periodontalSiteId:site,probingDepthMm:null,gingivalMarginMm:null,bleedingOnProbing:false,plaque:false,suppuration:false,observation:null};}
const root='/api/doctor';const path=(id:string)=>`${root}/periodontal-exams/${encodeURIComponent(id)}`;
export const periodontalApi={
 catalogs:async():Promise<Catalogs>=>{const [teeth,sites]=await Promise.all([api<Tooth[]>(root+'/dental-catalogs/teeth'),api<Site[]>(root+'/dental-catalogs/periodontal-sites')]);return {teeth,sites};},
 history:(patient:string)=>api<Exam[]>(`${root}/patients/${encodeURIComponent(patient)}/periodontal-exams`),
 get:(id:string)=>api<Detail>(path(id)),
 create:(patient:string,dentitionMode:Dentition,clinicalEncounterId:string|null)=>api<Detail>(`${root}/patients/${encodeURIComponent(patient)}/periodontal-exams`,'POST',{dentitionMode,clinicalDate:new Date().toISOString(),clinicalEncounterId,generalNotes:null}),
 save:(id:string,draft:Draft)=>api<void>(path(id),'PUT',{...draft,measurements:draft.measurements.map(({dentalToothId,periodontalSiteId,probingDepthMm,gingivalMarginMm,bleedingOnProbing,plaque,suppuration,observation})=>({dentalToothId,periodontalSiteId,probingDepthMm,gingivalMarginMm,bleedingOnProbing,plaque,suppuration,observation}))}),
 version:(id:string,clinicalEncounterId:string|null)=>api<Detail>(path(id)+'/new-version','POST',{clinicalDate:new Date().toISOString(),clinicalEncounterId}),
 finalize:(id:string,revision:number)=>api<void>(path(id)+'/finalize','POST',{revision}),
 missingTeeth:async(patient:string,date:string):Promise<number[]>=>{
  const exams=await api<Exam[]>(`${root}/patients/${encodeURIComponent(patient)}/odontograms`);
  const previous=exams.filter(e=>e.status==='FINALIZED'&&e.clinicalDate<=date).sort((a,b)=>b.clinicalDate.localeCompare(a.clinicalDate))[0];
  if(!previous)return [];
  const [detail,types]=await Promise.all([api<OdontogramDetail>(`${root}/odontograms/${previous.id}`),api<FindingType[]>(root+'/dental-catalogs/finding-types')]);
  const absent=types.find(t=>t.code==='MISSING_TOOTH');return absent?detail.findings.filter(f=>f.findingTypeId===absent.id).map(f=>f.dentalToothId):[];
 }
};
