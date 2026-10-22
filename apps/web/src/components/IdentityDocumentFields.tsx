import { useState } from 'react';

export const documentTypes = [
  { value: 'CI', label: 'Cédula de identidad' },
  { value: 'PASSPORT', label: 'Pasaporte' },
] as const;
export type DocumentType = typeof documentTypes[number]['value'];
export interface IdentityDocument { documentType?: string | null; documentNumber?: string | null; documentCountryCode?: string | null }
export const issuingCountries = [
  ['BO', 'Bolivia'], ['AR', 'Argentina'], ['BR', 'Brasil'], ['CL', 'Chile'], ['CO', 'Colombia'],
  ['EC', 'Ecuador'], ['PY', 'Paraguay'], ['PE', 'Perú'], ['UY', 'Uruguay'], ['VE', 'Venezuela'],
  ['US', 'Estados Unidos'], ['CA', 'Canadá'], ['MX', 'México'], ['ES', 'España'],
  ['FR', 'Francia'], ['DE', 'Alemania'], ['IT', 'Italia'], ['GB', 'Reino Unido'],
  ['CN', 'China'], ['JP', 'Japón'], ['IN', 'India'], ['AU', 'Australia'],
] as const;
export function documentLabel(value: IdentityDocument) {
  if (!value.documentNumber) return '—';
  return `${value.documentType === 'PASSPORT' ? 'Pasaporte' : 'CI'} · ${value.documentNumber}`;
}
export function documentData(data: FormData) {
  const documentType = String(data.get('documentType') ?? '');
  return { documentType, documentNumber: String(data.get('documentNumber') ?? '').trim().toUpperCase(),
    documentCountryCode: documentType === 'CI' ? 'BO' : String(data.get('documentCountryCode') ?? '') };
}
export default function IdentityDocumentFields({ value = {}, required = true, disabled = false }: { value?: IdentityDocument; required?: boolean; disabled?: boolean }) {
  const [type, setType] = useState(value.documentType ?? 'CI');
  const existingCountry = value.documentCountryCode;
  return <>
    <label>Tipo de documento<select name="documentType" value={type} required={required} disabled={disabled} onChange={e => setType(e.target.value)}>
      {documentTypes.map(t => <option key={t.value} value={t.value}>{t.label}</option>)}
    </select></label>
    <label>Número de documento<input name="documentNumber" type="text" required={required} maxLength={50} defaultValue={value.documentNumber ?? ''} disabled={disabled} /></label>
    {type === 'CI' ? <input type="hidden" name="documentCountryCode" value="BO" /> :
      <label>País emisor<select name="documentCountryCode" required disabled={disabled} defaultValue={existingCountry ?? ''}>
        <option value="">Selecciona un país</option>
        {existingCountry && !issuingCountries.some(([code]) => code === existingCountry) && <option value={existingCountry}>{existingCountry}</option>}
        {issuingCountries.map(([code, label]) => <option key={code} value={code}>{label}</option>)}
      </select></label>}
  </>;
}
