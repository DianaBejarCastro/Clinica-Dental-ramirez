import { useEffect, useState } from 'react';
import { apiUrl } from '../services/api';

const changed = 'clinic-name-changed';
export function notifyClinicNameChanged(){ window.dispatchEvent(new Event(changed)); }

export default function SidebarClinicName(){
  const [name,setName]=useState('Clínica');
  useEffect(()=>{
    let request:AbortController|undefined;
    async function load(){
      request?.abort();const current=new AbortController();request=current;
      try {
        const response=await fetch(apiUrl('/api/clinic/brand'),{signal:current.signal});
        if(!response.ok)return;
        const data:unknown=await response.json();
        if(!current.signal.aborted&&data&&typeof data==='object'&&'name' in data&&typeof data.name==='string'&&data.name.trim())setName(data.name);
      }catch{/* Keep the generic fallback or last successful name on a transient failure. */}
    }
    void load();window.addEventListener(changed,load);window.addEventListener('focus',load);
    return()=>{request?.abort();window.removeEventListener(changed,load);window.removeEventListener('focus',load);};
  },[]);
  return <span className="sidebar-clinic-name" title={name}>{name}</span>;
}
