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
const USER_ONE_BOARD_NAMES = ['Playwright User One Board'];
const USER_TWO_BOARD_NAMES = ['Playwright User Two Board'];

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

async function getBoardById(request, token, boardId) {
  const response = await request.get(`${API_URL}/api/boards/${boardId}/`, {
    headers: {
      Authorization: `Bearer ${token}`,
    },
  });

  expect(response.ok()).toBeTruthy();
  return await response.json();
}

async function createCard(request, token, boardId, columnId, title, description = '') {
  const response = await request.post(`${API_URL}/api/boards/${boardId}/cards`, {
    headers: {
      Authorization: `Bearer ${token}`,
    },
    data: {
      title,
      description,
      columnId,
    },
  });

  expect(response.status()).toBe(201);
  return await response.json();
}

async function deleteCard(request, token, boardId, cardId) {
  const response = await request.delete(`${API_URL}/api/boards/${boardId}/cards/${cardId}`, {
    headers: {
      Authorization: `Bearer ${token}`,
    },
  });

  expect([204, 404]).toContain(response.status());
}

async function deleteBoard(request, token, boardId) {
  const response = await request.delete(`${API_URL}/api/boards/${boardId}/`, {
    headers: {
      Authorization: `Bearer ${token}`,
    },
  });

  expect([204, 403, 404]).toContain(response.status());
}

async function assignCardToUser(request, token, boardId, cardId, userId) {
  const response = await request.put(`${API_URL}/api/boards/${boardId}/cards/${cardId}/assign`, {
    headers: {
      Authorization: `Bearer ${token}`,
    },
    data: {
      userId,
    },
  });

  expect(response.status()).toBe(200);
  return await response.json();
}

async function seedCardsInBoard(request, token, boardId, cardTitles) {
  const boardDetail = await getBoardById(request, token, boardId);
  const columns = Array.isArray(boardDetail.columns ?? boardDetail.Columns)
    ? (boardDetail.columns ?? boardDetail.Columns)
    : [];

  expect(columns.length).toBeGreaterThan(0);

  const firstColumnId = columns[0].id ?? columns[0].Id;

  for (let i = 0; i < cardTitles.length; i += 1) {
    await createCard(request, token, boardId, firstColumnId, cardTitles[i], 'Seeded by playwright');
  }
}

async function resetBoardCardsToFirstColumn(request, token, boardId, cardPrefix) {
  const boardDetail = await getBoardById(request, token, boardId);
  const columns = Array.isArray(boardDetail.columns ?? boardDetail.Columns)
    ? (boardDetail.columns ?? boardDetail.Columns)
    : [];

  expect(columns.length).toBeGreaterThan(0);

  const firstColumnId = columns[0].id ?? columns[0].Id;

  for (const column of columns) {
    const cards = Array.isArray(column.cards ?? column.Cards)
      ? (column.cards ?? column.Cards)
      : [];

    for (const card of cards) {
      const cardId = card.id ?? card.Id;
      if (cardId) {
        await deleteCard(request, token, boardId, cardId);
      }
    }
  }

  await createCard(request, token, boardId, firstColumnId, `${cardPrefix} Card 1`, 'Seeded by playwright');
  await createCard(request, token, boardId, firstColumnId, `${cardPrefix} Card 2`, 'Seeded by playwright');
  await createCard(request, token, boardId, firstColumnId, `${cardPrefix} Card 3`, 'Seeded by playwright');
}

