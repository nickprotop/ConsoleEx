// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A control shown inside a message, below its body.
	/// </summary>
	/// <remarks>
	/// <para>
	/// For something a message carries rather than says: an attached image shown where it was
	/// attached, instead of behind a footer button that opens it elsewhere.
	/// </para>
	/// <para>
	/// It is a sibling control inserted after the message panel, the same shape the collapsed
	/// preview row already uses, so scrolling, layout and removal need nothing new.
	/// </para>
	/// </remarks>
	public partial class ChatTranscriptControl
	{
		#region Attachment

		/// <summary>
		/// Puts a control inside a message, below its body, or removes the one already there.
		/// </summary>
		/// <param name="id">The message.</param>
		/// <param name="control">The control to show, or null to clear.</param>
		/// <remarks>
		/// <para>
		/// The attachment is drawn at the message's width, scrolls with it, is taken out while the
		/// message is collapsed and put back when it expands, and goes when the message does.
		/// </para>
		/// <para>
		/// IT NEVER TAKES FOCUS OR KEYS. A transcript is read by scrolling, and a focusable thing in
		/// the middle of it would be a stop the reader did not ask for. A message that needs a
		/// button has footer actions, which are focusable and already exist.
		/// </para>
		/// <para>
		/// Its height is its own. A control asked to lay out at the message's width reports the
		/// height it needs and the transcript scrolls; capping here would be the transcript
		/// second-guessing a control about its own size. A host that wants a thumbnail builds a
		/// small one.
		/// </para>
		/// </remarks>
		/// <exception cref="ArgumentException">No message has that id.</exception>
		public void SetMessageAttachment(ChatMessageId id, IWindowControl? control)
		{
			var entry = Require(id);

			if (ReferenceEquals(entry.Attachment, control))
				return;

			DetachAttachment(entry);

			entry.Attachment = control;
			if (control == null)
			{
				Invalidate(Invalidation.Relayout);
				return;
			}

			// Only shown while the message is open. A collapsed message is a single summary line,
			// and an image hanging under it would contradict the collapse.
			if (entry.Panel.IsExpanded)
				AttachToTranscript(entry);

			Invalidate(Invalidation.Relayout);
		}

		/// <summary>The control attached to a message, or null when it has none.</summary>
		/// <param name="id">The message.</param>
		/// <exception cref="ArgumentException">No message has that id.</exception>
		public IWindowControl? GetMessageAttachment(ChatMessageId id) => Require(id).Attachment;

		/// <summary>
		/// Puts the attachment into the transcript, immediately after the message panel.
		/// </summary>
		/// <remarks>
		/// Before the peek row and the footer rows, so a message reads body, attachment, then its
		/// chrome. The peek row cannot be present at the same time (it exists only while collapsed,
		/// and this runs only while expanded), so inserting right after the panel is unambiguous.
		/// </remarks>
		private void AttachToTranscript(MessageEntry entry)
		{
			if (entry.Attachment == null || entry.AttachmentShown)
				return;

			int panelIndex = IndexOfPanel(entry.Panel);
			if (panelIndex < 0)
				AddControl(entry.Attachment);   // defensive: append rather than lose it
			else
				InsertControl(panelIndex + 1, entry.Attachment);

			entry.AttachmentShown = true;
		}

		/// <summary>
		/// Takes the attachment out of the transcript, keeping the control itself.
		/// </summary>
		/// <remarks>
		/// The instance is kept on the entry so a host's reference stays valid across a collapse,
		/// and so an image is not decoded again every time the message is reopened.
		/// </remarks>
		private void DetachAttachment(MessageEntry entry)
		{
			if (entry.Attachment == null || !entry.AttachmentShown)
				return;

			RemoveControl(entry.Attachment);
			entry.AttachmentShown = false;
		}

		/// <summary>Shows or hides the attachment as the message opens and closes.</summary>
		private void OnMessageExpandedChangedForAttachment(MessageEntry entry, bool expanded)
		{
			if (expanded)
				AttachToTranscript(entry);
			else
				DetachAttachment(entry);
		}

		/// <summary>Drops a message's attachment entirely, for a message being removed.</summary>
		private void DropAttachment(MessageEntry entry)
		{
			DetachAttachment(entry);
			entry.Attachment = null;
		}

		/// <summary>The attachment currently in the transcript's children. For tests.</summary>
		internal IWindowControl? ShownAttachmentForTest(ChatMessageId id)
		{
			var entry = Require(id);
			return entry.AttachmentShown ? entry.Attachment : null;
		}

		#endregion
	}
}
