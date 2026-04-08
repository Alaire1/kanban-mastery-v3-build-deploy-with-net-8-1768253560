import { useDroppable } from '@dnd-kit/core';
import { getColumnDropId } from '../../pages/board/boardUtils';
import DraggableCard from './DraggableCard';

export default function BoardColumn({ column, isDragModeEnabled }) {
  const { setNodeRef, isOver } = useDroppable({
    id: getColumnDropId(column.id),
  });

  return (
    <div
      ref={setNodeRef}
      className={`rounded-xl border bg-white px-3 py-3 min-h-[180px] transition-colors ${
        isOver ? 'border-emerald-300 bg-emerald-50' : 'border-green-200'
      }`}
    >
      <p className="text-sm font-medium text-green-800 truncate mb-2">{column.name}</p>

      <div className="space-y-2">
        {column.cards.length > 0 ? (
          column.cards.map((card) => (
            <DraggableCard key={card.id} card={card} isDragModeEnabled={isDragModeEnabled} />
          ))
        ) : (
          <p className="text-xs text-green-500">No cards in this column.</p>
        )}
      </div>
    </div>
  );
}
