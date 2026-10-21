import i18n from 'i18next'
import LanguageDetector from 'i18next-browser-languagedetector'
import { initReactI18next } from 'react-i18next'
import en from './locales/en.json'

/**
 * Languages the UI ships in. Adding one = a JSON file in ./locales plus an entry here;
 * direction (ltr/rtl) comes from i18next, and the CSS is already logical-property based.
 */
export const supportedLanguages = ['en'] as const
export type Language = (typeof supportedLanguages)[number]

export const resources = {
  en: { translation: en },
} as const

/** Keeps <html lang dir> in sync so the browser, screen readers and CSS logical properties follow the language. */
export function applyDocumentLanguage(lng: string) {
  const root = document.documentElement
  root.lang = lng
  root.dir = i18n.dir(lng)
}

void i18n
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    resources,
    fallbackLng: 'en',
    supportedLngs: [...supportedLanguages],
    nonExplicitSupportedLngs: true, // en-GB → en
    interpolation: { escapeValue: false }, // React escapes already
    detection: {
      order: ['localStorage', 'navigator'],
      lookupLocalStorage: 'travether.lang',
      caches: ['localStorage'],
    },
    returnNull: false,
  })

i18n.on('languageChanged', applyDocumentLanguage)
applyDocumentLanguage(i18n.resolvedLanguage ?? 'en')

export default i18n
