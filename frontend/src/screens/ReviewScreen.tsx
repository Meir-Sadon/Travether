import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router'
import { Button, Chip, TextField } from '../components'
import { PersonRow } from '../features/PersonRow'
import { ScreenHeader } from '../layout/ScreenHeader'
import { categoryTint, me, person, plan as findPlan } from '../mock/data'
import { NotFoundScreen } from './NotFoundScreen'
import './ReviewScreen.css'

type Step = 'ask' | 'rate' | 'thanks'

/** "Did you meet?" + double-blind reviews (PLAN.md §4.6). */
export function ReviewScreen() {
  const { t } = useTranslation()
  const { planId } = useParams()
  const p = findPlan(planId)
  const [step, setStep] = useState<Step>('ask')
  const [stars, setStars] = useState<Record<string, number>>({})

  if (!p) return <NotFoundScreen />
  const hosts = p.hostIds.map(person)

  return (
    <div className="screen" style={{ minBlockSize: '100dvh' }}>
      <ScreenHeader back backTo="/inbox" />
      {step === 'ask' && (
        <section className="screen__section">
          <span>
            <Chip tint={categoryTint[p.category]}>{t(`category.${p.category}`)}</Chip>
          </span>
          <h1 className="review__title">{t('review.didItHappen', { plan: p.title })}</h1>
          <p className="screen__meta">{t('review.askMeta', { when: p.when.split(' · ')[0], group: p.hostGroup })}</p>
          <div className="screen__stack review__answers">
            <Button size="lg" block onClick={() => setStep('rate')}>
              {t('review.yesMet')}
            </Button>
            <Button size="lg" block variant="secondary" onClick={() => setStep('thanks')}>
              {t('review.cancelled')}
            </Button>
            <Button size="lg" block variant="secondary" onClick={() => setStep('thanks')}>
              {t('review.didntGo')}
            </Button>
          </div>
          <Button variant="ghost">{t('review.report')}</Button>
        </section>
      )}

      {step === 'rate' && (
        <>
          <section className="screen__section">
            <h1 className="review__title">{t('review.howWasIt')}</h1>
            <p className="screen__note">{t('review.blindNote')}</p>
          </section>
          {hosts.map((h) => (
            <section key={h.id} className="screen__section">
              <PersonRow person={h} meta={t('review.hostMeta', { country: h.country })} />
              <div className="review__stars" role="radiogroup" aria-label={t('review.starsFor', { name: h.name })}>
                {[1, 2, 3, 4, 5].map((n) => (
                  <button
                    key={n}
                    type="button"
                    role="radio"
                    aria-checked={stars[h.id] === n}
                    aria-label={t('profile.stars', { count: n })}
                    className={`review__star${n <= (stars[h.id] ?? 0) ? ' review__star--on' : ''}`}
                    onClick={() => setStars({ ...stars, [h.id]: n })}
                  >
                    ★
                  </button>
                ))}
              </div>
              <TextField multiline label={t('review.feedbackFor', { name: h.name })} placeholder={t('review.placeholder')} />
            </section>
          ))}
          <footer className="screen__footer">
            <Button size="lg" block onClick={() => setStep('thanks')}>
              {t('review.submit')}
            </Button>
          </footer>
        </>
      )}

      {step === 'thanks' && (
        <section className="screen__section review__thanks">
          <h1 className="review__title">{t('review.thanks', { name: me.name })}</h1>
          <p className="screen__meta">{t('review.thanksBody', { names: hosts.map((h) => h.name).join(' & ') })}</p>
          <Link to="/discover" className="btn btn--primary btn--lg btn--block">
            {t('review.findNext')}
          </Link>
        </section>
      )}
    </div>
  )
}
