// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text;
using SharpConsoleUI.Themes;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A chat transcript control that presents an ordered list of role-tagged messages, with
	/// per-message streaming (token append), collapsible verbose roles, thinking indicators and
	/// themed per-role styling.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <see cref="ChatTranscriptControl"/> is an <em>honest composition</em>: it subclasses
	/// <see cref="ScrollablePanelControl"/> and hosts one real <see cref="CollapsiblePanel"/> per
	/// message, each containing a <see cref="MarkupControl"/> body (or a started
	/// <see cref="SpinnerControl"/> while a message is "thinking"). Scrolling, wrapping, markdown
	/// rendering, selection and the collapse animation are all provided by those child controls —
	/// none of it is re-implemented here.
	/// </para>
	/// <para>
	/// Auto-scroll stickiness (following the newest content while pinned to the bottom) is inherited
	/// from the base <see cref="ScrollablePanelControl.AutoScroll"/> flag, which this control enables
	/// by default.
	/// </para>
	/// </remarks>
	public class ChatTranscriptControl : ScrollablePanelControl
	{
		#region Message tracking

		/// <summary>
		/// Internal per-message state. Mutable where streaming requires it (thinking flag and the
		/// active spinner/body references are swapped when the first token arrives).
		/// </summary>
		private sealed class MessageEntry
		{
			public MessageEntry(ChatMessageId id, ChatRole role, string? author,
				CollapsiblePanel panel, MarkupControl? body, SpinnerControl? spinner, bool thinking)
			{
				Id = id;
				Role = role;
				Author = author;
				Panel = panel;
				Body = body;
				Spinner = spinner;
				Thinking = thinking;
			}

			public ChatMessageId Id { get; }
			public ChatRole Role { get; }
			public string? Author { get; }
			public StringBuilder Buffer { get; } = new();
			public CollapsiblePanel Panel { get; }

			/// <summary>The markdown/text body. Null only while a thinking message shows its spinner.</summary>
			public MarkupControl? Body { get; set; }

			/// <summary>The spinner shown while thinking; cleared (Stopped + removed) on first token.</summary>
			public SpinnerControl? Spinner { get; set; }

			public bool Thinking { get; set; }
		}

		private readonly List<MessageEntry> _order = new();
		private readonly Dictionary<ChatMessageId, MessageEntry> _byId = new();
		// Starts at 1 so that default(ChatMessageId) (Value = 0) is never a valid, live id.
		private int _nextId = 1;

		#endregion

		#region Role styles

		private readonly Dictionary<ChatRole, ChatRoleStyle> _roleStyles = new();

		#endregion

		#region Polish properties

		private bool _animateMessages = true;
		private bool _messagesSelectable = true;
		private SpinnerStyle _thinkingSpinnerStyle = SpinnerStyle.Dots;

		/// <summary>
		/// Gets or sets the control-level baseline controlling whether message bodies in this transcript
		/// can be selected (and, with copy enabled, copied). Defaults to <c>true</c>. Each message resolves
		/// its selectability as <c>role.Selectable ?? MessagesSelectable</c>: a role whose
		/// <see cref="ChatRoleStyle.Selectable"/> is <c>null</c> inherits this baseline, while a role that
		/// sets it to <c>true</c> or <c>false</c> overrides the baseline in either direction. Changing this
		/// value updates existing message bodies (respecting each role's override) as well as any added
		/// afterwards.
		/// </summary>
		public bool MessagesSelectable
		{
			get => _messagesSelectable;
			set
			{
				if (!SetProperty(ref _messagesSelectable, value))
					return;

				foreach (var entry in _order)
				{
					if (entry.Body != null)
						entry.Body.EnableSelection = GetRoleStyle(entry.Role).Selectable ?? value;
				}
			}
		}

		/// <summary>
		/// Gets or sets whether message panels animate their expand/collapse (height tween). When
		/// <c>true</c> (the default) collapsible message panels use
		/// <see cref="CollapsibleAnimationMode.Height"/>; otherwise <see cref="CollapsibleAnimationMode.None"/>.
		/// Only affects messages added after the value changes.
		/// </summary>
		public bool AnimateMessages
		{
			get => _animateMessages;
			set => SetProperty(ref _animateMessages, value);
		}

		/// <summary>
		/// Gets or sets the spinner style used for thinking messages. Defaults to
		/// <see cref="SpinnerStyle.Dots"/>. Only affects thinking messages added after the change.
		/// </summary>
		public SpinnerStyle ThinkingSpinnerStyle
		{
			get => _thinkingSpinnerStyle;
			set => SetProperty(ref _thinkingSpinnerStyle, value);
		}

		#endregion

		/// <summary>
		/// Initialises a new <see cref="ChatTranscriptControl"/> with themed per-role defaults and
		/// auto-scroll enabled (so the transcript stays pinned to the newest message while at the bottom).
		/// </summary>
		public ChatTranscriptControl()
		{
			AutoScroll = true;
			SeedDefaultRoleStyles();
		}

		private void SeedDefaultRoleStyles()
		{
			_roleStyles[ChatRole.User] = new ChatRoleStyle
			{
				ColorRole = ColorRole.Primary,
				HeaderStyle = CollapsibleHeaderStyle.Rounded,
				Header = static (_, author) => author ?? "You"
			};

			_roleStyles[ChatRole.Assistant] = new ChatRoleStyle
			{
				ColorRole = ColorRole.Default,
				HeaderStyle = CollapsibleHeaderStyle.Borderless,
				Header = static (_, author) => author ?? "Assistant"
			};

			_roleStyles[ChatRole.System] = new ChatRoleStyle
			{
				ColorRole = ColorRole.Info,
				HeaderStyle = CollapsibleHeaderStyle.Borderless,
				Collapsible = true,
				StartCollapsed = true,
				Header = static (_, author) => author ?? "System"
			};

			_roleStyles[ChatRole.Tool] = new ChatRoleStyle
			{
				ColorRole = ColorRole.Secondary,
				HeaderStyle = CollapsibleHeaderStyle.Borderless,
				Collapsible = true,
				StartCollapsed = true,
				Header = static (_, author) => author ?? "🔧 tool"
			};

			_roleStyles[ChatRole.Error] = new ChatRoleStyle
			{
				ColorRole = ColorRole.Danger,
				HeaderStyle = CollapsibleHeaderStyle.Rounded,
				Header = static (_, author) => author ?? "Error"
			};
		}

		#region Role style API

		/// <summary>
		/// Sets the visual style used for messages of the given role.
		/// </summary>
		/// <remarks>
		/// This affects messages added <em>after</em> the call only; already-added message panels keep
		/// the style they were built with and are not retroactively restyled. To restyle an existing
		/// conversation, set the role styles first and re-add the messages.
		/// </remarks>
		/// <param name="role">The role whose style is being set.</param>
		/// <param name="style">The style to apply. Must not be <c>null</c>.</param>
		public void SetRoleStyle(ChatRole role, ChatRoleStyle style)
		{
			_roleStyles[role] = style ?? throw new ArgumentNullException(nameof(style));
		}

		/// <summary>
		/// Gets the visual style currently associated with the given role. Every role has a themed
		/// default, so this never returns <c>null</c>.
		/// </summary>
		/// <param name="role">The role whose style is requested.</param>
		/// <returns>The <see cref="ChatRoleStyle"/> for the role.</returns>
		public ChatRoleStyle GetRoleStyle(ChatRole role)
		{
			return _roleStyles.TryGetValue(role, out var style) ? style : new ChatRoleStyle();
		}

		#endregion

		#region Message query API

		/// <summary>Gets the ids of all messages currently in the transcript, in display order.</summary>
		public IReadOnlyList<ChatMessageId> MessageIds
		{
			get
			{
				var result = new List<ChatMessageId>(_order.Count);
				foreach (var e in _order)
					result.Add(e.Id);
				return result;
			}
		}

		/// <summary>Gets the role of the message with the given id.</summary>
		/// <param name="id">The message id.</param>
		/// <returns>The message's <see cref="ChatRole"/>.</returns>
		/// <exception cref="KeyNotFoundException">No message with the id exists.</exception>
		public ChatRole GetRole(ChatMessageId id) => Require(id).Role;

		/// <summary>Gets whether the message with the given id is still showing its thinking indicator.</summary>
		/// <param name="id">The message id.</param>
		/// <returns><c>true</c> while the message is thinking (no content yet); otherwise <c>false</c>.</returns>
		/// <exception cref="KeyNotFoundException">No message with the id exists.</exception>
		public bool IsThinking(ChatMessageId id) => Require(id).Thinking;

		#endregion

		#region Message mutation API

		/// <summary>
		/// Adds a message to the transcript and returns its id.
		/// </summary>
		/// <remarks>
		/// This mutates the control's children and MUST be called on the UI thread. When streaming from
		/// a background thread (e.g. an agent producing tokens off-thread), marshal the call via
		/// <c>windowSystem.EnqueueOnUIThread(() =&gt; chat.AddMessage(...))</c> — see CLAUDE.md Rule 13.
		/// </remarks>
		/// <param name="role">The role of the message author.</param>
		/// <param name="content">The initial message content (markdown or plain text per the role style).</param>
		/// <param name="author">An optional author name that overrides the role's default header label.</param>
		/// <param name="thinking">
		/// When <c>true</c>, the message initially shows a spinner (a "thinking" indicator) instead of a
		/// text body. The spinner is cleared automatically on the first <see cref="Append(ChatMessageId, string)"/>
		/// or <see cref="UpdateMessage"/> for the message.
		/// </param>
		/// <returns>The id of the newly added message.</returns>
		public ChatMessageId AddMessage(ChatRole role, string content, string? author = null, bool thinking = false)
		{
			var style = GetRoleStyle(role);
			var id = new ChatMessageId(_nextId++);

			var panel = new CollapsiblePanel
			{
				Title = ComposeHeader(style, role, author),
				ShowHeader = style.ShowHeader,
				HeaderStyle = style.HeaderStyle,
				HeaderAlignment = style.HeaderAlignment,
				Collapsible = style.Collapsible,
				ColorRole = style.ColorRole,
				Margin = style.Margin
			};

			if (style.BorderColor.HasValue)
				panel.BorderColor = style.BorderColor.Value;

			if (style.Background.HasValue)
				panel.BackgroundColor = style.Background.Value;

			panel.AnimationMode = (AnimateMessages && style.Collapsible)
				? CollapsibleAnimationMode.Height
				: CollapsibleAnimationMode.None;

			// A collapsible panel that starts collapsed; otherwise expanded.
			if (style.Collapsible && style.StartCollapsed)
				panel.IsExpanded = false;

			MessageEntry entry;
			if (thinking)
			{
				var spinner = new SpinnerControl { Style = ThinkingSpinnerStyle };
				spinner.Start();
				panel.AddControl(spinner);
				entry = new MessageEntry(id, role, author, panel, body: null, spinner: spinner, thinking: true);
			}
			else
			{
				var body = new MarkupControl(new List<string>()) { EnableSelection = style.Selectable ?? MessagesSelectable };
				panel.AddControl(body);
				entry = new MessageEntry(id, role, author, panel, body: body, spinner: null, thinking: false);
				entry.Buffer.Append(content);
				RenderBody(entry, style);
			}

			_order.Add(entry);
			_byId[id] = entry;

			AddControl(panel);
			return id;
		}

		/// <summary>
		/// Appends a token/chunk to the most recently added message. Convenience for the common
		/// single-message streaming case.
		/// </summary>
		/// <remarks>
		/// Mutates child-control state and MUST run on the UI thread. Callers streaming tokens from a
		/// background thread MUST marshal via <c>windowSystem.EnqueueOnUIThread(() =&gt; chat.Append(...))</c>
		/// (CLAUDE.md Rule 13).
		/// </remarks>
		/// <param name="token">The text chunk to append.</param>
		/// <exception cref="InvalidOperationException">The transcript is empty.</exception>
		public void Append(string token)
		{
			if (_order.Count == 0)
				throw new InvalidOperationException("Cannot Append: the transcript has no messages.");
			Append(_order[_order.Count - 1].Id, token);
		}

		/// <summary>
		/// Appends a token/chunk to a specific message, growing its body. If the message was
		/// "thinking", the spinner is cleared and replaced by a text body on the first token.
		/// </summary>
		/// <remarks>
		/// Mutates child-control state and MUST run on the UI thread. Callers streaming tokens from a
		/// background thread MUST marshal via <c>windowSystem.EnqueueOnUIThread(() =&gt; chat.Append(id, ...))</c>
		/// (CLAUDE.md Rule 13).
		/// </remarks>
		/// <param name="id">The target message id.</param>
		/// <param name="token">The text chunk to append.</param>
		/// <exception cref="KeyNotFoundException">No message with the id exists.</exception>
		public void Append(ChatMessageId id, string token)
		{
			var entry = Require(id);
			EnsureTextBody(entry);
			entry.Buffer.Append(token);
			RenderBody(entry, GetRoleStyle(entry.Role));
		}

		/// <summary>
		/// Replaces the entire body of a message with the given content. If the message was
		/// "thinking", the spinner is cleared first.
		/// </summary>
		/// <remarks>
		/// Mutates child-control state and MUST run on the UI thread. Callers updating from a background
		/// thread MUST marshal via <c>windowSystem.EnqueueOnUIThread(() =&gt; chat.UpdateMessage(id, ...))</c>
		/// (CLAUDE.md Rule 13).
		/// </remarks>
		/// <param name="id">The target message id.</param>
		/// <param name="content">The new full content (markdown or plain text per the role style).</param>
		/// <exception cref="KeyNotFoundException">No message with the id exists.</exception>
		public void UpdateMessage(ChatMessageId id, string content)
		{
			var entry = Require(id);
			EnsureTextBody(entry);
			entry.Buffer.Clear();
			entry.Buffer.Append(content);
			RenderBody(entry, GetRoleStyle(entry.Role));
		}

		/// <summary>
		/// Removes the message with the given id from the transcript. No-op if the id is unknown.
		/// </summary>
		/// <param name="id">The message id to remove.</param>
		public void RemoveMessage(ChatMessageId id)
		{
			if (!_byId.TryGetValue(id, out var entry))
				return;

			entry.Spinner?.Stop();
			RemoveControl(entry.Panel);
			_byId.Remove(id);
			_order.Remove(entry);
		}

		/// <summary>
		/// Removes all messages from the transcript.
		/// </summary>
		public void Clear()
		{
			foreach (var entry in _order)
				entry.Spinner?.Stop();

			ClearContents();
			_order.Clear();
			_byId.Clear();
		}

		#endregion

		#region Test seams

		/// <summary>Returns the child panel's <see cref="CollapsibleAnimationMode"/> for the message with the given id.</summary>
		internal CollapsibleAnimationMode AnimationModeForTest(ChatMessageId id) => Require(id).Panel.AnimationMode;

		/// <summary>Returns the child panel's <see cref="CollapsiblePanel.Title"/> for the message with the given id.</summary>
		internal string? HeaderTextForTest(ChatMessageId id) => Require(id).Panel.Title;

		/// <summary>
		/// Returns the accumulated body text for the message with the given id — the exact string that
		/// has been streamed into it via <see cref="AddMessage"/>/<see cref="Append(ChatMessageId, string)"/>/<see cref="UpdateMessage"/>.
		/// Used by tests to verify per-message (multi-in-flight) streaming independently of markdown rendering.
		/// </summary>
		internal string BodyTextForTest(ChatMessageId id) => Require(id).Buffer.ToString();

		/// <summary>Returns the child <see cref="CollapsiblePanel"/> for the message with the given id (test-only observation seam).</summary>
		internal CollapsiblePanel PanelForTest(ChatMessageId id) => Require(id).Panel;

		/// <summary>Returns the message body's <see cref="MarkupControl.EnableSelection"/> flag, or <c>null</c> when no body exists yet (test-only seam).</summary>
		internal bool? BodySelectionEnabledForTest(ChatMessageId id) => Require(id).Body?.EnableSelection;

		/// <summary>
		/// Returns the on-screen row, in this transcript's own content-viewport space (0 == the first
		/// visible row), of the top (header) of the message panel with the given id — or <c>-1</c> when
		/// the panel is scrolled out of the viewport. Computed from the base
		/// <see cref="ScrollablePanelControl"/>'s live stacked layout (the same slot algorithm paint
		/// uses) minus the current scroll offset, so it agrees with where the panel is actually painted.
		/// Test-only: real dispatch needs the true screen row of a self-painting SPC child, whose
		/// registered layout node is an orphan (0,0) subtree.
		/// </summary>
		internal int HeaderViewportRowForTest(ChatMessageId id)
		{
			var panel = Require(id).Panel;
			foreach (var slot in GetVisibleChildLayout(ContentViewportWidth))
			{
				if (ReferenceEquals(slot.Control, panel))
				{
					int row = slot.Top - VerticalScrollOffset;
					if (row < 0 || row >= ContentViewportHeight)
						return -1; // header scrolled out of view
					return row;
				}
			}
			return -1;
		}

		/// <summary>
		/// Returns the child panel's explicit background color for the message with the given id.
		/// The value is <c>null</c> when no explicit background was set; otherwise it carries the
		/// exact <see cref="Color"/> (including any alpha channel) assigned by
		/// <see cref="AddMessage"/>.
		/// </summary>
		internal Color? BackgroundForTest(ChatMessageId id)
		{
			var panel = Require(id).Panel;
			// Read the raw nullable via the internal hook so the alpha channel is preserved.
			// CollapsiblePanel.BackgroundColor (non-nullable) falls back to Transparent when no
			// explicit color is set; using SetBackgroundColorNullable / the internal field round-
			// trips the original nullable value.  We exploit the internal setter in reverse by
			// reading the resolved value only when an explicit color was set.
			var resolved = panel.BackgroundColor;
			// Color.Transparent signals "no explicit value was set" in the resolver chain.
			return resolved == Color.Transparent ? null : resolved;
		}

		#endregion

		#region Helpers

		private MessageEntry Require(ChatMessageId id)
		{
			if (_byId.TryGetValue(id, out var entry))
				return entry;
			throw new KeyNotFoundException($"No chat message with id {id}.");
		}

		/// <summary>
		/// Swaps a thinking message's spinner for a real text body on the first token, and clears the
		/// thinking flag. Idempotent: a non-thinking message with an existing body is left untouched.
		/// </summary>
		private void EnsureTextBody(MessageEntry entry)
		{
			if (!entry.Thinking && entry.Body != null)
				return;

			if (entry.Spinner != null)
			{
				entry.Spinner.Stop();
				entry.Panel.RemoveControl(entry.Spinner);
				entry.Spinner = null;
			}

			if (entry.Body == null)
			{
				var body = new MarkupControl(new List<string>()) { EnableSelection = GetRoleStyle(entry.Role).Selectable ?? MessagesSelectable };
				entry.Panel.AddControl(body);
				entry.Body = body;
			}

			entry.Thinking = false;
		}

		/// <summary>
		/// Re-renders a message's body from its accumulated buffer, using markdown or plain content
		/// per the role style.
		/// </summary>
		private static void RenderBody(MessageEntry entry, ChatRoleStyle style)
		{
			if (entry.Body == null)
				return;

			string content = entry.Buffer.ToString();
			if (style.Markdown)
				entry.Body.SetMarkdown(content);
			else
				entry.Body.SetContent(SplitLines(content));
		}

		private static List<string> SplitLines(string content)
		{
			return new List<string>(content.Replace("\r\n", "\n").Split('\n'));
		}

		/// <summary>
		/// Composes the header text for a message: the role style's header factory (or a built-in
		/// label), optionally wrapped in a gradient markup tag when the style specifies a header gradient.
		/// </summary>
		private static string ComposeHeader(ChatRoleStyle style, ChatRole role, string? author)
		{
			string header = style.Header?.Invoke(role, author) ?? DefaultHeader(role);

			if (style.HeaderGradient is (Color from, Color to))
			{
				// The gradient markup spec parses named colors or 6-digit hex — NOT rgb(...). Build a
				// hex "RRGGBB→RRGGBB" spec (see ColorGradient.Parse / ParseSpectreColor).
				string spec = $"{ToHex(from)}→{ToHex(to)}";
				return $"[gradient={spec}]{header}[/]";
			}

			return header;
		}

		private static string ToHex(Color c) => $"{c.R:X2}{c.G:X2}{c.B:X2}";

		private static string DefaultHeader(ChatRole role) => role switch
		{
			ChatRole.User => "You",
			ChatRole.Assistant => "Assistant",
			ChatRole.System => "System",
			ChatRole.Tool => "🔧 tool",
			ChatRole.Error => "Error",
			_ => role.ToString()
		};

		#endregion
	}
}
