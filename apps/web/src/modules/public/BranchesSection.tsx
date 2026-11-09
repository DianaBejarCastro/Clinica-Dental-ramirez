import { useEffect, useState } from 'react';
import GoogleMap from '../../components/maps/GoogleMap';
import { apiUrl } from '../../services/api';
import { PublicSection } from './PublicSection';
import { googleMapsLink, validCoordinates } from '../../components/maps/coordinates';
import './branches-section.css';

export interface PublicBranch { id: string; name: string; address: string; city: string | null; department: string | null; phone: string | null; latitude: number | null; longitude: number | null }
export default function BranchesSection() {
  const [branches, setBranches] = useState<PublicBranch[]>([]);
  useEffect(() => {
    const controller = new AbortController();
    fetch(apiUrl('/api/clinic/branches'), { signal: controller.signal }).then(r => r.ok ? r.json() : [])
      .then(data => { if (!controller.signal.aborted && Array.isArray(data)) setBranches(data); }).catch(() => undefined);
    return () => controller.abort();
  }, []);
  if (!branches.length) return null;
  return <PublicSection id="sucursales" eyebrow="CERCA DE TI" title="Nuestras sucursales">
    <div className="public-branches-layout">
      {branches.some(b => validCoordinates(b.latitude, b.longitude)) && <GoogleMap locations={branches} label="Mapa de nuestras sucursales" className="public-branches-map" />}
      <div className="public-branches-list">{branches.map(branch => {
        const directions = googleMapsLink(branch.latitude, branch.longitude);
        return <article className="public-branch-card" key={branch.id}><h3>{branch.name}</h3>
          <address>{branch.address}<br />{[branch.city, branch.department].filter(Boolean).join(', ')}</address>
          {branch.phone && <p><a href={`tel:${branch.phone.replace(/[^+\d]/g, '')}`}>{branch.phone}</a></p>}
          {directions && <a className="public-button" href={directions} target="_blank" rel="noopener noreferrer">Cómo llegar<span className="sr-only"> a {branch.name}</span></a>}
        </article>;
      })}</div>
    </div>
  </PublicSection>;
}
