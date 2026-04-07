const getInitials = (name) => {
  if (!name || typeof name !== 'string') return '??';

  const parts = name
    .trim()
    .split(/\s+/)
    .filter(Boolean);

  if (parts.length === 0) return '??';
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();

  return `${parts[0][0]}${parts[parts.length - 1][0]}`.toUpperCase();
};

export const normalizeCard = (card) => {
  const displayName = card.assigneeDisplayName ?? card.AssigneeDisplayName ?? null;
  const userName = card.assigneeUserName ?? card.AssigneeUserName ?? null;
  const assignedUserId = card.assignedUserId ?? card.AssignedUserId ?? null;
  const avatarUrl =
    card.assigneeAvatarUrl ??
    card.AssigneeAvatarUrl ??
    card.assigneeProfilePictureUrl ??
    card.AssigneeProfilePictureUrl ??
    card.assigneePhotoUrl ??
    card.AssigneePhotoUrl ??
    null;

  const fallbackAssigneeName = displayName || userName || 'Unassigned';

  return {
    id: card.id ?? card.Id,
    title: card.title ?? card.Title ?? 'Untitled card',
    description: card.description ?? card.Description ?? 'No description provided.',
    assignedUserId,
    assigneeDisplayName: displayName,
    assigneeUserName: userName,
    assigneeAvatarUrl: avatarUrl,
    assigneeFallbackName: fallbackAssigneeName,
    assigneeInitials: getInitials(fallbackAssigneeName),
    isAssigned: Boolean(assignedUserId),
  };
};

export const getCardDragId = (cardId) => `card-${cardId}`;
export const getColumnDropId = (columnId) => `column-${columnId}`;

export const parseCardIdFromDragId = (dragId) => {
  if (typeof dragId !== 'string' || !dragId.startsWith('card-')) return null;
  const parsed = Number(dragId.slice(5));
  return Number.isNaN(parsed) ? null : parsed;
};

export const parseColumnIdFromDropId = (dropId) => {
  if (typeof dropId !== 'string' || !dropId.startsWith('column-')) return null;
  const parsed = Number(dropId.slice(7));
  return Number.isNaN(parsed) ? null : parsed;
};

export const findCardLocation = (allColumns, cardId) => {
  for (const column of allColumns) {
    if (column.cards.some((card) => card.id === cardId)) {
      return column;
    }
  }

  return null;
};

export const moveCardToAnotherColumn = (allColumns, cardId, targetColumnId) => {
  let movingCard = null;
  let sourceColumnId = null;

  const withoutCard = allColumns.map((column) => {
    const cardIndex = column.cards.findIndex((card) => card.id === cardId);
    if (cardIndex === -1) return column;

    sourceColumnId = column.id;
    movingCard = column.cards[cardIndex];

    return {
      ...column,
      cards: [...column.cards.slice(0, cardIndex), ...column.cards.slice(cardIndex + 1)],
    };
  });

  if (!movingCard || sourceColumnId === null || sourceColumnId === targetColumnId) {
    return { nextColumns: allColumns, moved: false, sourceColumnId };
  }

  const targetExists = withoutCard.some((column) => column.id === targetColumnId);
  if (!targetExists) {
    return { nextColumns: allColumns, moved: false, sourceColumnId };
  }

  const nextColumns = withoutCard.map((column) =>
    column.id === targetColumnId
      ? {
          ...column,
          cards: [...column.cards, { ...movingCard, columnId: targetColumnId }],
        }
      : column
  );

  return { nextColumns, moved: true, sourceColumnId };
};
