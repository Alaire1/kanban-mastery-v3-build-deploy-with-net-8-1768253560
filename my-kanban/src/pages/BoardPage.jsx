import { Link, useParams } from 'react-router-dom';
import { useState } from 'react';
import {
  closestCorners,
  DndContext,
  pointerWithin,
} from '@dnd-kit/core';
import { ROUTES } from '../constants/routes';
import BoardColumn from '../components/board/BoardColumn';
import InviteUserModal from '../components/board/InviteUserModal';
import useBoardData from '../hooks/useBoardData';
import useBoardDragDrop from '../hooks/useBoardDragDrop';
import useColumnsPagination from '../hooks/useColumnsPagination';
import api from '../services/api';
import { normalizeCard } from './board/boardUtils';

const getMemberDisplayLabel = (member) => (
  member.displayName
  || member.email
  || member.userName
  || (member.userId?.includes('@') ? member.userId : null)
  || 'Board member'
);

function BoardPage() {
  const COLUMNS_PER_VIEW = 4;
  const { boardId } = useParams();
  const [isInviteModalOpen, setIsInviteModalOpen] = useState(false);
  const [isCreateColumnVisible, setIsCreateColumnVisible] = useState(false);
  const [isDeleteModeEnabled, setIsDeleteModeEnabled] = useState(false);
  const [newColumnName, setNewColumnName] = useState('');
  const [isCreatingColumn, setIsCreatingColumn] = useState(false);
  const [createColumnError, setCreateColumnError] = useState('');
  const { board, columns, setColumns, loading, apiError } = useBoardData(boardId);

  const members = (board?.members ?? board?.Members ?? [])
    .map((member) => {
      if (typeof member === 'string') return null;

      const userId = member.userId ?? member.UserId;
      if (!userId) return null;

      return {
        userId,
        userName: member.userName ?? member.UserName ?? null,
        displayName: member.displayName ?? member.DisplayName ?? null,
        email: member.email ?? member.Email ?? null,
        profileImageUrl: member.profileImageUrl ?? member.ProfileImageUrl ?? null,
      };
    })
    .filter(Boolean);

  const addCardToColumn = (columnId, cardData) => {
    const normalizedCard = normalizeCard(cardData);

    setColumns((prevColumns) =>
      prevColumns.map((column) =>
        column.id === columnId
          ? {
              ...column,
              cards: [...column.cards, { ...normalizedCard, columnId }],
            }
          : column
      )
    );
  };

  const handleCreateCard = async (columnId, title, description) => {
    try {
      const response = await api.post(`/api/boards/${boardId}/cards`, {
        title,
        description,
        columnId,
      });
      const createdCard = response.data?.card ?? response.data;
      addCardToColumn(columnId, createdCard);
    } catch (error) {
      throw new Error(error.response?.data?.message || 'Could not create card. Please try again.');
    }
  };

  const handleCreateColumn = async (event) => {
    event.preventDefault();

    const trimmedName = newColumnName.trim();
    if (!trimmedName) {
      setCreateColumnError('Column name is required.');
      return;
    }

    const maxPosition = columns.reduce((max, column) => {
      const position = Number(column.position ?? column.Position ?? 0);
      return Number.isNaN(position) ? max : Math.max(max, position);
    }, -1);

    setCreateColumnError('');
    setIsCreatingColumn(true);

    try {
      const response = await api.post(`/api/boards/${boardId}/columns`, {
        name: trimmedName,
        position: maxPosition + 1,
      });

      const created = response.data ?? {};
      const normalizedCreatedColumn = {
        id: created.id ?? created.Id,
        name: created.name ?? created.Name ?? trimmedName,
        position: created.position ?? created.Position ?? maxPosition + 1,
        cards: [],
      };

      setColumns((prevColumns) =>
        [...prevColumns, normalizedCreatedColumn].sort((a, b) => (a.position ?? 0) - (b.position ?? 0))
      );

      setNewColumnName('');
      setIsCreateColumnVisible(false);
    } catch (error) {
      setCreateColumnError(error.response?.data?.message || 'Could not create column.');
    } finally {
      setIsCreatingColumn(false);
    }
  };

  const handleDeleteCard = async (cardId) => {
    const previousColumns = columns;

    setColumns((prevColumns) =>
      prevColumns.map((column) => ({
        ...column,
        cards: column.cards.filter((card) => card.id !== cardId),
      }))
    );

    try {
      await api.delete(`/api/boards/${boardId}/cards/${cardId}`);
    } catch (error) {
      setColumns(previousColumns);
      throw new Error(error.response?.data?.message || 'Could not delete the card.');
    }
  };

  const handleDeleteColumn = async (columnId) => {
    const columnToDelete = columns.find((column) => column.id === columnId);
    if (!columnToDelete) {
      throw new Error('Column not found.');
    }

    if ((columnToDelete.cards?.length ?? 0) > 0) {
      const message = 'Cannot delete a column that still has cards. Remove or move the cards first.';
      window.alert(message);
      throw new Error(message);
    }

    try {
      await api.delete(`/api/boards/${boardId}/columns/${columnId}`);
      setColumns((prevColumns) => prevColumns.filter((column) => column.id !== columnId));
    } catch (error) {
      throw new Error(error.response?.data?.message || 'Could not delete the column.');
    }
  };

  const handleInviteUser = async (identifier) => {
    try {
      const trimmedIdentifier = identifier.trim();
      const isEmail = /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(trimmedIdentifier);

      await api.post(`/api/boards/${boardId}/members`,
        isEmail
          ? { email: trimmedIdentifier }
          : { userName: trimmedIdentifier });
    } catch (error) {
      throw new Error(error.response?.data?.message || 'Could not send invitation. Please try again.');
    }
  };

  const handleAssignCard = async (cardId, userId) => {
    const selectedMember = members.find((member) => member.userId === userId);
    if (!selectedMember) {
      throw new Error('Member not found for assignment.');
    }

    const previousColumns = columns;

    setColumns((prevColumns) =>
      prevColumns.map((column) => ({
        ...column,
        cards: column.cards.map((card) => {
          if (card.id !== cardId) return card;

          return normalizeCard({
            ...card,
            assignedUserId: selectedMember.userId,
            assigneeDisplayName: selectedMember.displayName,
            assigneeUserName: selectedMember.userName,
            assigneeAvatarUrl: selectedMember.profileImageUrl,
          });
        }),
      }))
    );

    try {
      await api.put(`/api/boards/${boardId}/cards/${cardId}/assign`, { userId });
    } catch (error) {
      setColumns(previousColumns);
      throw new Error(error.response?.data?.message || 'Could not assign this card.');
    }
  };

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

  const handleToggleDeleteMode = () => {
    if (!isDeleteModeEnabled && isDragModeEnabled) {
      handleToggleDragMode();
    }
    setIsDeleteModeEnabled((prev) => !prev);
  };

  const handleToggleDragModeSafe = () => {
    if (!isDragModeEnabled && isDeleteModeEnabled) {
      setIsDeleteModeEnabled(false);
    }
    handleToggleDragMode();
  };

  const {
    visibleColumns,
    canGoPrev,
    canGoNext,
    handlePrevColumns,
    handleNextColumns,
  } = useColumnsPagination(columns, COLUMNS_PER_VIEW);

  const collisionDetectionStrategy = (args) => {
    const pointerCollisions = pointerWithin(args);
    if (pointerCollisions.length > 0) {
      return pointerCollisions;
    }

    return closestCorners(args);
  };

  return (
    <div className="min-h-screen bg-gradient-to-br from-green-50 via-emerald-50 to-teal-50 p-6 md:p-10">
      <div className="max-w-4xl mx-auto bg-white/80 border border-green-100 rounded-2xl p-6 shadow-sm">
        <div className="flex items-center justify-between gap-3">
          <Link to={ROUTES.DASHBOARD} className="text-sm text-green-700 hover:underline">← Back to dashboard</Link>
          <button
            type="button"
            onClick={() => setIsInviteModalOpen(true)}
            className="rounded-lg border border-green-200 bg-white px-3 py-1.5 text-xs font-semibold text-green-700 hover:bg-green-100"
          >
            Invite
          </button>
        </div>

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

                <div className="flex items-center gap-2">
                  <button
                    type="button"
                    onClick={handleToggleDeleteMode}
                    aria-pressed={isDeleteModeEnabled}
                    className={`rounded-lg border px-3 py-1.5 text-xs font-semibold transition-colors ${
                      isDeleteModeEnabled
                        ? 'border-red-300 bg-red-100 text-red-800 hover:bg-red-200'
                        : 'border-green-200 bg-white text-green-700 hover:bg-green-100'
                    }`}
                  >
                    {isDeleteModeEnabled ? 'Disable delete mode' : 'Enable delete mode'}
                  </button>

                  <button
                    type="button"
                    onClick={handleToggleDragModeSafe}
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
              </div>

              {(isDeleteModeEnabled || isDragModeEnabled) && (
                <p className="mb-3 text-xs text-green-600">
                  {isDeleteModeEnabled
                    ? 'Delete mode is ON. X buttons are visible on cards and columns.'
                    : 'Move mode is ON. You can drag cards and columns.'}
                </p>
              )}

              {columns.length > 0 ? (
                <DndContext
                  sensors={sensors}
                  collisionDetection={collisionDetectionStrategy}
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
                        <BoardColumn
                          key={column.id}
                          column={column}
                          isDragModeEnabled={isDragModeEnabled}
                          isDeleteModeEnabled={isDeleteModeEnabled}
                          onCreateCard={handleCreateCard}
                          members={members.map((member) => ({
                            ...member,
                            displayLabel: getMemberDisplayLabel(member),
                          }))}
                          onAssignCard={handleAssignCard}
                          onDeleteCard={handleDeleteCard}
                          onDeleteColumn={handleDeleteColumn}
                        />
                      ))}
                    </div>

                    <div className="relative w-12 shrink-0">
                      <button
                        type="button"
                        onClick={() => setIsCreateColumnVisible((prev) => !prev)}
                        className="h-full min-h-[180px] w-full rounded-xl border border-dashed border-emerald-300 bg-emerald-50 text-3xl font-light leading-none text-emerald-700 hover:bg-emerald-100 flex items-center justify-center"
                        aria-label="Add new column"
                        title="Add new column"
                      >
                        +
                      </button>

                      {isCreateColumnVisible && (
                        <form
                          onSubmit={handleCreateColumn}
                          className="absolute right-0 top-0 z-30 w-56 space-y-2 rounded-lg border border-emerald-200 bg-white p-2 shadow-xl"
                        >
                          <input
                            type="text"
                            value={newColumnName}
                            onChange={(event) => {
                              setNewColumnName(event.target.value);
                              if (createColumnError) setCreateColumnError('');
                            }}
                            placeholder="Column name"
                            className="w-full rounded-md border border-green-200 px-2 py-1.5 text-xs focus:border-emerald-400 focus:outline-none"
                            autoFocus
                            disabled={isCreatingColumn}
                            maxLength={50}
                          />
                          <div className="flex items-center gap-1">
                            <button
                              type="submit"
                              disabled={isCreatingColumn}
                              className="rounded-md bg-emerald-600 px-2 py-1 text-[11px] font-semibold text-white hover:bg-emerald-700 disabled:opacity-60"
                            >
                              {isCreatingColumn ? '...' : 'Add'}
                            </button>
                            <button
                              type="button"
                              disabled={isCreatingColumn}
                              onClick={() => {
                                setIsCreateColumnVisible(false);
                                setNewColumnName('');
                                setCreateColumnError('');
                              }}
                              className="rounded-md border border-green-200 bg-white px-2 py-1 text-[11px] font-semibold text-green-700 hover:bg-green-50"
                            >
                              Cancel
                            </button>
                          </div>
                        </form>
                      )}
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
                <div className="space-y-2">
                  <p className="text-sm text-green-500">No columns found.</p>
                  <button
                    type="button"
                    onClick={() => setIsCreateColumnVisible(true)}
                    className="rounded-md border border-dashed border-emerald-300 bg-emerald-50 px-3 py-1.5 text-xs font-semibold text-emerald-800 hover:bg-emerald-100"
                  >
                    + Add first column
                  </button>

                  {isCreateColumnVisible && (
                    <form onSubmit={handleCreateColumn} className="max-w-xs space-y-2 rounded-lg border border-emerald-200 bg-white p-2">
                      <input
                        type="text"
                        value={newColumnName}
                        onChange={(event) => {
                          setNewColumnName(event.target.value);
                          if (createColumnError) setCreateColumnError('');
                        }}
                        placeholder="Column name"
                        className="w-full rounded-md border border-green-200 px-2 py-1.5 text-sm focus:border-emerald-400 focus:outline-none"
                        autoFocus
                        disabled={isCreatingColumn}
                        maxLength={50}
                      />
                      <div className="flex items-center gap-2">
                        <button
                          type="submit"
                          disabled={isCreatingColumn}
                          className="rounded-md bg-emerald-600 px-3 py-1.5 text-xs font-semibold text-white hover:bg-emerald-700 disabled:opacity-60"
                        >
                          {isCreatingColumn ? 'Creating...' : 'Create'}
                        </button>
                        <button
                          type="button"
                          disabled={isCreatingColumn}
                          onClick={() => {
                            setIsCreateColumnVisible(false);
                            setNewColumnName('');
                            setCreateColumnError('');
                          }}
                          className="rounded-md border border-green-200 bg-white px-3 py-1.5 text-xs font-semibold text-green-700 hover:bg-green-50"
                        >
                          Cancel
                        </button>
                      </div>
                    </form>
                  )}
                </div>
              )}

              {createColumnError && (
                <p className="mt-3 rounded-md border border-red-200 bg-red-50 px-2 py-1 text-xs text-red-700">{createColumnError}</p>
              )}
            </div>

            <InviteUserModal
              isOpen={isInviteModalOpen}
              onClose={() => setIsInviteModalOpen(false)}
              onInvite={handleInviteUser}
            />
          </>
        )}
      </div>
    </div>
  );
}

export default BoardPage;
