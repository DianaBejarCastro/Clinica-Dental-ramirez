import { useEffect, useState } from 'react';
import type { Catalogs, Finding, Tooth } from './api';
export default function ToothFindingsPanel({tooth,catalogs,findings,surfaces,onSurfaces,onChange,editable,editIndex,onEditDone}:{editIndex:number|null;onEditDone:()=>void;tooth:Tooth;catalogs:Catalogs;findings:Finding[];surfaces:number[];onSurfaces:(ids:number[])=>void;onChange:(findings:Finding[])=>void;editable:boolean}) {
  const [search,setSearch]=useState(''),[typeId,setTypeId]=useState(''),[whole,setWhole]=useState(false),[observation,setObservation]=useState(''),[editing,setEditing]=useState<number|null>(null),[error,setError]=useState('');
  const type=catalogs.findings.find(x=>x.id===Number(typeId));
  const options=catalogs.findings.filter(x=>x.isActive&&(x.name.toLocaleLowerCase().includes(search.toLocaleLowerCase())||x.code.toLowerCase().includes(search.toLowerCase())||x.id===Number(typeId)));
  useEffect(()=>{const f=editIndex===null?null:findings[editIndex];if(f){setEditing(editIndex);setTypeId(String(f.findingTypeId));setWhole(!f.surfaceIds.length);setObservation(f.observation??'');setError('');}},[editIndex]);
  function reset(){onEditDone();setEditing(null);setObservation('');setTypeId('');setSearch('');setWhole(false);onSurfaces([]);setError('');}
  function apply(){
    if(!type){setError('Selecciona un hallazgo.');return;}
    const ids=whole?[]:surfaces;
    if(!whole&&!ids.length){setError('Selecciona al menos una superficie.');return;}
    const next:Finding={...(editing===null?{}:findings[editing]),dentalToothId:tooth.id,findingTypeId:type.id,surfaceIds:ids,observation:observation.trim()||null};
    const rest=findings.filter((_,i)=>i!==editing);
    if(rest.some(f=>f.dentalToothId===tooth.id&&f.findingTypeId===type.id&&[...f.surfaceIds].sort().join() === [...ids].sort().join())){setError('Este hallazgo ya está registrado con esas superficies.');return;}
    onChange(editing===null?[...findings,next]:findings.map((f,i)=>i===editing?next:f));reset();
  }
  return <div role="complementary" className="odo-editor" aria-label={`Hallazgos del diente ${tooth.fdiCode}`}><h3>Diente {tooth.fdiCode}</h3><p className="muted">{tooth.dentitionClass==='PRIMARY'?'Dentición temporal':'Dentición permanente'}</p>
    {editable&&<form onSubmit={e=>{e.preventDefault();apply();}}><h4>{editing===null?'Agregar hallazgo':'Editar hallazgo'}</h4>
      <label>Buscar hallazgo<input type="search" value={search} onChange={e=>setSearch(e.target.value)} placeholder="Caries, endodoncia…"/></label>
      <label>Hallazgo<select value={typeId} onChange={e=>{setTypeId(e.target.value);const t=catalogs.findings.find(x=>x.id===Number(e.target.value));setWhole(!!t?.appliesToWholeTooth&&!t.appliesToSurface);setError('');}}><option value="">Seleccionar hallazgo</option>{[...new Set(options.map(x=>x.category))].map(category=><optgroup key={category} label={category}>{options.filter(x=>x.category===category).sort((a,b)=>a.displayOrder-b.displayOrder).map(x=><option key={x.id} value={x.id}>{x.name}</option>)}</optgroup>)}</select></label>
      {type?.appliesToWholeTooth&&type.appliesToSurface&&<label><input type="checkbox" checked={whole} onChange={e=>setWhole(e.target.checked)}/> Afecta a la pieza completa</label>}
      {whole?<p>Este hallazgo se registra en la pieza completa, sin superficies ficticias.</p>:<fieldset className="odo-surface-options"><legend>Superficies · selección múltiple</legend>{catalogs.surfaces.filter(x=>x.isActive).map(s=><label key={s.id}><input type="checkbox" checked={surfaces.includes(s.id)} onChange={()=>onSurfaces(surfaces.includes(s.id)?surfaces.filter(id=>id!==s.id):[...surfaces,s.id])}/>{s.name}</label>)}</fieldset>}
      <label>Observación del hallazgo<textarea value={observation} onChange={e=>setObservation(e.target.value)} maxLength={4000} rows={3}/></label>
      {error&&<p role="alert">{error}</p>}<button type="submit">{editing===null?'Agregar hallazgo':'Aplicar cambio'}</button>{editing!==null&&<button type="button" className="secondary" onClick={reset}>Cancelar edición</button>}
    </form>}
  </div>;
}
