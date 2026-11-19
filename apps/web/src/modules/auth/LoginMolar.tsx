import { useState, type ReactNode } from 'react';

// Vite resolves only files actually supplied; no missing-asset request or JS binary payload.
const files = import.meta.glob<string>('../../assets/login/tooth-molar-login*.{webp,avif}', {
  eager: true, query: '?url&no-inline', import: 'default',
});
const asset = (name: string) => files[`../../assets/login/${name}`];
export const loginMolarSources = {
  webp: asset('tooth-molar-login.webp'),
  avif: asset('tooth-molar-login.avif'),
  mobile: asset('tooth-molar-login-mobile.webp'),
};
type Sources = { webp?: string; avif?: string; mobile?: string };

export default function LoginMolar({ children, sources = loginMolarSources }: { children: ReactNode; sources?: Sources }) {
  const [loaded, setLoaded] = useState(false);
  const [failed, setFailed] = useState(false);
  return <>
    <g data-login-molar-fallback="" visibility={loaded && !failed ? 'hidden' : 'visible'}>{children}</g>
    {sources.webp && !failed && <foreignObject x="150" y="125" width="200" height="260" pointerEvents="none">
      <picture>
        {sources.mobile && <source media="(max-width: 767px)" srcSet={sources.mobile} type="image/webp" />}
        {sources.avif && <source srcSet={sources.avif} type="image/avif" />}
        <img className="login-molar-image" src={sources.webp} width="800" height="1040" alt="" decoding="async" loading="eager" fetchPriority="high"
          style={{ opacity: loaded ? 1 : 0 }} onLoad={() => setLoaded(true)} onError={() => setFailed(true)} />
      </picture>
    </foreignObject>}
  </>;
}
