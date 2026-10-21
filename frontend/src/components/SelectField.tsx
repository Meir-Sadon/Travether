import { useId, type SelectHTMLAttributes } from 'react'
import './TextField.css'

type SelectFieldProps = SelectHTMLAttributes<HTMLSelectElement> & {
  label: string
  hint?: string
  options: { value: string; label: string }[]
  /** Shown first with an empty value, e.g. "Choose a country". */
  placeholder?: string
}

/** Native select styled like TextField: the platform picker is the most usable on phones. */
export function SelectField({ label, hint, options, placeholder, ...rest }: SelectFieldProps) {
  const id = useId()
  const hintId = `${id}-hint`
  return (
    <div className="field">
      <label htmlFor={id} className="field__label">
        {label}
      </label>
      <select {...rest} id={id} className="field__control field__control--select" aria-describedby={hint ? hintId : undefined}>
        {placeholder !== undefined && <option value="">{placeholder}</option>}
        {options.map((o) => (
          <option key={o.value} value={o.value}>
            {o.label}
          </option>
        ))}
      </select>
      {hint && (
        <p id={hintId} className="field__hint">
          {hint}
        </p>
      )}
    </div>
  )
}
