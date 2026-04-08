import { useEffect, useState } from 'react';
import Modal from 'react-modal';

Modal.setAppElement('#root');

export default function InviteUserModal({ isOpen, onClose, onInvite }) {
  const [identifier, setIdentifier] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [successMessage, setSuccessMessage] = useState('');
  const [errorMessage, setErrorMessage] = useState('');

  useEffect(() => {
    if (!isOpen) {
      setIdentifier('');
      setIsSubmitting(false);
      setSuccessMessage('');
      setErrorMessage('');
    }
  }, [isOpen]);

  const handleInvite = async (event) => {
    event.preventDefault();

    const trimmedIdentifier = identifier.trim();
    if (!trimmedIdentifier) {
      setErrorMessage('Email or username is required.');
      return;
    }

    setIsSubmitting(true);
    setErrorMessage('');
    setSuccessMessage('');

    try {
      await onInvite(trimmedIdentifier);
      setSuccessMessage('Invitation sent successfully.');
      setIdentifier('');
    } catch (error) {
      setErrorMessage(error.message || 'Failed to send invitation.');
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <Modal
      isOpen={isOpen}
      onRequestClose={onClose}
      shouldCloseOnEsc
      shouldCloseOnOverlayClick
      className="w-full max-w-md rounded-2xl border border-green-200 bg-white p-5 shadow-xl outline-none"
      overlayClassName="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4"
      contentLabel="Invite user to board"
    >
      <div className="mb-3 flex items-start justify-between gap-3">
        <h2 className="text-lg font-semibold text-green-800">Invite teammate</h2>
        <button
          type="button"
          onClick={onClose}
          className="rounded-md border border-green-200 px-2 py-1 text-xs text-green-700 hover:bg-green-50"
          aria-label="Close invite dialog"
        >
          ✕
        </button>
      </div>

      <form onSubmit={handleInvite} className="space-y-3">
        <label className="block text-xs font-semibold uppercase tracking-wide text-green-700" htmlFor="invite-email">
          Email or username
        </label>

        <input
          id="invite-email"
          type="text"
          value={identifier}
          onChange={(event) => setIdentifier(event.target.value)}
          placeholder="teammate@example.com or teammate123"
          className="w-full rounded-md border border-green-200 px-3 py-2 text-sm focus:border-emerald-400 focus:outline-none"
          required
          disabled={isSubmitting}
          autoFocus
        />

        {successMessage && (
          <p className="rounded-md border border-emerald-200 bg-emerald-50 px-3 py-2 text-xs text-emerald-700">{successMessage}</p>
        )}

        {errorMessage && (
          <p className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700">{errorMessage}</p>
        )}

        <div className="flex justify-end gap-2 pt-1">
          <button
            type="button"
            onClick={onClose}
            disabled={isSubmitting}
            className="rounded-md border border-green-200 bg-white px-3 py-2 text-xs font-semibold text-green-700 hover:bg-green-50 disabled:cursor-not-allowed disabled:opacity-60"
          >
            Cancel
          </button>
          <button
            type="submit"
            disabled={isSubmitting}
            className="rounded-md bg-emerald-600 px-3 py-2 text-xs font-semibold text-white hover:bg-emerald-700 disabled:cursor-not-allowed disabled:opacity-60"
          >
            {isSubmitting ? 'Inviting...' : 'Invite'}
          </button>
        </div>
      </form>
    </Modal>
  );
}
