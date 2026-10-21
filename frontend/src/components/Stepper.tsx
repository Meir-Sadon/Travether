import { Icon } from './Icon'
import './Stepper.css'

type StepperProps = {
  steps: string[]
  /** Index of the current step. Earlier steps show as done. */
  current: number
  /** Accessible name for the list, e.g. "Request status". */
  label: string
}

/** Horizontal status steps, e.g. Requested → Approved → Chat open (PLAN.md §4.5). */
export function Stepper({ steps, current, label }: StepperProps) {
  return (
    <ol className="stepper" aria-label={label}>
      {steps.map((step, i) => {
        const state = i < current ? 'done' : i === current ? 'current' : 'todo'
        return (
          <li key={step} className={`stepper__step stepper__step--${state}`} aria-current={state === 'current' ? 'step' : undefined}>
            <span className="stepper__dot">{state === 'done' ? <Icon name="check" size={14} /> : i + 1}</span>
            <span className="stepper__label">{step}</span>
            {state === 'done' && <span className="visually-hidden"> (done)</span>}
          </li>
        )
      })}
    </ol>
  )
}
