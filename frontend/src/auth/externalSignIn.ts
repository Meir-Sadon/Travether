/** Loads Google Identity Services / Sign in with Apple JS on demand; both return an OpenID ID token. */

type GoogleId = {
  accounts: {
    id: {
      initialize: (opts: { client_id: string; callback: (r: { credential: string }) => void; ux_mode?: string }) => void
      renderButton: (el: HTMLElement, opts: Record<string, unknown>) => void
    }
  }
}

type AppleId = {
  auth: {
    init: (opts: { clientId: string; scope: string; redirectURI: string; usePopup: boolean }) => void
    signIn: () => Promise<{ authorization: { id_token: string }; user?: { name?: { firstName?: string } } }>
  }
}

declare global {
  interface Window {
    google?: GoogleId
    AppleID?: AppleId
  }
}

const loaded = new Map<string, Promise<void>>()

function loadScript(src: string): Promise<void> {
  let p = loaded.get(src)
  if (!p) {
    p = new Promise((resolve, reject) => {
      const s = document.createElement('script')
      s.src = src
      s.async = true
      s.onload = () => resolve()
      s.onerror = () => reject(new Error(`Failed to load ${src}`))
      document.head.appendChild(s)
    })
    loaded.set(src, p)
  }
  return p
}

/** Renders Google's own button (required by their branding rules) into `el`. */
export async function renderGoogleButton(el: HTMLElement, clientId: string, onToken: (idToken: string) => void, locale: string) {
  await loadScript('https://accounts.google.com/gsi/client')
  const google = window.google
  if (!google) throw new Error('Google Identity Services unavailable')
  google.accounts.id.initialize({ client_id: clientId, callback: (r) => onToken(r.credential) })
  google.accounts.id.renderButton(el, { theme: 'outline', size: 'large', shape: 'pill', text: 'continue_with', width: el.clientWidth || 320, locale })
}

/** Opens Apple's popup and returns the ID token plus the first name (Apple sends it only on the first sign-in). */
export async function signInWithApple(clientId: string, redirectUri: string): Promise<{ idToken: string; givenName?: string }> {
  await loadScript('https://appleid.cdn-apple.com/appleauth/static/jsapi/appleid/1/en_US/appleid.auth.js')
  const apple = window.AppleID
  if (!apple) throw new Error('Sign in with Apple unavailable')
  apple.auth.init({ clientId, scope: 'name email', redirectURI: redirectUri, usePopup: true })
  const res = await apple.auth.signIn()
  return { idToken: res.authorization.id_token, givenName: res.user?.name?.firstName }
}
