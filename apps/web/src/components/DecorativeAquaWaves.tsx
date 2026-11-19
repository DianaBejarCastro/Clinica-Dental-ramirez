import { useId } from 'react';

export default function DecorativeAquaWaves({ className }: { className: string }) {
  const waveId = `aqua-wave-${useId().replace(/[^a-zA-Z0-9_-]/g, '')}`;
  const hazeId = `${waveId}-haze`;
  const aquaId = `${waveId}-aqua`;

  return <svg className={className} viewBox="0 0 1600 196" preserveAspectRatio="none" aria-hidden="true" focusable="false">
    <defs>
      <linearGradient id={hazeId} x1="0%" y1="0%" x2="100%" y2="0%">
        <stop offset="0%" stopColor="#79DADB" stopOpacity=".3" />
        <stop offset="54%" stopColor="#D3F3F5" stopOpacity=".24" />
        <stop offset="100%" stopColor="#F4FAFD" stopOpacity=".12" />
      </linearGradient>
      <linearGradient id={aquaId} x1="0%" y1="25%" x2="100%" y2="70%">
        <stop offset="0%" stopColor="#39B3B6" stopOpacity=".58" />
        <stop offset="28%" stopColor="#79DADB" stopOpacity=".46" />
        <stop offset="66%" stopColor="#D3F3F5" stopOpacity=".32" />
        <stop offset="100%" stopColor="#F4FAFD" stopOpacity=".16" />
      </linearGradient>
    </defs>
    <path fill={`url(#${hazeId})`} d="M0 88c169 23 319 49 510 24 211-28 357-44 545-21 198 24 352 67 545 44v61c-167 8-319-13-492-29-203-19-356-2-555 24-202 27-384-1-553-32Z" />
    <path fill={`url(#${aquaId})`} d="M0 62c164 12 301-9 455-31C650 3 794 12 966 37c203 29 409 71 634 52v61c-195 22-347-27-545-48-190-20-348-6-551 25-190 29-340 17-504-2Z" />
  </svg>;
}
