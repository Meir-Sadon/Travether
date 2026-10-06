import type { ButtonHTMLAttributes, ReactNode } from 'react'
import { Icon, type IconName } from './Icon'
import './Button.css'

export type ButtonVariant = 'primary' | 'secondary' | 'ghost' | 'brand'
export type ButtonSize = 'sm' | 'md' | 'lg'

type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  /** primary: lime with ink text (one per screen) · brand: ink · secondary: outlined · ghost: text only */
  variant?: ButtonVariant
  size?: ButtonSize
  /** Stretch to the container's width. */
  block?: boolean
  icon?: IconName
  /** Shows a spinner, disables the button and announces "busy". */
  loading?: boolean
  children: ReactNode
}

export function Button({
  variant = 'primary',
  size = 'md',
  block = false,
  icon,
  loading = false,
  disabled,
  type = 'button',
  className,
  children,
  ...rest
}: ButtonProps) {
  const classes = ['btn', `btn--${variant}`, `btn--${size}`, block && 'btn--block', className].filter(Boolean).join(' ')
  return (
    <button type={type} className={classes} disabled={disabled || loading} aria-busy={loading || undefined} {...rest}>
      {loading ? <span className="btn__spinner" aria-hidden="true" /> : icon && <Icon name={icon} size={18} />}
      <span>{children}</span>
    </button>
  )
}

type IconButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  icon: IconName
  /** Required: icon-only buttons need an accessible name. */
  label: string
  variant?: 'plain' | 'raised'
}

export function IconButton({ icon, label, variant = 'plain', type = 'button', className, ...rest }: IconButtonProps) {
  return (
    <button
      type={type}
      aria-label={label}
      className={['icon-btn', `icon-btn--${variant}`, className].filter(Boolean).join(' ')}
      {...rest}
    >
      <Icon name={icon} />
    </button>
  )
}
