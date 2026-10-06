import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, FormError, TextField } from '../components'
import { api, errorCode } from '../lib/api'
import type { Review, ReviewPage } from '../lib/types'
import { useApi } from '../lib/useApi'
import { useAuth } from '../auth/useAuth'
import { PersonItem } from './PersonItem'
import { ReportSheet, type ReportTarget } from './ReportSheet'
import './ReviewList.css'

export function Stars({ count }: { count: number }) {
  const { t } = useTranslation()
  return (
    <span className="review-list__stars" role="img" aria-label={t('profile.stars', { count })}>
      {'★'.repeat(count)}
      <span className="review-list__stars-off">{'★'.repeat(5 - count)}</span>
    </span>
  )
}

/** Published reviews about someone (PLAN.md §4.6). On your own profile you can answer each one once. */
export function ReviewList({ userId, canReply = false }: { userId: string; canReply?: boolean }) {
  const { t, i18n } = useTranslation()
  const { data, setData } = useApi<ReviewPage>(`/users/${userId}/reviews`)
  const { user } = useAuth()
  const [reporting, setReporting] = useState<ReportTarget | null>(null)
  const when = new Intl.DateTimeFormat(i18n.language, { month: 'short', year: 'numeric' })

  if (!data) return null
  const replaced = (r: Review) => setData({ ...data, items: data.items.map((x) => (x.id === r.id ? r : x)) })

  return (
    <section className="screen__section" aria-labelledby="profile-reviews">
      <h2 id="profile-reviews" className="screen__section-title">
        {t('profile.reviews')}
      </h2>
      {data.items.length === 0 && <p className="screen__empty">{t('review.none')}</p>}
      <ul className="list-reset review-list">
        {data.items.map((r) => (
          <li key={r.id} className="review-list__item">
            {r.reviewer ? <PersonItem person={r.reviewer} meta={`${r.planTitle} · ${when.format(new Date(r.createdAt))}`} /> : <strong>{t('review.deletedUser')}</strong>}
            <Stars count={r.stars} />
            {r.text && <p className="screen__body">{r.text}</p>}
            {r.reply && (
              <blockquote className="review-list__reply">
                <span className="screen__meta">{t('review.replyLabel')}</span>
                {r.reply}
              </blockquote>
            )}
            {canReply && !r.reply && <ReplyForm review={r} onReplied={replaced} />}
            {user && r.reviewer?.id !== user.id && (
              <Button size="sm" variant="ghost" onClick={() => setReporting({ type: 'review', id: r.id })}>
                {t('report.title_review')}
              </Button>
            )}
          </li>
        ))}
      </ul>
      <ReportSheet target={reporting} onClose={() => setReporting(null)} />
    </section>
  )
}

function ReplyForm({ review, onReplied }: { review: Review; onReplied: (r: Review) => void }) {
  const { t } = useTranslation()
  const [open, setOpen] = useState(false)
  const [text, setText] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  if (!open)
    return (
      <Button size="sm" variant="ghost" onClick={() => setOpen(true)}>
        {t('review.reply')}
      </Button>
    )

  const send = async () => {
    setBusy(true)
    setError(null)
    try {
      onReplied(await api.post<Review>(`/reviews/${review.id}/reply`, { text }))
    } catch (err) {
      setError(errorCode(err))
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="screen__stack">
      <TextField multiline label={t('review.replyField')} hint={t('review.replyHint')} value={text} maxLength={500} onChange={(e) => setText(e.target.value)} />
      <FormError code={error} />
      <Button size="sm" loading={busy} disabled={!text.trim()} onClick={() => void send()}>
        {t('review.sendReply')}
      </Button>
    </div>
  )
}
