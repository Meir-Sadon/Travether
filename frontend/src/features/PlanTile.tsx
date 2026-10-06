import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { Avatar, Card, Chip, Icon } from '../components'
import { categoryTint, formatPlanWhen } from '../lib/plans'
import type { PlanSummary } from '../lib/types'
import './PlanCard.css'

/** A real plan in a list: category, title, when, area, host and seats. */
export function PlanTile({ plan, distance }: { plan: PlanSummary; distance?: string }) {
  const { t, i18n } = useTranslation()
  const left = plan.seatLimit - plan.seatsTaken
  return (
    <Card as="li" interactive className="plan-card">
      <Link to={`/plans/${plan.id}`} className="plan-card__link">
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
          {formatPlanWhen(plan.localDate, plan.localTime, i18n.language)}
        </span>
        <span className="plan-card__meta">
          <Icon name="pin" size={14} />
          {plan.areaLabel}
        </span>
        <div className="plan-card__host">
          <Avatar person={{ name: plan.host.displayName, photoUrl: plan.host.photoUrl ?? undefined, tint: 'var(--color-accent-soft)' }} />
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
