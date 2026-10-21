import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { describe, expect, it, vi } from 'vitest'
import { AvatarStack, BottomSheet, Button, Chip, IconButton, Stepper } from '.'

describe('Button', () => {
  it('is a non-submitting button by default and calls onClick', async () => {
    const onClick = vi.fn<() => void>()
    render(<Button onClick={onClick}>Request to join</Button>)
    const btn = screen.getByRole('button', { name: 'Request to join' })
    expect(btn).toHaveAttribute('type', 'button')
    await userEvent.click(btn)
    expect(onClick).toHaveBeenCalledOnce()
  })

  it('is disabled and busy while loading', async () => {
    const onClick = vi.fn<() => void>()
    render(
      <Button loading onClick={onClick}>
        Saving
      </Button>,
    )
    const btn = screen.getByRole('button', { name: 'Saving' })
    expect(btn).toBeDisabled()
    expect(btn).toHaveAttribute('aria-busy', 'true')
    await userEvent.click(btn)
    expect(onClick).not.toHaveBeenCalled()
  })

  it('gives icon-only buttons an accessible name', () => {
    render(<IconButton icon="back" label="Back" />)
    expect(screen.getByRole('button', { name: 'Back' })).toBeInTheDocument()
  })
})

describe('Chip', () => {
  it('is a plain label without onToggle', () => {
    render(<Chip>Hike</Chip>)
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
    expect(screen.getByText('Hike')).toBeInTheDocument()
  })

  it('toggles and announces its pressed state', async () => {
    function Toggle() {
      const [on, setOn] = useState(false)
      return (
        <Chip selected={on} onToggle={setOn}>
          Food
        </Chip>
      )
    }
    render(<Toggle />)
    const chip = screen.getByRole('button', { name: 'Food' })
    expect(chip).toHaveAttribute('aria-pressed', 'false')
    await userEvent.click(chip)
    expect(chip).toHaveAttribute('aria-pressed', 'true')
  })
})

describe('AvatarStack', () => {
  const people = ['Lena', 'Jonas', 'Maya', 'Tom', 'Ana', 'Yuki'].map((name) => ({ name }))

  it('shows up to max avatars and collapses the rest', () => {
    const { container } = render(<AvatarStack people={people} max={4} />)
    expect(container.querySelectorAll('.avatar:not(.avatar--more)')).toHaveLength(4)
    expect(screen.getByText('+2')).toBeInTheDocument()
  })

  it('names everyone for screen readers', () => {
    render(<AvatarStack people={people.slice(0, 3)} />)
    expect(screen.getByRole('img', { name: 'Lena, Jonas, and Maya' })).toBeInTheDocument()
  })
})

describe('Stepper', () => {
  it('marks done and current steps', () => {
    render(<Stepper label="Request status" steps={['Requested', 'Approved', 'Chat open']} current={1} />)
    const list = screen.getByRole('list', { name: 'Request status' })
    const items = Array.from(list.querySelectorAll('li'))
    expect(items[0]).toHaveTextContent('(done)')
    expect(items[1]).toHaveAttribute('aria-current', 'step')
    expect(items[2]).not.toHaveAttribute('aria-current')
  })
})

describe('BottomSheet', () => {
  function Harness({ onClose = () => {} }: { onClose?: () => void }) {
    const [open, setOpen] = useState(false)
    return (
      <>
        <Button onClick={() => setOpen(true)}>Open</Button>
        <BottomSheet
          open={open}
          title="New plan"
          onClose={() => {
            onClose()
            setOpen(false)
          }}
          footer={<Button>Publish</Button>}
        >
          <input aria-label="Title" />
        </BottomSheet>
      </>
    )
  }

  it('opens as a labelled modal dialog and moves focus inside', async () => {
    render(<Harness />)
    await userEvent.click(screen.getByRole('button', { name: 'Open' }))
    const dialog = screen.getByRole('dialog', { name: 'New plan' })
    expect(dialog).toHaveAttribute('aria-modal', 'true')
    expect(dialog).toContainElement(document.activeElement as HTMLElement)
  })

  it('closes on Escape and returns focus to the opener', async () => {
    const onClose = vi.fn<() => void>()
    render(<Harness onClose={onClose} />)
    const opener = screen.getByRole('button', { name: 'Open' })
    await userEvent.click(opener)
    await userEvent.keyboard('{Escape}')
    expect(onClose).toHaveBeenCalledOnce()
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(opener).toHaveFocus()
  })

  it('keeps Tab focus inside the sheet', async () => {
    render(<Harness />)
    await userEvent.click(screen.getByRole('button', { name: 'Open' }))
    const dialog = screen.getByRole('dialog')
    for (let i = 0; i < 5; i++) {
      await userEvent.tab()
      expect(dialog).toContainElement(document.activeElement as HTMLElement)
    }
  })
})
