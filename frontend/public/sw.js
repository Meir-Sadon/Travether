// Travether service worker: shows Web Push notifications and opens the app where they point.
// Offline caching comes later (vite-plugin-pwa, PLAN.md §6); this file only handles push.

self.addEventListener('install', () => self.skipWaiting())
self.addEventListener('activate', (event) => event.waitUntil(self.clients.claim()))

self.addEventListener('push', (event) => {
  let data = { title: 'Travether', body: '', url: '/', tag: undefined }
  try {
    data = { ...data, ...event.data.json() }
  } catch {
    // An empty or unreadable push still shows something, as browsers require.
  }
  event.waitUntil(
    self.registration.showNotification(data.title, {
      body: data.body,
      tag: data.tag,
      renotify: Boolean(data.tag),
      icon: '/favicon.svg',
      badge: '/favicon.svg',
      data: { url: data.url },
    }),
  )
})

self.addEventListener('notificationclick', (event) => {
  event.notification.close()
  const url = new URL(event.notification.data?.url ?? '/', self.location.origin)
  if (url.origin !== self.location.origin) return
  event.waitUntil(
    self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then((windows) => {
      const open = windows.find((w) => new URL(w.url).origin === url.origin)
      if (open) return open.focus().then((w) => w.navigate(url.href))
      return self.clients.openWindow(url.href)
    }),
  )
})
