import { useTranslation } from 'react-i18next'
import { Avatar, Button } from '../components'
import { ScreenHeader } from '../layout/ScreenHeader'
import { api } from '../lib/api'
import { useApi } from '../lib/useApi'
import '../features/PersonRow.css'

type Blocked = { id: string; displayName: string; photoUrl: string | null; blockedAt: string }

/** People I blocked; unblocking lets us see each other again. */
export function BlockedScreen() {
  const { t } = useTranslation()
  const { data, setData } = useApi<Blocked[]>('/me/blocks')

  const unblock = async (id: string) => {
    await api.del(`/users/${id}/block`)
    setData((data ?? []).filter((b) => b.id !== id))
  }

  return (
    <div className="screen">
      <ScreenHeader back backTo="/settings" title={t('settings.blocked')} />
      <section className="screen__section">
        <p className="screen__meta">{t('blocked.explain')}</p>
        {data?.length === 0 && <p className="screen__empty">{t('blocked.none')}</p>}
        <ul className="list-reset screen__stack">
          {data?.map((b) => {
            const name = b.displayName || t('review.deletedUser')
            return (
              <li key={b.id} className="person-row">
                <Avatar person={{ name, photoUrl: b.photoUrl ?? undefined, tint: 'var(--color-accent-soft)' }} />
                <div className="person-row__text">
                  <strong>{name}</strong>
                </div>
                <Button size="sm" variant="secondary" onClick={() => void unblock(b.id)} aria-label={t('blocked.unblockName', { name })}>
                  {t('blocked.unblock')}
                </Button>
              </li>
            )
          })}
        </ul>
      </section>
    </div>
  )
}
