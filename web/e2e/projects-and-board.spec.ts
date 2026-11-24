import { expect, test } from '@playwright/test';
import { addTask, createProjectAndOpen, register } from './helpers';

test.describe('projects', () => {
  test('a new workspace shows the empty state, then a created project, and search narrows the list', async ({ page }) => {
    await register(page);
    await page.goto('/projects');
    await expect(page.getByText('No projects yet')).toBeVisible();

    await page.getByRole('button', { name: 'New project' }).first().click();
    await page.locator('#pname').fill('Website redesign');
    await page.getByRole('button', { name: 'Create project' }).click();
    await expect(page.getByRole('heading', { name: 'Website redesign' })).toBeVisible();

    await page.locator('input[type=search]').fill('zzzz');
    await expect(page.getByText('No projects match your search')).toBeVisible();
    await page.locator('input[type=search]').fill('');
    await expect(page.getByRole('heading', { name: 'Website redesign' })).toBeVisible();
  });

  test("another company's projects are never visible", async ({ page, browser }) => {
    await register(page);
    await createProjectAndOpen(page, 'Private to the first company');

    const other = await (await browser.newContext()).newPage();
    await register(other);
    await other.goto('/projects');
    await expect(other.getByText('No projects yet')).toBeVisible();
    await expect(other.getByText('Private to the first company')).toHaveCount(0);
    await other.context().close();
  });
});

test.describe('board', () => {
  test('a task added with Enter appears in a column and clears the field', async ({ page }) => {
    await register(page);
    await createProjectAndOpen(page);
    await addTask(page, 'Draft the homepage');
    await expect(page.getByLabel('New task title')).toHaveValue('');
  });

  test('dragging a task to another column moves it, and the move survives a reload', async ({ page }) => {
    await register(page);
    await createProjectAndOpen(page);
    await addTask(page, 'Move me');

    const columns = page.locator("[aria-label$=' column']");
    // The board moves the card at once and rolls it back if the server refuses, so what is on screen
    // straight after the drop is only a promise. Wait for the server's answer before checking the result
    // or reloading; a reload sooner would abort the request and lose the move.
    const stored = page.waitForResponse((r) => /\/tasks\/[^/]+$/.test(r.url()) && r.request().method() === 'PUT');
    await columns.nth(0).locator('[draggable=true]').first().dragTo(columns.nth(1));
    expect((await stored).ok()).toBe(true);
    await expect(columns.nth(1).locator('[draggable=true]')).toHaveCount(1);
    await expect(columns.nth(0).locator('[draggable=true]')).toHaveCount(0);

    await page.reload();
    await expect(columns.nth(1).locator('[draggable=true]')).toHaveCount(1);
  });

  test('the task drawer takes a comment, Save stores the edit and closes it, and both are there on reopening', async ({ page }) => {
    await register(page);
    await createProjectAndOpen(page);
    await addTask(page, 'Write the brief');

    const open = () => page.locator('[draggable=true]').first().locator('button').first().click();
    await open();
    await page.locator('#d-desc').fill('Hero, features, footer.');
    await page.getByLabel('Write a comment').fill('Looks good');
    await page.getByRole('button', { name: 'Post' }).click();
    await expect(page.getByRole('dialog').getByText('Looks good')).toBeVisible();

    // Save closes the drawer by design.
    await page.getByRole('button', { name: /^Save/ }).click();
    await expect(page.getByRole('dialog')).toHaveCount(0);

    await open();
    await expect(page.locator('#d-desc')).toHaveValue('Hero, features, footer.');
    await expect(page.getByRole('dialog').getByText('Looks good')).toBeVisible();
    await page.getByRole('button', { name: 'Close', exact: true }).click();
    await expect(page.getByRole('dialog')).toHaveCount(0);
  });

  test('filtering by text hides tasks that do not match', async ({ page }) => {
    await register(page);
    await createProjectAndOpen(page);
    await addTask(page, 'Homepage copy');
    await addTask(page, 'Pricing table');

    await page.getByLabel('Filter tasks').fill('homepage');
    await expect(page.locator('[draggable=true]')).toHaveCount(1);
    await page.getByLabel('Filter tasks').fill('nothing-matches-this');
    await expect(page.locator('[draggable=true]')).toHaveCount(0);
  });
});

// Regression: a task created with priority Low used to be stored as Medium (Low is the enum's zero value
// and the column also had a database default). The API tests cover it directly; this checks it end to end.
//
// The title must not contain the word "Low": the card shows the title and the priority badge side by
// side, and an earlier version of this test passed against the bug by matching its own title.
test.describe('regression: task priority', () => {
  test('each priority chosen when adding a task is the one shown after a reload', async ({ page }) => {
    await register(page);
    await createProjectAndOpen(page);
    for (const [title, priority] of [['Alpha job', 'Low'], ['Bravo job', 'Medium'], ['Charlie job', 'High']] as const) {
      await page.getByLabel('Priority', { exact: true }).selectOption(priority);
      await addTask(page, title);
    }

    await page.reload();
    for (const [title, priority] of [['Alpha job', 'Low'], ['Bravo job', 'Medium'], ['Charlie job', 'High']] as const) {
      const card = page.locator('[draggable=true]').filter({ hasText: title });
      // The badge is the only element on the card with the priority as its whole text.
      await expect(card.getByText(priority, { exact: true })).toBeVisible();
    }
    await expect(page.locator('[draggable=true]').filter({ hasText: 'Alpha job' }).getByText('Medium', { exact: true })).toHaveCount(0);
  });
});
