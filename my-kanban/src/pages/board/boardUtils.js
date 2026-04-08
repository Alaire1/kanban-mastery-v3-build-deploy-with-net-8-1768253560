import api from '../../services/api';

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

const toAbsoluteApiUrl = (value) => {
  if (!value || typeof value !== 'string') return null;
  if (/^https?:\/\//i.test(value)) return value;

  try {
    return new URL(value, api.defaults.baseURL).toString();
  } catch {
    return value;
  }
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
    assigneeAvatarUrl: toAbsoluteApiUrl(avatarUrl),
    assigneeFallbackName: fallbackAssigneeName,
    assigneeInitials: getInitials(fallbackAssigneeName),
    isAssigned: Boolean(assignedUserId),
  };
};

export const getCardDragId = (cardId) => `card-${cardId}`;
export const getColumnDropId = (columnId) => `column-${columnId}`;
export const getColumnEndDropId = (columnId) => `column-end-${columnId}`;
export const getColumnDragId = (columnId) => `column-drag-${columnId}`;

export const parseCardIdFromDragId = (dragId) => {
  if (typeof dragId !== 'string' || !dragId.startsWith('card-')) return null;
  const parsed = Number(dragId.slice(5));
  return Number.isNaN(parsed) ? null : parsed;
};

export const parseColumnIdFromDragId = (dragId) => {
  if (typeof dragId !== 'string' || !dragId.startsWith('column-drag-')) return null;
  const parsed = Number(dragId.slice(12));
  return Number.isNaN(parsed) ? null : parsed;
};

export const parseColumnIdFromDropId = (dropId) => {
  if (typeof dropId !== 'string' || !dropId.startsWith('column-')) return null;
  const parsed = Number(dropId.slice(7));
  return Number.isNaN(parsed) ? null : parsed;
};

export const parseColumnIdFromEndDropId = (dropId) => {
  if (typeof dropId !== 'string' || !dropId.startsWith('column-end-')) return null;
  const parsed = Number(dropId.slice(11));
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

export const moveCardToColumnEnd = (allColumns, cardId, targetColumnId) => {
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

  if (!movingCard || sourceColumnId === null) {
    return { nextColumns: allColumns, moved: false, sourceColumnId };
  }

  const targetExists = withoutCard.some((column) => column.id === targetColumnId);
  if (!targetExists) {
    return { nextColumns: allColumns, moved: false, sourceColumnId };
  }

  const targetColumnWithoutCard = withoutCard.find((column) => column.id === targetColumnId);
  const sourceColumnOriginal = allColumns.find((column) => column.id === sourceColumnId);

  const wasAlreadyLastInSameColumn = sourceColumnId === targetColumnId
    && sourceColumnOriginal
    && sourceColumnOriginal.cards.length > 0
    && sourceColumnOriginal.cards[sourceColumnOriginal.cards.length - 1].id === cardId;

  if (wasAlreadyLastInSameColumn) {
    return { nextColumns: allColumns, moved: false, sourceColumnId };
  }

  const nextColumns = withoutCard.map((column) =>
    column.id === targetColumnId
      ? {
          ...column,
          cards: [...(targetColumnWithoutCard?.cards ?? []), { ...movingCard, columnId: targetColumnId }],
        }
      : column
  );

  return { nextColumns, moved: true, sourceColumnId };
};

export const moveCardToCardPosition = (allColumns, draggedCardId, targetCardId) => {
  if (draggedCardId === targetCardId) {
    return { nextColumns: allColumns, moved: false, sourceColumnId: null, targetColumnId: null };
  }

  let draggedCard = null;
  let sourceColumnId = null;
  let targetColumnId = null;
  let targetIndex = -1;

  allColumns.forEach((column) => {
    const draggedIndex = column.cards.findIndex((card) => card.id === draggedCardId);
    if (draggedIndex !== -1) {
      draggedCard = column.cards[draggedIndex];
      sourceColumnId = column.id;
    }

    const foundTargetIndex = column.cards.findIndex((card) => card.id === targetCardId);
    if (foundTargetIndex !== -1) {
      targetColumnId = column.id;
      targetIndex = foundTargetIndex;
    }
  });

  if (!draggedCard || sourceColumnId === null || targetColumnId === null || targetIndex === -1) {
    return { nextColumns: allColumns, moved: false, sourceColumnId, targetColumnId };
  }

  const withoutDraggedCard = allColumns.map((column) => {
    if (column.id !== sourceColumnId) return column;

    const draggedIndex = column.cards.findIndex((card) => card.id === draggedCardId);
    if (draggedIndex === -1) return column;

    return {
      ...column,
      cards: [...column.cards.slice(0, draggedIndex), ...column.cards.slice(draggedIndex + 1)],
    };
  });

  const nextColumns = withoutDraggedCard.map((column) => {
    if (column.id !== targetColumnId) return column;

    let insertionIndex = targetIndex;

    if (sourceColumnId === targetColumnId) {
      const sourceIndexInOriginal = allColumns
        .find((c) => c.id === sourceColumnId)
        ?.cards.findIndex((card) => card.id === draggedCardId) ?? -1;

      if (sourceIndexInOriginal !== -1 && sourceIndexInOriginal < targetIndex) {
        insertionIndex = Math.max(0, targetIndex - 1);
      }
    }

    return {
      ...column,
      cards: [
        ...column.cards.slice(0, insertionIndex),
        { ...draggedCard, columnId: targetColumnId },
        ...column.cards.slice(insertionIndex),
      ],
    };
  });

  return {
    nextColumns,
    moved: true,
    sourceColumnId,
    targetColumnId,
  };
};

export const moveColumnToTargetColumn = (allColumns, draggedColumnId, targetColumnId) => {
  const sourceIndex = allColumns.findIndex((column) => column.id === draggedColumnId);
  const targetIndex = allColumns.findIndex((column) => column.id === targetColumnId);

  if (sourceIndex === -1 || targetIndex === -1 || sourceIndex === targetIndex) {
    return { nextColumns: allColumns, moved: false };
  }

  const nextColumns = [...allColumns];
  const [draggedColumn] = nextColumns.splice(sourceIndex, 1);
  const adjustedTargetIndex = sourceIndex < targetIndex ? targetIndex - 1 : targetIndex;
  nextColumns.splice(adjustedTargetIndex, 0, draggedColumn);

  return { nextColumns, moved: true };
};
