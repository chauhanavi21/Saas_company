import { expect, test, type Page } from '@playwright/test';
import { PASSWORD, addTask, createProjectAndOpen, register, signIn, signOut } from './helpers';

/** Creates an invite on the Team page and returns the one-time link it shows. */
async function inviteTeammate(page: Page, email: string): Promise<string> {
  await page.goto('/team');
  await page.locator('#inv-email').fill(email);
  await page.getByRole('button', { name: 'Create invite' }).click();
  const link = page.getByLabel('Invite link');
  await expect(link).toBeVisible();
  return link.inputValue();
}

test.describe('team and invitations', () => {
  test('the Team page lists the owner as the only member of a new workspace', async ({ page }) => {
    const owner = await register(page);
    await page.goto('/team');
    await expect(page.getByRole('region', { name: 'Members' })).toContainText(owner.fullName);
  });

  test('an invited person joins the same company, and the link cannot be used twice', async ({ page, browser }) => {
    const owner = await register(page);
    const link = await inviteTeammate(page, 'colleague@example.com');
    expect(link).toContain('accept-invite');

    const guest = await (await browser.newContext()).newPage();
    await guest.goto(link);
    await expect(guest.getByRole('heading', { name: new RegExp(`Join ${owner.company}`) })).toBeVisible();
    await guest.locator('#name').fill('Colleague Person');
    await guest.locator('#password').fill(PASSWORD);
    await guest.getByRole('button', { name: 'Join workspace' }).click();
    await expect(guest).toHaveURL(/\/dashboard/);

    // The newcomer is in the owner's workspace, as a member.
    await guest.goto('/team');
    await expect(guest.getByRole('region', { name: 'Members' })).toContainText(owner.fullName);
    await expect(guest.getByRole('region', { name: 'Members' })).toContainText('Colleague Person');

    // The same link is now spent.
    const again = await (await browser.newContext()).newPage();
    await again.goto(link);
    await expect(again.getByRole('heading', { name: 'Invitation unavailable' })).toBeVisible();
    await again.context().close();
    await guest.context().close();
  });

  test('a made-up invitation token shows the unavailable page, not a form', async ({ page }) => {
    await page.goto('/auth/accept-invite?token=this-is-not-a-real-token');
    await expect(page.getByRole('heading', { name: 'Invitation unavailable' })).toBeVisible();
    await expect(page.locator('#name')).toHaveCount(0);
  });

  test('a revoked invite no longer works', async ({ page, browser }) => {
    await register(page);
    const link = await inviteTeammate(page, 'revoked@example.com');

    await page.getByRole('button', { name: 'Revoke' }).first().click();
    await expect(page.getByText('Invite revoked')).toBeVisible();

    const guest = await (await browser.newContext()).newPage();
    await guest.goto(link);
    await expect(guest.getByRole('heading', { name: 'Invitation unavailable' })).toBeVisible();
    await guest.context().close();
  });

  test('a member cannot see or reach the admin-only invite controls', async ({ page, browser }) => {
    await register(page);
    const link = await inviteTeammate(page, 'member@example.com');
    const guest = await (await browser.newContext()).newPage();
    await guest.goto(link);
    await guest.locator('#name').fill('Plain Member');
    await guest.locator('#password').fill(PASSWORD);
    await guest.getByRole('button', { name: 'Join workspace' }).click();
    await expect(guest).toHaveURL(/\/dashboard/);

    await guest.goto('/team');
    await expect(guest.getByRole('region', { name: 'Members' })).toBeVisible();
    await expect(guest.locator('#inv-email')).toHaveCount(0);
    await guest.context().close();
  });
});

test.describe('settings', () => {
  test('the profile shows the current name and email, and a rename is kept after reload', async ({ page }) => {
    const owner = await register(page);
    await page.goto('/settings');
    await expect(page.locator('#s-name')).toHaveValue(owner.fullName);
    await expect(page.locator('#s-email')).toHaveValue(owner.email);
    await expect(page.locator('#s-email')).toBeDisabled();

    await page.locator('#s-name').fill('Renamed Owner');
    await page.getByRole('button', { name: /^Save/ }).first().click();
    await expect(page.getByText(/saved|updated/i).first()).toBeVisible();
    await page.reload();
    await expect(page.locator('#s-name')).toHaveValue('Renamed Owner');
  });

  test('the theme choice sticks across a reload', async ({ page }) => {
    await register(page);
    await page.goto('/settings');
    const dark = page.getByRole('radiogroup', { name: 'Theme' }).getByRole('radio', { name: /dark/i });
    await dark.click();
    await expect(page.locator('html')).toHaveClass(/dark/);
    await page.reload();
    await expect(page.locator('html')).toHaveClass(/dark/);
  });

  test('changing the password: the old one stops working and the new one works', async ({ page, browser }) => {
    const owner = await register(page);
    await page.goto('/settings');
    await page.locator('#p-cur').fill(PASSWORD);
    await page.locator('#p-new').fill('AnotherHorse42!');
    await page.getByRole('button', { name: /Update password/ }).click();
    await expect(page.getByText(/Password updated/)).toBeVisible();

    await signOut(page, owner);
    await signIn(page, owner, PASSWORD);
    await expect(page.getByRole('alert')).toContainText(/invalid email or password/i);
    await signIn(page, owner, 'AnotherHorse42!');
    await expect(page).toHaveURL(/\/dashboard/);
  });

  test('a wrong current password is refused and nothing changes', async ({ page }) => {
    const owner = await register(page);
    await page.goto('/settings');
    await page.locator('#p-cur').fill('NotMyPassword1!');
    await page.locator('#p-new').fill('AnotherHorse42!');
    await page.getByRole('button', { name: /Update password/ }).click();
    await expect(page.getByText(/password updated/i)).toHaveCount(0);

    await signOut(page, owner);
    await signIn(page, owner, PASSWORD);
    await expect(page).toHaveURL(/\/dashboard/);
  });
});

test.describe('dashboard', () => {
  /** The number shown on a stat card, found by its label inside the link that wraps both. */
  const stat = (page: Page, label: string) =>
    page.getByRole('link').filter({ has: page.getByText(label, { exact: true }) }).first();

  test('a new workspace starts at zero and has no error banner', async ({ page }) => {
    await register(page);
    for (const label of ['Active projects', 'Open tasks', 'Overdue']) {
      await expect(stat(page, label)).toContainText('0');
    }
    await expect(page.getByRole('alert')).toHaveCount(0);
  });

  test('the counts follow the work: a project and a task move the numbers', async ({ page }) => {
    await register(page);
    await createProjectAndOpen(page, 'Counted project');
    await addTask(page, 'Counted task');

    await page.goto('/dashboard');
    await expect(stat(page, 'Active projects')).toContainText('1');
    await expect(stat(page, 'Open tasks')).toContainText('1');
    await expect(page.getByRole('heading', { name: 'My open tasks' })).toBeVisible();
  });
});
