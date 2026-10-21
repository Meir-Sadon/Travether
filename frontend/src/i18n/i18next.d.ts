import 'i18next'
import type en from './locales/en.json'

// Type-checks translation keys: t('status.api') compiles, t('status.typo') doesn't.
declare module 'i18next' {
  interface CustomTypeOptions {
    defaultNS: 'translation'
    resources: { translation: typeof en }
  }
}
