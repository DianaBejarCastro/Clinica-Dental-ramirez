import { useEffect, useState, type ReactNode } from 'react';
import { useAuth } from '../modules/auth/AuthProvider';
import LoginPage from '../modules/auth/LoginPage';
export function useRoute() {
  const [path, setPath] = useState(window.location.hash.slice(1) || '/');
  useEffect(() => { const change = () => setPath(window.location.hash.slice(1) || '/'); window.addEventListener('hashchange', change); return () => window.removeEventListener('hashchange', change); }, []);
  return path;
}
export function Protected({ children, permission }: { children: ReactNode; permission?: string }) {
  const { session, loading } = useAuth();
  if (loading) return <p role="status" className="loading">Restaurando sesión…</p>;
  if (!session) return <LoginPage />;
  if (permission && !session.user.permissions.includes(permission)) return <section className="card"><h2>Acceso restringido</h2><p>No tienes permiso para ver esta sección.</p></section>;
  return <>{children}</>;
}
