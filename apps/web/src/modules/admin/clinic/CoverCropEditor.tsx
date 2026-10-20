import { useEffect, useId, useRef, useState } from 'react';
import { clamp, coverCrop, renderCover } from './coverCrop';

export default function CoverCropEditor({ file, onApply, onCancel }: {
  file: File; onApply: (blob: Blob) => void; onCancel: () => void;
}) {
  const dialog = useRef<HTMLDialogElement>(null), image = useRef<HTMLImageElement>(null);
  const drag = useRef<{ x: number; y: number; px: number; py: number } | null>(null);
  const id = useId(); const [url, setUrl] = useState('');
  const [size, setSize] = useState({ w: 0, h: 0 }); const [zoom, setZoom] = useState(1);
  const [position, setPosition] = useState({ x: 0.5, y: 0.5 });
  const [busy, setBusy] = useState(false), [error, setError] = useState('');
  useEffect(() => {
    const previous = document.activeElement as HTMLElement | null;
    const src = URL.createObjectURL(file); setUrl(src); dialog.current?.showModal();
    return () => { URL.revokeObjectURL(src); previous?.focus(); };
  }, [file]);
  const crop = size.w ? coverCrop(size.w, size.h, zoom, position.x, position.y) : null;
  function reset() { setZoom(1); setPosition({ x: 0.5, y: 0.5 }); }
  return <dialog ref={dialog} className="cover-dialog" aria-labelledby={`${id}-title`} onCancel={event => { event.preventDefault(); if (!busy) onCancel(); }}>
    <h2 id={`${id}-title`}>Editar foto de portada</h2>
    <p>Encuadre fijo 1916 × 821 px. Arrastra la imagen o usa las flechas del teclado para moverla.</p>
    {error && <p role="alert">{error}</p>}
    <div className="cover-crop-frame" role="group" aria-label="Encuadre de portada" tabIndex={0} aria-describedby={`${id}-help`}
      onKeyDown={event => {
        if (busy || !['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown'].includes(event.key)) return;
        event.preventDefault(); setPosition(p => ({ x: clamp(p.x + (event.key === 'ArrowRight' ? .02 : event.key === 'ArrowLeft' ? -.02 : 0)), y: clamp(p.y + (event.key === 'ArrowDown' ? .02 : event.key === 'ArrowUp' ? -.02 : 0)) }));
      }}
      onPointerDown={event => { if (!crop || busy) return; event.currentTarget.setPointerCapture(event.pointerId); drag.current = { x: event.clientX, y: event.clientY, px: position.x, py: position.y }; }}
      onPointerUp={() => { drag.current = null; }} onPointerCancel={() => { drag.current = null; }}
      onPointerMove={event => {
        if (!drag.current || !crop || busy) return;
        const rect = event.currentTarget.getBoundingClientRect();
        setPosition({ x: size.w > crop.width ? clamp(drag.current.px - (event.clientX - drag.current.x) / rect.width * crop.width / (size.w - crop.width)) : .5,
          y: size.h > crop.height ? clamp(drag.current.py - (event.clientY - drag.current.y) / rect.height * crop.height / (size.h - crop.height)) : .5 });
      }}>
      {url && <img ref={image} src={url} alt="Imagen para recortar" draggable={false}
        onLoad={event => setSize({ w: event.currentTarget.naturalWidth, h: event.currentTarget.naturalHeight })}
        onError={() => { setSize({ w: 0, h: 0 }); setError('No se pudo abrir la imagen.'); }}
        style={crop ? { width: `${size.w / crop.width * 100}%`, height: `${size.h / crop.height * 100}%`, left: `${-crop.x / crop.width * 100}%`, top: `${-crop.y / crop.height * 100}%` } : { visibility: 'hidden' }} />}
    </div>
    <p id={`${id}-help`} className="muted">La imagen siempre cubre el área completa, sin deformarse.</p>
    <div className="cover-zoom"><button type="button" aria-label="Alejar" disabled={busy || zoom <= 1} onClick={() => setZoom(z => clamp(z - .1, 1, 3))}>−</button>
      <label>Zoom<input type="range" min="1" max="3" step="0.01" value={zoom} disabled={busy} onChange={event => setZoom(Number(event.target.value))} /></label>
      <output>{zoom.toFixed(2)}×</output><button type="button" aria-label="Acercar" disabled={busy || zoom >= 3} onClick={() => setZoom(z => clamp(z + .1, 1, 3))}>+</button></div>
    <div className="actions"><button type="button" className="secondary" disabled={busy} onClick={reset}>Restablecer</button>
      <button type="button" className="secondary" disabled={busy} onClick={onCancel}>Cancelar</button>
      <button type="button" disabled={busy || !crop} onClick={async () => {
        if (!image.current) return; setBusy(true); setError('');
        try { onApply(await renderCover(image.current, zoom, position.x, position.y, file.type)); }
        catch (e) { setError((e as Error).message); setBusy(false); }
      }}>{busy ? 'Procesando…' : 'Aplicar recorte'}</button></div>
  </dialog>;
}
