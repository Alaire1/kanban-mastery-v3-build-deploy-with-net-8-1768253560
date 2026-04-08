import { useDraggable, useDroppable } from '@dnd-kit/core';
import { CSS } from '@dnd-kit/utilities';
import { useState } from 'react';
import { getColumnDragId, getColumnDropId, getColumnEndDropId } from '../../pages/board/boardUtils';
import DraggableCard from './DraggableCard';

export default function BoardColumn({
  column,
  isDragModeEnabled,
  isDeleteModeEnabled,
  onCreateCard,
  members,
  onAssignCard,
  onDeleteCard,
  onDeleteColumn,
}) {
  const { setNodeRef: setDropNodeRef, isOver } = useDroppable({
    id: getColumnDropId(column.id),
  });
  const { setNodeRef: setEndDropNodeRef } = useDroppable({
    id: getColumnEndDropId(column.id),
  });
  const {
    attributes,
    listeners,
    setNodeRef: setDragNodeRef,
    transform,
    transition,
    isDragging,
  } = useDraggable({
    id: getColumnDragId(column.id),
    disabled: !isDragModeEnabled,
  });
  const [isCreateFormVisible, setIsCreateFormVisible] = useState(false);
  const [newCardTitle, setNewCardTitle] = useState('');
  const [newCardDescription, setNewCardDescription] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [formError, setFormError] = useState('');
  const [isDeletingColumn, setIsDeletingColumn] = useState(false);

  const setCombinedNodeRef = (node) => {
    setDropNodeRef(node);
    setDragNodeRef(node);
  };

  const normalizedTransform = transform
    ? { ...transform, scaleX: 1, scaleY: 1 }
    : null;

  const style = {
    transform: CSS.Transform.toString(normalizedTransform),
    transition,
    opacity: isDragging ? 0.8 : 1,
  };

  const handleSubmitCreateCard = async (event) => {
    event.preventDefault();

    const trimmedTitle = newCardTitle.trim();
    if (!trimmedTitle) {
      setFormError('Card title is required.');
      return;
    }

    setFormError('');
    setIsSubmitting(true);

    try {
      await onCreateCard(column.id, trimmedTitle, newCardDescription.trim());
      setNewCardTitle('');
      setNewCardDescription('');
      setIsCreateFormVisible(false);
    } catch (error) {
      setFormError(error.message || 'Could not create the card.');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleDeleteColumn = async () => {
    if (!onDeleteColumn || isDeletingColumn) return;

    setIsDeletingColumn(true);

    try {
      await onDeleteColumn(column.id);
    } catch {
      // Alert messaging is handled upstream.
    } finally {
      setIsDeletingColumn(false);
    }
  };

  return (
    <div
      ref={setCombinedNodeRef}
      style={style}
      className={`rounded-xl border bg-white px-3 py-3 min-h-[180px] transition-colors ${
        isOver ? 'border-emerald-300 bg-emerald-50' : 'border-green-200'
      } ${isDragging ? 'shadow-lg ring-2 ring-emerald-200' : ''}`}
    >
      <div className="mb-2 flex items-center justify-between gap-2">
        <p className="text-sm font-medium text-green-800 truncate">{column.name}</p>
        <div className="flex items-center gap-1">
          {isDeleteModeEnabled && (
            <button
              type="button"
              onClick={handleDeleteColumn}
              disabled={isDeletingColumn}
              className="h-7 w-7 shrink-0 rounded-md border border-red-200 bg-red-50 text-xs font-semibold text-red-700 hover:bg-red-100 disabled:opacity-60"
              aria-label={`Delete ${column.name} column`}
              title="Delete column"
            >
              {isDeletingColumn ? '…' : '✕'}
            </button>
          )}

          <button
            type="button"
            {...attributes}
            {...listeners}
            disabled={!isDragModeEnabled}
            className={`h-7 w-7 shrink-0 rounded-md border text-sm font-semibold ${
              isDragModeEnabled
                ? 'border-emerald-300 bg-emerald-50 text-emerald-800 cursor-grab active:cursor-grabbing hover:bg-emerald-100'
                : 'border-green-200 bg-white text-green-300 cursor-not-allowed'
            }`}
            aria-label={`Drag ${column.name} column`}
            title={isDragModeEnabled ? 'Drag column' : 'Enable drag mode to move columns'}
          >
            ↕
          </button>
        </div>
      </div>

      <div className="space-y-2">
        {column.cards.length > 0 ? (
          column.cards.map((card) => (
            <DraggableCard
              key={card.id}
              card={card}
              isDragModeEnabled={isDragModeEnabled}
              isDeleteModeEnabled={isDeleteModeEnabled}
              members={members}
              onAssignCard={onAssignCard}
              onDeleteCard={onDeleteCard}
            />
          ))
        ) : (
          <p className="text-xs text-green-500">No cards in this column.</p>
        )}
      </div>

      <div
        ref={setEndDropNodeRef}
        className={isDragModeEnabled ? 'mt-2 h-5 w-full' : 'h-0 w-full pointer-events-none'}
      />

      {formError && (
        <p className="mt-3 rounded-md border border-red-200 bg-red-50 px-2 py-1 text-xs text-red-700">{formError}</p>
      )}

      {isCreateFormVisible ? (
        <form onSubmit={handleSubmitCreateCard} className="mt-3 space-y-2">
          <input
            type="text"
            value={newCardTitle}
            onChange={(event) => {
              setNewCardTitle(event.target.value);
              if (formError) setFormError('');
            }}
            placeholder="Card title"
            className="w-full rounded-md border border-green-200 px-2 py-1.5 text-sm focus:border-emerald-400 focus:outline-none"
            autoFocus
            disabled={isSubmitting}
            maxLength={120}
          />

          <textarea
            value={newCardDescription}
            onChange={(event) => setNewCardDescription(event.target.value)}
            placeholder="Description (optional)"
            className="w-full rounded-md border border-green-200 px-2 py-1.5 text-sm focus:border-emerald-400 focus:outline-none"
            rows={3}
            disabled={isSubmitting}
            maxLength={500}
          />

          <div className="flex items-center gap-2">
            <button
              type="submit"
              disabled={isSubmitting}
              className="rounded-md bg-emerald-600 px-3 py-1.5 text-xs font-semibold text-white hover:bg-emerald-700 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {isSubmitting ? 'Creating...' : 'Create'}
            </button>
            <button
              type="button"
              onClick={() => {
                setIsCreateFormVisible(false);
                setNewCardTitle('');
                setNewCardDescription('');
                setFormError('');
              }}
              disabled={isSubmitting}
              className="rounded-md border border-green-200 bg-white px-3 py-1.5 text-xs font-semibold text-green-700 hover:bg-green-50 disabled:cursor-not-allowed disabled:opacity-60"
            >
              Cancel
            </button>
          </div>
        </form>
      ) : (
        <button
          type="button"
          onClick={() => setIsCreateFormVisible(true)}
          className="mt-3 w-full rounded-md border border-dashed border-emerald-300 bg-emerald-50 px-3 py-1.5 text-xs font-semibold text-emerald-800 hover:bg-emerald-100"
        >
          + Create Card
        </button>
      )}
    </div>
  );
}
