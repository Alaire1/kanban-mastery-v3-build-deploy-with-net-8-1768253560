import { useEffect, useState } from 'react';
import { KeyboardSensor, PointerSensor, useSensor, useSensors } from '@dnd-kit/core';
import api from '../services/api';
import {
  findCardLocation,
  moveCardToCardPosition,
  moveCardToColumnEnd,
  moveColumnToTargetColumn,
  moveCardToAnotherColumn,
  parseCardIdFromDragId,
  parseColumnIdFromDragId,
  parseColumnIdFromEndDropId,
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

    const activeId = String(event.active.id);
    const cardId = parseCardIdFromDragId(activeId);
    const columnId = parseColumnIdFromDragId(activeId);
    if (cardId === null && columnId === null) return;

    setActionError('');
  };

  const handleDragEnd = async (event) => {
    if (!isDragModeEnabled || !event.over) return;

    const activeId = String(event.active.id);
    const overId = String(event.over.id);

    const draggedColumnId = parseColumnIdFromDragId(activeId);
    if (draggedColumnId !== null) {
      const targetColumnId = parseColumnIdFromDropId(overId);
      if (targetColumnId === null || draggedColumnId === targetColumnId) return;

      const { nextColumns, moved } = moveColumnToTargetColumn(columns, draggedColumnId, targetColumnId);
      if (!moved) return;

      setColumns(nextColumns);
      return;
    }

    const draggedCardId = parseCardIdFromDragId(activeId);
    const targetColumnEndId = parseColumnIdFromEndDropId(overId);
    const overCardId = parseCardIdFromDragId(overId);
    const targetColumnId = parseColumnIdFromDropId(overId);

    if (draggedCardId !== null && targetColumnEndId !== null) {
      const sourceColumn = findCardLocation(columns, draggedCardId);
      if (!sourceColumn) return;

      const draggedCard = sourceColumn.cards.find((card) => card.id === draggedCardId);
      if (!draggedCard) return;

      const previousColumns = columns;
      const { nextColumns, moved, sourceColumnId } = moveCardToColumnEnd(previousColumns, draggedCardId, targetColumnEndId);
      if (!moved) return;

      setColumns(nextColumns);

      if (sourceColumnId !== targetColumnEndId) {
        try {
          await api.put(`/api/boards/${boardId}/cards/${draggedCardId}`, {
            title: draggedCard.title,
            description: draggedCard.description,
            columnId: targetColumnEndId,
          });
        } catch (error) {
          setColumns(previousColumns);
          setActionError(error.response?.data?.message || 'Could not move the card. Changes were reverted.');
        }
      }

      return;
    }

    if (draggedCardId !== null && overCardId !== null && draggedCardId !== overCardId) {
      const sourceColumn = findCardLocation(columns, draggedCardId);
      if (!sourceColumn) return;

      const draggedCard = sourceColumn.cards.find((card) => card.id === draggedCardId);
      if (!draggedCard) return;

      const previousColumns = columns;
      const {
        nextColumns,
        moved,
        sourceColumnId,
        targetColumnId: dropTargetColumnId,
      } = moveCardToCardPosition(previousColumns, draggedCardId, overCardId);

      if (!moved || dropTargetColumnId === null) return;

      setColumns(nextColumns);

      if (sourceColumnId !== dropTargetColumnId) {
        try {
          await api.put(`/api/boards/${boardId}/cards/${draggedCardId}`, {
            title: draggedCard.title,
            description: draggedCard.description,
            columnId: dropTargetColumnId,
          });
        } catch (error) {
          setColumns(previousColumns);
          setActionError(error.response?.data?.message || 'Could not move the card. Changes were reverted.');
        }
      }

      return;
    }

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
