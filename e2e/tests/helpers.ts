import { expect, type Browser, type Page } from '@playwright/test'

export const password = 'correct horse battery'

/** A unique address per run, so tests never collide in a reused database. */
export const newEmail = (name: string) => `${name.toLowerCase()}-${Date.now()}-${Math.floor(Math.random() * 1e6)}@e2e.example`

export type Traveler = { page: Page; name: string; email: string }

/** Signs up through the three onboarding steps with email + password and lands on Home. */
export async function signUp(browser: Browser, name: string): Promise<Traveler> {
  const context = await browser.newContext()
  const page = await context.newPage()
  const email = newEmail(name)

  await page.goto('/signup')
  await page.getByRole('button', { name: 'Create a password instead' }).click()
  await page.getByLabel('Email').fill(email)
  await page.getByLabel('Password').fill(password)
  await page.getByRole('button', { name: 'Continue' }).click()

  await page.getByLabel('First name').fill(name)
  await page.getByLabel('Last name').fill('Tester')
  await page.getByLabel('Date of birth').fill('1995-05-05')
  await page.getByLabel('Country of origin').selectOption('IL')
  await page.getByRole('checkbox', { name: /I'm 18 or older/ }).check()
  await page.getByRole('button', { name: 'Continue' }).click()

  await page.getByRole('button', { name: 'Skip for now' }).click()
  await expect(page.getByRole('heading', { level: 1, name: `Hi ${name}` })).toBeVisible()
  return { page, name, email }
}

/** yyyy-mm-dd, `days` from today. */
export function isoDate(days: number): string {
  const d = new Date()
  d.setDate(d.getDate() + days)
  return d.toISOString().slice(0, 10)
}

const thaPhaeGate = { name: 'Tha Phae Gate', area: 'Old City, Chiang Mai', lat: 18.7877, lng: 98.9933 }

/** Meeting-point search goes to an outside geocoder; answer it locally so the test is stable. */
export async function stubPlaces(page: Page) {
  await page.route('**/api/places/search**', (route) => route.fulfill({ json: [thaPhaeGate] }))
}

/** From Home: the + button → New Vacation Card. Ends on the trip page. */
export async function createTrip(page: Page, name: string) {
  await page.getByRole('button', { name: 'Create a plan or trip' }).click()
  await page.getByRole('dialog').getByRole('button', { name: /New Vacation Card/ }).click()
  await page.getByLabel('Trip name').fill(name)
  await page.getByLabel('Country').selectOption('TH')
  await page.getByLabel('Cities or regions').fill('Chiang Mai, Pai')
  await page.getByLabel('From').fill(isoDate(3))
  await page.getByLabel('To').fill(isoDate(14))
  await page.getByRole('button', { name: 'Create trip' }).click()
  await expect(page.getByRole('heading', { level: 1, name })).toBeVisible()
}

/** From a trip page: Plans → New Activity Plan, meeting at Tha Phae Gate. Ends on the plan page. */
export async function createPlan(page: Page, title: string) {
  await stubPlaces(page)
  await page.getByRole('radio', { name: 'Plans' }).click()
  await page.getByRole('button', { name: 'New Activity Plan' }).click()
  await page.getByLabel('Title').fill(title)
  await page.getByLabel('Date').fill(isoDate(5))
  await page.getByLabel('Time').fill('06:00')
  await page.getByLabel('Meeting point').fill('Tha Phae')
  await page.getByRole('button', { name: /Tha Phae Gate/ }).click()
  await page.getByRole('textbox', { name: 'Destination' }).fill('Wat Phra That Doi Suthep')
  await page.getByRole('button', { name: 'Publish plan' }).click()
  await expect(page.getByRole('heading', { level: 1, name: title })).toBeVisible()
}
