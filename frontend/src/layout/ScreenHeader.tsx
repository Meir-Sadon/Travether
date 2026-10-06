import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import { IconButton } from '../components'
import './ScreenHeader.css'

type ScreenHeaderProps = {
  title?: string
  subtitle?: string
  /** Show a back button (history back, or `backTo` when there's no history). */
  back?: boolean
  backTo?: string
  action?: ReactNode
  /** Large page title instead of a compact bar title. */
  large?: boolean
}

export function ScreenHeader({ title, subtitle, back = false, backTo = '/', action, large = false }: ScreenHeaderProps) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const goBack = () => (window.history.length > 1 ? navigate(-1) : navigate(backTo))

  return (
    <header className={`screen-header${large ? ' screen-header--large' : ''}`}>
      {back && <IconButton icon="back" label={t('common.back')} onClick={() => void goBack()} />}
      <div className="screen-header__titles">
        {title && <h1 className="screen-header__title">{title}</h1>}
        {subtitle && <p className="screen-header__subtitle">{subtitle}</p>}
      </div>
      {action}
    </header>
  )
}
