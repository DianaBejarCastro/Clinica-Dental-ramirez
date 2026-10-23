import { useEffect, useState } from 'react';
import { api, ApiError } from '../../services/api';
import { usePermissions } from '../admin/shared';
import { CatalogEditor, MovementEditor } from './InventoryEditors';
import { type Options, type Stock, type Item, type Category, type MovementPage, units, stockStatuses, movementTypes } from './types';
import './inventory.css';
const emptyCategory:Category={id:'',code:'',name:'',description:null,isActive:true,displayOrder:0};
export default function InventoryPage({doctor=false}:{doctor?:boolean}) {
  const can=usePermissions();const base=doctor?'/api/doctor/inventory':'/api/admin/inventory';
  const [options,setOptions]=useState<Options|null>(null),[stocks,setStocks]=useState<Stock[]>([]),[items,setItems]=useState<Item[]>([]);
  const [history,setHistory]=useState<MovementPage|null>(null),[tab,setTab]=useState('stock'),[center,setCenter]=useState(''),[category,setCategory]=useState(''),[status,setStatus]=useState(''),[search,setSearch]=useState('');
  const [type,setType]=useState(''),[recipient,setRecipient]=useState(''),[itemFilter,setItemFilter]=useState(''),[from,setFrom]=useState(''),[to,setTo]=useState(''),[page,setPage]=useState(1);
  const [editor,setEditor]=useState<'item'|'category'|'movement'|null>(null),[item,setItem]=useState<Item>(),[cat,setCat]=useState<Category>();
  const [loading,setLoading]=useState(true),[busy,setBusy]=useState(false),[error,setError]=useState(''),[notice,setNotice]=useState(''),[revision,setRevision]=useState(0);
  function fail(e:unknown){setError(e instanceof ApiError&&[400,403,409].includes(e.status)?e.message:'No se pudo cargar o guardar el inventario. Inténtalo nuevamente.');}
  useEffect(()=>{let live=true;setLoading(true);setError('');
    Promise.all([api<Options>(base+'/options'),api<Stock[]>(doctor?base:base+'/stocks'),doctor?Promise.resolve([] as Item[]):api<Item[]>(base+'/items')])
      .then(([o,s,i])=>{if(live){setOptions(o);setStocks(s);setItems(i);}}).catch(e=>{if(live)fail(e);}).finally(()=>{if(live)setLoading(false);});return()=>{live=false;};
  },[base,doctor,revision]);
  useEffect(()=>{if(tab!=='history')return;let live=true;setHistory(null);setError('');
    const q=new URLSearchParams({page:String(page)});if(center)q.set('centerId',center);if(itemFilter)q.set('itemId',itemFilter);if(type&&!doctor)q.set('type',type);if(recipient&&!doctor)q.set('doctorId',recipient);
    if(from)q.set('from',new Date(from+'T00:00:00').toISOString());if(to)q.set('to',new Date(to+'T23:59:59.999').toISOString());
    api<MovementPage>(base+(doctor?'/my-withdrawals':'/movements')+'?'+q).then(h=>{if(live)setHistory(h);}).catch(e=>{if(live)fail(e);});return()=>{live=false;};
  },[tab,base,doctor,center,itemFilter,type,recipient,from,to,page,revision]);
  async function save(body:object,movementType?:string) {if(busy)return;setBusy(true);setError('');setNotice('');
    const path=editor==='movement'?(doctor?'/withdraw':'/movements/'+movementType?.toLowerCase()):editor==='category'?'/categories'+(cat?.id?'/'+cat.id:''):'/items'+(item?.id?'/'+item.id:'');
    try {await api(base+path,editor==='movement'?'POST':(editor==='category'?cat?.id:item?.id)?'PUT':'POST',body);
      setEditor(null);setNotice('Operación registrada correctamente.');setRevision(r=>r+1);
    }catch(e){fail(e);}finally{setBusy(false);}
  }
  const visible=stocks.filter(s=>(!center||s.centerId===center)&&(!category||s.categoryId===category)&&(!status||s.status===status)&&
    (s.code+' '+s.name+' '+s.category).toLocaleLowerCase().includes(search.toLocaleLowerCase()));
  const canMove=doctor?can('inventory.doctor.withdraw'):['entry','withdrawal','return','adjustment'].some(t=>can('inventory.'+t+'.create'));
  return <div className="inventory-page"><header className="inventory-heading"><div><h1>Inventario clínico</h1><p>Insumos, existencias y trazabilidad por sucursal.</p></div>
    <div>{!doctor&&can('inventory.item.manage')&&<><button onClick={()=>{setItem(undefined);setEditor('item');}}>Crear artículo</button><button className="secondary" onClick={()=>{setCat(emptyCategory);setEditor('category');}}>Crear categoría</button></>}
    {canMove&&<button onClick={()=>setEditor('movement')}>{doctor?'Registrar mi retiro':'Registrar movimiento'}</button>}</div></header>
    {error&&<div role="alert" className="card">{error}<button className="secondary" onClick={()=>setRevision(r=>r+1)}>Reintentar</button></div>}
    {notice&&<p role="status">{notice}</p>}
    {loading&&<p role="status">Cargando inventario…</p>}
    {!loading&&options&&<><section className="inventory-summary" aria-label="Resumen de inventario">
      <article className="card"><strong>{new Set(stocks.filter(s=>s.isActive).map(s=>s.inventoryItemId)).size}</strong> Artículos activos</article>
      <article className="card"><strong>{stocks.filter(s=>s.isActive&&s.status==='LOW_STOCK').length}</strong> Con stock bajo</article>
      <article className="card"><strong>{stocks.filter(s=>s.isActive&&s.status==='OUT_OF_STOCK').length}</strong> Sin stock</article></section>
      {editor==='movement'&&<MovementEditor doctor={doctor} options={options} stocks={stocks} busy={busy} save={save} cancel={()=>setEditor(null)}/>}
      {(editor==='item'||editor==='category')&&<CatalogEditor key={item?.id??cat?.id??editor} item={editor==='item'?item:undefined} category={editor==='category'?cat:undefined} options={options} busy={busy} save={save} cancel={()=>setEditor(null)}/>}
      <nav className="inventory-tabs" aria-label="Secciones de inventario"><button className={tab==='stock'?'':'secondary'} aria-pressed={tab==='stock'} onClick={()=>setTab('stock')}>Existencias</button>
        {(doctor||can('inventory.movement.view'))&&<button className={tab==='history'?'':'secondary'} aria-pressed={tab==='history'} onClick={()=>setTab('history')}>{doctor?'Mis retiros':'Historial de movimientos'}</button>}
        {!doctor&&can('inventory.item.manage')&&<button className={tab==='categories'?'':'secondary'} onClick={()=>setTab('categories')}>Categorías</button>}</nav>
      <div className="inventory-filters"><label>Sucursal<select value={center} onChange={e=>{setCenter(e.target.value);setPage(1);}}><option value="">Todas</option>{options.centers.map(c=><option key={c.id} value={c.id}>{c.name}</option>)}</select></label>
      {tab==='stock'&&<><label>Categoría<select value={category} onChange={e=>setCategory(e.target.value)}><option value="">Todas</option>{options.categories.map(c=><option key={c.id} value={c.id}>{c.name}</option>)}</select></label>
      <label>Estado<select value={status} onChange={e=>setStatus(e.target.value)}><option value="">Todos</option>{Object.entries(stockStatuses).map(([k,v])=><option key={k} value={k}>{v}</option>)}</select></label>
      <label>Buscar artículo<input value={search} onChange={e=>setSearch(e.target.value)} placeholder="Código, nombre o categoría"/></label></>}
      {tab==='history'&&<><label>Desde<input type="date" value={from} onChange={e=>{setFrom(e.target.value);setPage(1);}}/></label><label>Hasta<input type="date" value={to} onChange={e=>{setTo(e.target.value);setPage(1);}}/></label>
      {!doctor&&<><label>Tipo<select value={type} onChange={e=>{setType(e.target.value);setPage(1);}}><option value="">Todos</option>{Object.entries(movementTypes).map(([k,v])=><option key={k} value={k}>{v}</option>)}</select></label>
      <label>Doctor<select value={recipient} onChange={e=>{setRecipient(e.target.value);setPage(1);}}><option value="">Todos</option>{options.doctors.map(d=><option key={d.id} value={d.id}>{d.name} · {d.professionalLicense}</option>)}</select></label></>}
      <label>Artículo<select value={itemFilter} onChange={e=>{setItemFilter(e.target.value);setPage(1);}}><option value="">Todos</option>{[...new Map(stocks.map(s=>[s.inventoryItemId,s])).values()].map(s=><option key={s.inventoryItemId} value={s.inventoryItemId}>{s.name}</option>)}</select></label></>}</div>
      {tab==='stock'&&<div className="inventory-scroll" tabIndex={0} role="region" aria-label="Existencias por sucursal"><table><thead><tr>{['Código','Artículo','Categoría','Unidad','Sucursal','Stock','Mínimo','Estado',...(!doctor?['Acciones']:[])].map(h=><th key={h}>{h}</th>)}</tr></thead><tbody>
        {visible.map(s=><tr key={s.inventoryItemId+s.centerId}><td>{s.code}</td><td>{s.name}</td><td>{s.category}</td><td>{units[s.unit]}</td><td>{s.center}</td><td>{s.quantityOnHand}</td><td>{s.minimumStock}</td><td><span className={'inventory-status '+s.status}>{s.isActive?stockStatuses[s.status]:'Inactivo'}</span></td>
        {!doctor&&<td>{can('inventory.item.manage')&&<button className="secondary" onClick={()=>{setItem(items.find(i=>i.id===s.inventoryItemId));setEditor('item');}}>Editar {s.name}</button>}</td>}</tr>)}</tbody></table>{!visible.length&&<p>No hay artículos para estos filtros.</p>}</div>}
      {tab==='categories'&&<section className="card"><h2>Categorías</h2>{options.categories.map(c=><p key={c.id}>{c.name} · {c.isActive?'Activa':'Inactiva'} <button className="secondary" onClick={()=>{setCat(c);setEditor('category');}}>Editar {c.name}</button></p>)}</section>}
      {tab==='history'&&(!history?<p role="status">Cargando movimientos…</p>:<><div className="inventory-scroll" tabIndex={0} role="region" aria-label="Historial de inventario"><table><thead><tr>{['Fecha y hora','Tipo','Sucursal','Registrado por','Doctor','Artículos','Notas'].map(h=><th key={h}>{h}</th>)}</tr></thead><tbody>{history.items.map(m=><tr key={m.id}><td>{new Date(m.occurredAt).toLocaleString('es-BO')}</td><td>{movementTypes[m.movementType]} {m.adjustmentDirection==='DECREASE'?'−':m.adjustmentDirection==='INCREASE'?'+':''}</td><td>{m.center}</td><td>{m.registeredBy}</td><td>{m.doctor?m.doctor+' · '+m.professionalLicense:'—'}</td><td>{m.lines.map(l=><div key={l.inventoryItemId}>{l.name}: {l.quantity} {units[l.unit]}</div>)}</td><td>{m.notes??'—'}</td></tr>)}</tbody></table>{!history.items.length&&<p>No hay movimientos registrados.</p>}</div>
      <div className="inventory-tabs"><button className="secondary" disabled={page===1} onClick={()=>setPage(page-1)}>Anterior</button><span>Página {page} · {history.total} movimientos</span><button className="secondary" disabled={page*history.pageSize>=history.total} onClick={()=>setPage(page+1)}>Siguiente</button></div></>)}
    </>}</div>;
}
