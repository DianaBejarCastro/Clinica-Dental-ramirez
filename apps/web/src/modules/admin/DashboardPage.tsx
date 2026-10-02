import { useEffect, useState } from 'react';
import { api } from '../../services/api';
import { useAuth } from '../auth/AuthProvider';
import { Feedback } from './shared';
import type { Dashboard } from './types';
const metrics: { key: keyof Dashboard; label: string; url: string; permission: string }[] = [
  { key: 'totalUsers', label: 'Total usuarios', url: '/users', permission: 'users.view' }, { key: 'totalDoctors', label: 'Total doctores', url: '/doctors', permission: 'doctors.view' },
  { key: 'activeDoctors', label: 'Doctores activos', url: '/doctors', permission: 'doctors.view' }, { key: 'totalCenters', label: 'Total sucursales', url: '/centers', permission: 'centers.view' },
  { key: 'activeCenters', label: 'Sucursales activas', url: '/centers', permission: 'centers.view' }, { key: 'totalSpecialties', label: 'Total especialidades', url: '/specialties', permission: 'specialties.view' },
];
export default function DashboardPage() {
  const { session } = useAuth(); const [summary, setSummary] = useState<Dashboard | null>(null); const [error, setError] = useState('');
  const visibleMetrics = metrics.filter(metric => session?.user.permissions.includes(metric.permission));
  const summaryPermissions = visibleMetrics.map(metric => metric.key).join(',');
  const canViewSummary = visibleMetrics.length > 0;
  useEffect(() => {
    let active = true;
    setSummary(null); setError('');
    if (summaryPermissions) api<Dashboard>('/api/admin/dashboard')
      .then(value => { if (active) setSummary(value); })
      .catch(e => { if (active) setError(e.message); });
    return () => { active = false; };
  }, [summaryPermissions]);
  return <><p className="eyebrow">RESUMEN</p><h1>Hola, {session?.user.firstName}.</h1><p className="muted">Bienvenido a la administración de Clínica Dental RAMÍREZ.</p><Feedback error={error} />
    <div className="metrics-grid">{visibleMetrics.filter(metric => summary?.[metric.key] != null).map(metric => <a className="card metric" key={metric.key} href={`#${metric.url}`}><span>{metric.label}</span><strong>{summary![metric.key]}</strong><small>Ver registros →</small></a>)}</div>
    {canViewSummary && !summary && !error && <p role="status">Cargando resumen…</p>}<section className="card"><h2>Tu espacio de trabajo</h2><p>Usa el menú para gestionar el equipo, las sedes y la disponibilidad profesional.</p><p><strong>Roles:</strong> {session?.user.roles.join(', ') || 'Sin roles asignados'}</p>{(!canViewSummary || summary && !visibleMetrics.some(metric => summary[metric.key] != null)) && <p className="muted">No hay contadores disponibles para tus permisos actuales.</p>}</section></>;
}
