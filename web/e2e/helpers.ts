import { expect, type Page } from '@playwright/test';

export const PASSWORD = 'CorrectHorse9!';

let counter = 0;

export interface Account {
  email: string;
  fullName: string;
  company: string;
}

/** A unique account per call, so tests never share a company or an email. */
export function newAccount(label = 'Owner'): Account {
  const id = `${Date.now().toString(36)}${(counter++).toString(36)}${Math.random().toString(36).slice(2, 6)}`;
  return { email: `${label.toLowerCase()}-${id}@example.com`, fullName: `${label} ${id.slice(-4)}`, company: `Acme ${id}` };
}

/** Registers through the real form and waits for the dashboard. */
export async function register(page: Page, account = newAccount()): Promise<Account> {
  await page.goto('/auth/register');
  await page.locator('#company').fill(account.company);
  await page.locator('#name').fill(account.fullName);
  await page.locator('#email').fill(account.email);
  await page.locator('#password').fill(PASSWORD);
  await page.locator('button[type=submit]').click();
  await expect(page).toHaveURL(/\/dashboard/);
  return account;
}

export async function signIn(page: Page, account: Account, password = PASSWORD) {
  await page.locator('#email').fill(account.email);
  await page.locator('#password').fill(password);
  await page.locator('button[type=submit]').click();
}

/** Opens the user menu in the header and signs out, then waits for the sign-in page. */
export async function signOut(page: Page, account: Account) {
  // The menu button is only present once the session has loaded, so wait for it like a user would.
  const menu = page.getByRole('button', { name: new RegExp(account.fullName) });
  await menu.waitFor({ state: 'visible' });
  await menu.click();
  await page.getByRole('menuitem', { name: 'Sign out' }).click();
  await expect(page).toHaveURL(/\/auth\/login/);
}

/** Creates a project from the Projects page and opens its board. */
export async function createProjectAndOpen(page: Page, name = 'Website redesign') {
  await page.goto('/projects');
  await page.getByRole('button', { name: 'New project' }).first().click();
  await page.locator('#pname').fill(name);
  await page.getByRole('button', { name: 'Create project' }).click();
  await page.locator("a[href^='/projects/']").first().click();
  await expect(page).toHaveURL(/\/projects\/[0-9a-f-]+/);
  await expect(page.locator("[aria-label$=' column']").first()).toBeVisible();
}

/** Adds a task from the board's quick-add field. */
export async function addTask(page: Page, title: string) {
  const box = page.getByLabel('New task title');
  await box.fill(title);
  // Wait for the server to confirm the task: until then the card on screen is provisional, and acting on
  // it (dragging, opening) can race the response that replaces it.
  const saved = page.waitForResponse((r) => /\/projects\/[^/]+\/tasks$/.test(r.url()) && r.request().method() === 'POST');
  await box.press('Enter');
  expect((await saved).ok()).toBe(true);
  await expect(page.locator('[draggable=true]').filter({ hasText: title })).toBeVisible();
}
