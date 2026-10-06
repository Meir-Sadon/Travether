import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate } from 'react-router'
import { useAuth } from '../auth/useAuth'
import { BottomSheet, Button, FormError, Icon } from '../components'
import { ScreenHeader } from '../layout/ScreenHeader'
import { api, errorCode } from '../lib/api'
import { deviceTimeZone, disablePush, enablePush, pushState, type PushState } from '../lib/push'
import type { NotificationSettings } from '../lib/types'
import { useApi } from '../lib/useApi'
import './SettingsScreen.css'

const notificationKeys = ['requests', 'messages', 'matches', 'reminders', 'reviews'] as const

/** 10 · Settings: notifications, privacy, data export, delete account (PLAN.md §4.7, §4.9). */
export function SettingsScreen() {
  const { t } = useTranslation()
  const [confirmDelete, setConfirmDelete] = useState(false)
  const { logout, user } = useAuth()
  const navigate = useNavigate()

  const logOut = async () => {
    await logout()
    void navigate('/welcome', { replace: true })
  }

  return (
    <div className="screen">
      <ScreenHeader back backTo="/profile" title={t('settings.title')} />

      <NotificationSettingsSection />

      <section className="screen__section" aria-labelledby="settings-privacy">
        <h2 id="settings-privacy" className="screen__section-title">
          {t('settings.privacy')}
        </h2>
        <ul className="list-reset settings__list">
          {(['whoSees', 'export'] as const).map((k) => (
            <li key={k}>
              <button type="button" className="settings__row">
                {t(`settings.${k}`)}
                <Icon name="forward" size={18} />
              </button>
            </li>
          ))}
          <li>
            <Link to="/safety" className="settings__row">
              {t('settings.safety')}
              <Icon name="forward" size={18} />
            </Link>
          </li>
          <li>
            <Link to="/settings/blocked" className="settings__row">
              {t('settings.blocked')}
              <Icon name="forward" size={18} />
            </Link>
          </li>
          {user?.role === 'moderator' && (
            <li>
              <Link to="/admin" className="settings__row">
                {t('admin.title')}
                <Icon name="forward" size={18} />
              </Link>
            </li>
          )}
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

/** "22:00:00" → "22:00" for the time input, and back. */
const hhmm = (time: string | null) => (time ?? '').slice(0, 5)

/** Notification categories, email, quiet hours and push on this device (PLAN.md §4.7). Each change saves at once. */
function NotificationSettingsSection() {
  const { t } = useTranslation()
  const { data, setData } = useApi<NotificationSettings>('/notifications/settings')
  const [error, setError] = useState<string | null>(null)
  const [push, setPush] = useState<PushState | null>(null)
  const [pushBusy, setPushBusy] = useState(false)

  useEffect(() => {
    void pushState().then(setPush)
  }, [])

  const save = async (next: NotificationSettings) => {
    const previous = data
    setData(next)
    setError(null)
    try {
      setData(await api.put<NotificationSettings>('/notifications/settings', { ...next, timeZone: deviceTimeZone() }))
    } catch (err) {
      if (previous) setData(previous)
      setError(errorCode(err))
    }
  }

  const togglePush = async () => {
    setPushBusy(true)
    try {
      setPush(push === 'on' ? await disablePush() : await enablePush())
    } catch (err) {
      setError(errorCode(err))
    } finally {
      setPushBusy(false)
    }
  }

  const quietOn = !!data?.quietFrom

  return (
    <section className="screen__section" aria-labelledby="settings-notifications">
      <h2 id="settings-notifications" className="screen__section-title">
        {t('settings.notifications')}
      </h2>

      {push && (
        <div className="settings__push">
          <strong>{t('settings.push_title')}</strong>
          <p className="screen__meta">{t(`settings.push_${push}`)}</p>
          {push === 'install' && <p className="screen__meta">{t('settings.push_installSteps')}</p>}
          {(push === 'on' || push === 'off') && (
            <Button size="sm" variant={push === 'on' ? 'secondary' : 'brand'} disabled={pushBusy} onClick={() => void togglePush()}>
              {push === 'on' ? t('settings.push_disable') : t('settings.push_enable')}
            </Button>
          )}
        </div>
      )}

      {data && (
        <ul className="list-reset settings__list">
          {notificationKeys.map((k) => (
            <li key={k}>
              <label className="settings__toggle">
                <span>
                  {t(`settings.notify_${k}`)}
                  <span className="screen__meta">{t(`settings.notify_${k}_hint`)}</span>
                </span>
                <input type="checkbox" role="switch" aria-checked={data[k]} checked={data[k]} onChange={(e) => void save({ ...data, [k]: e.target.checked })} />
              </label>
            </li>
          ))}
          <li>
            <label className="settings__toggle">
              <span>
                {t('settings.notify_email')}
                <span className="screen__meta">{t('settings.notify_email_hint')}</span>
              </span>
              <input type="checkbox" role="switch" aria-checked={data.email} checked={data.email} onChange={(e) => void save({ ...data, email: e.target.checked })} />
            </label>
          </li>
          <li>
            <label className="settings__toggle">
              <span>
                {t('settings.quietHours')}
                <span className="screen__meta">{t('settings.quietHours_hint', { zone: deviceTimeZone() })}</span>
              </span>
              <input
                type="checkbox"
                role="switch"
                aria-checked={quietOn}
                checked={quietOn}
                onChange={(e) => void save({ ...data, quietFrom: e.target.checked ? '22:00:00' : null, quietTo: e.target.checked ? '08:00:00' : null })}
              />
            </label>
            {quietOn && (
              <div className="settings__quiet">
                <label>
                  <span className="screen__meta">{t('settings.quietFrom')}</span>
                  <input type="time" value={hhmm(data.quietFrom)} onChange={(e) => e.target.value && void save({ ...data, quietFrom: `${e.target.value}:00` })} />
                </label>
                <label>
                  <span className="screen__meta">{t('settings.quietTo')}</span>
                  <input type="time" value={hhmm(data.quietTo)} onChange={(e) => e.target.value && void save({ ...data, quietTo: `${e.target.value}:00` })} />
                </label>
              </div>
            )}
          </li>
        </ul>
      )}
      <FormError code={error} />
    </section>
  )
}
