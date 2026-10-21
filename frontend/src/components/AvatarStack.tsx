import './AvatarStack.css'

export type Person = {
  name: string
  photoUrl?: string
  /** Background behind the initial when there's no photo. */
  tint?: string
}

type AvatarProps = { person: Person; size?: 'sm' | 'md' | 'lg' }

function initial(name: string) {
  return Array.from(name.trim())[0]?.toLocaleUpperCase() ?? '?'
}

export function Avatar({ person, size = 'md' }: AvatarProps) {
  return (
    <span className={`avatar avatar--${size}`} style={{ background: person.tint }} title={person.name}>
      {person.photoUrl ? <img src={person.photoUrl} alt="" className="avatar__img" /> : <span aria-hidden="true">{initial(person.name)}</span>}
    </span>
  )
}

type AvatarStackProps = {
  people: Person[]
  /** Avatars shown before collapsing the rest into "+N". */
  max?: number
  size?: 'sm' | 'md' | 'lg'
  /** Accessible summary; defaults to the names. */
  label?: string
}

/** Overlapping avatars for plan and trip cards: who's going at a glance. */
export function AvatarStack({ people, max = 4, size = 'sm', label }: AvatarStackProps) {
  const shown = people.slice(0, max)
  const rest = people.length - shown.length
  const summary = label ?? new Intl.ListFormat(undefined, { type: 'conjunction' }).format(people.map((p) => p.name))

  return (
    <span className={`avatar-stack avatar-stack--${size}`} role="img" aria-label={summary}>
      {shown.map((p, i) => (
        <Avatar key={`${p.name}-${i}`} person={p} size={size} />
      ))}
      {rest > 0 && (
        <span className={`avatar avatar--${size} avatar--more`} aria-hidden="true">
          +{rest}
        </span>
      )}
    </span>
  )
}
