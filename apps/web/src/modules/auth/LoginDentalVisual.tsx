import HeroArtwork, { HeroBackground } from '../public/HeroArtwork';
import '../public/public-hero.css';

export default function LoginDentalVisual() {
  return <div className="public-hero login-hero-art">
    <HeroBackground />
    <HeroArtwork />
  </div>;
}
