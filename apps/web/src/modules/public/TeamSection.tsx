import { useEffect, useState } from 'react';
import { apiUrl, getSession, refresh } from '../../services/api';
import type { PublicBranch } from './BranchesSection';
import './team-section.css';
import { normalizeSearch } from './teamFilters';

export interface PublicDoctor {
  id: string;
  firstName: string;
  lastName: string;
  specialties: string[];
  photoVersion: string | null;
  specialtyOptions?: { id: string; name: string }[];
  branches?: { id: string; name: string }[];
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

export default function TeamSection(_props: { branches?: PublicBranch[] | null } = {}) {
  const [search, setSearch] = useState('');
  const [selectedSpecialties, setSpecialties] = useState<string[]>([]);
  const [selectedBranches, setBranches] = useState<string[]>([]);
  const [sort, setSort] = useState('default');
  const [view, setView] = useState('grid');
  const [page, setPage] = useState(1);
  const clear = () => { setSearch(''); setSpecialties([]); setBranches([]); setSort('default'); setPage(1); };
  const toggle = (ids: string[], id: string) => ids.includes(id) ? ids.filter(x => x !== id) : [...ids, id];
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

  const refs = (doctor: PublicDoctor) => doctor.specialtyOptions ?? doctor.specialties.map(name => ({ id: name, name }));
  const specialtyCounts = new Map<string, { name: string; count: number }>();
  const branchesById = new Map<string, { id: string; name: string }>();
  for (const doctor of doctors) {
    for (const [id, specialty] of new Map(refs(doctor).map(item => [item.id, item]))) {
      specialtyCounts.set(id, { name: specialty.name, count: (specialtyCounts.get(id)?.count ?? 0) + 1 });
    }
    for (const branch of doctor.branches ?? []) branchesById.set(branch.id, branch);
  }
  const specialties = [...specialtyCounts].sort((a, b) => a[1].name.localeCompare(b[1].name, 'es'));
  const branchList = [...branchesById.values()].sort((a, b) => a.name.localeCompare(b.name, 'es'));
  const words = normalizeSearch(search).split(' ').filter(Boolean);
  const filtered = doctors.filter(doctor => {
    const text = normalizeSearch(`${doctor.firstName} ${doctor.lastName} ${refs(doctor).map(s => s.name).join(' ')}`);
    return words.every(word => text.includes(word)) &&
      (!selectedSpecialties.length || refs(doctor).some(s => selectedSpecialties.includes(s.id))) &&
      (!selectedBranches.length || doctor.branches?.some(b => selectedBranches.includes(b.id)));
  });
  if (sort !== 'default') filtered.sort((a, b) => (`${a.firstName} ${a.lastName}`).localeCompare(`${b.firstName} ${b.lastName}`, 'es', { sensitivity: 'base' }) * (sort === 'asc' ? 1 : -1));
  const pages = Math.max(1, Math.ceil(filtered.length / 6));
  const visible = filtered.slice((page - 1) * 6, page * 6);

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
        <div className="team-preview-toolbar" role="group" aria-label="Búsqueda y vistas">
          <label className="team-preview-search">
            <SearchIcon />
            <input type="search" value={search} onChange={e => { setSearch(e.target.value); setPage(1); }} placeholder="Buscar por nombre o especialidad…" aria-label="Buscar profesionales" />
          </label>
          <label className="team-preview-select">
            <ToothIcon />
            <select value={selectedSpecialties.length === 1 ? selectedSpecialties[0] : selectedSpecialties.length ? "multiple" : ""} onChange={e => { setSpecialties(e.target.value ? [e.target.value] : []); setPage(1); }} aria-label="Especialidades">
              <option value="">Todas las especialidades</option>
              {selectedSpecialties.length > 1 && <option value="multiple" disabled>Varias especialidades</option>}
              {specialties.map(([id, specialty]) => <option key={id} value={id}>{specialty.name}</option>)}
            </select>
            <SortIcon />
          </label>
          <label className="team-preview-select">
            <PinIcon />
            <select value={selectedBranches.length === 1 ? selectedBranches[0] : selectedBranches.length ? "multiple" : ""} onChange={e => { setBranches(e.target.value ? [e.target.value] : []); setPage(1); }} aria-label="Sucursales">
              <option value="">Todas las sucursales</option>
              {selectedBranches.length > 1 && <option value="multiple" disabled>Varias sucursales</option>}
              {branchList.map(branch => <option key={branch.id} value={branch.id}>{branch.name}</option>)}
            </select>
            <SortIcon />
          </label>
          <div className="team-preview-view" role="group" aria-label="Modo de visualización">
            <button type="button" className={view === "grid" ? "is-selected public-button-primary" : ""} onClick={() => setView("grid")} aria-label="Vista de cuadrícula" aria-pressed={view === "grid"}><ViewGridIcon /></button>
            <button type="button" className={view === "list" ? "is-selected" : ""} onClick={() => setView("list")} aria-label="Vista de lista" aria-pressed={view === "list"}><ViewListIcon /></button>
          </div>
        </div>

        <div className="team-results-layout">
          <aside className="team-filter-panel" aria-labelledby="team-filter-title">
            <h3 id="team-filter-title"><FilterIcon />Filtrar profesionales</h3>
            <fieldset disabled={loading}>
              <legend>Especialidad</legend>
              {loading && <p className="team-filter-message">Cargando especialidades…</p>}
              {!loading && specialties.length === 0 && <p className="team-filter-message">No hay especialidades para mostrar.</p>}
              {specialties.map(([specialty, { name, count }]) => <label className="team-filter-option" key={specialty}>
                <input type="checkbox" checked={selectedSpecialties.includes(specialty)} onChange={() => { setSpecialties(toggle(selectedSpecialties, specialty)); setPage(1); }} />
                <span>{name}</span>
                <span className="team-filter-count">{count}</span>
              </label>)}
            </fieldset>
            <fieldset disabled={loading}>
              <legend>Sucursal</legend>
              {loading && <p className="team-filter-message">Cargando sucursales…</p>}
              {!loading && branchList.length === 0 && <p className="team-filter-message">No hay sucursales para mostrar.</p>}
              {branchList.map(branch => <label className="team-filter-option" key={branch.id}>
                <input type="checkbox" checked={selectedBranches.includes(branch.id)} onChange={() => { setBranches(toggle(selectedBranches, branch.id)); setPage(1); }} />
                <span>{branch.name}</span>
              </label>)}
            </fieldset>
            <button type="button" className="team-filter-clear" onClick={clear}><span aria-hidden="true">↻</span>Limpiar filtros</button>
          </aside>

          <div className="team-results">
            <div className="team-results-heading">
              <p className="team-results-count" aria-live="polite">
                {loading ? 'Consultando profesionales…' : loadError || <>
                  <strong>{filtered.length}</strong> {filtered.length === 1 ? 'profesional' : 'profesionales'} en nuestro equipo
                </>}
              </p>
              <select className="team-sort-preview" aria-label="Ordenar profesionales" value={sort} onChange={e => { setSort(e.target.value); setPage(1); }}><option value="default">Ordenar profesionales</option><option value="asc">Nombre A–Z</option><option value="desc">Nombre Z–A</option></select>
            </div>

            {message && <p role="alert" className="team-booking-message">{message}</p>}
            {loadError && <p role="alert" className="team-booking-message">{loadError}</p>}
            {loading && <p role="status" className="team-results-status">Cargando profesionales…</p>}
            {!loading && !loadError && filtered.length > 0 && <div className="public-team-grid" style={view === 'list' ? { gridTemplateColumns: 'minmax(0, 1fr)' } : undefined}>
              {visible.map(doctor => <article className="public-team-card" key={doctor.id}>
                <Portrait doctor={doctor} />
                <div className="public-team-caption">
                  <h3>{doctor.firstName} {doctor.lastName}</h3>
                  {doctor.specialties.length > 0 && <p>
                    <ToothIcon /><span>{doctor.specialties.join(' · ')}</span>
                  </p>}
                  <button className="public-button public-button-primary" disabled={opening} onClick={() => void book(doctor.id)} aria-label={`Reservar cita con ${doctor.firstName} ${doctor.lastName}`}>
                    <CalendarIcon />Reservar cita
                  </button>
                </div>
              </article>)}
            </div>}
            {!loading && !loadError && doctors.length === 0 && <div className="public-team-empty team-directory-empty">
              <span className="public-team-symbol" aria-hidden="true">+</span>
              <div><h3>Conoce a quienes te acompañan</h3><p>Consulta con la clínica qué profesional puede atenderte.</p></div>
            </div>}
            {!loading && !loadError && doctors.length > 0 && filtered.length === 0 && <div className="team-directory-empty"><p>No encontramos profesionales con estos filtros.</p><button type="button" onClick={clear}>Limpiar filtros</button></div>}
            {loadError && <p className="team-results-status">No se pudo mostrar el equipo en este momento.</p>}

            {!loading && !loadError && filtered.length > 0 && <div className="team-preview-pagination" role="group" aria-label="Paginación">
              <button type="button" disabled={page === 1} onClick={() => setPage(p => p - 1)} aria-label="Página anterior"><ArrowIcon direction="left" /></button>
              <span>Página {page} de {pages}</span>
              <button type="button" disabled={page === pages} onClick={() => setPage(p => p + 1)} aria-label="Página siguiente"><ArrowIcon direction="right" /></button>
            </div>}
          </div>
        </div>
      </div>
    </div>
  </section>;
}
