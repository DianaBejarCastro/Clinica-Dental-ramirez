import { useEffect, useState } from 'react';
import { api, type Permission } from '../../../services/api';
export default function PermissionsPage() {
  const [items, setItems] = useState<Permission[]>([]); const [error, setError] = useState(''); const [loading, setLoading] = useState(true);
  useEffect(() => { api<Permission[]>('/api/admin/permissions').then(setItems).catch(e => setError(e.message)).finally(() => setLoading(false)); }, []);
  return <><h1>Permisos</h1><p className="muted">Catálogo de acceso por módulo. Los permisos se asignan desde Roles.</p>{error && <p role="alert" className="error">{error}</p>}{loading && <p role="status">Cargando permisos…</p>}<div className="grid">{Array.from(new Set(items.map(p => p.module))).map(module => <section className="card" key={module}><h2>{module}</h2><ul className="permission-list">{items.filter(p => p.module === module).map(p => <li key={p.id}>{p.code}</li>)}</ul></section>)}</div></>;
}
