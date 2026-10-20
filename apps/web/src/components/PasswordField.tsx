import { useId, useState, type InputHTMLAttributes } from 'react';
import './interactive-fields.css';

type Props = Omit<InputHTMLAttributes<HTMLInputElement>, 'type'> & { label: string; error?: string };
export default function PasswordField({label,error,id,...input}:Props){
  const generated=useId(), fieldId=id??generated;
  const [visible,setVisible]=useState(false);
  return <div className="password-field">
    <label htmlFor={fieldId}>{label}</label>
    <div className="password-control"><input {...input} id={fieldId} type={visible?'text':'password'} aria-invalid={error?true:input['aria-invalid']} aria-describedby={[input['aria-describedby'],error?`${fieldId}-error`:null].filter(Boolean).join(' ')||undefined}/>
      <button type="button" className="password-toggle" disabled={input.disabled} aria-label={visible?'Ocultar contraseña':'Mostrar contraseña'} aria-controls={fieldId} onClick={()=>setVisible(value=>!value)}>
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12Z"/><circle cx="12" cy="12" r="3"/>{visible&&<path d="m3 3 18 18"/>}</svg>
      </button>
    </div>
    {error&&<small id={`${fieldId}-error`} className="password-error" role="alert">{error}</small>}
  </div>;
}
