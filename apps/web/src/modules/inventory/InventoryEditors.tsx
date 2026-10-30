import { LotLineFields } from './InventoryLots';
import { useState, useRef, type FormEvent } from 'react';
import { usePermissions } from '../admin/shared';
import { units, type Item, type Category, type Options, type Stock, type Line, movementTypes } from './types';
export function CatalogEditor({ item, category, options, busy, save, cancel }: {item?:Item;category?:Category;options:Options;busy:boolean;save:(body:object)=>void|Promise<void>;cancel:()=>void}) {
  const can=usePermissions();
  const [initial,setInitial]=useState(false),[center,setCenter]=useState(''),[quantity,setQuantity]=useState(''),[batch,setBatch]=useState(''),[expiry,setExpiry]=useState(''),[expiration,setExpiration]=useState(item?.tracksExpiration??false);
  const locked=useRef(false),retry=useRef<{fingerprint:string;key:string}|null>(null);
  const [unit,setUnit]=useState<keyof typeof units>(item?.unit??'UNIT'),[packageUnit,setPackageUnit]=useState(item?.packageUnit??''),[content,setContent]=useState(String(item?.unitsPerPackage??'')),[name,setName]=useState(item?.name??''),[minimum,setMinimum]=useState(String(item?.minimumStock??0));
  const structuralLocked=!!item?.hasMovements;
  const countUnit=(count:number,code:keyof typeof units=unit)=>units[code].toLocaleLowerCase()+(count===1?'':code==='UNIT'||code==='PAIR'?'es':'s');
  const total=Number(quantity)*(packageUnit?Number(content):1);
  const formatted=(value:number)=>Number.isFinite(value)?new Intl.NumberFormat('es-BO',{maximumFractionDigits:3}).format(value):'—';
  const isCategory=!!category;const [tracksLots,setTracksLots]=useState(item?.tracksLots??false);
  async function submit(e:FormEvent<HTMLFormElement>) { e.preventDefault();if(locked.current)return;const f=new FormData(e.currentTarget);
    const body={code:f.get('code'),name:f.get('name'),description:f.get('description')||null,isActive:f.get('isActive')==='on',
      ...(isCategory?{displayOrder:Number(f.get('displayOrder'))}:{categoryId:f.get('categoryId'),unit,minimumStock:Number(minimum),tracksLots,tracksExpiration:tracksLots&&f.has('tracksExpiration'),packageUnit:packageUnit||null,unitsPerPackage:packageUnit?Number(content):null})};
    let request:object=body;
    if(!isCategory&&!item&&initial&&can('inventory.entry.create')){
      const input={item:body,initialStock:{centerId:center,quantity:Number(quantity),...(packageUnit?{quantityInPackages:true}:{}),...(tracksLots?{batchNumber:batch,...(expiration?{expirationDate:expiry}:{})}:{})}};
      const fingerprint=JSON.stringify(input);if(retry.current?.fingerprint!==fingerprint)retry.current={fingerprint,key:crypto.randomUUID()};
      request={...input,idempotencyKey:retry.current.key};
    }
    locked.current=true;try{await save(request);}finally{locked.current=false;}
  }
  if(!isCategory)return <form className="inventory-item-editor" onSubmit={submit}>
    <div className="inventory-item-heading"><div><h2>{item?'Editar artículo':'Crear artículo'}</h2><p>{item?'Actualiza la información del artículo.':'Registra un nuevo insumo, medicamento o material para la clínica.'}</p></div><button type="button" className="secondary" disabled={busy} onClick={cancel}>← Volver al inventario</button></div>
    <fieldset disabled={busy} className="inventory-item-layout">
      <div className="inventory-item-sections">
        <section className="card"><h3>Información básica</h3><p>Datos principales del artículo.</p><div className="inventory-item-fields">
          <label>Código<input name="code" required maxLength={50} defaultValue={item?.code}/></label>
          <label>Nombre<input name="name" required maxLength={150} value={name} onChange={e=>setName(e.target.value)}/></label>
          <label>Categoría<select name="categoryId" required defaultValue={item?.categoryId}><option value="">Selecciona</option>{options.categories.map(c=><option key={c.id} value={c.id}>{c.name}</option>)}</select></label>
        </div><label>Descripción<textarea name="description" maxLength={1000} rows={3} defaultValue={item?.description??''}/></label><small>Describe el uso, las características o la información relevante.</small></section>
        <section className="card"><h3>Cómo se cuenta el stock</h3><label>Unidad de stock<select name="unit" value={unit} disabled={structuralLocked} onChange={e=>setUnit(e.target.value as keyof typeof units)} aria-describedby="stock-unit-help">{Object.entries(units).map(([k,v])=><option key={k} value={k}>{v}</option>)}</select></label><p id="stock-unit-help">Es la unidad que el sistema usará para contar y descontar este artículo.</p><small>{unit==='BOX'?'El inventario se llevará por cajas completas.':'Ejemplo: si quieres saber exactamente cuántos guantes quedan, usa Unidad.'}</small>{structuralLocked&&<p role="note">Esta configuración no se puede modificar porque el artículo ya tiene movimientos de inventario.</p>}</section>
        <section className="card"><h3>Presentación del producto</h3><p>Indica cómo recibes o compras el producto. No cambia la unidad en la que se descuenta el stock.</p><div className="inventory-item-fields">
          <label>Presentación<select value={packageUnit} disabled={structuralLocked} onChange={e=>setPackageUnit(e.target.value as keyof typeof units|'')}><option value="">Sin presentación: contar directamente</option>{Object.entries(units).map(([k,v])=><option key={k} value={k}>{v}</option>)}</select></label>
          {packageUnit&&<label>Unidades por presentación<input type="number" min=".001" max="99999999999.999" step=".001" required disabled={structuralLocked} value={content} onChange={e=>setContent(e.target.value)}/></label>}
        </div>{packageUnit&&<p className="inventory-conversion" aria-live="polite">1 {units[packageUnit as keyof typeof units].toLocaleLowerCase()} = {formatted(Number(content))} {countUnit(Number(content))}</p>}<small>Ejemplo: guantes contados por unidad, recibidos en cajas de 100. Dos cajas contienen 200 unidades.</small></section>
        <section className="card"><h3>Control de stock</h3><label>Stock mínimo<input name="minimumStock" type="number" min="0" max="99999999999.999" step=".001" required value={minimum} onChange={e=>setMinimum(e.target.value)} aria-describedby="minimum-stock-help"/></label><p id="minimum-stock-help">Cuando el stock llegue a esta cantidad o sea menor, el sistema mostrará una alerta de stock bajo. No representa el stock actual.</p><small aria-live="polite">Se mostrará una alerta cuando queden {minimum||'0'} {countUnit(Number(minimum))} o menos.</small></section>
        <section className="card"><h3>Vencimiento y lotes</h3><p>Configura el control de vencimiento y la trazabilidad.</p>
          <label className="inventory-item-check"><input type="checkbox" checked={tracksLots} onChange={e=>setTracksLots(e.target.checked)}/> Seguimiento de lotes</label><small>Activa esta opción para identificar cada grupo de productos recibidos mediante un número de lote. Ejemplos: anestésicos, resinas y medicamentos.</small>
          <label className="inventory-item-check"><input name="tracksExpiration" type="checkbox" disabled={!tracksLots} checked={expiration} onChange={e=>setExpiration(e.target.checked)}/> Control de vencimiento</label><small>Permite registrar el vencimiento de cada lote y recibir alertas. Primero activa el seguimiento de lotes.</small>
        </section>
        {!item&&<section className="card"><h3>Stock inicial</h3><p>¿Actualmente tienes existencias de este artículo?</p>{can('inventory.entry.create')?<><div className="inventory-initial-choice" role="group" aria-label="Registrar stock inicial"><label className="inventory-item-check"><input type="radio" name="initial-choice" checked={!initial} onChange={()=>setInitial(false)}/> No, crear sin stock</label><label className="inventory-item-check"><input type="radio" name="initial-choice" checked={initial} onChange={()=>setInitial(true)}/> Sí, registrar stock</label></div>{initial&&<><div className="inventory-item-fields"><label>Sucursal inicial<select required value={center} onChange={e=>setCenter(e.target.value)}><option value="">Selecciona</option>{options.centers.map(c=><option key={c.id} value={c.id}>{c.name}</option>)}</select></label><label>{packageUnit?'Número de envases':'Cantidad inicial'}<input type="number" required min={packageUnit?'1':'.001'} max={packageUnit?Math.min(99999999999,Math.floor(99999999999.999/Number(content||1))):'99999999999.999'} step={packageUnit?'1':'.001'} value={quantity} onChange={e=>setQuantity(e.target.value)}/></label>{tracksLots&&<label>Número de lote inicial<input required maxLength={100} value={batch} onChange={e=>setBatch(e.target.value)}/></label>}{tracksLots&&expiration&&<label>Vencimiento del lote inicial<input type="date" required value={expiry} onChange={e=>setExpiry(e.target.value)}/></label>}</div><div className="inventory-conversion" role="status">{packageUnit&&<span>{quantity||'0'} {countUnit(Number(quantity),packageUnit as keyof typeof units)} × {content||'0'} {countUnit(Number(content))} = {formatted(total)} {countUnit(total)}</span>}<strong>Se añadirán {formatted(total)} {countUnit(total)} al inventario.</strong></div></>}</>:<p>Para registrar una entrada necesitas el permiso correspondiente.</p>}{!initial&&<small>Sin entrada inicial, el artículo se crea con stock cero.</small>}</section>}
      </div>
      <div className="inventory-item-secondary">
        <section className="card"><h3>Estado</h3><label className="inventory-item-check"><input className="inventory-active-switch" name="isActive" type="checkbox" defaultChecked={item?.isActive??true}/> Activo</label><p>El artículo estará disponible en inventario.</p></section>
        <section className="card"><h3>Información</h3><p>El stock se modifica mediante movimientos de inventario.</p><p>Los lotes y las fechas de vencimiento se registran al recibir existencias.</p></section>
        <section className="card inventory-registration-summary" aria-label="Resumen de registro"><h3>Resumen de registro</h3><strong>{name||'Nombre del artículo'}</strong><dl><div><dt>Unidad de stock</dt><dd>{units[unit]}</dd></div><div><dt>Presentación</dt><dd>{packageUnit?`${units[packageUnit as keyof typeof units]} de ${content||'0'} ${countUnit(Number(content))}`:'Conteo directo'}</dd></div><div><dt>Stock mínimo</dt><dd>{minimum||'0'} {countUnit(Number(minimum))}</dd></div>{!item&&<><div><dt>Stock inicial</dt><dd>{initial?`${quantity||'0'} ${countUnit(Number(quantity),(packageUnit||unit) as keyof typeof units)}`:'Sin stock'}</dd></div><div><dt>Total a registrar</dt><dd>{initial?formatted(total):'0'} {countUnit(initial?total:0)}</dd></div>{initial&&<><div><dt>Sucursal</dt><dd>{options.centers.find(c=>c.id===center)?.name||'Selecciona una sucursal'}</dd></div>{tracksLots&&<div><dt>Lote</dt><dd>{batch||'Pendiente'}</dd></div>}{tracksLots&&expiration&&<div><dt>Vencimiento</dt><dd>{expiry||'Pendiente'}</dd></div>}</>}</>}</dl></section>
        <div className="inventory-item-actions"><button type="button" className="secondary" onClick={cancel}>Cancelar</button><button type="submit">{busy?'Guardando…':item?'Guardar cambios':'Guardar artículo'}</button></div>
      </div>
    </fieldset>
  </form>;
  return <form className="card inventory-editor" onSubmit={submit}><h2>{isCategory?'Categoría':'Artículo'}</h2><fieldset disabled={busy}>
    <label>Código<input name="code" required maxLength={50} defaultValue={item?.code??category?.code}/></label>
    <label>Nombre<input name="name" required maxLength={150} defaultValue={item?.name??category?.name}/></label>
    {!isCategory&&<><label>Categoría<select name="categoryId" required defaultValue={item?.categoryId}><option value="">Selecciona</option>{options.categories.map(c=><option key={c.id} value={c.id}>{c.name}</option>)}</select></label>
    <label>Unidad<select name="unit" defaultValue={item?.unit??'UNIT'}>{Object.entries(units).map(([k,v])=><option key={k} value={k}>{v}</option>)}</select></label>
    <label>Stock mínimo<input name="minimumStock" type="number" min="0" max="99999999999.999" step=".001" required defaultValue={item?.minimumStock??0}/></label></>}
    {!isCategory&&<><label><input type="checkbox" checked={tracksLots} onChange={e=>setTracksLots(e.target.checked)}/> Seguimiento de lotes</label><label><input name="tracksExpiration" type="checkbox" disabled={!tracksLots} defaultChecked={item?.tracksExpiration}/> Control de vencimiento</label></>}
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
    lines:lines.map(l=>({inventoryItemId:l.inventoryItemId,quantity:Number(l.quantity),...(!doctor&&type!=='WITHDRAWAL'&&available.find(s=>s.inventoryItemId===l.inventoryItemId)?.tracksLots?(type==='ENTRY'?{batchNumber:l.batchNumber||null,expirationDate:l.expirationDate||null,manufacturedAt:l.manufacturedAt||null,supplierName:l.supplierName||null}:{inventoryLotId:l.inventoryLotId||null}):{})}))},type);}
  return <form className="card inventory-editor" onSubmit={submit}><h2>{doctor?'Registrar mi retiro':'Registrar movimiento'}</h2><fieldset disabled={busy}>
    {!doctor&&<label>Tipo de movimiento<select value={type} onChange={e=>{setType(e.target.value);setLines(prev=>prev.map(l=>({inventoryItemId:l.inventoryItemId,quantity:l.quantity})));}}>{Object.entries(movementTypes).map(([k,v])=><option key={k} value={k}>{v}</option>)}</select></label>}
    <label>Sucursal<select required value={center} onChange={e=>{setCenter(e.target.value);setLines([{inventoryItemId:'',quantity:''}]);}}><option value="">Selecciona</option>{options.centers.map(c=><option key={c.id} value={c.id}>{c.name}</option>)}</select></label>
    {!doctor&&(type==='WITHDRAWAL'||type==='RETURN')&&<label>Doctor<select name="doctor" key={center} required={type==='WITHDRAWAL'}><option value="">Selecciona</option>{options.doctors.filter(d=>d.centerIds.includes(center)).map(d=><option key={d.id} value={d.id}>{d.name} · {d.professionalLicense}</option>)}</select></label>}
    {type==='ADJUSTMENT'&&<label>Dirección<select name="direction"><option value="INCREASE">Aumentar</option><option value="DECREASE">Disminuir</option></select></label>}
    {lines.map((l,i)=><div className="inventory-line" key={i}><label>Artículo {i+1}<select required value={l.inventoryItemId} onChange={e=>setLines(prev=>prev.map((a,n)=>n===i?{inventoryItemId:e.target.value,quantity:a.quantity}:a))}><option value="">Selecciona</option>{available.filter(s=>s.inventoryItemId===l.inventoryItemId||!lines.some(a=>a.inventoryItemId===s.inventoryItemId)).map(s=><option key={s.inventoryItemId} value={s.inventoryItemId}>{s.code} · {s.name} ({units[s.unit]})</option>)}</select></label>
      <span>Disponible: {available.find(s=>s.inventoryItemId===l.inventoryItemId)?.quantityOnHand??'—'}</span>
      <label>Cantidad {i+1}<input required type="number" min=".001" max="99999999999.999" step=".001" value={l.quantity} onChange={e=>line(i,{quantity:e.target.value})}/></label>
      {!doctor&&available.find(s=>s.inventoryItemId===l.inventoryItemId)?.tracksLots&&type!=='WITHDRAWAL'&&<LotLineFields line={l} index={i} centerId={center} entry={type==='ENTRY'} expiration={!!available.find(s=>s.inventoryItemId===l.inventoryItemId)?.tracksExpiration} change={patch=>line(i,patch)}/>}
      {lines.length>1&&<button type="button" className="secondary" onClick={()=>setLines(lines.filter((_,n)=>n!==i))}>Quitar artículo {i+1}</button>}</div>)}
    <button type="button" className="secondary" disabled={lines.length>=100} onClick={()=>setLines([...lines,{inventoryItemId:'',quantity:''}])}>Agregar artículo</button>
    <label>{type==='ADJUSTMENT'?'Motivo obligatorio':'Notas'}<textarea name="notes" maxLength={2000} required={type==='ADJUSTMENT'}/></label>
    <button type="submit">{busy?'Registrando…':doctor?'Confirmar retiro':'Confirmar movimiento'}</button><button type="button" className="secondary" onClick={cancel}>Cancelar</button>
  </fieldset></form>;
}
