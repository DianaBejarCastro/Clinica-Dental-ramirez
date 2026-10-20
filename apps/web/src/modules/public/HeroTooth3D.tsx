import { useEffect, useId, useRef, useState } from 'react';
import type { ToothScene } from './toothScene';
import './hero-tooth.css';

export default function HeroTooth3D(){
  const host=useRef<HTMLDivElement>(null),scene=useRef<ToothScene|null>(null),drag=useRef<{id:number;x:number;y:number}|null>(null);
  const [state,setState]=useState<'loading'|'ready'|'fallback'>('loading'),[paused,setPaused]=useState(false);const help=useId();
  useEffect(()=>{let cancelled=false;const element=host.current!;
    void import('./toothScene').then(({createToothScene})=>{if(cancelled)return;scene.current=createToothScene(element,()=>{scene.current?.dispose();scene.current=null;setState('fallback');});setState('ready');}).catch(()=>{if(!cancelled)setState('fallback');});
    return()=>{cancelled=true;scene.current?.dispose();scene.current=null;};
  },[]);
  return <div className="hero-tooth" data-state={state}>
    <div ref={host} className="hero-tooth-stage" role="group" aria-label="Modelo dental 3D interactivo" aria-describedby={help} tabIndex={state==='ready'?0:-1}
      onKeyDown={e=>{const directions:Record<string,[number,number]>={ArrowLeft:[0,-.15],ArrowRight:[0,.15],ArrowUp:[-.1,0],ArrowDown:[.1,0]};if(directions[e.key]){e.preventDefault();scene.current?.rotate(...directions[e.key]);}}}
      onPointerDown={e=>{if(state!=='ready'||e.button!==0)return;drag.current={id:e.pointerId,x:e.clientX,y:e.clientY};e.currentTarget.setPointerCapture(e.pointerId);}}
      onPointerMove={e=>{const previous=drag.current;if(!previous||previous.id!==e.pointerId)return;scene.current?.rotate((e.clientY-previous.y)*.006,(e.clientX-previous.x)*.008);drag.current={id:e.pointerId,x:e.clientX,y:e.clientY};}}
      onPointerUp={()=>{drag.current=null;}} onPointerCancel={()=>{drag.current=null;}} onLostPointerCapture={()=>{drag.current=null;}} />
    {state!=='ready'&&<div className="hero-tooth-fallback" role="status"><span className="hero-tooth-monogram" aria-hidden="true">R</span><strong>Odontología con precisión</strong><p>{state==='loading'?'Preparando experiencia 3D…':'Cuidado profesional, en cada detalle.'}</p></div>}
    <div className="hero-tooth-controls"><span id={help}>{state==='ready'?'Arrastra para explorar · Usa las flechas del teclado':'Clínica Dental RAMÍREZ'}</span>{state==='ready'&&<button type="button" className="secondary" aria-pressed={paused} onClick={()=>{scene.current?.setPaused(!paused);setPaused(!paused);}}>{paused?'Reanudar giro':'Pausar giro'}</button>}</div>
  </div>;
}
