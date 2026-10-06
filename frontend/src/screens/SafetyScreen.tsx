import { useTranslation } from 'react-i18next'
import { Card, CardBody, Icon } from '../components'
import { ScreenHeader } from '../layout/ScreenHeader'

const tips = ['public', 'tell', 'own', 'chat', 'trust', 'report'] as const

/** Safety center (PLAN.md §4.8): tips before meeting people, and how to block and report. */
export function SafetyScreen() {
  const { t } = useTranslation()
  return (
    <div className="screen">
      <ScreenHeader back backTo="/settings" title={t('safety.title')} />
      <section className="screen__section">
        <p className="screen__body">{t('safety.intro')}</p>
      </section>
      <section className="screen__section" aria-labelledby="safety-tips">
        <h2 id="safety-tips" className="screen__section-title">
          {t('safety.tipsTitle')}
        </h2>
        <ol className="list-reset screen__stack">
          {tips.map((k) => (
            <li key={k}>
              <Card variant="filled" as="div">
                <CardBody>
                  <strong>
                    <Icon name="shieldCheck" size={16} /> {t(`safety.tip_${k}`)}
                  </strong>
                  <span className="screen__meta">{t(`safety.tip_${k}_body`)}</span>
                </CardBody>
              </Card>
            </li>
          ))}
        </ol>
      </section>
      <section className="screen__section" aria-labelledby="safety-tools">
        <h2 id="safety-tools" className="screen__section-title">
          {t('safety.toolsTitle')}
        </h2>
        <p className="screen__body">{t('safety.tools')}</p>
        <p className="screen__note">{t('safety.emergency')}</p>
      </section>
    </div>
  )
}
