import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { BottomSheet, Button, Chip, Icon, IconButton, Segmented } from '../components'
import { PlanCard } from '../features/PlanCard'
import { ScreenHeader } from '../layout/ScreenHeader'
import { categoryTint, plans, type CategoryKey } from '../mock/data'
import './DiscoverScreen.css'

type View = 'list' | 'map'
const filters: ('all' | CategoryKey)[] = ['all', 'dayTrip', 'food', 'hike', 'nightlife', 'transport']

/** 5 · Discover: plans in the same city and dates, nearest first; list ⇄ map (PLAN.md §4.4). */
export function DiscoverScreen() {
  const { t } = useTranslation()
  const [view, setView] = useState<View>('list')
  const [category, setCategory] = useState<'all' | CategoryKey>('all')
  const [filtersOpen, setFiltersOpen] = useState(false)
  const [radius, setRadius] = useState(30)

  const visible = plans.filter((p) => p.tripId !== 'cm-crew' && (category === 'all' || p.category === category))

  return (
    <div className="screen">
      <ScreenHeader
        large
        title="Chiang Mai"
        subtitle={t('discover.subtitle', { dates: '12 – 26 Oct', km: radius })}
        action={<IconButton icon="filter" label={t('discover.filters')} variant="raised" onClick={() => setFiltersOpen(true)} />}
      />
      <div className="screen__section">
        <div className="discover__chips" role="group" aria-label={t('discover.categories')}>
          {filters.map((c) => (
            <Chip key={c} size="md" selected={category === c} onToggle={() => setCategory(c)}>
              {c === 'all' ? t('discover.all') : t(`category.${c}`)}
            </Chip>
          ))}
        </div>
        <div className="screen__section-head">
          <span className="screen__meta">{t('discover.count', { count: visible.length })}</span>
          <Segmented
            label={t('discover.view')}
            value={view}
            onChange={setView}
            options={[
              { value: 'list', label: t('discover.list') },
              { value: 'map', label: t('discover.map') },
            ]}
          />
        </div>
      </div>

      {view === 'list' ? (
        <ul className="list-reset screen__section screen__stack">
          {visible.length === 0 && <li className="screen__empty">{t('discover.empty')}</li>}
          {visible.map((p) => (
            <PlanCard key={p.id} plan={p} />
          ))}
        </ul>
      ) : (
        <div className="screen__section">
          <div className="discover__map" role="img" aria-label={t('discover.mapLabel', { count: visible.length })}>
            <svg viewBox="0 0 100 100" preserveAspectRatio="none" aria-hidden="true">
              <path d="M0 70 C20 60 30 80 50 65 S80 50 100 58" stroke="#B9D6DA" strokeWidth="3" fill="none" />
              <path d="M30 0 L36 100 M0 30 L100 38 M60 0 L64 100" stroke="#E6E1D6" strokeWidth="1.2" />
            </svg>
            <span className="discover__you" style={{ insetInlineStart: '48%', insetBlockStart: '50%' }} aria-hidden="true" />
            {visible.map((p) => (
              <Link
                key={p.id}
                to={`/plans/${p.id}`}
                className="discover__pin"
                style={{ insetInlineStart: `${p.map.x}%`, insetBlockStart: `${p.map.y}%`, background: categoryTint[p.category] }}
              >
                <Icon name="pin" size={14} />
                {p.distance}
                <span className="visually-hidden">: {p.title}</span>
              </Link>
            ))}
          </div>
          <p className="screen__note">{t('discover.mapNote')}</p>
        </div>
      )}

      <BottomSheet
        open={filtersOpen}
        onClose={() => setFiltersOpen(false)}
        title={t('discover.filters')}
        footer={
          <Button block size="lg" onClick={() => setFiltersOpen(false)}>
            {t('discover.showPlans', { count: visible.length })}
          </Button>
        }
      >
        <div className="screen__stack">
          <label className="field__label" htmlFor="radius">
            {t('discover.radius', { km: radius })}
          </label>
          <input id="radius" type="range" min={5} max={100} step={5} value={radius} onChange={(e) => setRadius(Number(e.target.value))} />
          <span className="field__label">{t('discover.groupSize')}</span>
          <div className="screen__row">
            <Chip size="md" selected onToggle={() => {}}>
              {t('discover.anySize')}
            </Chip>
            <Chip size="md" onToggle={() => {}}>
              {t('discover.solo')}
            </Chip>
            <Chip size="md" onToggle={() => {}}>
              {t('discover.groups')}
            </Chip>
          </div>
          <span className="field__label">{t('discover.language')}</span>
          <div className="screen__row">
            {['English', 'Hebrew', 'German', 'Spanish'].map((l, i) => (
              <Chip key={l} size="md" selected={i === 0} onToggle={() => {}}>
                {l}
              </Chip>
            ))}
          </div>
        </div>
      </BottomSheet>
    </div>
  )
}
