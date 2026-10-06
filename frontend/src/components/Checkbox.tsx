import { useId, type InputHTMLAttributes, type ReactNode } from 'react'
import './Checkbox.css'

type CheckboxProps = Omit<InputHTMLAttributes<HTMLInputElement>, 'type'> & { children: ReactNode }

/** A labelled checkbox with a large tap target. */
export function Checkbox({ children, ...rest }: CheckboxProps) {
  const id = useId()
  return (
    <div className="checkbox">
      <input {...rest} id={id} type="checkbox" className="checkbox__input" />
      <label htmlFor={id} className="checkbox__label">
        {children}
      </label>
    </div>
  )
}
