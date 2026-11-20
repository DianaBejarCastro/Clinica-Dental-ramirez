import { useEffect, useState } from 'react';
import { apiUrl, getSession, refresh } from '../../services/api';
import type { PublicBranch } from './BranchesSection';
import './team-section.css';

export interface PublicDoctor {
  id: string;
  firstName: string;
  lastName: string;
  specialties: string[];
  photoVersion: string | null;
}

export function Portrait({ doctor }: { doctor: PublicDoctor }) {
  const [failed, setFailed] = useState(false);
  return <div className="team-portrait">
    {doctor.photoVersion && !failed
      ? <img loading="lazy" width={1080} height={1350}
        src={apiUrl(`/api/clinic/team/${doctor.id}/photo?v=${encodeURIComponent(doctor.photoVersion)}`)}
        alt={`${doctor.firstName} ${doctor.lastName}`} onError={() => setFailed(true)} />
      : <span className="team-initials" role="img"
        aria-label={`Sin foto de ${doctor.firstName} ${doctor.lastName}`}>
        {doctor.firstName[0]}{doctor.lastName[0]}
      </span>}
  </div>;
}

function SearchIcon() {
  return <svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="10.8" cy="10.8" r="6.8" /><path d="m16 16 5 5" /></svg>;
}

function ToothIcon() {
  return <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 5C8 2 3 3 4 9c1 5 2 12 5 12 2 0 1-7 3-7s1 7 3 7c3 0 4-7 5-12 1-6-4-7-8-4Z" /></svg>;
}

function PinIcon() {
  return <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M20 10c0 5-8 12-8 12S4 15 4 10a8 8 0 1 1 16 0Z" /><circle cx="12" cy="10" r="2.5" /></svg>;
}

function CalendarIcon() {
  return <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M8 3v4m8-4v4M4 10h16M5 5h14a1 1 0 0 1 1 1v14H4V6a1 1 0 0 1 1-1Z" /></svg>;
}

function ViewGridIcon() {
  return <svg viewBox="0 0 24 24" aria-hidden="true"><rect x="3" y="3" width="7" height="7" rx="1" /><rect x="14" y="3" width="7" height="7" rx="1" /><rect x="3" y="14" width="7" height="7" rx="1" /><rect x="14" y="14" width="7" height="7" rx="1" /></svg>;
}

function ViewListIcon() {
  return <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M9 5h12M9 12h12M9 19h12" /><circle cx="4" cy="5" r="1" /><circle cx="4" cy="12" r="1" /><circle cx="4" cy="19" r="1" /></svg>;
}

function FilterIcon() {
  return <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M4 7h16M4 17h16" /><circle cx="9" cy="7" r="2" /><circle cx="15" cy="17" r="2" /></svg>;
}

function SortIcon() {
  return <svg viewBox="0 0 24 24" aria-hidden="true"><path d="m7 10 5 5 5-5" /></svg>;
}

function ArrowIcon({ direction }: { direction: 'left' | 'right' }) {
  return <svg viewBox="0 0 24 24" aria-hidden="true"><path d={direction === 'left' ? 'm14 5-7 7 7 7' : 'm10 5 7 7-7 7'} /></svg>;
}

