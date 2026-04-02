import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import api from '../services/api';
import { ROUTES, getBoardDetailPath } from '../constants/routes';

function DashboardPage() {
  const navigate = useNavigate();
  const [boards, setBoards] = useState([]);
  const [loading, setLoading] = useState(true);
  const [apiError, setApiError] = useState('');

  useEffect(() => {
    let isMounted = true;

    const fetchBoards = async () => {
      setLoading(true);
      setApiError('');

      try {
        const response = await api.get('/api/users/me');
        if (!isMounted) return;

        const boardList = response.data?.boards ?? response.data?.Boards ?? [];
        setBoards(Array.isArray(boardList) ? boardList : []);
      } catch (error) {
        if (!isMounted) return;
        setApiError(error.response?.data?.message || 'Failed to load boards');
      } finally {
        if (isMounted) setLoading(false);
      }
    };

    fetchBoards();

    return () => {
      isMounted = false;
    };
  }, []);

  const handleLogout = () => {
    localStorage.removeItem('token');
    navigate(ROUTES.LOGIN);
  };

  const handleOpenBoard = (boardId) => {
    navigate(getBoardDetailPath(boardId));
  };

  return (
    <div className="min-h-screen bg-gradient-to-br from-green-50 via-emerald-50 to-teal-50 p-6 md:p-10">
      <div className="max-w-5xl mx-auto">
        <div className="flex items-start justify-between gap-4 mb-8">
          <div>
            <div className="text-3xl mb-2">🌿</div>
            <h1 className="text-2xl font-bold text-green-800">Dashboard</h1>
            <p className="text-sm text-green-500">Your boards</p>
          </div>

          <button
            onClick={handleLogout}
            className="bg-white border border-green-200 hover:border-green-300 text-green-700 font-semibold px-4 py-2 rounded-xl shadow-sm transition text-sm"
          >
            Log out
          </button>
        </div>

        {loading && (
          <div className="bg-white/80 border border-green-100 rounded-2xl p-8 text-center text-green-700 shadow-sm">
            Loading boards…
          </div>
        )}

        {!loading && apiError && (
          <div className="bg-red-50 border border-red-100 rounded-2xl p-4 text-red-600 text-sm">
            {apiError}
          </div>
        )}

        {!loading && !apiError && boards.length === 0 && (
          <div className="bg-white/80 border border-green-100 rounded-2xl p-8 text-center shadow-sm">
            <p className="text-green-700 font-medium mb-1">No boards yet</p>
            <p className="text-sm text-green-500">Create your first board to get started.</p>
          </div>
        )}

        {!loading && !apiError && boards.length > 0 && (
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {boards.map((board) => (
              <button
                key={board.id}
                onClick={() => handleOpenBoard(board.id)}
                className="text-left bg-white border border-green-100 hover:border-green-300 rounded-2xl p-5 shadow-sm hover:shadow-md transition"
              >
                <p className="text-lg font-semibold text-green-800 mb-1 line-clamp-1">{board.name}</p>
                <p className="text-xs text-green-500">Role: {board.role}</p>
              </button>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}

export default DashboardPage;