import { useId } from 'react';

export default function PainIntensitySelector({ defaultValue = null, required = false }: { defaultValue?: number | null; required?: boolean }) {
  const hintId = useId();
  return <fieldset className="clinical-pain-scale" aria-describedby={hintId}>
    <legend>Intensidad del dolor (0–10)</legend>
    <div className="clinical-pain-options">
      {Array.from({ length: 11 }, (_, value) => <label key={value}>
        <input type="radio" name="painIntensity" value={value} defaultChecked={defaultValue === value} required={required}/>
        <span>{value}</span>
      </label>)}
    </div>
    <p id={hintId} className="clinical-pain-hint"><span>0 = Sin dolor</span><span>10 = Dolor muy intenso</span></p>
  </fieldset>;
}
