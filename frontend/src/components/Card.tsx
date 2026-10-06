import type { HTMLAttributes, ReactNode } from 'react'
import './Card.css'

type CardProps = HTMLAttributes<HTMLElement> & {
  /** outlined: white with a hairline (lists) · filled: surface grey (callouts) */
  variant?: 'outlined' | 'filled'
  /** Adds hover/press feedback; use when the whole card is a link or button. */
  interactive?: boolean
  as?: 'article' | 'section' | 'div' | 'li'
  children: ReactNode
}

/** Rounded container for plans, trips and callouts. Compose with CardMedia and CardBody. */
export function Card({ variant = 'outlined', interactive = false, as: Tag = 'article', className, children, ...rest }: CardProps) {
  const classes = ['card', `card--${variant}`, interactive && 'card--interactive', className].filter(Boolean).join(' ')
  return (
    <Tag className={classes} {...rest}>
      {children}
    </Tag>
  )
}

type CardMediaProps = {
  /** Background while there's no photo, e.g. a category tint token. */
  tint?: string
  imageUrl?: string
  /** Decorative image unless alt is given. */
  alt?: string
  height?: 'sm' | 'md' | 'lg'
  /** Overlaid in the bottom start corner (e.g. a Chip). */
  overlay?: ReactNode
}

export function CardMedia({ tint = 'var(--tint-other)', imageUrl, alt = '', height = 'md', overlay }: CardMediaProps) {
  return (
    <div className={`card__media card__media--${height}`} style={{ background: tint }}>
      {imageUrl && <img src={imageUrl} alt={alt} className="card__img" loading="lazy" />}
      {overlay && <div className="card__overlay">{overlay}</div>}
    </div>
  )
}

export function CardBody({ children, className }: { children: ReactNode; className?: string }) {
  return <div className={['card__body', className].filter(Boolean).join(' ')}>{children}</div>
}
