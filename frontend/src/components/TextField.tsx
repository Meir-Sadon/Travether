import { useId, type InputHTMLAttributes, type TextareaHTMLAttributes } from 'react'
import './TextField.css'

type Common = {
  label: string
  /** Helper text under the field, e.g. a privacy note. */
  hint?: string
}

type InputProps = Common & InputHTMLAttributes<HTMLInputElement> & { multiline?: false }
type AreaProps = Common & TextareaHTMLAttributes<HTMLTextAreaElement> & { multiline: true }

/** Labelled input or textarea with optional hint, wired with aria-describedby. */
export function TextField(props: InputProps | AreaProps) {
  const id = useId()
  const hintId = `${id}-hint`
  const { label, hint } = props

  return (
    <div className="field">
      <label htmlFor={id} className="field__label">
        {label}
      </label>
      {props.multiline ? (
        <textarea
          {...withoutCommon(props)}
          id={id}
          className="field__control field__control--area"
          aria-describedby={hint ? hintId : undefined}
        />
      ) : (
        <input {...withoutCommon(props)} id={id} className="field__control" aria-describedby={hint ? hintId : undefined} />
      )}
      {hint && (
        <p id={hintId} className="field__hint">
          {hint}
        </p>
      )}
    </div>
  )
}

function withoutCommon<T extends Common & { multiline?: boolean }>(props: T) {
  // eslint-disable-next-line @typescript-eslint/no-unused-vars
  const { label: _label, hint: _hint, multiline: _multiline, ...rest } = props
  return rest
}
