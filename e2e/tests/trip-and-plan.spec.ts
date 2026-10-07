import { expect, test } from '@playwright/test'
import { createPlan, createTrip, signUp } from './helpers'

test('a trip owner invites a traveler, they plan a hike together and chat', async ({ browser }) => {
  const noa = await signUp(browser, 'Noa')
  const lena = await signUp(browser, 'Lena')

  // Noa creates a trip and shares its invite link.
  await createTrip(noa.page, 'Chiang Mai Crew')
  await noa.page.getByRole('button', { name: 'Share trip' }).click()
  const invite = (await noa.page.locator('.pass__url').textContent())!.trim()
  await noa.page.keyboard.press('Escape')

  // Lena opens the link and asks to join.
  await lena.page.goto(`/c/${invite.split('/c/')[1]}`)
  await lena.page.getByRole('button', { name: 'Request to join this trip' }).click()
  await lena.page.getByLabel('A note to the group (optional)').fill('Same dates, love hiking!')
  await lena.page.getByRole('button', { name: 'Send request' }).click()
  await expect(lena.page.getByText('Request sent. The group will see it and decide.')).toBeVisible()

  // Noa approves.
  await noa.page.reload()
  await noa.page.getByRole('radio', { name: /Members/ }).click()
  await noa.page.getByRole('button', { name: 'Approve' }).click()
  await expect(noa.page.getByRole('button', { name: 'Approve' })).toHaveCount(0)

  // Noa posts a plan in the trip.
  await createPlan(noa.page, 'Sunrise hike to Doi Suthep')
  const planUrl = noa.page.url()

  // A visitor sees the area, never the meeting point.
  const visitor = await (await browser.newContext()).newPage()
  await visitor.goto(planUrl)
  await expect(visitor.getByRole('heading', { level: 1, name: 'Sunrise hike to Doi Suthep' })).toBeVisible()
  await expect(visitor.getByText("Exact meeting point shown once you're approved")).toBeVisible()
  await expect(visitor.getByText('Tha Phae Gate')).toHaveCount(0)

  // Lena, now in the trip, joins directly and sees where to meet.
  await lena.page.goto(planUrl)
  await lena.page.getByRole('button', { name: 'Join this plan' }).click()
  await expect(lena.page.getByText("You're going")).toBeVisible()
  await expect(lena.page.getByText('Meet: Tha Phae Gate')).toBeVisible()
  await expect(lena.page.getByRole('link', { name: 'Add to calendar' })).toHaveAttribute('href', /calendar\.ics$/)

  // They talk in the plan chat; the message arrives live.
  await noa.page.getByRole('link', { name: 'Open plan chat' }).click()
  await lena.page.getByRole('link', { name: 'Open plan chat' }).click()
  await lena.page.getByRole('textbox').fill('See you at the gate at 6!')
  await lena.page.keyboard.press('Enter')
  await expect(noa.page.getByText('See you at the gate at 6!')).toBeVisible()
})
