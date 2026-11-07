import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'
import de from './locales/de-DE.json'
import tr from './locales/tr-TR.json'
i18n.use(initReactI18next).init({ resources: { 'de-DE': { translation: de }, 'tr-TR': { translation: tr } }, lng: localStorage.getItem('almer-locale') ?? 'de-DE', fallbackLng: 'de-DE', supportedLngs: ['de-DE', 'tr-TR'], interpolation: { escapeValue: false } })
const syncDocumentLanguage = (language: string) => { document.documentElement.lang = language }
syncDocumentLanguage(i18n.language)
i18n.on('languageChanged', syncDocumentLanguage)
export default i18n

