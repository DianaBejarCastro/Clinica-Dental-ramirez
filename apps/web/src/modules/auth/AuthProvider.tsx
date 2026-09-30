import { createContext, useContext, useEffect, useState, useSyncExternalStore, type ReactNode } from 'react';
import { getSession, refresh, subscribe, type Session } from '../../services/api';
const Context = createContext<{ session: Session | null; loading: boolean }>({ session: null, loading: true });
export function AuthProvider({ children }: { children: ReactNode }) {
  const session = useSyncExternalStore(subscribe, getSession);
  const [loading, setLoading] = useState(true);
  useEffect(() => { let active = true; refresh().catch(() => undefined).finally(() => { if (active) setLoading(false); }); return () => { active = false; }; }, []);
  return <Context.Provider value={{ session, loading }}>{children}</Context.Provider>;
}
export const useAuth = () => useContext(Context);
