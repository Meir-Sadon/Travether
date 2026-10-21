import { useTranslation } from 'react-i18next'
import type en from '../i18n/locales/en.json'

export type ErrorCode = keyof (typeof en)['errors']

/** Translates an API error code (`errors.<code>`), falling back to a generic message for unknown codes. */
export function useErrorText() {
  const { t, i18n } = useTranslation()
  return (code: string) => (i18n.exists(`errors.${code}`) ? t(`errors.${code as ErrorCode}`) : t('errors.Unknown'))
}
