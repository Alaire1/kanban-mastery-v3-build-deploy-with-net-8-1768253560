import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import api from '../services/api';
import { ROUTES } from '../constants/routes';

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

        setBoard(boardData);
        setColumns(
          Array.isArray(boardColumns)
            ? [...boardColumns].sort((a, b) => (a.position ?? 0) - (b.position ?? 0))
            : [],
        );
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
                        className="rounded-xl border border-green-200 bg-white px-3 py-2 min-h-[52px]"
                      >
                        <p className="text-sm font-medium text-green-800 truncate">{column.name}</p>
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
