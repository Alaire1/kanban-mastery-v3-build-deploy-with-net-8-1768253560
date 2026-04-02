import { test, expect } from '@playwright/test';

// ─── Helpers ───────────────────────────────────────────────────────────────
async function clearAuth(page) {
  await page.goto('/');
  await page.evaluate(() => localStorage.clear());
}

async function login(page) {
  await page.goto('/');
  await page.fill('[type=email]', 'test111@gmail.com');     // 👈 your real credentials
  await page.fill('[type=password]', 'tesT111!');  // 👈 your real credentials
  await page.click('[type=submit]');
  await page.waitForURL('**/dashboard');
}

// ─── Auth: Login ───────────────────────────────────────────────────────────
test.describe('Login', () => {
  test.beforeEach(async ({ page }) => clearAuth(page));

  test('redirects to login when no token', async ({ page }) => {
    await page.goto('/dashboard');
    await expect(page).toHaveURL('http://localhost:3000/');
  });

  test('shows error on wrong credentials', async ({ page }) => {
    await page.goto('/');
    await page.fill('[type=email]', 'test11@gmail.com');
    await page.fill('[type=password]', 'Wrongpassword1!');
    await page.click('[type=submit]');
    await page.waitForSelector('[data-testid="api-error"]', { timeout: 10000 });
  });

  test('redirects to dashboard after successful login', async ({ page }) => {
    await login(page);
    await expect(page).toHaveURL('http://localhost:3000/dashboard');
  });
});



// ─── Auth: Register ────────────────────────────────────────────────────────
test.describe('Register', () => {
  test.beforeEach(async ({ page }) => clearAuth(page));

  test('shows validation error on empty form submit', async ({ page }) => {
    await page.goto('/register');
    await page.click('[type=submit]');
    await expect(page.locator('p.text-red-400').first()).toBeVisible();
  });

  test('shows error when passwords do not match', async ({ page }) => {
    await page.goto('/register');
    await page.fill('[name=name]', 'Test User');
    await page.fill('[name=email]', 'test@test.com');
    await page.fill('[name=password]', 'Password1!');
    await page.fill('[name=confirmPassword]', 'Password2!');
    await page.click('[type=submit]');
    await expect(page.locator('p.text-red-400').first()).toBeVisible();
  });

  test('shows error on duplicate email', async ({ page }) => {
    await page.goto('/register');
    await page.fill('[name=name]', 'Test User');
    await page.fill('[name=email]', 'real@email.com'); // 👈 already registered email
    await page.fill('[name=password]', 'Password1!');
    await page.fill('[name=confirmPassword]', 'Password1!');
    await page.click('[type=submit]');
    await page.waitForSelector('[data-testid="api-error"]', { timeout: 10000 });
  });

  test('redirects to login after successful registration', async ({ page }) => {
    const unique = `testuser_${Date.now()}@test.com`; // unique email each run
    await page.goto('/register');
    await page.fill('[name=name]', 'Test User');
    await page.fill('[name=email]', unique);
    await page.fill('[name=password]', 'Password1!');
    await page.fill('[name=confirmPassword]', 'Password1!');
    await page.click('[type=submit]');
    await expect(page).toHaveURL('http://localhost:3000/');
  });
});