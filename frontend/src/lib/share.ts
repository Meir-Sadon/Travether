/** Accepts a full share link or just its code, returns the code. */
export function shareCodeFrom(input: string): string {
  const trimmed = input.trim()
  const match = /\/c\/([A-Za-z0-9]+)/.exec(trimmed)
  return match ? match[1] : trimmed
}
