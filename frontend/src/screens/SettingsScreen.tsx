import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import { useAuth } from '../auth/useAuth'
import { BottomSheet, Button, Icon } from '../components'
import { ScreenHeader } from '../layout/ScreenHeader'
import './SettingsScreen.css'

const notificationKeys = ['requests', 'messages', 'matches', 'reminders', 'reviews'] as const

/** 10 · Settings: notifications, privacy, data export, delete account (PLAN.md §4.7, §4.9). */
export function SettingsScreen() {
  const { t } = useTranslation()
  const [on, setOn] = useState<Record<string, boolean>>({ requests: true, messages: true, matches: true, reminders: true, reviews: false })
  const [confirmDelete, setConfirmDelete] = useState(false)
  const { logout } = useAuth()
  const navigate = useNavigate()

  const logOut = async () => {
    await logout()
    void navigate('/welcome', { replace: true })
  }

  return (
    <div className="screen">
      <ScreenHeader back backTo="/profile" title={t('settings.title')} />

      <section className="screen__section" aria-labelledby="settings-notifications">
        <h2 id="settings-notifications" className="screen__section-title">
          {t('settings.notifications')}
        </h2>
        <ul className="list-reset settings__list">
          {notificationKeys.map((k) => (
            <li key={k}>
              <label className="settings__toggle">
                <span>
                  {t(`settings.notify_${k}`)}
                  <span className="screen__meta">{t(`settings.notify_${k}_hint`)}</span>
                </span>
                <input type="checkbox" role="switch" aria-checked={on[k]} checked={on[k]} onChange={(e) => setOn({ ...on, [k]: e.target.checked })} />
              </label>
            </li>
          ))}
        </ul>
        <p className="screen__meta">{t('settings.quietHours')}</p>
      </section>

      <section className="screen__section" aria-labelledby="settings-privacy">
        <h2 id="settings-privacy" className="screen__section-title">
          {t('settings.privacy')}
        </h2>
        <ul className="list-reset settings__list">
          {(['whoSees', 'safety', 'blocked', 'export'] as const).map((k) => (
            <li key={k}>
              <button type="button" className="settings__row">
                {t(`settings.${k}`)}
                <Icon name="forward" size={18} />
              </button>
            </li>
          ))}
          <li>
            <button type="button" className="settings__row settings__row--danger" onClick={() => setConfirmDelete(true)}>
              {t('settings.delete')}
              <Icon name="forward" size={18} />
            </button>
          </li>
        </ul>
      </section>

      <section className="screen__section" aria-labelledby="settings-account">
        <h2 id="settings-account" className="screen__section-title">
          {t('settings.account')}
        </h2>
        <ul className="list-reset settings__list">
          <li>
            <button type="button" className="settings__row" onClick={() => void logOut()}>
              {t('settings.logOut')}
              <Icon name="forward" size={18} />
            </button>
          </li>
        </ul>
      </section>

      <BottomSheet
        open={confirmDelete}
        onClose={() => setConfirmDelete(false)}
        title={t('settings.deleteTitle')}
        footer={
          <div className="screen__stack">
            <Button block size="lg" variant="brand" onClick={() => setConfirmDelete(false)}>
              {t('settings.keepAccount')}
            </Button>
            <Button block variant="ghost" onClick={() => setConfirmDelete(false)}>
              {t('settings.deleteConfirm')}
            </Button>
          </div>
        }
      >
        <p className="screen__body">{t('settings.deleteBody')}</p>
      </BottomSheet>
    </div>
  )
}
