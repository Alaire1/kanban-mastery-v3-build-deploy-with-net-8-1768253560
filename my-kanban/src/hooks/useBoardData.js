import { useEffect, useState } from 'react';
import api from '../services/api';
import { normalizeCard } from '../pages/board/boardUtils';

export default function useBoardData(boardId) {
  const [board, setBoard] = useState(null);
  const [columns, setColumns] = useState([]);
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

  return {
    board,
    columns,
    setColumns,
    loading,
    apiError,
  };
}
