import { expect, test } from '@playwright/test'
import { password, signUp } from './helpers'

test('a traveler downloads their data and deletes their account', async ({ browser }) => {
  const maya = await signUp(browser, 'Maya')
  const { page } = maya

  await page.goto('/settings/privacy')
  const [download] = await Promise.all([page.waitForEvent('download'), page.getByRole('link', { name: 'Download my data' }).click()])
  expect(download.suggestedFilename()).toMatch(/^travether-data-\d{4}-\d{2}-\d{2}\.json$/)
  const data = JSON.parse(await (await download.createReadStream()).toArray().then((c) => Buffer.concat(c).toString()))
  expect(data.account.email).toBe(maya.email)

  await page.goto('/settings')
  await page.getByRole('button', { name: 'Delete account' }).click()
  const sheet = page.getByRole('dialog', { name: 'Delete your account?' })
  await sheet.getByRole('button', { name: 'Continue' }).click()
  await sheet.getByLabel('Password').fill(password)
  await sheet.getByRole('button', { name: 'Delete permanently' }).click()
  await expect(page.getByText('Your account was deleted. Safe travels.')).toBeVisible()

  // The old password no longer works.
  await page.goto('/login')
  await page.getByRole('button', { name: 'Use a password instead' }).click()
  await page.getByLabel('Email').fill(maya.email)
  await page.getByLabel('Password').fill(password)
  await page.getByRole('button', { name: 'Log in' }).click()
  await expect(page.getByRole('alert')).toHaveText('Email or password is incorrect.')
})
