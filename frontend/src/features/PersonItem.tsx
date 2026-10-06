import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { Avatar, Icon } from '../components'
import { countryName, flag } from '../lib/countries'
import type { Person } from '../lib/types'
import './PersonRow.css'

type PersonItemProps = {
  person: Person
  /** Replaces the default "age · country" line. */
  meta?: string
  trailing?: ReactNode
  you?: boolean
}

/** A real traveler in a list; the name links to their profile. */
export function PersonItem({ person, meta, trailing, you = false }: PersonItemProps) {
  const { t, i18n } = useTranslation()
  const verified = person.badges.includes('contactVerified')
  return (
    <div className="person-row">
      <Avatar person={{ name: person.displayName, photoUrl: person.photoUrl ?? undefined, tint: 'var(--color-accent-soft)' }} />
      <div className="person-row__text">
        <Link to={`/people/${person.id}`} className="person-row__name">
          {you ? t('common.you', { name: person.displayName }) : person.displayName}
          {verified && <Icon name="shieldCheck" size={14} label={t('profile.badge_contact')} />}
        </Link>
        <span className="person-row__meta">
          {meta ?? `${person.age} · ${flag(person.countryCode)} ${countryName(person.countryCode, i18n.language)}`}
        </span>
      </div>
      {trailing}
    </div>
  )
}
