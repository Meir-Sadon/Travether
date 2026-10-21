import type { ReactNode } from 'react'
import { Icon, type IconName } from './Icon'
import './Chip.css'

export type ChipTone = 'neutral' | 'accent' | 'success' | 'inverse'

type ChipProps = {
  children: ReactNode
  tone?: ChipTone
  /** Background override, e.g. a category tint (var(--tint-hike)). */
  tint?: string
  icon?: IconName
  size?: 'sm' | 'md'
  /**
   * Makes the chip a toggle button (filters, interests). Omit for a static label.
   * The pressed state is announced to screen readers.
   */
  onToggle?: (selected: boolean) => void
  selected?: boolean
}

export function Chip({ children, tone = 'neutral', tint, icon, size = 'sm', onToggle, selected = false }: ChipProps) {
  const classes = ['chip', `chip--${tone}`, `chip--${size}`, onToggle && 'chip--toggle', selected && 'chip--selected']
    .filter(Boolean)
    .join(' ')
  const content = (
    <>
      {selected && onToggle ? <Icon name="check" size={14} /> : icon && <Icon name={icon} size={14} />}
      {children}
    </>
  )

  if (onToggle) {
    return (
      <button type="button" className={classes} aria-pressed={selected} onClick={() => onToggle(!selected)}>
        {content}
      </button>
    )
  }

  return (
    <span className={classes} style={tint ? { background: tint } : undefined}>
      {content}
    </span>
  )
}
