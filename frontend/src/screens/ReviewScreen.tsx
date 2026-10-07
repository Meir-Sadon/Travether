import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router'
import { Button, Chip, FormError, TextField } from '../components'
import { PersonItem } from '../features/PersonItem'
import { Stars } from '../features/ReviewList'
import { ScreenHeader } from '../layout/ScreenHeader'
import { api, errorCode } from '../lib/api'
import { track } from '../lib/telemetry'
import { parseDate } from '../lib/dates'
import { categoryTint } from '../lib/plans'
import type { MeetAnswer, WrapUp, WrapUpPerson } from '../lib/types'
import { useApi } from '../lib/useApi'
import { NotFoundScreen } from './NotFoundScreen'
import './ReviewScreen.css'

/** "Did you meet?" + double-blind reviews (PLAN.md §4.6). The API decides who can review whom, and when. */
export function ReviewScreen() {
  const { t, i18n } = useTranslation()
  const { planId } = useParams()
  const { data, error, setData } = useApi<WrapUp>(`/plans/${planId}/wrap-up`)
  const [busy, setBusy] = useState(false)
  const [actionError, setActionError] = useState<string | null>(null)

  if (error === 'NotFound' || error === 'NotParticipant') return <NotFoundScreen />

  const answer = async (a: MeetAnswer) => {
    setBusy(true)
    setActionError(null)
    try {
      setData(await api.post<WrapUp>(`/plans/${planId}/meet`, { answer: a }))
      if (a === 'met') track('met')
    } catch (err) {
      setActionError(errorCode(err))
    } finally {
      setBusy(false)
    }
  }

  const date = data ? parseDate(data.localDate).toLocaleDateString(i18n.language, { weekday: 'short', day: 'numeric', month: 'short' }) : ''

  return (
    <div className="screen" style={{ minBlockSize: '100dvh' }}>
      <ScreenHeader back backTo="/" />
      {!data && !error && (
        <p className="screen__loading" role="status">
          {t('common.loading')}
        </p>
      )}
      <FormError code={error} />

      {data && !data.myAnswer && (
        <section className="screen__section">
          <span>
            <Chip tint={categoryTint[data.category]}>{t(`category.${data.category}`)}</Chip>
          </span>
          <h1 className="review__title">{t('review.didItHappen', { plan: data.title })}</h1>
          <p className="screen__meta">{t('review.askMeta', { when: date, count: data.people.length })}</p>
          {data.canAnswer ? (
            <div className="screen__stack review__answers">
              <Button size="lg" block loading={busy} onClick={() => void answer('met')}>
                {t('review.yesMet')}
              </Button>
              <Button size="lg" block variant="secondary" disabled={busy} onClick={() => void answer('cancelled')}>
                {t('review.cancelled')}
              </Button>
              <Button size="lg" block variant="secondary" disabled={busy} onClick={() => void answer('noShow')}>
                {t('review.didntGo')}
              </Button>
            </div>
          ) : (
            <p className="screen__note">{t('review.answerClosed')}</p>
          )}
          <FormError code={actionError} />
        </section>
      )}

      {data?.myAnswer && (
        <>
          <section className="screen__section">
            <h1 className="review__title">{data.myAnswer === 'met' ? t('review.howWasIt') : t('review.thanksForTelling')}</h1>
            <p className="screen__meta">{data.title}</p>
            {data.myAnswer === 'met' && <p className="screen__note">{t('review.blindNote')}</p>}
          </section>
          {data.myAnswer === 'met' &&
            data.people.map((p) => <PersonReview key={p.person.id} planId={data.planId} entry={p} onSaved={setData} />)}
          <section className="screen__section">
            <Link to="/discover" className="btn btn--secondary btn--md btn--block">
              {t('review.findNext')}
            </Link>
          </section>
        </>
      )}
    </div>
  )
}

function PersonReview({ planId, entry, onSaved }: { planId: string; entry: WrapUpPerson; onSaved: (w: WrapUp) => void }) {
  const { t, i18n } = useTranslation()
  const [stars, setStars] = useState(0)
  const [text, setText] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const name = entry.person.displayName

  const submit = async () => {
    setBusy(true)
    setError(null)
    try {
      onSaved(await api.post<WrapUp>(`/plans/${planId}/reviews`, { revieweeId: entry.person.id, stars, text }))
    } catch (err) {
      setError(errorCode(err))
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="screen__section" aria-label={name}>
      <PersonItem person={entry.person} />

      {entry.state === 'open' && (
        <>
          <div className="review__stars" role="radiogroup" aria-label={t('review.starsFor', { name })}>
            {[1, 2, 3, 4, 5].map((n) => (
              <button
                key={n}
                type="button"
                role="radio"
                aria-checked={stars === n}
                aria-label={t('profile.stars', { count: n })}
                className={`review__star${n <= stars ? ' review__star--on' : ''}`}
                onClick={() => setStars(n)}
              >
                ★
              </button>
            ))}
          </div>
          <TextField multiline label={t('review.feedbackFor', { name })} placeholder={t('review.placeholder')} maxLength={1000} value={text} onChange={(e) => setText(e.target.value)} />
          {entry.reviewUntil && (
            <p className="screen__meta">{t('review.until', { date: new Date(entry.reviewUntil).toLocaleDateString(i18n.language, { day: 'numeric', month: 'short' }) })}</p>
          )}
          <FormError code={error} />
          <Button block loading={busy} disabled={stars === 0} onClick={() => void submit()}>
            {t('review.submitFor', { name })}
          </Button>
        </>
      )}
      {entry.state === 'waiting' && <p className="screen__meta">{t('review.waiting', { name })}</p>}
      {entry.state === 'closed' && !entry.myReview && <p className="screen__meta">{t('review.closed')}</p>}

      {entry.myReview && (
        <div className="review__given">
          <span className="screen__meta">{t('review.yourReview')}</span>
          <Stars count={entry.myReview.stars} />
          {entry.myReview.text && <p className="screen__body">{entry.myReview.text}</p>}
        </div>
      )}
      {entry.theirReview ? (
        <div className="review__given">
          <span className="screen__meta">{t('review.theirReview', { name })}</span>
          <Stars count={entry.theirReview.stars} />
          {entry.theirReview.text && <p className="screen__body">{entry.theirReview.text}</p>}
        </div>
      ) : (
        entry.theyReviewedMe && <p className="screen__note">{t('review.theyReviewedYou', { name })}</p>
      )}
    </section>
  )
}
