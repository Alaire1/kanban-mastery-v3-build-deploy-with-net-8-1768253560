export const ROUTES = {
  LOGIN: '/',
  LOGIN_ALIAS: '/login',
  REGISTER: '/register',
  DASHBOARD: '/dashboard',
  BOARD_DETAIL: '/board/:boardId',
};

export const getBoardDetailPath = (boardId) => `/board/${boardId}`;
