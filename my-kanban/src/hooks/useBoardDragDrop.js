import { useEffect, useState } from 'react';
import { KeyboardSensor, PointerSensor, useSensor, useSensors } from '@dnd-kit/core';
import api from '../services/api';
import {
  findCardLocation,
  moveCardToAnotherColumn,
  parseCardIdFromDragId,
  parseColumnIdFromDropId,
} from '../pages/board/boardUtils';

export default function useBoardDragDrop({ boardId, columns, setColumns, loading, apiError }) {
  const [isDragModeEnabled, setIsDragModeEnabled] = useState(false);
  const [actionError, setActionError] = useState('');

  const sensors = useSensors(
    useSensor(PointerSensor, {
      activationConstraint: {
        distance: 8,
      },
    }),
    useSensor(KeyboardSensor)
  );

  useEffect(() => {
    if (!loading && !apiError) {
      setActionError('');
    }
  }, [loading, apiError]);

  const handleToggleDragMode = () => {
    setActionError('');
    setIsDragModeEnabled((prev) => !prev);
  };

  const handleDragStart = (event) => {
    if (!isDragModeEnabled) return;

    const cardId = parseCardIdFromDragId(String(event.active.id));
    if (cardId === null) return;

    setActionError('');
  };

  const handleDragEnd = async (event) => {
    if (!isDragModeEnabled || !event.over) return;

    const draggedCardId = parseCardIdFromDragId(String(event.active.id));
    const targetColumnId = parseColumnIdFromDropId(String(event.over.id));

    if (draggedCardId === null || targetColumnId === null) return;

    const sourceColumn = findCardLocation(columns, draggedCardId);
    if (!sourceColumn || sourceColumn.id === targetColumnId) return;

    const draggedCard = sourceColumn.cards.find((card) => card.id === draggedCardId);
    if (!draggedCard) return;

    const previousColumns = columns;
    const { nextColumns, moved } = moveCardToAnotherColumn(previousColumns, draggedCardId, targetColumnId);

    if (!moved) return;

    setColumns(nextColumns);

    try {
      await api.put(`/api/boards/${boardId}/cards/${draggedCardId}`, {
        title: draggedCard.title,
        description: draggedCard.description,
        columnId: targetColumnId,
      });
    } catch (error) {
      setColumns(previousColumns);
      setActionError(error.response?.data?.message || 'Could not move the card. Changes were reverted.');
    }
  };

  return {
    sensors,
    isDragModeEnabled,
    actionError,
    handleToggleDragMode,
    handleDragStart,
    handleDragEnd,
  };
}
