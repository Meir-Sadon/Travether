/** Accepts a full share link or just its code, returns the code. */
export function shareCodeFrom(input: string): string {
  const trimmed = input.trim()
  const match = /\/c\/([A-Za-z0-9]+)/.exec(trimmed)
  return match ? match[1] : trimmed
}

/** WhatsApp's share link: opens the app (or web) with the text ready to send to any chat. */
export function whatsAppLink(text: string): string {
  return `https://wa.me/?text=${encodeURIComponent(text)}`
}

/** "Sunrise hike · Sat 12 Oct, 06:00\nhttps://…/plans/…" for sharing a plan. The link opens a public preview. */
export function planShareText(title: string, when: string, url: string): string {
  return `${title} · ${when}\n${url}`
}
