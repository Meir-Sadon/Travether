/** Calendar dates from the API are yyyy-mm-dd with no time zone; parse them as local dates. */
export function parseDate(iso: string): Date {
  const [y, m, d] = iso.split('-').map(Number)
  return new Date(y, m - 1, d)
}

/** "12 – 26 Oct", "28 Oct – 3 Nov", in the UI language. */
export function formatDateRange(startsOn: string, endsOn: string, lang: string): string {
  const fmt = new Intl.DateTimeFormat(lang, { day: 'numeric', month: 'short' })
  return fmt.formatRange(parseDate(startsOn), parseDate(endsOn))
}

/** Today as yyyy-mm-dd in the browser's time zone, for date inputs. */
export function todayIso(): string {
  const d = new Date()
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

/** A neutral tint per card so covers without a photo still differ. */
export function tintFor(id: string): string {
  const tints = ['#F6D7A7', '#C9E4DE', '#DDEBF7', '#FBE1DE', '#E8E1F5', '#E3EFE6']
  let h = 0
  for (const c of id) h = (h * 31 + c.charCodeAt(0)) >>> 0
  return tints[h % tints.length]
}
