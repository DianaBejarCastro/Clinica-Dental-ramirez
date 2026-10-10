import { useState } from 'react';
import { logoUrl, useLogoVersion } from './logoResource';
import './clinic-logo.css';

function LogoImage({ src }: { src: string }) {
  const [loaded, setLoaded] = useState(false); const [failed, setFailed] = useState(false);
  return <span className="clinic-brand-logo">
    {!loaded && <span className="mark" aria-label="Monograma de la clínica">R</span>}
    {!failed && <img src={src} alt="Logo de la clínica" hidden={!loaded}
      onLoad={() => setLoaded(true)} onError={() => { setFailed(true); setLoaded(false); }} />}
  </span>;
}
export default function ClinicLogo() {
  const version = useLogoVersion();
  return <LogoImage key={version} src={logoUrl(version)} />;
}
