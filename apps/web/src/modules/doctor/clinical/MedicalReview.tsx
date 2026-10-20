import type { Review, ReviewInput } from './types';
export function MedicalSummary({review}:{review:Review|null}) {
  if(!review)return <p>Antecedentes médicos aún no registrados.</p>;
  return <div className="clinical-summary"><p>Revisado: {new Date(review.reviewedAt).toLocaleString('es-BO')}</p>
    <h3>Alergias activas</h3>{review.noKnownAllergies?<p>Sin alergias conocidas, confirmado por el paciente.</p>:review.allergies.some(x=>x.isCurrentlyActive)?<ul>{review.allergies.filter(x=>x.isCurrentlyActive).map((x,i)=><li key={i}>{x.name} {x.reaction} {x.severity}</li>)}</ul>:<p>Sin entradas de alergias activas; ausencia no confirmada.</p>}
    <h3>Enfermedades activas</h3><ul>{review.conditions.filter(x=>x.isCurrentlyActive).map((x,i)=><li key={i}>{x.conditionName} {x.details}</li>)}</ul>
    <h3>Medicamentos activos</h3><ul>{review.medications.filter(x=>x.isCurrentlyActive).map((x,i)=><li key={i}>{x.medicationName} {x.dose} {x.frequency} {x.reason}</li>)}</ul>{review.notes&&<p>{review.notes}</p>}
  </div>;
}
export function initialReview(review:Review|null):ReviewInput {return {noChanges:!!review,expectedReviewId:review?.id??null,noKnownAllergies:false,notes:null,allergies:[],conditions:[],medications:[]};}
export default function MedicalReview({latest,value,onChange,disabled=false}:{latest:Review|null;value:ReviewInput;onChange:(value:ReviewInput)=>void;disabled?:boolean}){
  function update(){onChange({...value,noChanges:false,...(value.noChanges&&latest?{noKnownAllergies:latest.noKnownAllergies,allergies:latest.allergies.map(x=>({...x})),conditions:latest.conditions.map(x=>({...x})),medications:latest.medications.map(x=>({...x}))}:{})});}
  function entries(kind:'allergies'|'conditions'|'medications',fields:[string,string][],title:string){
    const rows=value[kind] as unknown as Record<string,string|boolean|null>[];
    return <fieldset><legend>{title}</legend>{rows.map((row,index)=><div className="clinical-entry" key={index}>{fields.map(([key,label],i)=><label key={key}>{label}<input aria-label={`${label} ${index+1}`} value={String(row[key]??'')} required={i===0} maxLength={i===0?200:8000} onChange={e=>onChange({...value,[kind]:rows.map((r,n)=>n===index?{...r,[key]:e.target.value}:r)})}/></label>)}
      <label><input type="checkbox" checked={!!row.isCurrentlyActive} onChange={e=>onChange({...value,[kind]:rows.map((r,n)=>n===index?{...r,isCurrentlyActive:e.target.checked}:r)})}/>Actualmente activo</label><button type="button" className="secondary" onClick={()=>onChange({...value,[kind]:rows.filter((_,n)=>n!==index)})}>Quitar de esta revisión</button></div>)}
      <button type="button" className="secondary" onClick={()=>onChange({...value,[kind]:[...rows,Object.fromEntries([...fields.map(([key])=>[key,'']),['isCurrentlyActive',true]])]})}>Agregar {title.toLowerCase()}</button></fieldset>;
  }
  return <fieldset disabled={disabled}><legend>Revisión de antecedentes en esta visita</legend><p>¿Ha habido cambios desde su última visita?</p>
    {latest&&<label><input type="radio" name="reviewMode" checked={value.noChanges} onChange={()=>onChange(initialReview(latest))}/>Sin cambios</label>}
    <label><input type="radio" name="reviewMode" checked={!value.noChanges} onChange={update}/>Actualizar antecedentes</label>
    {!value.noChanges&&<><p>Esta revisión conserva una nueva versión. No modifica las anteriores.</p>
      <label><input type="checkbox" checked={value.noKnownAllergies} onChange={e=>onChange({...value,noKnownAllergies:e.target.checked})}/>El paciente confirma que no tiene alergias conocidas</label>
      {entries('allergies',[['name','Alergia'],['reaction','Reacción'],['severity','Severidad']],'Alergias')}
      {entries('conditions',[['conditionName','Enfermedad'],['details','Detalles']],'Enfermedades')}
      {entries('medications',[['medicationName','Medicamento'],['dose','Dosis'],['frequency','Frecuencia'],['reason','Motivo del medicamento']],'Medicamentos')}
    </>}
    <label>Notas de revisión<textarea value={value.notes??''} maxLength={8000} onChange={e=>onChange({...value,notes:e.target.value})}/></label>
  </fieldset>;
}
