import { useErrorText } from './useErrorText'

/** An announced error under a form. Renders nothing without a code. */
export function FormError({ code }: { code: string | null }) {
  const text = useErrorText()
  if (!code) return null
  return (
    <p className="form-error" role="alert">
      {text(code)}
    </p>
  )
}
