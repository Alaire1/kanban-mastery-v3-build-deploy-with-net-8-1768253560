import { useDraggable, useDroppable } from '@dnd-kit/core';
import { CSS } from '@dnd-kit/utilities';
import { useEffect, useRef, useState } from 'react';
import { getColumnDragId, getColumnDropId, getColumnEndDropId } from '../../pages/board/boardUtils';
import DraggableCard from './DraggableCard';

export default function BoardColumn({
  column,
  isDragModeEnabled,
  onCreateCard,
  members,
  onAssignCard,
  onDeleteCard,
  onDeleteColumn,
  onUpdateColumn,
  onUpdateCard,
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
  const [isActionsOpen, setIsActionsOpen] = useState(false);
  const [isEditingColumn, setIsEditingColumn] = useState(false);
  const [columnNameInput, setColumnNameInput] = useState(column.name);
  const [isUpdatingColumn, setIsUpdatingColumn] = useState(false);
  const [columnActionError, setColumnActionError] = useState('');
  const actionsRef = useRef(null);

  useEffect(() => {
    setColumnNameInput(column.name);
  }, [column.name]);

  useEffect(() => {
    const handleOutsideClick = (event) => {
      if (!actionsRef.current?.contains(event.target)) {
        setIsActionsOpen(false);
      }
    };

    const handleEscape = (event) => {
      if (event.key === 'Escape') {
        setIsActionsOpen(false);
      }
    };

    if (isActionsOpen) {
      document.addEventListener('mousedown', handleOutsideClick);
      document.addEventListener('keydown', handleEscape);
    }

    return () => {
      document.removeEventListener('mousedown', handleOutsideClick);
      document.removeEventListener('keydown', handleEscape);
    };
  }, [isActionsOpen]);

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

    const confirmed = window.confirm(`Delete "${column.name}" column?`);
    if (!confirmed) return;

    setColumnActionError('');
    setIsDeletingColumn(true);

    try {
      await onDeleteColumn(column.id);
    } catch (error) {
      setColumnActionError(error.message || 'Could not delete the column.');
    } finally {
      setIsDeletingColumn(false);
      setIsActionsOpen(false);
    }
  };

  const handleSubmitUpdateColumn = async (event) => {
    event.preventDefault();

    if (!onUpdateColumn || isUpdatingColumn) return;

    const trimmedName = columnNameInput.trim();
    if (!trimmedName) {
      setColumnActionError('Column name is required.');
      return;
    }

    if (trimmedName === column.name) {
      setIsEditingColumn(false);
      setColumnActionError('');
      return;
    }

    setColumnActionError('');
    setIsUpdatingColumn(true);

    try {
      await onUpdateColumn(column.id, trimmedName);
      setIsEditingColumn(false);
    } catch (error) {
      setColumnActionError(error.message || 'Could not update the column.');
    } finally {
      setIsUpdatingColumn(false);
    }
  };

  return (
    <div
      ref={setCombinedNodeRef}
      style={style}
      className={`flex h-full min-h-0 flex-col rounded-xl border bg-white px-3 py-3 transition-colors ${
        isOver ? 'border-emerald-300 bg-emerald-50' : 'border-green-200'
      } ${isDragging ? 'shadow-lg ring-2 ring-emerald-200' : ''}`}
    >
      <div className="mb-2 flex items-center justify-between gap-2">
        {isEditingColumn ? (
          <form onSubmit={handleSubmitUpdateColumn} className="flex-1 flex items-center gap-1">
            <input
              type="text"
              value={columnNameInput}
              onChange={(event) => {
                setColumnNameInput(event.target.value);
                if (columnActionError) setColumnActionError('');
              }}
              className="w-full rounded-md border border-green-200 px-2 py-1 text-xs text-green-800 focus:border-emerald-400 focus:outline-none"
              maxLength={50}
              autoFocus
              disabled={isUpdatingColumn}
            />
            <button
              type="submit"
              disabled={isUpdatingColumn}
              className="rounded-md bg-emerald-600 px-2 py-1 text-[11px] font-semibold text-white hover:bg-emerald-700 disabled:opacity-60"
            >
              {isUpdatingColumn ? '…' : 'Save'}
            </button>
            <button
              type="button"
              disabled={isUpdatingColumn}
              onClick={() => {
                setIsEditingColumn(false);
                setColumnNameInput(column.name);
                setColumnActionError('');
              }}
              className="rounded-md border border-green-200 bg-white px-2 py-1 text-[11px] font-semibold text-green-700 hover:bg-green-50"
            >
              Cancel
            </button>
          </form>
        ) : (
          <p className="text-sm font-medium text-green-800 truncate">{column.name}</p>
        )}

        <div className="flex items-center gap-1">
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

          <div ref={actionsRef} className="relative">
            <button
              type="button"
              onClick={() => setIsActionsOpen((prev) => !prev)}
              className="h-7 w-7 shrink-0 rounded-md bg-transparent text-sm font-semibold text-green-700 hover:bg-green-50"
              aria-label={`Open actions for ${column.name}`}
              title="Column actions"
            >
              ⋮
            </button>

            {isActionsOpen && (
              <div className="absolute right-0 top-8 z-20 w-32 rounded-lg border border-green-100 bg-white p-1 shadow-lg">
                <button
                  type="button"
                  onClick={() => {
                    setIsActionsOpen(false);
                    setIsEditingColumn(true);
                    setColumnNameInput(column.name);
                    setColumnActionError('');
                  }}
                  className="w-full rounded-md px-2 py-1.5 text-left text-xs font-medium text-slate-700 hover:bg-green-50"
                >
                  Edit
                </button>
                <button
                  type="button"
                  onClick={handleDeleteColumn}
                  disabled={isDeletingColumn}
                  className="w-full rounded-md px-2 py-1.5 text-left text-xs font-medium text-red-600 hover:bg-red-50 disabled:opacity-60"
                >
                  {isDeletingColumn ? 'Deleting…' : 'Delete'}
                </button>
              </div>
            )}
          </div>
        </div>
      </div>

      <div className="flex-1 min-h-0 space-y-2 overflow-y-auto overflow-x-hidden pr-1">
        {column.cards.length > 0 ? (
          column.cards.map((card) => (
            <DraggableCard
              key={card.id}
              card={card}
              isDragModeEnabled={isDragModeEnabled}
              members={members}
              onAssignCard={onAssignCard}
              onDeleteCard={onDeleteCard}
              onUpdateCard={onUpdateCard}
            />
          ))
        ) : (
          <p className="text-xs text-green-500">No cards in this column.</p>
        )}

        <div
          ref={setEndDropNodeRef}
          className={`w-full rounded-md transition-colors ${
            isDragModeEnabled
              ? 'mt-1 h-10 border border-dashed border-emerald-300 bg-emerald-50/60'
              : 'h-0 pointer-events-none border-0 bg-transparent'
          }`}
          aria-hidden={!isDragModeEnabled}
        />
      </div>

      {formError && (
        <p className="mt-3 rounded-md border border-red-200 bg-red-50 px-2 py-1 text-xs text-red-700">{formError}</p>
      )}

      {columnActionError && (
        <p className="mt-3 rounded-md border border-red-200 bg-red-50 px-2 py-1 text-xs text-red-700">{columnActionError}</p>
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
