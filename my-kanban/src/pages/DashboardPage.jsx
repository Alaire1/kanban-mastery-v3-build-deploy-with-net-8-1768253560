import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import api from '../services/api';
import { ROUTES, getBoardDetailPath } from '../constants/routes';

const toAbsoluteApiUrl = (value) => {
  if (!value || typeof value !== 'string') return '';
  if (/^https?:\/\//i.test(value)) return value;

  try {
    return new URL(value, api.defaults.baseURL).toString();
  } catch {
    return value;
  }
};

function DashboardPage() {
  const navigate = useNavigate();
  const [boards, setBoards] = useState([]);
  const [loading, setLoading] = useState(true);
  const [apiError, setApiError] = useState('');
  const [newBoardName, setNewBoardName] = useState('');
  const [createError, setCreateError] = useState('');
  const [createLoading, setCreateLoading] = useState(false);
  const [userDisplayName, setUserDisplayName] = useState('');
  const [userName, setUserName] = useState('');
  const [userGreetingName, setUserGreetingName] = useState('');
  const [profileImageUrl, setProfileImageUrl] = useState('');
  const [isAccountMenuOpen, setIsAccountMenuOpen] = useState(false);
  const [isSettingsModalOpen, setIsSettingsModalOpen] = useState(false);
  const [profileImageFile, setProfileImageFile] = useState(null);
  const [profileImageError, setProfileImageError] = useState('');
  const [profileImageLoading, setProfileImageLoading] = useState(false);
  const [menuBoardId, setMenuBoardId] = useState(null);
  const [boardToDelete, setBoardToDelete] = useState(null);
  const [deleteConfirmText, setDeleteConfirmText] = useState('');
  const [deleteError, setDeleteError] = useState('');
  const [deleteLoading, setDeleteLoading] = useState(false);

  useEffect(() => {
    let isMounted = true;

    const fetchBoards = async () => {
      setLoading(true);
      setApiError('');

      try {
        const response = await api.get('/api/users/me');
        if (!isMounted) return;

        const boardList = response.data?.boards ?? response.data?.Boards ?? [];
        const displayName = response.data?.displayName ?? response.data?.DisplayName ?? '';
        const profileUserName = response.data?.userName ?? response.data?.UserName ?? '';
        const cleanValue = (value) => (typeof value === 'string' ? value.trim() : '');
        const isEmailLike = (value) => value.includes('@');

        const safeDisplayName = cleanValue(displayName);
        const safeUserName = cleanValue(profileUserName);

        let greetingName = '';

        if (safeDisplayName && !isEmailLike(safeDisplayName)) {
          greetingName = safeDisplayName;
        } else if (safeUserName && !isEmailLike(safeUserName)) {
          greetingName = safeUserName;
        }

        setUserDisplayName(safeDisplayName);
        setUserName(safeUserName);
        setUserGreetingName(greetingName);
        setProfileImageUrl(toAbsoluteApiUrl(response.data?.profileImageUrl ?? response.data?.ProfileImageUrl ?? ''));
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

  const handleToggleAccountMenu = () => {
    setIsAccountMenuOpen((prev) => !prev);
  };

  const handleOpenSettings = () => {
    setIsAccountMenuOpen(false);
    setProfileImageError('');
    setIsSettingsModalOpen(true);
  };

  const handleCloseSettings = () => {
    if (profileImageLoading) return;
    setProfileImageFile(null);
    setProfileImageError('');
    setIsSettingsModalOpen(false);
  };

  const handleProfileImageSelection = (event) => {
    const selectedFile = event.target.files?.[0] ?? null;
    setProfileImageError('');
    setProfileImageFile(selectedFile);
  };

  const handleUploadProfileImage = async () => {
    if (!profileImageFile) {
      setProfileImageError('Please choose an image first.');
      return;
    }

    const allowedTypes = ['image/jpeg', 'image/png', 'image/gif', 'image/webp'];
    if (!allowedTypes.includes(profileImageFile.type)) {
      setProfileImageError('Allowed formats: JPG, PNG, GIF, WEBP.');
      return;
    }

    const maxBytes = 2 * 1024 * 1024;
    if (profileImageFile.size > maxBytes) {
      setProfileImageError('Image must be 2 MB or smaller.');
      return;
    }

    const payload = new FormData();
    payload.append('file', profileImageFile);

    setProfileImageLoading(true);
    setProfileImageError('');
    try {
      const response = await api.post('/api/users/me/profile-image', payload);
      const imageUrl = toAbsoluteApiUrl(response.data?.imageUrl ?? '');
      if (!imageUrl) throw new Error('Upload succeeded but no image URL was returned.');

      setProfileImageUrl(imageUrl);
      setProfileImageFile(null);
    } catch (error) {
      setProfileImageError(
        error.response?.data?.message
        || error.response?.data?.detail
        || error.response?.data?.title
        || 'Failed to upload profile image.'
      );
    } finally {
      setProfileImageLoading(false);
    }
  };

  const handleOpenBoard = (boardId) => {
    navigate(getBoardDetailPath(boardId));
  };

  const handleToggleBoardMenu = (event, boardId) => {
    event.stopPropagation();
    setMenuBoardId((prev) => (prev === boardId ? null : boardId));
  };

  const handleOpenDeleteModal = (event, board) => {
    event.stopPropagation();
    setMenuBoardId(null);
    setDeleteError('');
    setDeleteConfirmText('');
    setBoardToDelete(board);
  };

  const handleCloseDeleteModal = () => {
    if (deleteLoading) return;
    setDeleteError('');
    setDeleteConfirmText('');
    setBoardToDelete(null);
  };

  const handleDeleteBoard = async () => {
    if (!boardToDelete) return;

    const typedName = deleteConfirmText.trim();
    const boardName = (boardToDelete.name ?? '').trim();
    if (typedName !== boardName) {
      setDeleteError('Board name does not match. Please type it exactly.');
      return;
    }

    setDeleteError('');
    setDeleteLoading(true);
    try {
      await api.delete(`/api/boards/${boardToDelete.id}/`);
      setBoards((prevBoards) => prevBoards.filter((board) => board.id !== boardToDelete.id));
      handleCloseDeleteModal();
    } catch (error) {
      setDeleteError(
        error.response?.data?.message
        || error.response?.data?.detail
        || error.response?.data?.title
        || 'Failed to delete board'
      );
    } finally {
      setDeleteLoading(false);
    }
  };

  const handleCreateBoard = async (event) => {
    event.preventDefault();
    setCreateError('');

    const trimmedName = newBoardName.trim();

    if (!trimmedName) {
      setCreateError('Board name is required.');
      return;
    }

    if (trimmedName.length < 2) {
      setCreateError('Board name must be at least 2 characters.');
      return;
    }

    if (trimmedName.length > 50) {
      setCreateError('Board name cannot exceed 50 characters.');
      return;
    }

    if (!/^[a-zA-Z0-9]+( [a-zA-Z0-9]+)*$/.test(trimmedName)) {
      setCreateError('Use letters and numbers with single spaces between words.');
      return;
    }

    setCreateLoading(true);
    try {
      const response = await api.post('/api/boards/', { boardName: trimmedName });
      const createdBoard = response.data ?? {};

      setBoards((prevBoards) => [
        {
          id: createdBoard.id,
          name: createdBoard.name ?? trimmedName,
          role: createdBoard.role ?? 'Owner',
        },
        ...prevBoards,
      ]);
      setNewBoardName('');
    } catch (error) {
      setCreateError(
        error.response?.data?.message
        || error.response?.data?.detail
        || error.response?.data?.title
        || 'Failed to create board'
      );
    } finally {
      setCreateLoading(false);
    }
  };

  const avatarLabelSource = userGreetingName || userDisplayName || userName || 'User';
  const avatarInitial = avatarLabelSource.charAt(0).toUpperCase();

  return (
    <div className="min-h-screen bg-gradient-to-br from-green-50 via-emerald-50 to-teal-50 p-6 md:p-10">
      <div className="max-w-5xl mx-auto">
        <div className="flex items-start justify-between gap-4 mb-8">
          <div className="flex items-start gap-3">
            {profileImageUrl ? (
              <img
                src={profileImageUrl}
                alt="User profile"
                className="mt-1 h-12 w-12 shrink-0 rounded-full border border-green-100 object-cover shadow-sm"
              />
            ) : (
              <div className="mt-1 flex h-12 w-12 shrink-0 items-center justify-center rounded-full bg-gradient-to-br from-emerald-500 to-green-600 text-base font-semibold text-white shadow-sm">
                {avatarInitial}
              </div>
            )}

            <div>
              <h1 className="text-2xl font-bold text-green-800">Dashboard</h1>
              <p className="text-lg font-medium text-green-700">
                {userGreetingName ? `Hello ${userGreetingName} 👋` : 'Hello 👋'}
              </p>
              <p className="text-sm text-green-500">Your boards</p>
            </div>
          </div>

          <div className="relative">
            <button
              type="button"
              onClick={handleToggleAccountMenu}
              className="flex items-center gap-2 rounded-xl border border-green-200 bg-white px-3 py-2 text-sm font-semibold text-green-700 shadow-sm transition hover:border-green-300"
            >
              Account
              <span aria-hidden="true">▾</span>
            </button>

            {isAccountMenuOpen && (
              <div className="absolute right-0 top-12 z-30 w-44 rounded-xl border border-green-100 bg-white p-1.5 shadow-lg">
                <button
                  type="button"
                  onClick={handleOpenSettings}
                  className="w-full rounded-lg px-3 py-2 text-left text-sm font-medium text-slate-700 hover:bg-green-50"
                >
                  Settings
                </button>
                <button
                  type="button"
                  onClick={handleLogout}
                  className="w-full rounded-lg px-3 py-2 text-left text-sm font-medium text-red-600 hover:bg-red-50"
                >
                  Log out
                </button>
              </div>
            )}
          </div>
        </div>

        {loading && (
          <div className="bg-white/80 border border-green-100 rounded-2xl p-8 text-center text-green-700 shadow-sm">
            Loading boards…
          </div>
        )}

        {!loading && !apiError && (
          <div className="mb-4 bg-white/80 border border-green-100 rounded-2xl p-4 shadow-sm">
            <form onSubmit={handleCreateBoard} className="flex flex-col gap-2 sm:flex-row sm:items-center">
              <input
                type="text"
                value={newBoardName}
                onChange={(e) => setNewBoardName(e.target.value)}
                placeholder="New board name"
                className="w-full sm:flex-1 rounded-xl border border-green-200 px-4 py-2.5 text-sm text-green-900 bg-green-50/60 placeholder-green-300 focus:outline-none focus:ring-2 focus:border-green-400 focus:ring-green-200"
                maxLength={50}
              />
              <button
                type="submit"
                disabled={createLoading}
                className="rounded-xl bg-gradient-to-r from-green-500 to-emerald-600 hover:from-green-600 hover:to-emerald-700 text-white font-semibold px-4 py-2.5 text-sm disabled:opacity-50 disabled:cursor-not-allowed"
              >
                {createLoading ? 'Creating…' : 'Add board'}
              </button>
            </form>

            {createError && (
              <p className="mt-2 text-xs text-red-600 bg-red-50 border border-red-100 rounded-lg px-3 py-2">
                {createError}
              </p>
            )}
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
              <div
                key={board.id}
                className="relative bg-white border border-green-100 hover:border-green-300 rounded-2xl p-5 shadow-sm hover:shadow-md transition"
              >
                <button
                  type="button"
                  onClick={() => handleOpenBoard(board.id)}
                  className="w-full pr-10 text-left"
                >
                  <p className="text-lg font-semibold text-green-800 mb-1 line-clamp-1">{board.name}</p>
                  <p className="text-xs text-green-500">Role: {board.role}</p>
                </button>

                <button
                  type="button"
                  onClick={(event) => handleToggleBoardMenu(event, board.id)}
                  className="absolute right-3 top-3 h-8 w-8 rounded-lg border border-green-200 bg-white text-green-700 hover:bg-green-50"
                  aria-label={`Open board actions for ${board.name}`}
                  title="Board actions"
                >
                  ⋯
                </button>

                {menuBoardId === board.id && (
                  <div className="absolute right-3 top-12 z-20 w-40 rounded-xl border border-red-100 bg-white p-1.5 shadow-lg">
                    <button
                      type="button"
                      onClick={(event) => handleOpenDeleteModal(event, board)}
                      className="w-full rounded-lg px-3 py-2 text-left text-sm font-medium text-red-600 hover:bg-red-50"
                    >
                      Delete board
                    </button>
                  </div>
                )}
              </div>
            ))}
          </div>
        )}

        {boardToDelete && (
          <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/35 p-4">
            <div className="w-full max-w-md rounded-2xl border border-red-100 bg-white p-5 shadow-xl">
              <h2 className="text-lg font-semibold text-red-700">Are you sure?</h2>
              <p className="mt-2 text-sm text-slate-600">
                This will permanently delete <span className="font-semibold">{boardToDelete.name}</span> and all its columns and cards.
              </p>

              <p className="mt-3 text-xs text-slate-500">
                Type the board name to confirm:
              </p>
              <input
                type="text"
                value={deleteConfirmText}
                onChange={(event) => {
                  setDeleteConfirmText(event.target.value);
                  if (deleteError) setDeleteError('');
                }}
                className="mt-1 w-full rounded-xl border border-red-200 px-3 py-2 text-sm focus:border-red-400 focus:outline-none focus:ring-2 focus:ring-red-100"
                placeholder={boardToDelete.name}
                disabled={deleteLoading}
              />

              {deleteError && (
                <p className="mt-2 rounded-lg border border-red-100 bg-red-50 px-3 py-2 text-xs text-red-600">
                  {deleteError}
                </p>
              )}

              <div className="mt-4 flex items-center justify-end gap-2">
                <button
                  type="button"
                  onClick={handleCloseDeleteModal}
                  disabled={deleteLoading}
                  className="rounded-xl border border-slate-200 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-60"
                >
                  Cancel
                </button>
                <button
                  type="button"
                  onClick={handleDeleteBoard}
                  disabled={deleteLoading || deleteConfirmText.trim() !== (boardToDelete.name ?? '').trim()}
                  className="rounded-xl bg-red-600 px-4 py-2 text-sm font-semibold text-white hover:bg-red-700 disabled:cursor-not-allowed disabled:opacity-60"
                >
                  {deleteLoading ? 'Deleting…' : 'Delete board'}
                </button>
              </div>
            </div>
          </div>
        )}

        {isSettingsModalOpen && (
          <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/35 p-4">
            <div className="w-full max-w-md rounded-2xl border border-green-100 bg-white p-5 shadow-xl">
              <h2 className="text-lg font-semibold text-green-800">Settings</h2>
              <p className="mt-2 text-sm text-slate-600">Profile settings page is coming soon.</p>

              <div className="mt-4 space-y-2 rounded-xl border border-green-100 bg-green-50/40 p-3 text-sm text-slate-700">
                <p><span className="font-semibold">Display name:</span> {userDisplayName || 'Not set'}</p>
                <p><span className="font-semibold">Username:</span> {userName || 'Not set'}</p>
              </div>

              <div className="mt-4 rounded-xl border border-green-100 bg-white p-3">
                <p className="text-sm font-semibold text-green-800">Profile picture</p>
                <p className="mt-1 text-xs text-slate-500">Upload JPG, PNG, GIF, or WEBP (max 2 MB).</p>

                <div className="mt-3 flex items-center gap-3">
                  {profileImageUrl ? (
                    <img
                      src={profileImageUrl}
                      alt="Current profile"
                      className="h-14 w-14 rounded-full border border-green-100 object-cover"
                    />
                  ) : (
                    <div className="flex h-14 w-14 items-center justify-center rounded-full bg-green-100 text-base font-semibold text-green-700">
                      {avatarInitial}
                    </div>
                  )}

                  <input
                    type="file"
                    accept="image/png,image/jpeg,image/gif,image/webp"
                    onChange={handleProfileImageSelection}
                    disabled={profileImageLoading}
                    className="block w-full text-xs text-slate-600 file:mr-3 file:rounded-lg file:border-0 file:bg-emerald-600 file:px-3 file:py-1.5 file:text-xs file:font-semibold file:text-white hover:file:bg-emerald-700"
                  />
                </div>

                {profileImageError && (
                  <p className="mt-2 rounded-lg border border-red-100 bg-red-50 px-2 py-1.5 text-xs text-red-600">
                    {profileImageError}
                  </p>
                )}

                <div className="mt-3 flex justify-end">
                  <button
                    type="button"
                    onClick={handleUploadProfileImage}
                    disabled={profileImageLoading || !profileImageFile}
                    className="rounded-xl bg-emerald-600 px-3 py-1.5 text-xs font-semibold text-white hover:bg-emerald-700 disabled:cursor-not-allowed disabled:opacity-60"
                  >
                    {profileImageLoading ? 'Uploading…' : 'Save picture'}
                  </button>
                </div>
              </div>

              <div className="mt-4 flex justify-end">
                <button
                  type="button"
                  onClick={handleCloseSettings}
                  className="rounded-xl border border-green-200 bg-white px-4 py-2 text-sm font-semibold text-green-700 hover:bg-green-50"
                >
                  Close
                </button>
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

export default DashboardPage;