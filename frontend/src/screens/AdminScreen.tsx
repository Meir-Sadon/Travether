import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useMe } from '../auth/useAuth'
import { Button, Chip, FormError, Segmented, TextField } from '../components'
import { ScreenHeader } from '../layout/ScreenHeader'
import { api, errorCode } from '../lib/api'
import type { Person } from '../lib/types'
import { useApi } from '../lib/useApi'
import { NotFoundScreen } from './NotFoundScreen'
import './AdminScreen.css'

type ReportStatus = 'open' | 'actioned' | 'dismissed'
type ModerationAction = 'dismiss' | 'remove' | 'ban' | 'removeAndBan'

type AdminReport = {
  id: string
  targetType: 'user' | 'card' | 'plan' | 'message' | 'review'
  targetId: string
  reason: string
  details: string | null
  status: ReportStatus
  createdAt: string
  reporter: Person
  target: { text: string; author: Person | null; authorBanned: boolean; removed: boolean } | null
  openReportsOnTarget: number
  resolutionNote: string | null
}

/** Moderation queue (PLAN.md §4.8). The API answers 404 to anyone who isn't a moderator. */
export function AdminScreen() {
  const { t } = useTranslation()
  const me = useMe()
  const [status, setStatus] = useState<ReportStatus>('open')
  const { data, reload } = useApi<AdminReport[]>(me.role === 'moderator' ? `/admin/reports?status=${status}` : null)

  if (me.role !== 'moderator') return <NotFoundScreen />

  return (
    <div className="screen">
      <ScreenHeader back backTo="/settings" title={t('admin.title')} />
      <section className="screen__section">
        <Segmented
          label={t('admin.status')}
          value={status}
          onChange={setStatus}
          options={(['open', 'actioned', 'dismissed'] as const).map((s) => ({ value: s, label: t(`admin.status_${s}`) }))}
        />
        {data?.length === 0 && <p className="screen__empty">{t('admin.empty')}</p>}
        <ul className="list-reset screen__stack">
          {data?.map((r) => (
            <ReportItem key={r.id} report={r} onDone={reload} />
          ))}
        </ul>
      </section>
    </div>
  )
}

function ReportItem({ report: r, onDone }: { report: AdminReport; onDone: () => void }) {
  const { t, i18n } = useTranslation()
  const [note, setNote] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const when = new Intl.DateTimeFormat(i18n.language, { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' })

  const resolve = async (action: ModerationAction) => {
    setBusy(true)
    setError(null)
    try {
      await api.post(`/admin/reports/${r.id}/resolve`, { action, note })
      onDone()
    } catch (err) {
      setError(errorCode(err))
    } finally {
      setBusy(false)
    }
  }

  return (
    <li className="admin__report" aria-label={t('admin.reportLabel', { type: t(`admin.type_${r.targetType}`), reason: t(`report.reason_${r.reason}` as 'report.reason_other') })}>
      <div className="screen__row">
        <Chip tone="accent">{t(`admin.type_${r.targetType}`)}</Chip>
        <Chip>{t(`report.reason_${r.reason}` as 'report.reason_other')}</Chip>
        {r.openReportsOnTarget > 1 && <Chip>{t('admin.reportCount', { count: r.openReportsOnTarget })}</Chip>}
      </div>
      {r.target ? (
        <blockquote className="admin__target">
          {r.target.text}
          <span className="screen__meta">
            {r.target.author ? t('admin.by', { name: r.target.author.displayName }) : t('review.deletedUser')}
            {r.target.authorBanned && ` · ${t('admin.banned')}`}
            {r.target.removed && ` · ${t('admin.removed')}`}
          </span>
        </blockquote>
      ) : (
        <p className="screen__meta">{t('admin.targetGone')}</p>
      )}
      {r.details && <p className="screen__body">“{r.details}”</p>}
      <p className="screen__meta">{t('admin.reportedBy', { name: r.reporter.displayName, when: when.format(new Date(r.createdAt)) })}</p>
      {r.status === 'open' ? (
        <>
          <TextField multiline label={t('admin.note')} hint={t('admin.noteHint')} maxLength={1000} value={note} onChange={(e) => setNote(e.target.value)} />
          <FormError code={error} />
          <div className="admin__actions">
            <Button size="sm" variant="secondary" disabled={busy} onClick={() => void resolve('dismiss')}>
              {t('admin.dismiss')}
            </Button>
            {r.targetType !== 'user' && (
              <Button size="sm" variant="secondary" disabled={busy || !note.trim()} onClick={() => void resolve('remove')}>
                {t('admin.remove')}
              </Button>
            )}
            <Button size="sm" variant="secondary" disabled={busy || !note.trim()} onClick={() => void resolve('ban')}>
              {t('admin.ban')}
            </Button>
            {r.targetType !== 'user' && (
              <Button size="sm" variant="primary" disabled={busy || !note.trim()} onClick={() => void resolve('removeAndBan')}>
                {t('admin.removeAndBan')}
              </Button>
            )}
          </div>
        </>
      ) : (
        r.resolutionNote && <p className="screen__meta">{t('admin.resolution', { note: r.resolutionNote })}</p>
      )}
    </li>
  )
}