async function enforceOnlyNamedBoardsWithStackedCards(request, token, desiredBoardNames, cardPrefix) {
  const me = await getCurrentUser(request, token);
  const boards = Array.isArray(me.boards) ? me.boards : [];

  for (const board of boards) {
    const boardName = board.name ?? board.Name;
    const boardId = board.id ?? board.Id;

    if (!boardId || desiredBoardNames.includes(boardName)) {
      continue;
    }

    await deleteBoard(request, token, boardId);
  }

  for (const boardName of desiredBoardNames) {
    const refreshed = await getCurrentUser(request, token);
    const refreshedBoards = Array.isArray(refreshed.boards) ? refreshed.boards : [];

    const existing = refreshedBoards.find((b) => (b.name ?? b.Name) === boardName);
    const board = existing ?? await createBoard(request, token, boardName);
    const boardId = board.id ?? board.Id;
    await resetBoardCardsToFirstColumn(request, token, boardId, boardName.replace('Playwright ', ''));
  }
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

  test('seeds only fixed boards for 3 users with 3 cards in first column', async ({ request }) => {
    const seedAuth = await registerAndLogin(request, SEEDED_EMAIL, SEEDED_PASSWORD);
    const userOneAuth = await registerAndLogin(request, USER_ONE_EMAIL, USER_ONE_PASSWORD);
    const userTwoAuth = await registerAndLogin(request, USER_TWO_EMAIL, USER_TWO_PASSWORD);

    await enforceOnlyNamedBoardsWithStackedCards(
      request,
      seedAuth.accessToken,
      SEEDED_BOARD_NAMES,
      'Seed User'
    );

    await enforceOnlyNamedBoardsWithStackedCards(
      request,
      userOneAuth.accessToken,
      USER_ONE_BOARD_NAMES,
      'User One'
    );

    await enforceOnlyNamedBoardsWithStackedCards(
      request,
      userTwoAuth.accessToken,
      USER_TWO_BOARD_NAMES,
      'User Two'
    );
  });

  test('fetches boards and renders them as clickable cards', async ({ page, request }) => {
    const auth = await registerAndLogin(request, SEEDED_EMAIL, SEEDED_PASSWORD);
    await seedBoardsForUser(request, auth.accessToken);

    const seededBoards = [];
    for (const boardName of SEEDED_BOARD_NAMES) {
      seededBoards.push(await createBoard(request, auth.accessToken, `${boardName} Cards ${Date.now()}`));
    }

    for (const board of seededBoards) {
      await seedCardsInBoard(request, auth.accessToken, board.id, [
        `SeedUser Card One ${Date.now()}`,
        `SeedUser Card Two ${Date.now()}`,
        `SeedUser Card Three ${Date.now()}`,
      ]);
    }

    await login(page);

    await expect(page.locator('text=Loading boards…')).toBeVisible({ timeout: 10000 });
    await expect(page.locator('text=Loading boards…')).not.toBeVisible();

    await expect(page.locator(`text=${SEEDED_BOARD_NAMES[0]}`).first()).toBeVisible();
    await expect(page.locator(`text=${SEEDED_BOARD_NAMES[1]}`).first()).toBeVisible();

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

    await seedCardsInBoard(request, authTwo.accessToken, userTwoBoardA.id, ['User2 Card One', 'User2 Card Two']);
    await seedCardsInBoard(request, authTwo.accessToken, userTwoBoardB.id, ['User2 Card Three']);

    await addMemberToBoard(request, authTwo.accessToken, userTwoBoardA.id, userOne.id);
    await addMemberToBoard(request, authTwo.accessToken, userTwoBoardB.id, userOne.id);

    const userOneBoardA = await createBoard(request, authOne.accessToken, 'User1 Board A');
    const userOneBoardB = await createBoard(request, authOne.accessToken, 'User1 Board B');
    await addMemberToBoard(request, authOne.accessToken, userOneBoardA.id, userTwo.id);
    await addMemberToBoard(request, authOne.accessToken, userOneBoardB.id, userTwo.id);

    await login(page, USER_ONE_EMAIL, USER_ONE_PASSWORD);
    await expect(page.locator('text=User1 Board A').first()).toBeVisible();
    await expect(page.locator('text=User1 Board B').first()).toBeVisible();
    await expect(page.locator('text=User2 Board A').first()).toBeVisible();
    await expect(page.locator('text=User2 Board B').first()).toBeVisible();

    await page.click('button:has-text("Log out")');

    await login(page, USER_TWO_EMAIL, USER_TWO_PASSWORD);
    await expect(page.locator('text=User2 Board A').first()).toBeVisible();
    await expect(page.locator('text=User2 Board B').first()).toBeVisible();
    await expect(page.locator('text=User1 Board A').first()).toBeVisible();
    await expect(page.locator('text=User1 Board B').first()).toBeVisible();
  });

  test('renders seeded cards on board detail page', async ({ page, request }) => {
    const email = uniqueEmail('cards');
    const password = 'Password1!';
    const auth = await registerAndLogin(request, email, password);

    const boardName = `Cards Board ${Date.now()}`;
    const board = await createBoard(request, auth.accessToken, boardName);
    const boardDetail = await getBoardById(request, auth.accessToken, board.id);
    const columns = Array.isArray(boardDetail.columns ?? boardDetail.Columns)
      ? (boardDetail.columns ?? boardDetail.Columns)
      : [];

    expect(columns.length).toBeGreaterThan(0);

    const firstColumnId = columns[0].id ?? columns[0].Id;
    const secondColumn = columns[1] ?? columns[0];
    const secondColumnId = secondColumn.id ?? secondColumn.Id;

    const cardTitleOne = `Seed Card One ${Date.now()}`;
    const cardTitleTwo = `Seed Card Two ${Date.now()}`;

    await createCard(request, auth.accessToken, board.id, firstColumnId, cardTitleOne, 'Seeded by e2e');
    await createCard(request, auth.accessToken, board.id, secondColumnId, cardTitleTwo, 'Seeded by e2e');

    await login(page, email, password);
    await page.locator(`button:has-text("${boardName}")`).first().click();

    await expect(page).toHaveURL(new RegExp(`/board/${board.id}$`));
    await expect(page.locator(`text=${cardTitleOne}`)).toBeVisible();
    await expect(page.locator(`text=${cardTitleTwo}`)).toBeVisible();
  });

  test('shows card descriptions, assignment state colors, and assignee hover details', async ({ page, request }) => {
    const ownerEmail = uniqueEmail('owner_cards');
    const memberEmail = uniqueEmail('member_cards');
    const password = 'Password1!';

    const ownerAuth = await registerAndLogin(request, ownerEmail, password);
    const memberAuth = await registerAndLogin(request, memberEmail, password);
    const memberProfile = await getCurrentUser(request, memberAuth.accessToken);

    const boardName = `Assignment Board ${Date.now()}`;
    const board = await createBoard(request, ownerAuth.accessToken, boardName);
    await addMemberToBoard(request, ownerAuth.accessToken, board.id, memberProfile.id);

    const boardDetail = await getBoardById(request, ownerAuth.accessToken, board.id);
    const columns = Array.isArray(boardDetail.columns ?? boardDetail.Columns)
      ? (boardDetail.columns ?? boardDetail.Columns)
      : [];
    const firstColumnId = columns[0].id ?? columns[0].Id;

    const assignedTitle = `Assigned Card ${Date.now()}`;
    const unassignedTitle = `Unassigned Card ${Date.now()}`;
    const assignedDescription = 'Assigned description seeded in e2e';
    const unassignedDescription = 'Unassigned description seeded in e2e';

    const assignedCard = await createCard(
      request,
      ownerAuth.accessToken,
      board.id,
      firstColumnId,
      assignedTitle,
      assignedDescription
    );

    await createCard(
      request,
      ownerAuth.accessToken,
      board.id,
      firstColumnId,
      unassignedTitle,
      unassignedDescription
    );

    await assignCardToUser(request, ownerAuth.accessToken, board.id, assignedCard.id, memberProfile.id);

    await login(page, ownerEmail, password);
    await page.locator(`button:has-text("${boardName}")`).first().click();

    await expect(page).toHaveURL(new RegExp(`/board/${board.id}$`));
    await expect(page.getByRole('heading', { name: assignedTitle, exact: true })).toBeVisible();
    await expect(page.getByRole('heading', { name: unassignedTitle, exact: true })).toBeVisible();

    await expect(page.locator('[data-testid="board-card"][data-assigned="assigned"]')).toHaveCount(1);
    await expect(page.locator('[data-testid="board-card"][data-assigned="unassigned"]')).toHaveCount(1);

    const assignedCardNode = page
      .locator('[data-testid="board-card"][data-assigned="assigned"]')
      .filter({ hasText: assignedTitle });
    const unassignedCardNode = page
      .locator('[data-testid="board-card"][data-assigned="unassigned"]')
      .filter({ hasText: unassignedTitle });

    await expect(assignedCardNode.getByTestId('card-description')).toBeVisible();
    await expect(assignedCardNode.getByTestId('card-description')).toContainText(/.+/);
    await expect(unassignedCardNode.getByTestId('card-description')).toBeVisible();
    await expect(unassignedCardNode.getByTestId('card-description')).toContainText(/.+/);

    const assignedAvatar = assignedCardNode.getByTestId('card-assignee-avatar');
    await assignedAvatar.hover();
    await expect(assignedCardNode.getByTestId('card-assignee-tooltip')).toContainText(memberProfile.id);
  });
});