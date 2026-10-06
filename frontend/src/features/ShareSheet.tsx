import QRCode from 'qrcode'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { BottomSheet, Button } from '../components'
import type { MockTrip } from '../mock/data'
import './ShareSheet.css'

type ShareSheetProps = { trip: MockTrip; open: boolean; onClose: () => void }

/** Boarding-pass style invite with link + QR code (PLAN.md §4.2). */
export function ShareSheet({ trip, open, onClose }: ShareSheetProps) {
  const { t } = useTranslation()
  const url = `https://travether.app/c/${trip.shareSlug}`
  const [qr, setQr] = useState<string | null>(null)
  const [copied, setCopied] = useState(false)

  useEffect(() => {
    if (!open) return
    let cancelled = false
    QRCode.toDataURL(url, { margin: 1, width: 360, color: { dark: '#0E2F2C', light: '#FFFFFF' } })
      .then((data) => {
        if (!cancelled) setQr(data)
      })
      .catch(() => setQr(null))
    return () => {
      cancelled = true
    }
  }, [open, url])

  const copy = () => {
    void navigator.clipboard?.writeText(url)
    setCopied(true)
  }

  return (
    <BottomSheet open={open} onClose={onClose} title={t('share.title')}>
      <div className="pass">
        <div className="pass__head">
          <span className="pass__kicker">{t('share.kicker')}</span>
          <strong className="pass__name">{trip.name}</strong>
        </div>
        <dl className="pass__grid">
          <div>
            <dt>{t('share.dates')}</dt>
            <dd>{trip.dates}</dd>
          </div>
          <div>
            <dt>{t('share.where')}</dt>
            <dd>
              {trip.regions.join(', ')} · {trip.countryCode}
            </dd>
          </div>
        </dl>
        <div className="pass__tear" aria-hidden="true" />
        <div className="pass__qr">
          {qr ? <img src={qr} alt={t('share.qrAlt', { name: trip.name })} width={180} height={180} /> : <span className="pass__qr-placeholder" />}
          <span className="screen__meta">{t('share.scan')}</span>
          <code className="pass__url">{url.replace('https://', '')}</code>
        </div>
      </div>
      <div className="screen__row pass__actions">
        <Button variant="brand" icon="share" onClick={copy}>
          {copied ? t('share.copied') : t('share.copy')}
        </Button>
        <Button variant="secondary" onClick={() => window.open(`https://wa.me/?text=${encodeURIComponent(url)}`, '_blank', 'noopener')}>
          {t('share.whatsapp')}
        </Button>
      </div>
      <p className="screen__note">{t('share.note')}</p>
    </BottomSheet>
  )
}
