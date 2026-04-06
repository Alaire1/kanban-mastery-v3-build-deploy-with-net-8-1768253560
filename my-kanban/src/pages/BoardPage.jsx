import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import api from '../services/api';
import { ROUTES } from '../constants/routes';

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

const normalizeCard = (card) => {
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
    assigneeAvatarUrl: avatarUrl,
    assigneeFallbackName: fallbackAssigneeName, //fallback name for tooltip and avatar title
    assigneeInitials: getInitials(fallbackAssigneeName),
    isAssigned: Boolean(assignedUserId),
  };
};

function BoardPage() {
  const COLUMNS_PER_VIEW = 4;
  const { boardId } = useParams();
  const [board, setBoard] = useState(null);
  const [columns, setColumns] = useState([]);
  const [startIndex, setStartIndex] = useState(0);
  const [loading, setLoading] = useState(true);
  const [apiError, setApiError] = useState('');

  useEffect(() => {
    let isMounted = true;

    const fetchBoard = async () => {
      if (!boardId) {
        setApiError('Board id is missing.');
        setLoading(false);
        return;
      }

      setLoading(true);
      setApiError('');

      try {
        const response = await api.get(`/api/boards/${boardId}/`);
        if (!isMounted) return;

        const boardData = response.data ?? {};
        const boardColumns = boardData.columns ?? boardData.Columns ?? [];

        const normalizedColumns = Array.isArray(boardColumns)
          ? boardColumns
              .map((column) => {
                const cards = Array.isArray(column.cards ?? column.Cards)
                  ? (column.cards ?? column.Cards)
                  : [];

                return {
                  ...column,
                  cards: cards.map(normalizeCard),
                };
              })
              .sort((a, b) => (a.position ?? 0) - (b.position ?? 0))
          : [];

        setBoard(boardData);
        setColumns(normalizedColumns);
      } catch (error) {
        if (!isMounted) return;
        setApiError(error.response?.data?.message || 'Failed to load board details.');
      } finally {
        if (isMounted) setLoading(false);
      }
    };

    fetchBoard();

    return () => {
      isMounted = false;
    };
  }, [boardId]);

  useEffect(() => {
    setStartIndex(0);
  }, [columns.length]);

  const maxStartIndex = Math.max(0, columns.length - COLUMNS_PER_VIEW);
  const visibleColumns = columns.slice(startIndex, startIndex + COLUMNS_PER_VIEW);
  const canGoPrev = startIndex > 0;
  const canGoNext = startIndex < maxStartIndex;

  const handlePrevColumns = () => {
    setStartIndex((prev) => Math.max(0, prev - COLUMNS_PER_VIEW));
  };

  const handleNextColumns = () => {
    setStartIndex((prev) => Math.min(maxStartIndex, prev + COLUMNS_PER_VIEW));
  };

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

            <div className="mt-5 rounded-2xl border border-green-100 bg-green-50/60 p-4">
              <h2 className="text-sm font-semibold uppercase tracking-wide text-green-700 mb-3">Columns</h2>

              {columns.length > 0 ? (
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
                      <div
                        key={column.id}
                        className="rounded-xl border border-green-200 bg-white px-3 py-3 min-h-[180px]"
                      >
                        <p className="text-sm font-medium text-green-800 truncate mb-2">{column.name}</p>

                        <div className="space-y-2">
                          {column.cards.length > 0 ? (
                            column.cards.map((card) => (
                              <article
                                key={card.id}
                                data-testid="board-card"
                                data-assigned={card.isAssigned ? 'assigned' : 'unassigned'}
                                className={`rounded-lg border p-2.5 transition-shadow ${
                                  card.isAssigned
                                    ? 'border-emerald-200 bg-emerald-50/70'
                                    : 'border-amber-200 bg-amber-50/80'
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
                            ))
                          ) : (
                            <p className="text-xs text-green-500">No cards in this column.</p>
                          )}
                        </div>
                      </div>
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
