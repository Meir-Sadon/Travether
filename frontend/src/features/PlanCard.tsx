import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { AvatarStack, Card, CardBody, CardMedia, Chip, Icon } from '../components'
import { categoryTint, person, seatsLeft, type MockPlan } from '../mock/data'
import './PlanCard.css'

type PlanCardProps = {
  plan: MockPlan
  /** compact: horizontal carousel tile · full: Discover list row with host details */
  variant?: 'full' | 'compact'
}

export function PlanCard({ plan, variant = 'full' }: PlanCardProps) {
  const { t } = useTranslation()
  const going = plan.goingIds.map(person)
  const left = seatsLeft(plan)
  const host = person(plan.hostIds[0])

  if (variant === 'compact') {
    return (
      <Card as="li" interactive className="plan-card plan-card--compact">
        <Link to={`/plans/${plan.id}`} className="plan-card__link">
          <CardMedia tint={categoryTint[plan.category]} height="sm" overlay={<Chip tone="inverse">{t(`category.${plan.category}`)}</Chip>} />
          <CardBody>
            <strong className="plan-card__title">{plan.title}</strong>
            <span className="plan-card__meta">
              {plan.when} · {plan.distance}
            </span>
          </CardBody>
        </Link>
      </Card>
    )
  }

  return (
    <Card as="li" interactive className="plan-card">
      <Link to={`/plans/${plan.id}`} className="plan-card__link">
        <div className="plan-card__top">
          <Chip tint={categoryTint[plan.category]}>{t(`category.${plan.category}`)}</Chip>
          <span className="plan-card__distance">
            <Icon name="pin" size={14} />
            {plan.distance}
          </span>
        </div>
        <strong className="plan-card__title">{plan.title}</strong>
        <span className="plan-card__meta">
          <Icon name="calendar" size={14} />
          {plan.when}
        </span>
        <div className="plan-card__host">
          <AvatarStack people={going} max={3} />
          <span className="plan-card__group">
            {plan.hostGroup}
            <span className="plan-card__meta">
              {host.rating !== null
                ? t('plan.rating', { rating: host.rating, count: host.reviewCount })
                : t('plan.newOnTravether')}{' '}
              · {plan.languages}
            </span>
          </span>
          <Chip tone={left > 0 ? 'success' : 'neutral'}>{left > 0 ? t('plan.seatsLeft', { count: left }) : t('plan.full')}</Chip>
        </div>
      </Link>
    </Card>
  )
}
