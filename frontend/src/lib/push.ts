import { api } from './api'

/**
 * Where Web Push stands on this device:
 * - `unsupported`: no service worker / Push API (older browsers, in-app browsers)
 * - `install`: iPhone/iPad Safari, where push needs the app on the Home Screen first
 * - `denied`: the user blocked notifications in the browser
 * - `off` / `on`: ready to subscribe, or subscribed
 */
export type PushState = 'unsupported' | 'install' | 'denied' | 'off' | 'on'

const isIos = () => /iPad|iPhone|iPod/.test(navigator.userAgent) || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1)
const isStandalone = () =>
  window.matchMedia?.('(display-mode: standalone)').matches || (navigator as Navigator & { standalone?: boolean }).standalone === true

export function supportsPush(): boolean {
  return typeof window !== 'undefined' && 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window
}

/** Registers the service worker; called once at startup. */
export function registerServiceWorker() {
  if (!('serviceWorker' in navigator)) return
  navigator.serviceWorker.register('/sw.js').catch(() => {
    // Push simply stays unavailable.
  })
}

export async function pushState(): Promise<PushState> {
  if (!supportsPush()) return isIos() && !isStandalone() ? 'install' : 'unsupported'
  if (Notification.permission === 'denied') return 'denied'
  const reg = await navigator.serviceWorker.getRegistration()
  const sub = await reg?.pushManager.getSubscription()
  return sub && Notification.permission === 'granted' ? 'on' : 'off'
}

const timeZone = () => Intl.DateTimeFormat().resolvedOptions().timeZone

function toBytes(base64url: string): Uint8Array<ArrayBuffer> {
  const base64 = (base64url + '='.repeat((4 - (base64url.length % 4)) % 4)).replace(/-/g, '+').replace(/_/g, '/')
  const raw = atob(base64)
  const bytes = new Uint8Array(new ArrayBuffer(raw.length))
  for (let i = 0; i < raw.length; i++) bytes[i] = raw.charCodeAt(i)
  return bytes
}

function toBase64Url(buffer: ArrayBuffer | null): string {
  if (!buffer) return ''
  let s = ''
  for (const b of new Uint8Array(buffer)) s += String.fromCharCode(b)
  return btoa(s).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
}

/** Asks permission, subscribes this device and tells the server. Returns the new state. */
export async function enablePush(): Promise<PushState> {
  if (!supportsPush()) return pushState()
  const { enabled, publicKey } = await api.get<{ enabled: boolean; publicKey: string | null }>('/push/key')
  if (!enabled || !publicKey) return 'unsupported'
  if ((await Notification.requestPermission()) !== 'granted') return pushState()

  const reg = (await navigator.serviceWorker.getRegistration()) ?? (await navigator.serviceWorker.register('/sw.js'))
  await navigator.serviceWorker.ready
  let sub = await reg.pushManager.getSubscription()
  const key = toBytes(publicKey)
  if (sub && toBase64Url(sub.options.applicationServerKey) !== publicKey) {
    // The server's key changed (e.g. a new deployment's keys): start over.
    await sub.unsubscribe()
    sub = null
  }
  sub ??= await reg.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: key })
  await api.post('/push/subscriptions', {
    endpoint: sub.endpoint,
    p256dh: toBase64Url(sub.getKey('p256dh')),
    auth: toBase64Url(sub.getKey('auth')),
    timeZone: timeZone(),
  })
  return 'on'
}

export async function disablePush(): Promise<PushState> {
  const reg = await navigator.serviceWorker?.getRegistration()
  const sub = await reg?.pushManager.getSubscription()
  if (sub) {
    await api.del('/push/subscriptions', { endpoint: sub.endpoint })
    await sub.unsubscribe()
  }
  return pushState()
}

export { timeZone as deviceTimeZone }
