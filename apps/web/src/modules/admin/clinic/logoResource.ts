import { useEffect, useState } from 'react';
import { apiUrl } from '../../../services/api';

export interface ClinicLogoMetadata { contentType: string; fileName: string; size: number; updatedAt: string }
const changed = 'clinic-logo-changed';
export const logoUrl = (version?: string) => apiUrl(`/api/clinic/logo${version ? `?v=${encodeURIComponent(version)}` : ''}`);
export function notifyLogoChanged() { window.dispatchEvent(new Event(changed)); }
export function useLogoVersion() {
  const [version, setVersion] = useState('');
  useEffect(() => {
    const update = () => setVersion(`${Date.now()}-${Math.random().toString(36).slice(2)}`);
    window.addEventListener(changed, update);
    return () => window.removeEventListener(changed, update);
  }, []);
  return version;
}
