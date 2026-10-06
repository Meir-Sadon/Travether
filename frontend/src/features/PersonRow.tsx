import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Avatar } from '../components'
import type { MockPerson } from '../mock/data'
import './PersonRow.css'

type PersonRowProps = {
  person: MockPerson
  /** Replaces the default "age · country · rating" line. */
  meta?: string
  trailing?: ReactNode
  you?: boolean
}

export function PersonRow({ person, meta, trailing, you = false }: PersonRowProps) {
  const { t } = useTranslation()
  const rating = person.rating !== null ? ` · ★ ${person.rating}` : ''
  return (
    <div className="person-row">
      <Avatar person={{ name: person.name, tint: person.tint }} />
      <div className="person-row__text">
        <span className="person-row__name">{you ? t('common.you', { name: person.name }) : person.name}</span>
        <span className="person-row__meta">{meta ?? `${person.age} · ${person.flag} ${person.country}${rating}`}</span>
      </div>
      {trailing}
    </div>
  )
}
