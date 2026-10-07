import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { BottomSheet, Button, FormError, TextField } from '../components'
import { api, errorCode } from '../lib/api'
import { track } from '../lib/telemetry'
import type { Card } from '../lib/types'

type JoinRequestProps = {
  card: Card
  /** The share slug the card was opened with; needed for invite-only cards. */
  shareSlug?: string
  onChange: (card: Card) => void
}

/** The preview's footer for a signed-in visitor: ask to join, or see and withdraw the open request (PLAN.md §4.5). */
export function JoinRequest({ card, shareSlug, onChange }: JoinRequestProps) {
  const { t } = useTranslation()
  const [open, setOpen] = useState(false)
  const [message, setMessage] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const request = card.myRequest

  const run = async (work: () => Promise<void>) => {
    setBusy(true)
    setError(null)
    try {
      await work()
    } catch (err) {
      setError(errorCode(err))
    } finally {
      setBusy(false)
    }
  }

  if (request?.status === 'requested') {
    return (
      <div className="screen__stack">
        <p className="screen__note" role="status">
          {t('trip.requestPending')}
        </p>
        <FormError code={error} />
        <Button
          block
          variant="ghost"
          loading={busy}
          onClick={() =>
            void run(async () => {
              await api.del(`/card-requests/${request.id}`)
              onChange({ ...card, myRequest: { ...request, status: 'withdrawn' } })
            })
          }
        >
          {t('trip.withdrawRequest')}
        </Button>
      </div>
    )
  }

  return (
    <div className="screen__stack">
      {request?.status === 'rejected' && <p className="screen__note">{t('trip.requestDeclined')}</p>}
      <Button block size="lg" onClick={() => setOpen(true)}>
        {t('trip.requestToJoin')}
      </Button>
      <BottomSheet
        open={open}
        onClose={() => setOpen(false)}
        title={t('trip.requestToJoin')}
        footer={
          <Button size="lg" block type="submit" form="join-request" loading={busy}>
            {t('trip.sendRequest')}
          </Button>
        }
      >
        <form
          id="join-request"
          className="screen__stack"
          onSubmit={(e) => {
            e.preventDefault()
            void run(async () => {
              onChange(await api.post<Card>(`/cards/${card.id}/requests`, { message, shareSlug }))
              track('card_requested')
              setOpen(false)
            })
          }}
        >
          <p className="screen__body">{t('trip.requestIntro')}</p>
          <TextField
            multiline
            label={t('trip.requestMessage')}
            hint={t('trip.requestMessageHint')}
            maxLength={300}
            rows={3}
            value={message}
            onChange={(e) => setMessage(e.target.value)}
          />
          <FormError code={error} />
        </form>
      </BottomSheet>
    </div>
  )
}
