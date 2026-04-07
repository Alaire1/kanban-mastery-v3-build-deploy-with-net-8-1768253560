import { Link, useParams } from 'react-router-dom';
import {
  closestCorners,
  DndContext,
} from '@dnd-kit/core';
import { ROUTES } from '../constants/routes';
import BoardColumn from '../components/board/BoardColumn';
import useBoardData from '../hooks/useBoardData';
import useBoardDragDrop from '../hooks/useBoardDragDrop';
import useColumnsPagination from '../hooks/useColumnsPagination';

function BoardPage() {
  const COLUMNS_PER_VIEW = 4;
  const { boardId } = useParams();
  const { board, columns, setColumns, loading, apiError } = useBoardData(boardId);

  const {
    sensors,
    isDragModeEnabled,
    actionError,
    handleToggleDragMode,
    handleDragStart,
    handleDragEnd,
  } = useBoardDragDrop({
    boardId,
    columns,
    setColumns,
    loading,
    apiError,
  });

  const {
    visibleColumns,
    canGoPrev,
    canGoNext,
    handlePrevColumns,
    handleNextColumns,
  } = useColumnsPagination(columns, COLUMNS_PER_VIEW);

  return (
    <div className="min-h-screen bg-gradient-to-br from-green-50 via-emerald-50 to-teal-50 p-6 md:p-10">
      <div className="max-w-4xl mx-auto bg-white/80 border border-green-100 rounded-2xl p-6 shadow-sm">
        <Link to={ROUTES.DASHBOARD} className="text-sm text-green-700 hover:underline">← Back to dashboard</Link>

        {loading && (
          <div className="mt-4 rounded-2xl border border-green-100 bg-white p-6 text-green-700">
            Loading board...
          </div>
        )}

        {!loading && apiError && (
          <div className="mt-4 rounded-2xl border border-red-100 bg-red-50 p-4 text-sm text-red-600">
            {apiError}
          </div>
        )}

        {!loading && !apiError && (
          <>
            <h1 className="text-2xl font-bold text-green-800 mt-3 mb-2">{board?.name ?? `Board ${boardId}`}</h1>
            <p className="text-sm text-green-500">
              {columns.length > 0
                ? `This board has ${columns.length} column${columns.length === 1 ? '' : 's'}.`
                : 'This board has no columns yet.'}
            </p>

            {actionError && (
              <div className="mt-3 rounded-xl border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700">
                {actionError}
              </div>
            )}

            <div className="mt-5 rounded-2xl border border-green-100 bg-green-50/60 p-4">
              <div className="mb-3 flex items-center justify-between gap-3">
                <h2 className="text-sm font-semibold uppercase tracking-wide text-green-700">Columns</h2>

                <button
                  type="button"
                  onClick={handleToggleDragMode}
                  aria-pressed={isDragModeEnabled}
                  className={`rounded-lg border px-3 py-1.5 text-xs font-semibold transition-colors ${
                    isDragModeEnabled
                      ? 'border-emerald-300 bg-emerald-100 text-emerald-800 hover:bg-emerald-200'
                      : 'border-green-200 bg-white text-green-700 hover:bg-green-100'
                  }`}
                >
                  {isDragModeEnabled ? 'Disable move cards' : 'Enable move cards'}
                </button>
              </div>

              <p className="mb-3 text-xs text-green-600">
                {isDragModeEnabled
                  ? 'Move mode is ON. You can drag a card from anywhere on the card.'
                  : 'Move mode is OFF to prevent accidental card moves.'}
              </p>

              {columns.length > 0 ? (
                <DndContext
                  sensors={sensors}
                  collisionDetection={closestCorners}
                  onDragStart={handleDragStart}
                  onDragEnd={handleDragEnd}
                >
                  <div className="flex items-center gap-3">
                    <button
                      type="button"
                      onClick={handlePrevColumns}
                      disabled={!canGoPrev}
                      className="h-9 w-9 rounded-full border border-green-200 bg-white text-green-700 disabled:opacity-40 disabled:cursor-not-allowed"
                      aria-label="Show previous columns"
                    >
                      ←
                    </button>

                    <div className="grid flex-1 grid-cols-4 gap-3">
                      {visibleColumns.map((column) => (
                        <BoardColumn key={column.id} column={column} isDragModeEnabled={isDragModeEnabled} />
                      ))}
                    </div>

                    <button
                      type="button"
                      onClick={handleNextColumns}
                      disabled={!canGoNext}
                      className="h-9 w-9 rounded-full border border-green-200 bg-white text-green-700 disabled:opacity-40 disabled:cursor-not-allowed"
                      aria-label="Show next columns"
                    >
                      →
                    </button>
                  </div>
                </DndContext>
              ) : (
                <p className="text-sm text-green-500">No columns found.</p>
              )}
            </div>
          </>
        )}
      </div>
    </div>
  );
}

export default BoardPage;
