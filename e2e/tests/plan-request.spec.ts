import { expect, test } from '@playwright/test'
import { createPlan, createTrip, signUp } from './helpers'

test('a solo traveler asks for a seat in a plan and the host approves', async ({ browser }) => {
  const noa = await signUp(browser, 'Noa')
  const tom = await signUp(browser, 'Tom')

  await createTrip(noa.page, 'Pai Hikers')
  await createPlan(noa.page, 'Waterfall hike')
  const planUrl = noa.page.url()

  // Tom isn't in the trip, so he asks with a note.
  await tom.page.goto(planUrl)
  await tom.page.getByRole('button', { name: 'Request to join' }).click()
  const sheet = tom.page.getByRole('dialog')
  await sheet.getByLabel('A note to the host (optional)').fill('Solo in Pai the same week.')
  await sheet.getByRole('button', { name: /Request/ }).click()
  await expect(tom.page.getByText('Your request')).toBeVisible()
  await expect(tom.page.getByText('Tha Phae Gate')).toHaveCount(0)

  // Noa sees the request on the plan and approves it.
  await noa.page.reload()
  await expect(noa.page.getByText('Solo in Pai the same week.')).toBeVisible()
  await noa.page.getByRole('button', { name: 'Approve' }).click()

  // Tom is going and now sees the exact meeting point.
  await tom.page.reload()
  await expect(tom.page.getByText("You're going")).toBeVisible()
  await expect(tom.page.getByText('Meet: Tha Phae Gate')).toBeVisible()
})
