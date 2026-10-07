import { expect, test } from '@playwright/test'
import { signUp } from './helpers'

test('a traveler signs up, logs out and logs back in', async ({ browser }) => {
  const noa = await signUp(browser, 'Noa')
  const { page } = noa

  await page.goto('/settings')
  await page.getByRole('button', { name: 'Log out' }).click()
  await expect(page).toHaveURL(/\/welcome/)

  await page.getByRole('link', { name: 'Log in' }).click()
  await page.getByRole('button', { name: 'Use a password instead' }).click()
  await page.getByLabel('Email').fill(noa.email)
  await page.getByLabel('Password').fill('correct horse battery')
  await page.getByRole('button', { name: 'Log in' }).click()
  // Back where they left off.
  await expect(page.getByRole('heading', { level: 1, name: 'Settings' })).toBeVisible()
  await page.goto('/')
  await expect(page.getByRole('heading', { level: 1, name: 'Hi Noa' })).toBeVisible()
})
