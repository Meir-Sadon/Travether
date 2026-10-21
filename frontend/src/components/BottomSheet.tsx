import { useEffect, useId, useRef, type ReactNode } from 'react'
import { createPortal } from 'react-dom'
import { useTranslation } from 'react-i18next'
import { IconButton } from './Button'
import './BottomSheet.css'

type BottomSheetProps = {
  open: boolean
  onClose: () => void
  title: string
  children: ReactNode
  /** Sticky area under the content, usually the primary action. */
  footer?: ReactNode
}

const focusableSelector =
  'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'

/**
 * Modal sheet that slides up from the bottom on mobile (create forms, filters, share).
 * Escape and the scrim close it; focus moves in on open, stays inside, and returns on close.
 */
export function BottomSheet({ open, onClose, title, children, footer }: BottomSheetProps) {
  const { t } = useTranslation()
  const titleId = useId()
  const sheetRef = useRef<HTMLDivElement>(null)
  const onCloseRef = useRef(onClose)
  useEffect(() => {
    onCloseRef.current = onClose
  }, [onClose])

  useEffect(() => {
    if (!open) return
    const previouslyFocused = document.activeElement as HTMLElement | null
    const sheet = sheetRef.current
    sheet?.querySelector<HTMLElement>(focusableSelector)?.focus()
    const { overflow } = document.body.style
    document.body.style.overflow = 'hidden'

    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        e.preventDefault()
        onCloseRef.current()
        return
      }
      if (e.key !== 'Tab' || !sheet) return
      const items = Array.from(sheet.querySelectorAll<HTMLElement>(focusableSelector))
      if (items.length === 0) return
      const first = items[0]
      const last = items[items.length - 1]
      if (e.shiftKey && document.activeElement === first) {
        e.preventDefault()
        last.focus()
      } else if (!e.shiftKey && document.activeElement === last) {
        e.preventDefault()
        first.focus()
      }
    }
    document.addEventListener('keydown', onKeyDown)

    return () => {
      document.removeEventListener('keydown', onKeyDown)
      document.body.style.overflow = overflow
      previouslyFocused?.focus()
    }
  }, [open])

  if (!open) return null

  return createPortal(
    <div className="sheet-root">
      <div className="sheet-scrim" onClick={onClose} aria-hidden="true" />
      <div ref={sheetRef} className="sheet" role="dialog" aria-modal="true" aria-labelledby={titleId}>
        <div className="sheet__handle" aria-hidden="true" />
        <header className="sheet__header">
          <h2 id={titleId} className="sheet__title">
            {title}
          </h2>
          <IconButton icon="close" label={t('common.close')} onClick={onClose} />
        </header>
        <div className="sheet__content">{children}</div>
        {footer && <footer className="sheet__footer">{footer}</footer>}
      </div>
    </div>,
    document.body,
  )
}