export default function TeamSection({ branches }: { branches?: PublicBranch[] | null } = {}) {
  const [message, setMessage] = useState('');
  const [opening, setOpening] = useState(false);
  const [doctors, setDoctors] = useState<PublicDoctor[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState('');

  async function book(id: string) {
    setOpening(true);
    setMessage('');
    try {
      const session = getSession() ?? await refresh().catch(() => null);
      if (!session) window.location.hash = `/login?bookingDoctor=${encodeURIComponent(id)}`;
      else if (session.user.roles.includes('PATIENT')) window.location.hash = `/patient/book?doctor=${encodeURIComponent(id)}`;
      else setMessage('Esta opción está disponible solo para pacientes.');
    } finally {
      setOpening(false);
    }
  }

  useEffect(() => {
    const controller = new AbortController();
    fetch(apiUrl('/api/clinic/team'), { signal: controller.signal })
      .then(response => {
        if (!response.ok) throw new Error('No se pudo cargar el equipo.');
        return response.json();
      })
      .then(data => {
        if (!controller.signal.aborted) setDoctors(Array.isArray(data) ? data : []);
      })
      .catch(error => {
        if (!controller.signal.aborted) setLoadError(error instanceof Error ? error.message : 'No se pudo cargar el equipo.');
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, []);

  const specialtyCounts = new Map<string, number>();
  for (const doctor of doctors) {
    for (const specialty of new Set(doctor.specialties)) {
      specialtyCounts.set(specialty, (specialtyCounts.get(specialty) ?? 0) + 1);
    }
  }
  const specialties = [...specialtyCounts].sort(([first], [second]) => first.localeCompare(second, 'es'));
  const branchList = branches ?? [];
  const previewDescription = 'La búsqueda, los filtros, el orden y la paginación son una vista previa y todavía no están activos.';

  return <section id="equipo" className="public-team-section" aria-labelledby="equipo-title" tabIndex={-1}>
    <header className="team-directory-hero">
      <div className="team-hero-container">
        <p className="team-directory-eyebrow">NUESTRO EQUIPO</p>
        <h2 id="equipo-title"><span>Agenda tu cita</span><span>con <strong>nuestros especialistas</strong></span></h2>
        <p className="team-directory-intro">Elige al profesional, revisa su disponibilidad y reserva tu cita en pocos pasos.</p>
      </div>
    </header>

    <div className="team-directory-body">
      <div className="team-directory-container">
        <p id="team-preview-note" className="team-preview-note" role="note">{previewDescription}</p>
        <div className="team-preview-toolbar" role="group" aria-label="Búsqueda y vistas, solo vista previa" aria-describedby="team-preview-note" aria-disabled="true">
          <label className="team-preview-search">
            <SearchIcon />
            <input type="search" disabled placeholder="Buscar por nombre, especialidad o servicio…" aria-label="Buscar profesionales (vista previa)" />
          </label>
          <label className="team-preview-select">
            <ToothIcon />
            <select disabled defaultValue="" aria-label="Especialidades (vista previa)">
              <option value="">Todas las especialidades</option>
              {specialties.map(([specialty]) => <option key={specialty} value={specialty}>{specialty}</option>)}
            </select>
            <SortIcon />
          </label>
          <label className="team-preview-select">
            <PinIcon />
            <select disabled defaultValue="" aria-label="Sucursales (vista previa)">
              <option value="">Todas las sucursales</option>
              {branchList.map(branch => <option key={branch.id} value={branch.id}>{branch.name}</option>)}
            </select>
            <SortIcon />
          </label>
          <div className="team-preview-view" role="group" aria-label="Modo de visualización (vista previa)" aria-describedby="team-preview-note">
            <button type="button" className="is-selected" disabled aria-label="Vista de cuadrícula (vista previa)" aria-pressed="true"><ViewGridIcon /></button>
            <button type="button" disabled aria-label="Vista de lista (vista previa)" aria-pressed="false"><ViewListIcon /></button>
          </div>
        </div>

        <div className="team-results-layout">
          <aside className="team-filter-panel" aria-labelledby="team-filter-title" aria-describedby="team-preview-note">
            <h3 id="team-filter-title"><FilterIcon />Filtrar profesionales</h3>
            <fieldset disabled>
              <legend>Especialidad</legend>
              {loading && <p className="team-filter-message">Cargando especialidades…</p>}
              {!loading && specialties.length === 0 && <p className="team-filter-message">No hay especialidades para mostrar.</p>}
              {specialties.map(([specialty, count]) => <label className="team-filter-option" key={specialty}>
                <input type="checkbox" disabled />
                <span>{specialty}</span>
                <span className="team-filter-count">{count}</span>
              </label>)}
            </fieldset>
            <fieldset disabled>
              <legend>Sucursal</legend>
              {branches === null && <p className="team-filter-message">Cargando sucursales…</p>}
              {branches !== null && branchList.length === 0 && <p className="team-filter-message">No hay sucursales para mostrar.</p>}
              {branchList.map(branch => <label className="team-filter-option" key={branch.id}>
                <input type="checkbox" disabled />
                <span>{branch.name}</span>
              </label>)}
            </fieldset>
            <button type="button" className="team-filter-clear" disabled><span aria-hidden="true">↻</span>Limpiar filtros</button>
          </aside>

          <div className="team-results">
            <div className="team-results-heading">
              <p className="team-results-count" aria-live="polite">
                {loading ? 'Consultando profesionales…' : loadError || <>
                  <strong>{doctors.length}</strong> {doctors.length === 1 ? 'profesional' : 'profesionales'} en nuestro equipo
                </>}
              </p>
              <button type="button" className="team-sort-preview" disabled aria-describedby="team-preview-note">
                Ordenar profesionales <SortIcon />
              </button>
            </div>

            {message && <p role="alert" className="team-booking-message">{message}</p>}
            {loadError && <p role="alert" className="team-booking-message">{loadError}</p>}
            {loading && <p role="status" className="team-results-status">Cargando profesionales…</p>}
            {!loading && !loadError && doctors.length > 0 && <div className="public-team-grid">
              {doctors.map(doctor => <article className="public-team-card" key={doctor.id}>
                <Portrait doctor={doctor} />
                <div className="public-team-caption">
                  <h3>{doctor.firstName} {doctor.lastName}</h3>
                  {doctor.specialties.length > 0 && <p>
                    <ToothIcon /><span>{doctor.specialties.join(' · ')}</span>
                  </p>}
                  <button className="public-button" disabled={opening} onClick={() => void book(doctor.id)} aria-label={`Reservar cita con ${doctor.firstName} ${doctor.lastName}`}>
                    <CalendarIcon />Reservar cita
                  </button>
                </div>
              </article>)}
            </div>}
            {!loading && !loadError && doctors.length === 0 && <div className="public-team-empty team-directory-empty">
              <span className="public-team-symbol" aria-hidden="true">+</span>
              <div><h3>Conoce a quienes te acompañan</h3><p>Consulta con la clínica qué profesional puede atenderte.</p></div>
            </div>}
            {loadError && <p className="team-results-status">No se pudo mostrar el equipo en este momento.</p>}

            {!loading && !loadError && doctors.length > 0 && <div className="team-preview-pagination" role="group" aria-label="Paginación, solo vista previa" aria-describedby="team-preview-note">
              <button type="button" disabled aria-label="Página anterior (vista previa)"><ArrowIcon direction="left" /></button>
              <span>Paginación no activa</span>
              <button type="button" disabled aria-label="Página siguiente (vista previa)"><ArrowIcon direction="right" /></button>
            </div>}
          </div>
        </div>
      </div>
    </div>
  </section>;
}
