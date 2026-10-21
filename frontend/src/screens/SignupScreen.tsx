import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import { Button, Chip, Stepper, TextField } from '../components'
import { ScreenHeader } from '../layout/ScreenHeader'

const interests = ['hiking', 'food', 'nightlife', 'culture', 'beach', 'dayTrips', 'diving', 'budget'] as const

/** 2 · Sign up / onboarding in 3 steps, under a minute (PLAN.md §4.1). */
export function SignupScreen() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [step, setStep] = useState(0)
  const [picked, setPicked] = useState<string[]>(['hiking', 'food'])

  const steps = [t('signup.stepAccount'), t('signup.stepAbout'), t('signup.stepYou')]
  const next = () => (step < 2 ? setStep(step + 1) : void navigate('/'))

  return (
    <div className="screen" style={{ minBlockSize: '100dvh' }}>
      <ScreenHeader back backTo="/welcome" title={t('signup.title')} />
      <div className="screen__section">
        <Stepper label={t('signup.progress')} steps={steps} current={step} />
      </div>

      {step === 0 && (
        <section className="screen__section">
          <h2>{t('signup.createAccount')}</h2>
          <p className="screen__meta">{t('signup.noId')}</p>
          <Button variant="secondary" block onClick={next}>
            {t('signup.google')}
          </Button>
          <Button variant="secondary" block onClick={next}>
            {t('signup.apple')}
          </Button>
          <p className="screen__meta" style={{ textAlign: 'center' }}>
            {t('signup.or')}
          </p>
          <TextField label={t('signup.email')} type="email" autoComplete="email" placeholder="noa@example.com" />
          <p className="screen__note">{t('signup.terms')}</p>
        </section>
      )}

      {step === 1 && (
        <section className="screen__section">
          <h2>{t('signup.aboutYou')}</h2>
          <p className="screen__meta">{t('signup.aboutHint')}</p>
          <TextField label={t('signup.firstName')} autoComplete="given-name" defaultValue="Noa" />
          <TextField label={t('signup.lastName')} autoComplete="family-name" hint={t('signup.lastNameHint')} />
          <TextField label={t('signup.dob')} type="date" autoComplete="bday" hint={t('signup.dobHint')} />
          <TextField label={t('signup.country')} autoComplete="country-name" defaultValue="Israel" />
        </section>
      )}

      {step === 2 && (
        <section className="screen__section">
          <h2>{t('signup.makeItYours')}</h2>
          <p className="screen__meta">{t('signup.optional')}</p>
          <Button variant="secondary" icon="user">
            {t('signup.addPhoto')}
          </Button>
          <h3 className="screen__section-title">{t('signup.likes')}</h3>
          <div className="screen__row">
            {interests.map((i) => (
              <Chip
                key={i}
                size="md"
                selected={picked.includes(i)}
                onToggle={(on) => setPicked((prev) => (on ? [...prev, i] : prev.filter((x) => x !== i)))}
              >
                {t(`interest.${i}`)}
              </Chip>
            ))}
          </div>
          <TextField label={t('signup.languages')} defaultValue="Hebrew, English" />
        </section>
      )}

      <footer className="screen__footer">
        <Button size="lg" block onClick={next}>
          {step === 0 ? t('signup.sendCode') : step === 1 ? t('signup.continue') : t('signup.startExploring')}
        </Button>
        {step === 2 && (
          <Button variant="ghost" block onClick={() => void navigate('/')}>
            {t('signup.skip')}
          </Button>
        )}
      </footer>
    </div>
  )
}
