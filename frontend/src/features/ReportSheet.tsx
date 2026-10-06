import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { BottomSheet, Button, FormError, TextField } from '../components'
import { api, errorCode } from '../lib/api'
import './ReportSheet.css'

export type ReportTargetType = 'user' | 'card' | 'plan' | 'message' | 'review'
export type ReportTarget = { type: ReportTargetType; id: string }

const reasons = ['harassment', 'inappropriate', 'scam', 'safety', 'fakeProfile', 'underage', 'spam', 'other'] as const

/** Report something to the moderators (PLAN.md §4.8). The reporter stays anonymous to the person reported. */
export function ReportSheet({ target, onClose }: { target: ReportTarget | null; onClose: () => void }) {
  const { t } = useTranslation()
  const [reason, setReason] = useState<(typeof reasons)[number] | null>(null)
  const [details, setDetails] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [sent, setSent] = useState(false)

  const close = () => {
    setReason(null)
    setDetails('')
    setError(null)
    setSent(false)
    onClose()
  }

  const submit = async () => {
    if (!target || !reason) return
    setBusy(true)
    setError(null)
    try {
      await api.post('/reports', { targetType: target.type, targetId: target.id, reason, details })
      setSent(true)
    } catch (err) {
      setError(errorCode(err))
    } finally {
      setBusy(false)
    }
  }

  return (
    <BottomSheet
      open={target !== null}
      onClose={close}
      title={sent ? t('report.sentTitle') : t(`report.title_${target?.type ?? 'user'}`)}
      footer={
        sent ? (
          <Button block size="lg" onClick={close}>
            {t('common.close')}
          </Button>
        ) : (
          <Button block size="lg" loading={busy} disabled={!reason} onClick={() => void submit()}>
            {t('report.send')}
          </Button>
        )
      }
    >
      {sent ? (
        <p className="screen__body">{t('report.sentBody')}</p>
      ) : (
        <div className="screen__stack">
          <fieldset className="report__reasons">
            <legend className="screen__meta">{t('report.why')}</legend>
            {reasons.map((r) => (
              <label key={r} className="report__reason">
                <input type="radio" name="report-reason" value={r} checked={reason === r} onChange={() => setReason(r)} />
                {t(`report.reason_${r}`)}
              </label>
            ))}
          </fieldset>
          <TextField multiline label={t('report.details')} hint={t('report.detailsHint')} maxLength={1000} value={details} onChange={(e) => setDetails(e.target.value)} />
          <FormError code={error} />
          <p className="screen__meta">{t('report.emergency')}</p>
        </div>
      )}
    </BottomSheet>
  )
}
