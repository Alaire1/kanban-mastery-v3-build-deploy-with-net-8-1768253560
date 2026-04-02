import { test, expect } from '@playwright/test';

// ─── Helpers ───────────────────────────────────────────────────────────────
const API_URL = 'http://localhost:5000';
const SEEDED_EMAIL = 'playwright.seed.user@test.com';
const SEEDED_PASSWORD = 'Playwright1!';
const SEEDED_BOARD_NAMES = ['Playwright Board One', 'Playwright Board Two'];
const USER_ONE_EMAIL = 'playwright.user.one@test.com';
const USER_ONE_PASSWORD = 'PlayUserOne1!';
const USER_TWO_EMAIL = 'playwright.user.two@test.com';
const USER_TWO_PASSWORD = 'PlayUserTwo1!';

async function clearAuth(page) {
  await page.goto('/');
  await page.evaluate(() => localStorage.clear());
}

async function login(page, email = SEEDED_EMAIL, password = SEEDED_PASSWORD) {
  await page.goto('/');
  await page.fill('[type=email]', email);
  await page.fill('[type=password]', password);
  await page.click('[type=submit]');
  await page.waitForURL('**/dashboard');
}

async function registerAndLogin(request, email = SEEDED_EMAIL, password = SEEDED_PASSWORD) {
  const registerRes = await request.post(`${API_URL}/register`, {
    data: { email, password },
  });

  expect([200, 400]).toContain(registerRes.status());

  const loginRes = await request.post(`${API_URL}/login`, {
    data: { email, password },
  });

  expect(loginRes.ok()).toBeTruthy();
  return await loginRes.json();
}

async function seedBoardsForUser(request, token, boardNames = SEEDED_BOARD_NAMES) {
  for (const boardName of boardNames) {
    await request.post(`${API_URL}/api/boards`, {
      headers: {
        Authorization: `Bearer ${token}`,
      },
      data: { boardName },
    });
  }
}

async function createBoard(request, token, boardName) {
  const response = await request.post(`${API_URL}/api/boards`, {
    headers: {
      Authorization: `Bearer ${token}`,
    },
    data: { boardName },
  });

  expect(response.status()).toBe(201);
  return await response.json();
}

async function getCurrentUser(request, token) {
  const response = await request.get(`${API_URL}/api/users/me`, {
    headers: {
      Authorization: `Bearer ${token}`,
    },
  });

  expect(response.ok()).toBeTruthy();
  return await response.json();
}

async function addMemberToBoard(request, token, boardId, userId) {
  const response = await request.post(`${API_URL}/api/boards/${boardId}/members`, {
    headers: {
      Authorization: `Bearer ${token}`,
    },
    data: { userId },
  });

  expect(response.status()).toBe(201);
}

function uniqueEmail(prefix = 'e2e') {
  return `${prefix}_${Date.now()}_${Math.floor(Math.random() * 10000)}@test.com`;
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
    await page.fill('[type=email]', uniqueEmail('wrong'));
    await page.fill('[type=password]', 'Wrongpassword1!');
    await page.click('[type=submit]');
    await page.waitForSelector('[data-testid="api-error"]', { timeout: 10000 });
  });

  test('redirects to dashboard after successful login', async ({ page, request }) => {
    await registerAndLogin(request, SEEDED_EMAIL, SEEDED_PASSWORD);
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

  test('shows error on duplicate email', async ({ page, request }) => {
    const duplicateEmail = uniqueEmail('dup');
    await registerAndLogin(request, duplicateEmail, SEEDED_PASSWORD);

    await page.goto('/register');
    await page.fill('[name=name]', 'Test User');
    await page.fill('[name=email]', duplicateEmail);
    await page.fill('[name=password]', SEEDED_PASSWORD);
    await page.fill('[name=confirmPassword]', SEEDED_PASSWORD);
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

// ─── Dashboard: Boards Fetch ──────────────────────────────────────────────
test.describe('Dashboard', () => {
  test.beforeEach(async ({ page }) => clearAuth(page));

  test('fetches boards and renders them as clickable cards', async ({ page, request }) => {
    const auth = await registerAndLogin(request, SEEDED_EMAIL, SEEDED_PASSWORD);
    await seedBoardsForUser(request, auth.accessToken);

    await login(page);

    await expect(page.locator('text=Loading boards…')).toBeVisible({ timeout: 10000 });
    await expect(page.locator('text=Loading boards…')).not.toBeVisible();

    await expect(page.locator(`text=${SEEDED_BOARD_NAMES[0]}`)).toBeVisible();
    await expect(page.locator(`text=${SEEDED_BOARD_NAMES[1]}`)).toBeVisible();

    await page.locator(`button:has-text("${SEEDED_BOARD_NAMES[0]}")`).first().click();
    await expect(page).toHaveURL(/\/board\/\d+$/);
  });

  test('shows empty state when user has no boards', async ({ page, request }) => {
    const emptyEmail = uniqueEmail('empty');
    await registerAndLogin(request, emptyEmail, SEEDED_PASSWORD);

    await login(page, emptyEmail, SEEDED_PASSWORD);

    await expect(page.locator('text=Loading boards…')).not.toBeVisible();
    await expect(page.locator('text=No boards yet')).toBeVisible();
    await expect(page.locator('text=Create your first board to get started.')).toBeVisible();
  });

  test('two users create boards and add each other; both dashboards show all boards', async ({ page, request }) => {
    const authOne = await registerAndLogin(request, USER_ONE_EMAIL, USER_ONE_PASSWORD);
    const authTwo = await registerAndLogin(request, USER_TWO_EMAIL, USER_TWO_PASSWORD);

    const userOne = await getCurrentUser(request, authOne.accessToken);
    const userTwo = await getCurrentUser(request, authTwo.accessToken);

    const userTwoBoardA = await createBoard(request, authTwo.accessToken, 'User2 Board A');
    const userTwoBoardB = await createBoard(request, authTwo.accessToken, 'User2 Board B');
    await addMemberToBoard(request, authTwo.accessToken, userTwoBoardA.id, userOne.id);
    await addMemberToBoard(request, authTwo.accessToken, userTwoBoardB.id, userOne.id);

    const userOneBoardA = await createBoard(request, authOne.accessToken, 'User1 Board A');
    const userOneBoardB = await createBoard(request, authOne.accessToken, 'User1 Board B');
    await addMemberToBoard(request, authOne.accessToken, userOneBoardA.id, userTwo.id);
    await addMemberToBoard(request, authOne.accessToken, userOneBoardB.id, userTwo.id);

    await login(page, USER_ONE_EMAIL, USER_ONE_PASSWORD);
    await expect(page.locator('text=User1 Board A')).toBeVisible();
    await expect(page.locator('text=User1 Board B')).toBeVisible();
    await expect(page.locator('text=User2 Board A')).toBeVisible();
    await expect(page.locator('text=User2 Board B')).toBeVisible();

    await page.click('button:has-text("Log out")');

    await login(page, USER_TWO_EMAIL, USER_TWO_PASSWORD);
    await expect(page.locator('text=User2 Board A')).toBeVisible();
    await expect(page.locator('text=User2 Board B')).toBeVisible();
    await expect(page.locator('text=User1 Board A')).toBeVisible();
    await expect(page.locator('text=User1 Board B')).toBeVisible();
  });
});