# Design system

"Fresh Explorer" (PLAN.md §5) implemented as CSS custom properties and a few React components. Run the app and open **`/design`** for the living reference (light and dark).

## Tokens

[`frontend/src/styles/tokens.css`](../frontend/src/styles/tokens.css). Components use semantic tokens only (`--color-accent`, `--color-text`, `--radius-lg`, `--space-4`…), never raw hex values, so dark mode is a token swap. Dark mode follows the OS; `<html data-theme="light|dark">` pins it.

Rules:
- Lime (`--color-accent`) always carries ink (`--color-on-accent`) text and icons. Never lime text on white.
- One primary (lime) button per screen.
- `--color-alert` is for notification dots only.
- Touch targets are at least 44 px (`--tap-target`).
- Rubik is self-hosted from npm (`@fontsource-variable/rubik`), not loaded from Google Fonts, so no visitor IP goes to a third party (GDPR). It covers Latin and Hebrew.

## Components

[`frontend/src/components`](../frontend/src/components), exported from `components/index.ts`:

| Component | Use |
|-----------|-----|
| `Button`, `IconButton` | Variants `primary` (lime), `brand` (ink), `secondary` (outlined), `ghost`; sizes `sm`/`md`/`lg`; `loading`, `block`. Icon buttons require a `label`. |
| `Card`, `CardMedia`, `CardBody` | Rounded containers for plans, trips and callouts; `interactive` adds hover/press feedback. |
| `Chip` | Static labels (category, seats left, badges) or toggles with `onToggle` (filters, interests; announces `aria-pressed`). |
| `Avatar`, `AvatarStack` | Overlapping avatars with a `+N` overflow and a spoken list of names. |
| `BottomSheet` | Modal sheet for create forms, filters and share. Escape and the scrim close it; focus is trapped and restored. |
| `Stepper` | Status steps such as *Requested → Approved → Chat open*; the current step has `aria-current="step"`. |
| `Icon` | The prototype's stroke icons; arrows mirror in RTL. |
