import { useState } from 'react';
import type { Catalogs, Detail } from './api';

export default function FindingsHistory({detail,catalogs,selected,editable,onEdit,onRemove}:{detail:Detail;catalogs:Catalogs;selected:number|null;editable:boolean;onEdit:(index:number)=>void;onRemove:(index:number)=>void}){
  const [search,setSearch]=useState(''),[onlySelected,setOnlySelected]=useState(false),[page,setPage]=useState(0);
  const rows=detail.findings.map((finding,index)=>({finding,index})).filter(({finding:f})=>{
    const tooth=catalogs.teeth.find(t=>t.id===f.dentalToothId);
    const name=catalogs.findings.find(t=>t.id===f.findingTypeId)?.name??'';
    return (!onlySelected||f.dentalToothId===selected)&&(`${tooth?.fdiCode} ${name}`).toLocaleLowerCase().includes(search.trim().toLocaleLowerCase());
  }).sort((a,b)=>(b.finding.createdAt?Date.parse(b.finding.createdAt):Infinity)-(a.finding.createdAt?Date.parse(a.finding.createdAt):Infinity)||a.index-b.index);
  const current=Math.min(page,Math.max(0,Math.ceil(rows.length/20)-1));
  return <section className="odo-history" aria-label="Hallazgos registrados">
    <h3>Hallazgos registrados</h3>
    <p className="muted">Datos de la versión consultada. Estado y profesional corresponden al examen.</p>
    <div className="odo-history-filters">
      <label>Buscar por diente o hallazgo<input type="search" value={search} onChange={e=>{setSearch(e.target.value);setPage(0);}}/></label>
      <label>Mostrar hallazgos<select value={onlySelected?'selected':'all'} onChange={e=>{setOnlySelected(e.target.value==='selected');setPage(0);}}><option value="all">Todos</option><option value="selected" disabled={selected===null}>Diente seleccionado</option></select></label>
    </div>
    <div className="odo-history-scroll" role="region" aria-label="Tabla de hallazgos" tabIndex={0}>
      <table><thead><tr>{['Fecha de registro','Diente','Superficie / zona','Hallazgo','Estado del examen','Profesional del examen','Acciones'].map(x=><th scope="col" key={x}>{x}</th>)}</tr></thead>
      <tbody>{rows.slice(current*20,current*20+20).map(({finding:f,index})=><tr key={f.id??index} className={f.dentalToothId===selected?'odo-history-selected':''}>
        <td data-label="Fecha de registro">{f.createdAt?<time dateTime={f.createdAt}>{new Date(f.createdAt).toLocaleString('es-BO',{day:'2-digit',month:'2-digit',year:'numeric',hour:'2-digit',minute:'2-digit'})}</time>:'Pendiente de guardar'}</td>
        <td data-label="Diente">{catalogs.teeth.find(t=>t.id===f.dentalToothId)?.fdiCode??'—'}</td>
        <td data-label="Superficie / zona">{f.surfaceIds.length?f.surfaceIds.map(id=>catalogs.surfaces.find(s=>s.id===id)?.name??'—').join(', '):'Pieza completa'}</td>
        <td data-label="Hallazgo"><strong>{catalogs.findings.find(t=>t.id===f.findingTypeId)?.name??'Hallazgo registrado'}</strong>{f.observation&&<p>{f.observation}</p>}</td>
        <td data-label="Estado del examen"><span className="odo-history-badge">{detail.exam.status==='DRAFT'?'Borrador':'Finalizado'}</span></td>
        <td data-label="Profesional del examen">{detail.exam.createdByDoctorName}</td>
        <td data-label="Acciones">{editable?<div className="actions"><button type="button" className="secondary" onClick={()=>onEdit(index)}>Editar</button><button type="button" className="secondary" onClick={()=>onRemove(index)}>Quitar</button></div>:<span>Solo lectura</span>}</td>
      </tr>)}</tbody></table>
      {!rows.length&&<p>No hay hallazgos para mostrar.</p>}
    </div>
    {rows.length>20&&<nav className="actions" aria-label="Paginación de hallazgos"><button type="button" className="secondary" disabled={!current} onClick={()=>setPage(current-1)}>Anterior</button><span>Página {current+1} de {Math.ceil(rows.length/20)}</span><button type="button" className="secondary" disabled={(current+1)*20>=rows.length} onClick={()=>setPage(current+1)}>Siguiente</button></nav>}
  </section>;
}
