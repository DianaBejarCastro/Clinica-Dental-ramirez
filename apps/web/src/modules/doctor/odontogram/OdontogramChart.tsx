import type { Catalogs, Dentition, Finding, FindingType, Tooth } from './api';
// Presentation only: catalog metadata remains the authority for the scope of every finding.
export function findingMark(type:FindingType|undefined) {
  const code=type?.code??'';
  if(code==='CARIES')return {symbol:'●',tone:'caries'};
  if(code==='MISSING_TOOTH')return {symbol:'×',tone:'missing'};
  if(code==='EXTRACTION_INDICATED')return {symbol:'↗',tone:'caries'};
  if(code==='HEALTHY')return {symbol:'✓',tone:'healthy'};
  if(code.startsWith('ENDODONTIC'))return {symbol:'Ψ',tone:'treatment'};
  if(code.startsWith('CROWN'))return {symbol:'⊓',tone:'treatment'};
  if(code.startsWith('IMPLANT'))return {symbol:'⊥',tone:'treatment'};
  return {symbol:'◆',tone:'treatment'};
}
export function ToothIllustration({type}:{type:string}) {
  const crowns:Record<string,string>={INCISOR:'M17 12 Q30 8 43 12 L41 33 Q30 39 19 33 Z',CANINE:'M17 21 L30 7 L43 21 L40 35 Q30 40 20 35 Z',PREMOLAR:'M14 17 Q18 5 30 13 Q42 5 46 17 L43 34 Q30 41 17 34 Z',MOLAR:'M10 17 Q12 4 23 12 Q30 5 37 12 Q48 4 50 17 L46 35 Q30 42 14 35 Z'};
  return <svg className="odo-tooth-svg" viewBox="0 0 60 85" aria-hidden="true"><path d={type==='MOLAR'?'M17 31 Q12 61 20 76 L29 45 L39 76 Q48 61 43 31':'M20 30 Q20 62 30 77 Q40 62 40 30'} fill="#f0f5f5" stroke="#8da7ad" strokeWidth="1.5"/><path d={crowns[type]??crowns.INCISOR} fill="white" stroke="#567b83" strokeWidth="1.6"/><path d="M23 22 Q30 28 37 22" fill="none" stroke="#b6c8cb"/></svg>;
}
function surfacePosition(code:string,tooth:Tooth) {
  const upper=[1,2,5,6].includes(tooth.quadrant),right=[1,4,5,8].includes(tooth.quadrant);
  if(code==='OCCLUSAL_INCISAL')return 'center';
  if(code==='VESTIBULAR')return upper?'top':'bottom';
  if(code==='LINGUAL_PALATAL')return upper?'bottom':'top';
  if(code==='MESIAL')return right?'right':'left';
  return right?'left':'right';
}
export function OdontogramChart({catalogs,mode,findings,selected,surfaces,editable,onTooth,onSurface}:{catalogs:Catalogs;mode:Dentition;findings:Finding[];selected:number|null;surfaces:number[];editable:boolean;onTooth:(id:number)=>void;onSurface:(tooth:number,surface:number)=>void}) {
  const rows: {dentition:Tooth['dentitionClass'];upper:boolean}[]=mode==='MIXED'?[{dentition:'PERMANENT',upper:true},{dentition:'PRIMARY',upper:true},{dentition:'PRIMARY',upper:false},{dentition:'PERMANENT',upper:false}]:[{dentition:mode,upper:true},{dentition:mode,upper:false}];
  return <div className="odo-chart-scroll" role="region" aria-label="Arcadas dentales, desplazamiento horizontal" tabIndex={0}><div className="odo-arches">{rows.map(row=><section className="odo-arch" key={row.dentition+row.upper} aria-label={`${row.dentition==='PRIMARY'?'Temporal':'Permanente'} ${row.upper?'superior':'inferior'}`}><h3>{row.upper?'Arcada superior':'Arcada inferior'} · {row.dentition==='PRIMARY'?'Temporal':'Permanente'}</h3><div className="odo-teeth">{catalogs.teeth.filter(t=>t.dentitionClass===row.dentition&&[1,2,5,6].includes(t.quadrant)===row.upper&&(t.isActive||findings.some(f=>f.dentalToothId===t.id))).sort((a,b)=>a.displayOrder-b.displayOrder).map(t=>{
    const entries=findings.filter(f=>f.dentalToothId===t.id);
    return <div key={t.id} className={`odo-tooth ${selected===t.id?'selected':''}`} data-fdi={t.fdiCode}><button type="button" className="odo-tooth-pick" aria-label={`${t.dentitionClass==='PRIMARY'?'Diente temporal':'Diente'} ${t.fdiCode}`} aria-pressed={selected===t.id} onClick={()=>onTooth(t.id)}><ToothIllustration type={t.toothType}/><strong>{t.fdiCode}</strong></button><div className="odo-surface-diagram">{catalogs.surfaces.map(s=>{
      const marked=entries.filter(f=>f.surfaceIds.includes(s.id));
      return <button key={s.id} type="button" className={`odo-surface ${surfacePosition(s.code,t)} ${marked.length?'marked '+findingMark(catalogs.findings.find(x=>x.id===marked[0].findingTypeId)).tone:''}`} disabled={!editable||!s.isActive} aria-label={`${t.fdiCode}: ${s.name}`} aria-pressed={selected===t.id&&surfaces.includes(s.id)} title={marked.map(f=>catalogs.findings.find(x=>x.id===f.findingTypeId)?.name).join(', ')||s.name} onClick={()=>onSurface(t.id,s.id)}>{marked.length?'•':''}</button>;
    })}</div><div className="odo-markers">{entries.map((f,i)=>{const type=catalogs.findings.find(x=>x.id===f.findingTypeId),mark=findingMark(type);return <span key={i} className={`odo-mark ${mark.tone}`} title={type?.name} aria-label={type?.name}>{mark.symbol}</span>;})}</div></div>;
  })}</div></section>)}</div></div>;
}
export function OdontogramLegend(){return <details className="odo-legend"><summary>Leyenda clínica</summary><p><span className="caries">● Caries · ↗ Extracción indicada</span><span>× Ausente</span><span className="healthy">✓ Sano</span><span className="treatment">Ψ Endodoncia · ⊓ Corona · ⊥ Implante · ◆ Otro hallazgo</span></p><small>Los marcadores identifican hallazgos. Selecciona una pieza para consultar su descripción y superficies.</small></details>;}
