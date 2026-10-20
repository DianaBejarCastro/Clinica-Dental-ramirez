import type { ReactNode } from 'react';
import './interactive-fields.css';
export default function BackLink({href,children}:{href:string;children:ReactNode}){
  return <a className="link-back login-back" href={href}><svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><path d="m10 5-7 7 7 7M3 12h18"/></svg><span>{children}</span></a>;
}
