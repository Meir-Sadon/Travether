import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { Avatar, Card, CardBody, CardMedia, Chip, Icon } from '../components'
import { categoryTint, formatPlanWhen } from '../lib/plans'
import { planLink } from '../lib/discover'
import type { Place, PlanSummary } from '../lib/types'
import './PlanCard.css'

/** A real plan in a list: category, title, when, area, host and seats. */
type PlanTileProps = {
  plan: PlanSummary
  /** Rounded distance label, e.g. "~2 km". */
  distance?: string
  /** The search origin, passed to the plan page so it shows the same distance. */
  origin?: Place | null
  /** A small tile for the Home carousel. */
  compact?: boolean
}

export function PlanTile({ plan, distance, origin, compact = false }: PlanTileProps) {
  const { t, i18n } = useTranslation()
  const left = plan.seatLimit - plan.seatsTaken
  const when = formatPlanWhen(plan.localDate, plan.localTime, i18n.language)

  if (compact) {
    return (
      <Card as="li" interactive className="plan-card plan-card--compact">
        <Link to={planLink(plan.id, origin)} className="plan-card__link">
          <CardMedia tint={categoryTint[plan.category]} height="sm" overlay={<Chip tone="inverse">{t(`category.${plan.category}`)}</Chip>} />
          <CardBody>
            <strong className="plan-card__title">{plan.title}</strong>
            <span className="plan-card__meta">{distance ? `${when} · ${distance}` : when}</span>
          </CardBody>
        </Link>
      </Card>
    )
  }
  return (
    <Card as="li" interactive className="plan-card">
      <Link to={planLink(plan.id, origin)} className="plan-card__link">
        <div className="plan-card__top">
          <Chip tint={categoryTint[plan.category]}>{t(`category.${plan.category}`)}</Chip>
          {distance && (
            <span className="plan-card__distance">
              <Icon name="pin" size={14} />
              {distance}
            </span>
          )}
        </div>
        <strong className="plan-card__title">{plan.title}</strong>
        <span className="plan-card__meta">
          <Icon name="calendar" size={14} />
          {when}
        </span>
        <span className="plan-card__meta">
          <Icon name="pin" size={14} />
          {plan.areaLabel}
        </span>
        <div className="plan-card__host">
          <Avatar
            person={{
              name: plan.host.displayName,
              photoUrl: plan.host.photoUrl ?? undefined,
              tint: 'var(--color-accent-soft)',
            }}
          />
          <span className="plan-card__group">{t('plan.hostName', { name: plan.host.displayName })}</span>
          {plan.joined ? (
            <Chip tone="accent">{t('plan.youreGoing')}</Chip>
          ) : (
            <Chip tone={left > 0 ? 'success' : 'neutral'}>{left > 0 ? t('plan.seatsLeft', { count: left }) : t('plan.full')}</Chip>
          )}
        </div>
      </Link>
    </Card>
  )
}
