import { useState, type FormEvent } from 'react';
import { units, type Item, type Category, type Options, type Stock, type Line, movementTypes } from './types';
export function CatalogEditor({ item, category, options, busy, save, cancel }: {item?:Item;category?:Category;options:Options;busy:boolean;save:(body:object)=>void;cancel:()=>void}) {
  const isCategory=!!category;
  function submit(e:FormEvent<HTMLFormElement>) { e.preventDefault();const f=new FormData(e.currentTarget);
    save({code:f.get('code'),name:f.get('name'),description:f.get('description')||null,isActive:f.get('isActive')==='on',
      ...(isCategory?{displayOrder:Number(f.get('displayOrder'))}:{categoryId:f.get('categoryId'),unit:f.get('unit'),minimumStock:Number(f.get('minimumStock'))})}); }
  return <form className="card inventory-editor" onSubmit={submit}><h2>{isCategory?'Categoría':'Artículo'}</h2><fieldset disabled={busy}>
    <label>Código<input name="code" required maxLength={50} defaultValue={item?.code??category?.code}/></label>
    <label>Nombre<input name="name" required maxLength={150} defaultValue={item?.name??category?.name}/></label>
    {!isCategory&&<><label>Categoría<select name="categoryId" required defaultValue={item?.categoryId}><option value="">Selecciona</option>{options.categories.map(c=><option key={c.id} value={c.id}>{c.name}</option>)}</select></label>
    <label>Unidad<select name="unit" defaultValue={item?.unit??'UNIT'}>{Object.entries(units).map(([k,v])=><option key={k} value={k}>{v}</option>)}</select></label>
    <label>Stock mínimo<input name="minimumStock" type="number" min="0" max="99999999999.999" step=".001" required defaultValue={item?.minimumStock??0}/></label></>}
    {isCategory&&<label>Orden<input name="displayOrder" type="number" defaultValue={category.displayOrder}/></label>}
    <label>Descripción<textarea name="description" maxLength={1000} defaultValue={item?.description??category?.description??''}/></label>
    <label><input name="isActive" type="checkbox" defaultChecked={item?.isActive??category?.isActive??true}/> Activo</label>
    <button type="submit">{busy?'Guardando…':'Guardar'}</button><button type="button" className="secondary" onClick={cancel}>Cancelar</button>
  </fieldset></form>;
}
export function MovementEditor({doctor,options,stocks,busy,save,cancel}:{doctor:boolean;options:Options;stocks:Stock[];busy:boolean;save:(body:object,type:string)=>void;cancel:()=>void}) {
  const [type,setType]=useState(doctor?'WITHDRAWAL':'ENTRY');const [center,setCenter]=useState('');const [lines,setLines]=useState<Line[]>([{inventoryItemId:'',quantity:''}]);
  const [requestKey]=useState(()=>crypto.randomUUID());
  const available=stocks.filter(s=>s.centerId===center&&s.isActive);
  function line(index:number,patch:Partial<Line>){setLines(prev=>prev.map((l,i)=>i===index?{...l,...patch}:l));}
  function submit(e:FormEvent<HTMLFormElement>){e.preventDefault();const f=new FormData(e.currentTarget);save({centerId:center,idempotencyKey:requestKey,notes:f.get('notes')||null,
    ...(!doctor?{recipientDoctorProfileId:f.get('doctor')||null,adjustmentDirection:type==='ADJUSTMENT'?f.get('direction'):null}:{}),
    lines:lines.map(l=>({inventoryItemId:l.inventoryItemId,quantity:Number(l.quantity)}))},type);}
  return <form className="card inventory-editor" onSubmit={submit}><h2>{doctor?'Registrar mi retiro':'Registrar movimiento'}</h2><fieldset disabled={busy}>
    {!doctor&&<label>Tipo de movimiento<select value={type} onChange={e=>setType(e.target.value)}>{Object.entries(movementTypes).map(([k,v])=><option key={k} value={k}>{v}</option>)}</select></label>}
    <label>Sucursal<select required value={center} onChange={e=>{setCenter(e.target.value);setLines([{inventoryItemId:'',quantity:''}]);}}><option value="">Selecciona</option>{options.centers.map(c=><option key={c.id} value={c.id}>{c.name}</option>)}</select></label>
    {!doctor&&(type==='WITHDRAWAL'||type==='RETURN')&&<label>Doctor<select name="doctor" key={center} required={type==='WITHDRAWAL'}><option value="">Selecciona</option>{options.doctors.filter(d=>d.centerIds.includes(center)).map(d=><option key={d.id} value={d.id}>{d.name} · {d.professionalLicense}</option>)}</select></label>}
    {type==='ADJUSTMENT'&&<label>Dirección<select name="direction"><option value="INCREASE">Aumentar</option><option value="DECREASE">Disminuir</option></select></label>}
    {lines.map((l,i)=><div className="inventory-line" key={i}><label>Artículo {i+1}<select required value={l.inventoryItemId} onChange={e=>line(i,{inventoryItemId:e.target.value})}><option value="">Selecciona</option>{available.filter(s=>s.inventoryItemId===l.inventoryItemId||!lines.some(a=>a.inventoryItemId===s.inventoryItemId)).map(s=><option key={s.inventoryItemId} value={s.inventoryItemId}>{s.code} · {s.name} ({units[s.unit]})</option>)}</select></label>
      <span>Disponible: {available.find(s=>s.inventoryItemId===l.inventoryItemId)?.quantityOnHand??'—'}</span>
      <label>Cantidad {i+1}<input required type="number" min=".001" max="99999999999.999" step=".001" value={l.quantity} onChange={e=>line(i,{quantity:e.target.value})}/></label>
      {lines.length>1&&<button type="button" className="secondary" onClick={()=>setLines(lines.filter((_,n)=>n!==i))}>Quitar artículo {i+1}</button>}</div>)}
    <button type="button" className="secondary" disabled={lines.length>=100} onClick={()=>setLines([...lines,{inventoryItemId:'',quantity:''}])}>Agregar artículo</button>
    <label>{type==='ADJUSTMENT'?'Motivo obligatorio':'Notas'}<textarea name="notes" maxLength={2000} required={type==='ADJUSTMENT'}/></label>
    <button type="submit">{busy?'Registrando…':doctor?'Confirmar retiro':'Confirmar movimiento'}</button><button type="button" className="secondary" onClick={cancel}>Cancelar</button>
  </fieldset></form>;
}
