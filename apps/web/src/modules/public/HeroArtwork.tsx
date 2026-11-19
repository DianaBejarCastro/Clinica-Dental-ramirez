import HeroTooth3D from './HeroTooth3D';
import DecorativeAquaWaves from '../../components/DecorativeAquaWaves';

export function HeroBackground() {
  return <div className="hero-clinic-scene" aria-hidden="true">
      <svg className="hero-clinic-room" viewBox="0 0 600 550" fill="none">
        <path d="M30 0v550M300 0v550M590 0v550M0 180h600" stroke="#fff" strokeWidth="12"/>
        <path d="M440 40v90h-120v80" stroke="#88b9ba" strokeWidth="12" strokeLinejoin="round"/>
        <ellipse cx="320" cy="215" rx="65" ry="27" fill="#fff" stroke="#a9d1d1" strokeWidth="12"/>
        <path d="M300 340 360 245" stroke="#52bcb8" strokeWidth="80" strokeLinecap="round"/>
        <path d="m300 340 130 65h80" stroke="#68c9c3" strokeWidth="62" strokeLinecap="round"/>
        <path d="M380 410v70m-65 20h140" stroke="#b7cece" strokeWidth="24" strokeLinecap="round"/>
        <path d="M520 340v125m-35-130h70" stroke="#a0bfc1" strokeWidth="14"/>
      </svg>
    </div>;
}

export default function HeroArtwork() {
  return <div className="public-hero-visual">
        <DecorativeAquaWaves className="public-footer-wave public-hero-waves" />
        <HeroTooth3D />
        <svg className="public-hero-shield" viewBox="0 0 140 165" aria-hidden="true">
          <defs><linearGradient id="hero-shield-fill" x2="1" y2="1"><stop stopColor="#53e9de"/><stop offset="1" stopColor="#008c86"/></linearGradient></defs>
          <path d="M70 8 128 30v47c0 36-58 77-58 77S12 113 12 77V30Z" fill="url(#hero-shield-fill)" stroke="#c2fffa" strokeWidth="5"/>
          <path d="M70 21 116 39v38c0 26-46 62-46 62S24 103 24 77V39Z" fill="none" stroke="#008a83" strokeWidth="2"/>
          <path d="M59 48h22v21h21v22H81v21H59V91H38V69h21Z" fill="white"/>
        </svg>
        <svg className="public-hero-mirror" viewBox="0 0 100 310" aria-hidden="true">
          <defs><linearGradient id="hero-mirror-metal"><stop stopColor="#789598"/><stop offset=".4" stopColor="#f8ffff"/><stop offset=".7" stopColor="#b6cdcf"/><stop offset="1" stopColor="#718c90"/></linearGradient></defs>
          <rect x="42" y="94" width="16" height="200" rx="8" fill="url(#hero-mirror-metal)" stroke="#789598"/>
          <ellipse cx="50" cy="52" rx="34" ry="46" fill="url(#hero-mirror-metal)" stroke="#789598" strokeWidth="3"/>
          <ellipse cx="50" cy="52" rx="27" ry="38" fill="#effafa" stroke="white" strokeWidth="2"/>
        </svg>
      </div>;
}
