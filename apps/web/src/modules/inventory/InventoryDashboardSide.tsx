import {useEffect,useState} from 'react';
import {api} from '../../services/api';
import {movementTypes,units,type Stock,type Lot,type MovementPage} from './types';

export default function InventoryDashboardSide({stocks,lots,center,revision,canHistory,showHistory,showLots,showStock}:{stocks:Stock[];lots:Lot[];center:string;revision:number;canHistory:boolean;showHistory:()=>void;showLots:()=>void;showStock:(status:string)=>void}){
 const [recent,setRecent]=useState<MovementPage|null>(null),[error,setError]=useState(false),[expanded,setExpanded]=useState(false);
 useEffect(()=>{if(!canHistory)return;let live=true;setRecent(null);setError(false);
 api<MovementPage>('/api/admin/inventory/movements?'+new URLSearchParams({page:'1',...(center?{centerId:center}:{})})).then(x=>{if(live)setRecent(x);}).catch(()=>{if(live)setError(true);});return()=>{live=false;};},[center,revision,canHistory]);
 const current=stocks.filter(s=>s.isActive&&(!center||s.centerId===center));
 const currentLots=lots.filter(l=>l.isActive&&l.quantityOnHand>0&&(!center||l.centerId===center));
 const alerts=[
 ...current.filter(s=>s.status==='OUT_OF_STOCK').map(s=>({key:s.inventoryItemId+s.centerId,name:s.name,info:s.center+' · Sin stock',action:()=>showStock('OUT_OF_STOCK'),kind:'OUT_OF_STOCK'})),
 ...currentLots.filter(l=>l.status==='EXPIRED').map(l=>({key:l.id+(l.centerId??''),name:l.item,info:l.batchNumber+' · Vencido '+l.expirationDate,action:showLots,kind:'EXPIRED'})),
 ...currentLots.filter(l=>l.status==='EXPIRING').sort((a,b)=>(a.daysRemaining??0)-(b.daysRemaining??0)).map(l=>({key:l.id+(l.centerId??''),name:l.item,info:l.batchNumber+' · Vence '+l.expirationDate,action:showLots,kind:'EXPIRING'})),
 ...current.filter(s=>s.status==='LOW_STOCK').map(s=>({key:s.inventoryItemId+s.centerId,name:s.name,info:s.center+' · '+s.quantityOnHand+' '+units[s.unit],action:()=>showStock('LOW_STOCK'),kind:'LOW_STOCK'}))];
 const categories=[...new Set(current.map(s=>s.categoryId))].map(id=>({id,name:current.find(s=>s.categoryId===id)!.category,count:new Set(current.filter(s=>s.categoryId===id&&s.quantityOnHand>0).map(s=>s.inventoryItemId)).size}));
 const max=Math.max(1,...categories.map(c=>c.count));
 return <div role="complementary" className="inventory-dashboard-side" aria-label="Resumen operativo de inventario">
 <section className="card"><h2>Alertas de inventario</h2>{alerts.length?<ul>{alerts.slice(0,expanded?undefined:6).map(a=><li key={a.kind+a.key}><button className="inventory-alert-button" onClick={a.action}><span className={'inventory-alert-dot '+a.kind} aria-hidden="true"/><span><strong>{a.name}</strong><small>{a.info}</small></span></button></li>)}</ul>:<p className="muted">Sin alertas en los datos disponibles.</p>}{alerts.length>6&&<button className="secondary" onClick={()=>setExpanded(!expanded)}>{expanded?'Ver menos alertas':'Ver todas las alertas'}</button>}</section>
 <section className="card"><h2>Stock por categoría</h2><p className="muted inventory-side-note">Artículos con existencias; no se suman unidades diferentes.</p>{categories.length?categories.map(c=><div className="inventory-category-bar" key={c.id}><div><span>{c.name}</span><strong>{c.count}</strong></div><meter min={0} max={max} value={c.count} aria-label={c.name}/></div>):<p className="muted">No hay artículos disponibles.</p>}</section>
 {canHistory&&<section className="card"><h2>Últimos movimientos</h2>{error?<p role="status">No se pudieron cargar los movimientos.</p>:recent?<ul>{recent.items.slice(0,5).map(m=><li key={m.id}><strong>{movementTypes[m.movementType]}</strong><small>{new Date(m.occurredAt).toLocaleDateString('es-BO')} · {m.center}</small>{m.lines.map((l,i)=><small key={i}>{l.name}: {m.movementType==='WITHDRAWAL'||m.adjustmentDirection==='DECREASE'?'−':'+'}{l.quantity} {units[l.unit]}</small>)}</li>)}</ul>:<p>Cargando movimientos recientes…</p>}{recent&&!recent.items.length&&<p className="muted">No hay movimientos registrados.</p>}<button className="secondary" onClick={showHistory}>Ver todos los movimientos</button></section>}
 </div>;
}
