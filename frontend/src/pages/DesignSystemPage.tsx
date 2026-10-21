import { useState } from 'react'
import { AvatarStack, BottomSheet, Button, Card, CardBody, CardMedia, Chip, Icon, IconButton, Stepper } from '../components'
import './DesignSystemPage.css'

const people = [
  { name: 'Lena', tint: '#F3E3CF' },
  { name: 'Jonas', tint: '#DDEBF7' },
  { name: 'Maya', tint: '#E3EFE6' },
  { name: 'Tom', tint: '#FBE7D9' },
  { name: 'Ana', tint: '#E8E1F5' },
  { name: 'Yuki', tint: '#D9F0F0' },
]

const swatches = [
  ['--color-accent', 'Accent (lime)'],
  ['--color-brand', 'Brand (ink)'],
  ['--color-bg', 'Background'],
  ['--color-surface', 'Surface'],
  ['--color-text', 'Text'],
  ['--color-text-secondary', 'Text secondary'],
  ['--color-success', 'Success'],
  ['--color-info', 'Info'],
  ['--color-warning', 'Warning'],
  ['--color-alert', 'Alert (dots only)'],
] as const

const interests = ['Hiking', 'Food', 'Nightlife', 'Culture', 'Budget']

/** Living reference for tokens and base components (PLAN.md §5). Route: /design */
export function DesignSystemPage() {
  const [sheetOpen, setSheetOpen] = useState(false)
  const [step, setStep] = useState(0)
  const [picked, setPicked] = useState<string[]>(['Hiking'])

  return (
    <main className="ds">
      <header className="ds__header">
        <h1>Travether design system</h1>
        <p className="ds__lead">"Fresh Explorer": ink + lime, Rubik, rounded cards. Toggle your OS dark mode to see the dark tokens.</p>
      </header>

      <section className="ds__section" aria-labelledby="ds-colors">
        <h2 id="ds-colors">Colors</h2>
        <ul className="ds__swatches">
          {swatches.map(([token, name]) => (
            <li key={token} className="ds__swatch">
              <span className="ds__chip" style={{ background: `var(${token})` }} />
              <span>
                <strong>{name}</strong>
                <code>{token}</code>
              </span>
            </li>
          ))}
        </ul>
      </section>

      <section className="ds__section" aria-labelledby="ds-type">
        <h2 id="ds-type">Typography</h2>
        <p className="ds__display">Find people to do things with on your trip.</p>
        <p className="ds__title">Sunrise hike to Doi Suthep</p>
        <p>Body: join travelers who are in the same city, on the same dates.</p>
        <p className="ds__meta">Meta: Tue 14 Oct · 05:30 · ~2 km away</p>
      </section>

      <section className="ds__section" aria-labelledby="ds-buttons">
        <h2 id="ds-buttons">Buttons</h2>
        <div className="ds__row">
          <Button size="lg" block>
            Request to join
          </Button>
          <Button variant="brand" icon="chat">
            Open chat
          </Button>
          <Button variant="secondary">Withdraw request</Button>
          <Button variant="ghost">Browse without an account</Button>
          <Button size="sm" loading>
            Saving
          </Button>
          <Button disabled>Disabled</Button>
          <IconButton icon="back" label="Back" variant="raised" />
          <IconButton icon="more" label="More options" />
        </div>
      </section>

      <section className="ds__section" aria-labelledby="ds-chips">
        <h2 id="ds-chips">Chips</h2>
        <div className="ds__row">
          <Chip tint="var(--tint-hike)">Hike</Chip>
          <Chip tint="var(--tint-food)">Food</Chip>
          <Chip tone="success">5 seats left</Chip>
          <Chip tone="accent">1 request</Chip>
          <Chip icon="shieldCheck" tone="success">
            ID verified
          </Chip>
        </div>
        <div className="ds__row">
          {interests.map((interest) => (
            <Chip
              key={interest}
              size="md"
              selected={picked.includes(interest)}
              onToggle={(on) => setPicked((prev) => (on ? [...prev, interest] : prev.filter((x) => x !== interest)))}
            >
              {interest}
            </Chip>
          ))}
        </div>
      </section>

      <section className="ds__section" aria-labelledby="ds-avatars">
        <h2 id="ds-avatars">Avatar stack</h2>
        <div className="ds__row">
          <AvatarStack people={people.slice(0, 2)} />
          <AvatarStack people={people} max={4} />
          <AvatarStack people={people.slice(0, 3)} size="md" />
        </div>
      </section>

      <section className="ds__section" aria-labelledby="ds-cards">
        <h2 id="ds-cards">Cards</h2>
        <div className="ds__cards">
          <Card interactive>
            <CardMedia tint="var(--tint-hike)" overlay={<Chip tone="inverse">Hike</Chip>} />
            <CardBody>
              <strong>Sunrise hike to Doi Suthep</strong>
              <span className="ds__meta">Tue 14 Oct · 05:30 · ~2 km away</span>
              <div className="ds__card-foot">
                <AvatarStack people={people.slice(0, 3)} />
                <Chip tone="success">3 seats left</Chip>
              </div>
            </CardBody>
          </Card>
          <Card variant="filled">
            <CardBody>
              <strong>
                <Icon name="shieldCheck" size={16} className="ds__inline-icon" /> Meet in a public place
              </strong>
              <span className="ds__meta">The exact meeting point is shared in the plan chat once you're approved.</span>
            </CardBody>
          </Card>
        </div>
      </section>

      <section className="ds__section" aria-labelledby="ds-stepper">
        <h2 id="ds-stepper">Stepper</h2>
        <Stepper label="Request status" steps={['Requested', 'Approved', 'Chat open']} current={step} />
        <div className="ds__row">
          <Button size="sm" variant="secondary" onClick={() => setStep((s) => (s + 1) % 3)}>
            Next step
          </Button>
        </div>
      </section>

      <section className="ds__section" aria-labelledby="ds-sheet">
        <h2 id="ds-sheet">Bottom sheet</h2>
        <Button variant="brand" icon="plus" onClick={() => setSheetOpen(true)}>
          New plan
        </Button>
        <BottomSheet
          open={sheetOpen}
          onClose={() => setSheetOpen(false)}
          title="New plan"
          footer={
            <Button block size="lg" onClick={() => setSheetOpen(false)}>
              Publish plan
            </Button>
          }
        >
          <p className="ds__meta">Create forms, filters and share options open in a sheet on mobile.</p>
        </BottomSheet>
      </section>
    </main>
  )
}
