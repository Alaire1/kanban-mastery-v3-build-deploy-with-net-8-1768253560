import { useDraggable, useDroppable } from '@dnd-kit/core';
import { CSS } from '@dnd-kit/utilities';
import { useEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { getCardDragId } from '../../pages/board/boardUtils';

export default function DraggableCard({
  card,
  isDragModeEnabled,
  members = [],
  onAssignCard,
  onDeleteCard,
  onUpdateCard,
}) {
  const [isPickerOpen, setIsPickerOpen] = useState(false);
  const [isAssigning, setIsAssigning] = useState(false);
  const [assignError, setAssignError] = useState('');
  const [isDeleting, setIsDeleting] = useState(false);
  const [deleteError, setDeleteError] = useState('');
  const [isActionsOpen, setIsActionsOpen] = useState(false);
  const [isEditModalOpen, setIsEditModalOpen] = useState(false);
  const [editTitle, setEditTitle] = useState(card.title);
  const [editDescription, setEditDescription] = useState(card.description ?? '');
  const [isUpdating, setIsUpdating] = useState(false);
  const [updateError, setUpdateError] = useState('');
  const [isTooltipOpen, setIsTooltipOpen] = useState(false);
  const [tooltipPosition, setTooltipPosition] = useState({
    top: 0,
    left: 0,
    placeAbove: false,
  });
  const [pickerPosition, setPickerPosition] = useState({
    top: 0,
    left: 0,
    placeAbove: false,
  });
  const pickerRef = useRef(null);
  const pickerPanelRef = useRef(null);
  const actionsRef = useRef(null);
  const assigneeButtonRef = useRef(null);

  useEffect(() => {
    setEditTitle(card.title);
    setEditDescription(card.description ?? '');
  }, [card.title, card.description]);

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
      const clickedOnPickerButton = assigneeButtonRef.current?.contains(event.target);
      const clickedOnPickerPanel = pickerPanelRef.current?.contains(event.target);

      if (!clickedOnPickerButton && !clickedOnPickerPanel) {
        setIsPickerOpen(false);
      }

      if (!actionsRef.current?.contains(event.target)) {
        setIsActionsOpen(false);
      }
    };

    const handleEscape = (event) => {
      if (event.key === 'Escape') {
        setIsPickerOpen(false);
        setIsActionsOpen(false);
      }
    };

    if (isPickerOpen || isActionsOpen) {
      document.addEventListener('mousedown', handleOutsideClick);
      document.addEventListener('keydown', handleEscape);
    }

    return () => {
      document.removeEventListener('mousedown', handleOutsideClick);
      document.removeEventListener('keydown', handleEscape);
    };
  }, [isPickerOpen, isActionsOpen]);

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

    if (!card?.id) {
      setDeleteError('Card id is missing. Refresh the board and try again.');
      return;
    }

    const confirmed = window.confirm(`Delete "${card.title}" card?`);
    if (!confirmed) return;

    setDeleteError('');
    setIsDeleting(true);

    try {
      await onDeleteCard(card.id);
    } catch (error) {
      setDeleteError(error.message || 'Could not delete card.');
    } finally {
      setIsDeleting(false);
      setIsActionsOpen(false);
    }
  };

  const handleUpdateCard = async (event) => {
    event.preventDefault();

    if (!onUpdateCard || isUpdating) return;

    const trimmedTitle = editTitle.trim();
    if (!trimmedTitle) {
      setUpdateError('Card title is required.');
      return;
    }

    const trimmedDescription = editDescription.trim();
    const sameTitle = trimmedTitle === (card.title ?? '').trim();
    const sameDescription = trimmedDescription === (card.description ?? '').trim();

    if (sameTitle && sameDescription) {
      setIsEditModalOpen(false);
      setUpdateError('');
      return;
    }

    setUpdateError('');
    setIsUpdating(true);

    try {
      await onUpdateCard(card.id, {
        title: trimmedTitle,
        description: trimmedDescription,
      });
      setIsEditModalOpen(false);
    } catch (error) {
      setUpdateError(error.message || 'Could not update card.');
    } finally {
      setIsUpdating(false);
    }
  };

  const handleOpenEditModal = () => {
    setEditTitle(card.title ?? '');
    setEditDescription(card.description ?? '');
    setUpdateError('');
    setIsEditModalOpen(true);
  };

  const updateTooltipPosition = () => {
    const anchor = assigneeButtonRef.current;
    if (!anchor) return;

    const rect = anchor.getBoundingClientRect();
    const tooltipWidth = 208; // w-52
    const tooltipApproxHeight = 96;
    const viewportPadding = 8;
    const gap = 8;

    const spaceBelow = window.innerHeight - rect.bottom;
    const placeAbove = spaceBelow < tooltipApproxHeight && rect.top > tooltipApproxHeight;

    const rightSideLeft = rect.right + gap;
    const leftSideLeft = rect.left - tooltipWidth - gap;
    const canOpenRight = rightSideLeft + tooltipWidth <= window.innerWidth - viewportPadding;

    const unclampedLeft = canOpenRight ? rightSideLeft : leftSideLeft;
    const left = Math.min(Math.max(unclampedLeft, viewportPadding), window.innerWidth - tooltipWidth - viewportPadding);

    const top = placeAbove ? rect.top - gap : rect.bottom + gap;

    setTooltipPosition({ top, left, placeAbove });
  };

  const openTooltip = () => {
    updateTooltipPosition();
    setIsTooltipOpen(true);
  };

  const closeTooltip = () => {
    setIsTooltipOpen(false);
  };

  useEffect(() => {
    if (!isTooltipOpen) return undefined;

    const handleReposition = () => updateTooltipPosition();

    window.addEventListener('resize', handleReposition);
    window.addEventListener('scroll', handleReposition, true);

    return () => {
      window.removeEventListener('resize', handleReposition);
      window.removeEventListener('scroll', handleReposition, true);
    };
  }, [isTooltipOpen]);

  const updatePickerPosition = () => {
    const anchor = assigneeButtonRef.current;
    if (!anchor) return;

    const rect = anchor.getBoundingClientRect();
    const pickerWidth = 256; // w-64
    const pickerApproxHeight = 240;
    const viewportPadding = 8;
    const gap = 8;

    const spaceBelow = window.innerHeight - rect.bottom;
    const placeAbove = spaceBelow < pickerApproxHeight && rect.top > pickerApproxHeight;

    const unclampedLeft = rect.right - pickerWidth;
    const left = Math.min(Math.max(unclampedLeft, viewportPadding), window.innerWidth - pickerWidth - viewportPadding);

    const top = placeAbove ? rect.top - gap : rect.bottom + gap;

    setPickerPosition({ top, left, placeAbove });
  };

  useEffect(() => {
    if (!isPickerOpen) return undefined;

    updatePickerPosition();

    const handleReposition = () => updatePickerPosition();

    window.addEventListener('resize', handleReposition);
    window.addEventListener('scroll', handleReposition, true);

    return () => {
      window.removeEventListener('resize', handleReposition);
      window.removeEventListener('scroll', handleReposition, true);
    };
  }, [isPickerOpen]);

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
          <div ref={actionsRef} className="relative shrink-0 order-2">
            <button
              type="button"
              onClick={(event) => {
                event.stopPropagation();
                setIsActionsOpen((prev) => !prev);
              }}
              onPointerDown={(event) => event.stopPropagation()}
              className="h-6 w-6 rounded-md bg-transparent text-xs font-bold text-slate-700 hover:bg-slate-50"
              aria-label="Card actions"
              title="Card actions"
            >
              ⋮
            </button>

            {isActionsOpen && (
              <div
                onPointerDown={(event) => event.stopPropagation()}
                className="absolute right-0 top-7 z-30 w-32 rounded-lg border border-slate-200 bg-white p-1 shadow-lg"
              >
                <button
                  type="button"
                  onClick={() => {
                    setIsActionsOpen(false);
                    handleOpenEditModal();
                  }}
                  className="w-full rounded-md px-2 py-1.5 text-left text-xs font-medium text-slate-700 hover:bg-slate-50"
                >
                  Edit
                </button>
                <button
                  type="button"
                  onClick={handleDeleteCard}
                  disabled={isDeleting}
                  className="w-full rounded-md px-2 py-1.5 text-left text-xs font-medium text-red-600 hover:bg-red-50 disabled:opacity-60"
                >
                  {isDeleting ? 'Deleting…' : 'Delete'}
                </button>
              </div>
            )}
          </div>

          <div ref={pickerRef} className="relative shrink-0 order-1">
          <button
            ref={assigneeButtonRef}
            type="button"
            onClick={(event) => {
              event.stopPropagation();
              setAssignError('');
              setIsPickerOpen((prev) => !prev);
              if (!isPickerOpen) updatePickerPosition();
            }}
            onMouseEnter={openTooltip}
            onMouseLeave={closeTooltip}
            onFocus={openTooltip}
            onBlur={closeTooltip}
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

          {isTooltipOpen && createPortal(
            <div
              data-testid="card-assignee-tooltip"
              className="pointer-events-none fixed z-[70] w-52 rounded-lg border border-slate-200 bg-white p-2 shadow-lg"
              style={{
                top: tooltipPosition.top,
                left: tooltipPosition.left,
                transform: tooltipPosition.placeAbove ? 'translateY(-100%)' : 'none',
              }}
            >
              <p className="text-xs font-semibold text-slate-800 truncate">
                {card.assigneeDisplayName || card.assigneeUserName || 'Unassigned'}
              </p>
              <p className="text-[11px] text-slate-500 truncate mt-0.5">
                {card.assigneeUserName ? `@${card.assigneeUserName}` : 'No username'}
              </p>
              <p className="text-[11px] text-slate-500 truncate mt-1">
                {card.assignedUserId ? 'Assigned to this card' : 'No member assigned'}
              </p>
            </div>,
            document.body,
          )}

          {isPickerOpen && createPortal(
            <div
              ref={pickerPanelRef}
              onPointerDown={(event) => event.stopPropagation()}
              className="fixed z-[70] w-64 rounded-lg border border-slate-200 bg-white p-2 shadow-xl"
              style={{
                top: pickerPosition.top,
                left: pickerPosition.left,
                transform: pickerPosition.placeAbove ? 'translateY(-100%)' : 'none',
              }}
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
            </div>,
            document.body,
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

      {isEditModalOpen && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/35 p-4"
          onClick={() => {
            if (isUpdating) return;
            setIsEditModalOpen(false);
            setUpdateError('');
          }}
        >
          <div
            className="w-full max-w-md rounded-xl border border-slate-200 bg-white p-4 shadow-xl"
            onClick={(event) => event.stopPropagation()}
          >
            <h4 className="text-sm font-semibold text-slate-800">Edit card</h4>

            <form onSubmit={handleUpdateCard} className="mt-3 space-y-2">
              <input
                type="text"
                value={editTitle}
                onChange={(event) => {
                  setEditTitle(event.target.value);
                  if (updateError) setUpdateError('');
                }}
                maxLength={120}
                autoFocus
                disabled={isUpdating}
                className="w-full rounded-md border border-slate-200 px-2 py-1.5 text-sm text-slate-800 focus:border-emerald-400 focus:outline-none"
                placeholder="Card title"
              />

              <textarea
                value={editDescription}
                onChange={(event) => setEditDescription(event.target.value)}
                maxLength={500}
                rows={4}
                disabled={isUpdating}
                className="w-full rounded-md border border-slate-200 px-2 py-1.5 text-sm text-slate-700 focus:border-emerald-400 focus:outline-none"
                placeholder="Description (optional)"
              />

              {updateError && (
                <p className="text-xs text-red-700">{updateError}</p>
              )}

              <div className="flex justify-end gap-2 pt-1">
                <button
                  type="button"
                  disabled={isUpdating}
                  onClick={() => {
                    setIsEditModalOpen(false);
                    setUpdateError('');
                  }}
                  className="rounded-md border border-slate-200 bg-white px-3 py-1.5 text-xs font-semibold text-slate-700 hover:bg-slate-50 disabled:opacity-60"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={isUpdating}
                  className="rounded-md bg-emerald-600 px-3 py-1.5 text-xs font-semibold text-white hover:bg-emerald-700 disabled:opacity-60"
                >
                  {isUpdating ? 'Saving…' : 'Save'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </article>
  );
}
