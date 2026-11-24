import { expect, test } from '@playwright/test';
import { PASSWORD, newAccount, register, signIn, signOut } from './helpers';

test.describe('access', () => {
  test('a signed-out visitor is sent to sign-in, and the page they wanted is remembered', async ({ page }) => {
    await page.goto('/team');
    await expect(page).toHaveURL(/\/auth\/login\?returnUrl=%2Fteam/);
  });

  test('a weak password cannot be submitted', async ({ page }) => {
    const account = newAccount();
    await page.goto('/auth/register');
    await page.locator('#company').fill(account.company);
    await page.locator('#name').fill(account.fullName);
    await page.locator('#email').fill(account.email);
    await page.locator('#password').fill('short');
    await expect(page.locator('button[type=submit]')).toBeDisabled();
  });

  test('registering creates a workspace and lands on the dashboard', async ({ page }) => {
    const account = await register(page);
    await expect(page.getByRole('heading', { name: new RegExp(account.fullName.split(' ')[0]) })).toBeVisible();
    await expect(page.getByText('Active projects')).toBeVisible();
  });

  test('a wrong password shows an error and stays on sign-in', async ({ page }) => {
    const account = await register(page);
    await signOut(page, account);
    await signIn(page, account, 'WrongPassword1!');
    await expect(page.getByRole('alert')).toContainText(/invalid email or password/i);
    await expect(page).toHaveURL(/\/auth\/login/);
  });

  test('signing in after being sent to sign-in returns to the page that was asked for', async ({ page, browser }) => {
    const account = await register(page);
    await signOut(page, account);
    const fresh = await (await browser.newContext()).newPage();
    await fresh.goto('/projects');
    await expect(fresh).toHaveURL(/returnUrl=%2Fprojects/);
    await signIn(fresh, account);
    await expect(fresh).toHaveURL(/\/projects$/);
    await fresh.context().close();
  });

  for (const hostile of ['https://evil.example', '//evil.example', '/\\evil.example', 'javascript:alert(1)']) {
    test(`a crafted returnUrl (${hostile}) never leaves the site`, async ({ page, browser }) => {
      const account = await register(page);
      await signOut(page, account);
      const fresh = await (await browser.newContext()).newPage();
      await fresh.goto('/auth/login?returnUrl=' + encodeURIComponent(hostile));
      await signIn(fresh, account);
      await expect(fresh).toHaveURL(/localhost:4200\/dashboard/);
      await fresh.context().close();
    });
  }
});

// Regression: after signing out inside the app, signing back in used to hang on "Signing in..." because
// the route input was undefined and safeReturnUrl() called .startsWith on it. A reload got past it.
test.describe('regression: signing in again without a reload', () => {
  test('sign out, then sign in again, lands on the dashboard', async ({ page }) => {
    const account = await register(page);
    await signOut(page, account);
    await signIn(page, account);
    await expect(page).toHaveURL(/\/dashboard/);
  });

  test('a mistyped password followed by the right one signs in', async ({ page }) => {
    const account = await register(page);
    await signOut(page, account);
    await signIn(page, account, 'WrongPassword1!');
    await expect(page.getByRole('alert')).toBeVisible();
    await page.locator('#password').fill(PASSWORD);
    await page.locator('button[type=submit]').click();
    await expect(page).toHaveURL(/\/dashboard/);
  });
});

test.describe('sign-out', () => {
  test('clears the session and revokes the refresh token on the server', async ({ page, request }) => {
    const account = await register(page);
    const stored = await page.evaluate(() => JSON.parse(localStorage.getItem('saas.session.v2') ?? 'null'));
    expect(stored?.refreshToken).toBeTruthy();

    await signOut(page, account);
    expect(await page.evaluate(() => localStorage.getItem('saas.session.v2'))).toBeNull();

    const reuse = await request.post('http://localhost:5000/api/auth/refresh', { data: { refreshToken: stored.refreshToken } });
    expect(reuse.status()).toBe(401);
  });
});
