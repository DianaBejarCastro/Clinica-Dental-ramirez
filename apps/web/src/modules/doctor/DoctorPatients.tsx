import { useEffect, useState } from 'react';
import AdminLayout from '../../layouts/AdminLayout';
import { api } from '../../services/api';
import { documentLabel } from '../../components/IdentityDocumentFields';
import { usePermissions } from '../admin/shared';
import './doctor-patients.css';

type Patient = { id:string; firstName:string; lastName:string; documentType:string|null; documentNumber:string|null; documentCountryCode:string|null; phone:string|null };
type Results = { items:Patient[]; total:number; page:number; pageSize:number };
export default function DoctorPatients() {
  const can=usePermissions();
  const [search,setSearch]=useState(''),[query,setQuery]=useState(''),[page,setPage]=useState(1);
  const [data,setData]=useState<Results|null>(null),[loading,setLoading]=useState(true),[error,setError]=useState(''),[retry,setRetry]=useState(0);
  useEffect(()=>{const timer=window.setTimeout(()=>{setQuery(search.trim());setPage(1);},300);return()=>window.clearTimeout(timer);},[search]);
  useEffect(()=>{
    let live=true;setLoading(true);setData(null);setError('');
    api<Results>(`/api/doctor/me/patients?q=${encodeURIComponent(query)}&page=${page}&pageSize=20`)
      .then(next=>{if(live)setData(next);}).catch(e=>{if(live)setError(e.message);}).finally(()=>{if(live)setLoading(false);});
    return()=>{live=false;};
  },[query,page,retry]);
  const waiting=loading||search.trim()!==query;
  return <AdminLayout path="/doctor/patients" doctorPortal><div className="doctor-patients">
    <header><span className="doctor-patients-title-icon" aria-hidden="true"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7"><circle cx="9" cy="7" r="3"/><path d="M3 21v-3a6 6 0 0 1 12 0v3M16 4a3 3 0 0 1 0 6m2 4a5 5 0 0 1 3 4v3"/></svg></span><div><h1>Mis pacientes</h1><p>Consulta tus pacientes y accede rápidamente a su información clínica.</p></div></header>
    <section className="card" aria-label="Pacientes relacionados">
      <label className="doctor-patients-search">Buscar paciente<input type="search" value={search} maxLength={200} onChange={e=>setSearch(e.target.value)} placeholder="Nombre, apellido, documento o teléfono" autoComplete="off"/></label>
      {waiting&&<p role="status">Buscando pacientes…</p>}
      {!waiting&&error&&<div role="alert"><p>{error}</p><button className="secondary" onClick={()=>setRetry(x=>x+1)}>Reintentar</button></div>}
      {!waiting&&data&&<><p role="status">{data.total} {data.total===1?'paciente':'pacientes'}</p>
        {data.items.length===0?<div className="doctor-patients-empty"><h2>{query?'No se encontraron pacientes':'Aún no tienes pacientes relacionados'}</h2><p>{query?'Prueba con otro nombre, documento o teléfono.':'Aquí aparecerán los pacientes que tengan citas asignadas contigo.'}</p></div>:
        <ul className="doctor-patients-list">{data.items.map(patient=><li key={patient.id}>
          <span className="doctor-patients-avatar" aria-hidden="true">{[patient.firstName,patient.lastName].map(x=>x.trim().charAt(0)).join('').toLocaleUpperCase('es')}</span>
          <div className="doctor-patients-identity"><h2>{patient.firstName} {patient.lastName}</h2><span>{documentLabel(patient)||'Documento no registrado'}</span></div>
          <div className="doctor-patients-phone"><small>Teléfono</small><span>{patient.phone||'No registrado'}</span></div>
          {can('clinical_history.view')&&<a className="doctor-patients-history" href={`#/doctor/patient-history?patient=${encodeURIComponent(patient.id)}`}>Ver historial<span className="sr-only"> de {patient.firstName} {patient.lastName}</span></a>}
        </li>)}</ul>}
        {data.total>20&&<nav className="doctor-patients-pagination" aria-label="Páginas de pacientes"><button className="secondary" disabled={page===1} onClick={()=>setPage(p=>p-1)}>Anterior</button><span>Página {page} de {Math.ceil(data.total/20)}</span><button className="secondary" disabled={page*20>=data.total} onClick={()=>setPage(p=>p+1)}>Siguiente</button></nav>}
      </>}
    </section>
  </div></AdminLayout>;
}
