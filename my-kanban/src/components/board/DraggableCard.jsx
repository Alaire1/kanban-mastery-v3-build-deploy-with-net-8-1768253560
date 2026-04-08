import { useDraggable, useDroppable } from '@dnd-kit/core';
import { CSS } from '@dnd-kit/utilities';
import { useEffect, useRef, useState } from 'react';
import { getCardDragId } from '../../pages/board/boardUtils';

export default function DraggableCard({
  card,
  isDragModeEnabled,
  isDeleteModeEnabled,
  members = [],
  onAssignCard,
  onDeleteCard,
}) {
  const [isPickerOpen, setIsPickerOpen] = useState(false);
  const [isAssigning, setIsAssigning] = useState(false);
  const [assignError, setAssignError] = useState('');
  const [isDeleting, setIsDeleting] = useState(false);
  const [deleteError, setDeleteError] = useState('');
  const pickerRef = useRef(null);

  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useDraggable({
    id: getCardDragId(card.id),
    disabled: !isDragModeEnabled,
  });
  const { setNodeRef: setDropNodeRef, isOver } = useDroppable({
    id: getCardDragId(card.id),
  });

  const setCombinedNodeRef = (node) => {
    setNodeRef(node);
    setDropNodeRef(node);
  };

  useEffect(() => {
    const handleOutsideClick = (event) => {
      if (!pickerRef.current?.contains(event.target)) {
        setIsPickerOpen(false);
      }
    };

    const handleEscape = (event) => {
      if (event.key === 'Escape') {
        setIsPickerOpen(false);
      }
    };

    if (isPickerOpen) {
      document.addEventListener('mousedown', handleOutsideClick);
      document.addEventListener('keydown', handleEscape);
    }

    return () => {
      document.removeEventListener('mousedown', handleOutsideClick);
      document.removeEventListener('keydown', handleEscape);
    };
  }, [isPickerOpen]);

  const normalizedTransform = transform
    ? { ...transform, scaleX: 1, scaleY: 1 }
    : null;

  const style = {
    transform: CSS.Transform.toString(normalizedTransform),
    transition,
    opacity: isDragging ? 0.75 : 1,
  };

  const handleAssignToMember = async (member) => {
    if (!onAssignCard || isAssigning) return;

    setAssignError('');
    setIsAssigning(true);

    try {
      await onAssignCard(card.id, member.userId);
      setIsPickerOpen(false);
    } catch (error) {
      setAssignError(error.message || 'Could not assign card.');
    } finally {
      setIsAssigning(false);
    }
  };

  const handleDeleteCard = async () => {
    if (!onDeleteCard || isDeleting) return;

    setDeleteError('');
    setIsDeleting(true);

    try {
      await onDeleteCard(card.id);
    } catch (error) {
      setDeleteError(error.message || 'Could not delete card.');
    } finally {
      setIsDeleting(false);
    }
  };

  return (
    <article
      ref={setCombinedNodeRef}
      style={style}
      data-testid="board-card"
      data-assigned={card.isAssigned ? 'assigned' : 'unassigned'}
      {...attributes}
      {...listeners}
      className={`rounded-lg border p-2.5 transition-shadow ${
        card.isAssigned
          ? 'border-emerald-200 bg-emerald-50/70'
          : 'border-amber-200 bg-amber-50/80'
      } ${isDragModeEnabled ? 'cursor-grab active:cursor-grabbing' : 'cursor-default'} ${
        isDragging ? 'shadow-lg ring-2 ring-emerald-200' : ''
      } ${isOver && !isDragging ? 'ring-2 ring-sky-300' : ''}`}
    >
      <div className="flex items-start justify-between gap-2">
        <h3 data-testid="card-title" className="text-sm font-semibold text-slate-800 leading-5 line-clamp-2">
          {card.title}
        </h3>

        <div className="flex items-start gap-1.5">
          {isDeleteModeEnabled && (
            <button
              type="button"
              onClick={(event) => {
                event.stopPropagation();
                handleDeleteCard();
              }}
              onPointerDown={(event) => event.stopPropagation()}
              disabled={isDeleting}
              className="h-6 w-6 shrink-0 rounded-md border border-red-200 bg-red-50 text-xs font-bold text-red-700 hover:bg-red-100 disabled:opacity-60"
              aria-label="Delete card"
              title="Delete card"
            >
              {isDeleting ? '…' : '✕'}
            </button>
          )}

          <div ref={pickerRef} className="relative group shrink-0">
          <button
            type="button"
            onClick={(event) => {
              event.stopPropagation();
              setAssignError('');
              setIsPickerOpen((prev) => !prev);
            }}
            onPointerDown={(event) => event.stopPropagation()}
            data-testid="card-assignee-avatar"
            className={`h-8 w-8 rounded-full border overflow-hidden flex items-center justify-center text-[10px] font-semibold ${
              card.isAssigned
                ? 'border-emerald-300 bg-emerald-100 text-emerald-800'
                : 'border-amber-300 bg-amber-100 text-amber-800'
            }`}
            title={card.isAssigned ? card.assigneeFallbackName : 'Unassigned'}
            aria-label="Assign card"
            disabled={isAssigning || members.length === 0}
          >
            {card.isAssigned && card.assigneeAvatarUrl ? (
              <img
                src={card.assigneeAvatarUrl}
                alt={card.assigneeFallbackName}
                className="h-full w-full object-cover"
              />
            ) : (
              <span>{card.assigneeInitials}</span>
            )}
          </button>

          <div data-testid="card-assignee-tooltip" className="absolute left-full top-0 ml-2 z-20 hidden group-hover:block w-52 rounded-lg border border-slate-200 bg-white p-2 shadow-lg">
            <p className="text-xs font-semibold text-slate-800 truncate">
              {card.assigneeDisplayName || card.assigneeUserName || 'Unassigned'}
            </p>
            <p className="text-[11px] text-slate-500 truncate mt-0.5">
              {card.assigneeUserName ? `@${card.assigneeUserName}` : 'No username'}
            </p>
            <p className="text-[11px] text-slate-500 truncate mt-1">
              {card.assignedUserId ? 'Assigned to this card' : 'No member assigned'}
            </p>
          </div>

          {isPickerOpen && (
            <div
              onPointerDown={(event) => event.stopPropagation()}
              className="absolute right-0 top-10 z-30 w-64 rounded-lg border border-slate-200 bg-white p-2 shadow-xl"
            >
              <p className="mb-1 text-[11px] font-semibold uppercase tracking-wide text-slate-500">Assign to</p>
              {members.length > 0 ? (
                <div className="max-h-48 space-y-1 overflow-y-auto">
                  {members.map((member) => (
                    <button
                      key={member.userId}
                      type="button"
                      disabled={isAssigning}
                      onClick={() => handleAssignToMember(member)}
                      className="w-full rounded-md border border-slate-200 px-2 py-1.5 text-left hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-60"
                    >
                      <p className="text-xs font-semibold text-slate-800 truncate">{member.displayLabel}</p>
                      {member.email && (
                        <p className="text-[11px] text-slate-500 truncate">{member.email}</p>
                      )}
                    </button>
                  ))}
                </div>
              ) : (
                <p className="text-xs text-slate-500">No board members available.</p>
              )}
            </div>
          )}
          </div>
        </div>
      </div>

      <p data-testid="card-description" className="mt-1.5 text-xs text-slate-600 leading-4 line-clamp-3">
        {card.description}
      </p>

      {assignError && (
        <p className="mt-1.5 text-[11px] text-red-700">{assignError}</p>
      )}

      {deleteError && (
        <p className="mt-1.5 text-[11px] text-red-700">{deleteError}</p>
      )}
    </article>
  );
}
