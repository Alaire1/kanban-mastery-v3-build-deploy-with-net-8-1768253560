import { Link, useParams } from 'react-router-dom';
import { useState } from 'react';
import {
  closestCorners,
  DndContext,
  DragOverlay,
  pointerWithin,
} from '@dnd-kit/core';
import { ROUTES } from '../constants/routes';
import BoardColumn from '../components/board/BoardColumn';
import InviteUserModal from '../components/board/InviteUserModal';
import useBoardData from '../hooks/useBoardData';
import useBoardDragDrop from '../hooks/useBoardDragDrop';
import useColumnsPagination from '../hooks/useColumnsPagination';
import api from '../services/api';
import { normalizeCard, parseCardIdFromDragId, parseColumnIdFromDragId } from './board/boardUtils';

const getMemberDisplayLabel = (member) => (
  member.displayName
  || member.email
  || member.userName
  || (member.userId?.includes('@') ? member.userId : null)
  || 'Board member'
);

const getInitials = (value) => {
  if (!value || typeof value !== 'string') return '??';

  const parts = value.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return '??';
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();

  return `${parts[0][0]}${parts[parts.length - 1][0]}`.toUpperCase();
};

const toAbsoluteMemberAvatarUrl = (value) => {
  if (!value || typeof value !== 'string') return null;
  if (/^https?:\/\//i.test(value)) return value;

  try {
    return new URL(value, api.defaults.baseURL).toString();
  } catch {
    return value;
  }
};

const getCurrentUserIdFromToken = () => {
  try {
    const token = localStorage.getItem('token');
    if (!token) return null;

    const payload = JSON.parse(atob(token.split('.')[1]));
    return payload.sub
      || payload.nameid
      || payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier']
      || null;
  } catch {
    return null;
  }
};

function BoardPage() {
  const COLUMNS_PER_VIEW = 4;
  const { boardId } = useParams();
  const [isInviteModalOpen, setIsInviteModalOpen] = useState(false);
  const [isCreateColumnVisible, setIsCreateColumnVisible] = useState(false);
  const [newColumnName, setNewColumnName] = useState('');
  const [isCreatingColumn, setIsCreatingColumn] = useState(false);
  const [createColumnError, setCreateColumnError] = useState('');
  const { board, columns, setColumns, loading, apiError } = useBoardData(boardId);
  const currentUserRole = String(board?.role ?? board?.Role ?? '').toLowerCase();
  const currentUserId = getCurrentUserIdFromToken();
  const boardOwnerId = board?.ownerId ?? board?.OwnerId ?? null;
  const canInviteMembers = currentUserRole === 'owner' || (currentUserId && boardOwnerId && currentUserId === boardOwnerId);

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
        profileImageUrl: toAbsoluteMemberAvatarUrl(
          member.profileImageUrl
          ?? member.ProfileImageUrl
          ?? member.profilePictureUrl
          ?? member.ProfilePictureUrl
          ?? member.photoUrl
          ?? member.PhotoUrl
          ?? member.avatarUrl
          ?? member.AvatarUrl
          ?? null
        ),
      };
    })
    .filter(Boolean);

  const visibleMemberAvatars = members.slice(0, 3);
  const remainingMembersCount = Math.max(0, members.length - visibleMemberAvatars.length);

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
      const titleError = error.response?.data?.errors?.Title?.[0] || error.response?.data?.errors?.title?.[0];
      throw new Error(titleError || error.response?.data?.message || 'Could not create card. Please try again.');
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
      return;
    }

    try {
      await api.delete(`/api/boards/${boardId}/columns/${columnId}`);
      setColumns((prevColumns) => prevColumns.filter((column) => column.id !== columnId));
    } catch (error) {
      throw new Error(error.response?.data?.message || 'Could not delete the column.');
    }
  };

  const handleUpdateColumn = async (columnId, name) => {
    const trimmedName = name.trim();
    if (!trimmedName) {
      throw new Error('Column name is required.');
    }

    const previousColumns = columns;

    setColumns((prevColumns) =>
      prevColumns.map((column) => (
        column.id === columnId
          ? { ...column, name: trimmedName }
          : column
      ))
    );

    try {
      await api.put(`/api/boards/${boardId}/columns/${columnId}`, { name: trimmedName });
    } catch (error) {
      setColumns(previousColumns);
      throw new Error(error.response?.data?.message || 'Could not update the column.');
    }
  };

  const handleUpdateCard = async (cardId, payload) => {
    const title = payload.title?.trim() ?? '';
    const description = payload.description?.trim() ?? '';

    if (!title) {
      throw new Error('Card title is required.');
    }

    const sourceColumn = columns.find((column) =>
      column.cards.some((card) => card.id === cardId)
    );

    if (!sourceColumn) {
      throw new Error('Card not found.');
    }

    const previousColumns = columns;

    setColumns((prevColumns) =>
      prevColumns.map((column) => ({
        ...column,
        cards: column.cards.map((card) => (
          card.id === cardId
            ? normalizeCard({
                ...card,
                title,
                description: description || null,
              })
            : card
        )),
      }))
    );

    try {
      await api.put(`/api/boards/${boardId}/cards/${cardId}`, {
        title,
        description: description || null,
        columnId: sourceColumn.id,
      });
    } catch (error) {
      setColumns(previousColumns);
      throw new Error(error.response?.data?.message || 'Could not update the card.');
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
    activeDragId,
    activeDragRect,
    actionError,
    handleToggleDragMode,
    handleDragStart,
    handleDragCancel,
    handleDragEnd,
  } = useBoardDragDrop({
    boardId,
    columns,
    setColumns,
    loading,
    apiError,
  });

  const handleToggleDragModeSafe = () => {
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

  const activeCardId = activeDragId ? parseCardIdFromDragId(activeDragId) : null;
  const activeColumnId = activeDragId ? parseColumnIdFromDragId(activeDragId) : null;

  const activeCard = activeCardId === null
    ? null
    : columns
      .flatMap((column) => column.cards)
      .find((card) => card.id === activeCardId);

  const activeColumn = activeColumnId === null
    ? null
    : columns.find((column) => column.id === activeColumnId);

  return (
    <div className="h-screen overflow-hidden bg-gradient-to-br from-green-50 via-emerald-50 to-teal-50 p-6 md:p-10">
      <div className="max-w-[96.6rem] mx-auto h-[calc(100vh-3rem)] md:h-[calc(100vh-5rem)] overflow-hidden bg-white/80 border border-green-100 rounded-2xl p-6 shadow-sm flex flex-col">
        <div className="flex items-center justify-between gap-3">
          <Link to={ROUTES.DASHBOARD} className="text-sm text-green-700 hover:underline">← Back to dashboard</Link>
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
          <div className="flex flex-1 min-h-0 flex-col">
            <div className="mt-3 mb-2 flex items-baseline gap-2">
              <h1 className="text-2xl font-bold text-green-800">{board?.name ?? `Board ${boardId}`}</h1>
              <span className="text-sm font-medium text-green-500">
                ({columns.length} column{columns.length === 1 ? '' : 's'})
              </span>
            </div>

            {actionError && (
              <div className="mt-3 rounded-xl border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700">
                {actionError}
              </div>
            )}

            <div className="-mt-10 flex items-center justify-end gap-2">
              <div className="flex items-center">
                <div className="flex -space-x-2">
                  {visibleMemberAvatars.map((member) => {
                    const memberLabel = getMemberDisplayLabel(member);

                    return (
                      <div
                        key={member.userId}
                        className="h-8 w-8 rounded-full border-2 border-white bg-green-100 text-[10px] font-semibold text-green-800 overflow-hidden flex items-center justify-center shadow-sm"
                        title={memberLabel}
                        aria-label={memberLabel}
                      >
                        {member.profileImageUrl ? (
                          <img
                            src={member.profileImageUrl}
                            alt={memberLabel}
                            className="h-full w-full object-cover"
                          />
                        ) : (
                          <span>{getInitials(memberLabel)}</span>
                        )}
                      </div>
                    );
                  })}
                </div>

                {remainingMembersCount > 0 && (
                  <span
                    className="ml-2 text-xs font-semibold text-green-700"
                    title={`${remainingMembersCount} more member${remainingMembersCount === 1 ? '' : 's'}`}
                    aria-label={`${remainingMembersCount} more members`}
                  >
                    +{remainingMembersCount}
                  </span>
                )}
              </div>

              {canInviteMembers && (
                <button
                  type="button"
                  onClick={() => setIsInviteModalOpen(true)}
                  className="rounded-lg border border-green-200 bg-white px-3 py-1.5 text-xs font-semibold text-green-700 hover:bg-green-100"
                >
                  Invite
                </button>
              )}

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

            <div className="mt-2 flex-1 min-h-0 overflow-hidden rounded-2xl border border-green-100 bg-green-50/60 p-4">
              {columns.length > 0 ? (
                <DndContext
                  sensors={sensors}
                  collisionDetection={collisionDetectionStrategy}
                  onDragStart={handleDragStart}
                  onDragCancel={handleDragCancel}
                  onDragEnd={handleDragEnd}
                >
                  <div className="flex h-full min-h-0 items-center gap-3">
                    <button
                      type="button"
                      onClick={handlePrevColumns}
                      disabled={!canGoPrev}
                      className="h-9 w-9 rounded-full border border-green-200 bg-white text-green-700 disabled:opacity-40 disabled:cursor-not-allowed"
                      aria-label="Show previous columns"
                    >
                      ←
                    </button>

                    <div className="grid h-full min-h-0 flex-1 grid-cols-4 gap-3">
                      {visibleColumns.map((column) => (
                        <BoardColumn
                          key={column.id}
                          column={column}
                          isDragModeEnabled={isDragModeEnabled}
                          onCreateCard={handleCreateCard}
                          members={members.map((member) => ({
                            ...member,
                            displayLabel: getMemberDisplayLabel(member),
                          }))}
                          onAssignCard={handleAssignCard}
                          onDeleteCard={handleDeleteCard}
                          onDeleteColumn={handleDeleteColumn}
                          onUpdateColumn={handleUpdateColumn}
                          onUpdateCard={handleUpdateCard}
                        />
                      ))}
                    </div>

                    <div className="relative w-12 shrink-0 self-stretch">
                      <button
                        type="button"
                        onClick={() => setIsCreateColumnVisible((prev) => !prev)}
                        className="h-full w-full rounded-xl border border-dashed border-emerald-300 bg-emerald-50 text-3xl font-light leading-none text-emerald-700 hover:bg-emerald-100 flex items-center justify-center"
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

                  <DragOverlay zIndex={2000}>
                    {activeCard ? (
                      <article
                        className={`rounded-lg border p-2.5 opacity-100 shadow-2xl ring-2 ring-emerald-200 ${
                          activeCard.isAssigned
                            ? 'border-emerald-200 bg-emerald-50'
                            : 'border-amber-200 bg-amber-50'
                        }`}
                        style={{
                          width: activeDragRect?.width ? `${activeDragRect.width}px` : undefined,
                          minHeight: activeDragRect?.height ? `${activeDragRect.height}px` : undefined,
                        }}
                      >
                        <div className="flex items-start justify-between gap-2">
                          <h3 className="text-sm font-semibold text-slate-800 leading-5 line-clamp-2">
                            {activeCard.title}
                          </h3>
                          <div className={`h-8 w-8 shrink-0 rounded-full border flex items-center justify-center text-[10px] font-semibold ${
                            activeCard.isAssigned
                              ? 'border-emerald-300 bg-emerald-100 text-emerald-800'
                              : 'border-amber-300 bg-amber-100 text-amber-800'
                          }`}>
                            {activeCard.isAssigned && activeCard.assigneeAvatarUrl ? (
                              <img
                                src={activeCard.assigneeAvatarUrl}
                                alt={activeCard.assigneeFallbackName}
                                className="h-full w-full rounded-full object-cover"
                              />
                            ) : (
                              <span>{activeCard.assigneeInitials}</span>
                            )}
                          </div>
                        </div>
                        <p className="mt-1.5 text-xs text-slate-600 leading-4 line-clamp-3">{activeCard.description}</p>
                      </article>
                    ) : activeColumn ? (
                      <div
                        className="rounded-xl border border-emerald-300 bg-white px-3 py-3 opacity-100 shadow-2xl ring-2 ring-emerald-200"
                        style={{
                          width: activeDragRect?.width ? `${activeDragRect.width}px` : undefined,
                          minHeight: activeDragRect?.height ? `${activeDragRect.height}px` : undefined,
                        }}
                      >
                        <p className="text-sm font-medium text-green-800 truncate">{activeColumn.name}</p>
                      </div>
                    ) : null}
                  </DragOverlay>
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
          </div>
        )}
      </div>
    </div>
  );
}

export default BoardPage;
