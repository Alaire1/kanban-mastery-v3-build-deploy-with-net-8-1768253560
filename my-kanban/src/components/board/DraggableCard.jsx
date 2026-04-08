import { useDraggable } from '@dnd-kit/core';
import { CSS } from '@dnd-kit/utilities';
import { getCardDragId } from '../../pages/board/boardUtils';

export default function DraggableCard({ card, isDragModeEnabled }) {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useDraggable({
    id: getCardDragId(card.id),
    disabled: !isDragModeEnabled,
  });

  const normalizedTransform = transform
    ? { ...transform, scaleX: 1, scaleY: 1 }
    : null;

  const style = {
    transform: CSS.Transform.toString(normalizedTransform),
    transition,
    opacity: isDragging ? 0.75 : 1,
  };

  return (
    <article
      ref={setNodeRef}
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
      }`}
    >
      <div className="flex items-start justify-between gap-2">
        <h3 data-testid="card-title" className="text-sm font-semibold text-slate-800 leading-5 line-clamp-2">
          {card.title}
        </h3>

        <div className="relative group shrink-0">
          <div
            data-testid="card-assignee-avatar"
            className={`h-8 w-8 rounded-full border overflow-hidden flex items-center justify-center text-[10px] font-semibold ${
              card.isAssigned
                ? 'border-emerald-300 bg-emerald-100 text-emerald-800'
                : 'border-amber-300 bg-amber-100 text-amber-800'
            }`}
            title={card.isAssigned ? card.assigneeFallbackName : 'Unassigned'}
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
          </div>

          <div data-testid="card-assignee-tooltip" className="absolute left-full top-0 ml-2 z-20 hidden group-hover:block w-52 rounded-lg border border-slate-200 bg-white p-2 shadow-lg">
            <p className="text-xs font-semibold text-slate-800 truncate">
              {card.assigneeDisplayName || card.assigneeUserName || 'Unassigned'}
            </p>
            <p className="text-[11px] text-slate-500 truncate mt-0.5">
              {card.assigneeUserName ? `@${card.assigneeUserName}` : 'No username'}
            </p>
            <p className="text-[11px] text-slate-500 truncate mt-1">
              {card.assignedUserId ? `ID: ${card.assignedUserId}` : 'No member assigned'}
            </p>
          </div>
        </div>
      </div>

      <p data-testid="card-description" className="mt-1.5 text-xs text-slate-600 leading-4 line-clamp-3">
        {card.description}
      </p>
    </article>
  );
}
