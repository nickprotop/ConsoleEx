// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text;
using SharpConsoleUI.Core;
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
	public partial class ChatTranscriptControl : ScrollablePanelControl
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

			/// <summary>
			/// The message's actions toolbar row, or <c>null</c> when the message has no actions row.
			/// </summary>
			public ToolbarControl? ActionsToolbar { get; set; }

			/// <summary>The message's status row, or <c>null</c> when the message has no status.</summary>
			public StatusBarControl? StatusBar { get; set; }

			/// <summary>
			/// The collapsed-message preview ("peek") row shown as a sibling immediately after the panel while
			/// the message is collapsed and <see cref="CollapsedPreview"/> is enabled; <c>null</c> otherwise.
			/// </summary>
			public MarkupControl? PeekRow { get; set; }

			/// <summary>The message's declared actions, in display order.</summary>
			public List<ChatMessageAction> Actions { get; } = new();

			/// <summary>
			/// Suppresses the footer's separator RULE for THIS message. Off by default, so existing
			/// transcripts are unchanged. The trailing spacer is kept: the rule is chrome that says
			/// nothing, but the blank line is what stops consecutive rows running together.
			///
			/// <para>Exists for hosts that render many short, low-information rows — a per-step tool
			/// log, a file list — where the divider and the trailing blank line cost more vertical
			/// space than the row's own content. A five-step sequence pays ten lines of chrome for
			/// five lines of substance, and the rule is a full-width divider that says nothing.</para>
			///
			/// <para>Deliberately per-MESSAGE, not per-control: <see cref="ApplyFooterSeparator"/> and
			/// <see cref="ApplyFooterSpacer"/> re-derive both on every status update, so setting
			/// <c>ShowAboveLine</c> or a margin directly on the row is overwritten on the next call.
			/// The decision has to live where those two methods can see it.</para>
			/// </summary>
			public bool CompactFooter { get; set; }

			/// <summary>
			/// Explicit per-message rail request, or <c>null</c> (the default) to inherit the footer-gated
			/// behaviour. Tri-state rather than a bool so a caller can rail a footerless message, un-rail a
			/// footered one, and hand the decision back to the default — see
			/// <see cref="SetMessageRail(ChatMessageId, bool?)"/>.
			/// </summary>
			public bool? RailOverride { get; set; }

			/// <summary>
			/// Whether this message shows the left rail: the explicit <see cref="RailOverride"/> when one is
			/// set, then its role's <see cref="ChatRoleStyle.Rail"/>, otherwise footer presence. This is the single rule both rail gates read — the gutter
			/// inset and the painted glyph — so the reserved column and the drawn rail cannot disagree.
			/// </summary>
			public bool WantsRail => RailOverride ?? RoleRail ?? HasFooter;

			/// <summary>The role style's <see cref="ChatRoleStyle.Rail"/>, captured at add and on every
			/// restyle — the entry cannot read role styles itself.</summary>
			public bool? RoleRail { get; set; }

			/// <summary>A header the host set with <see cref="SetHeader"/>, which a restyle keeps; null
			/// while the header is the role's own.</summary>
			public string? HeaderOverride { get; set; }

			/// <summary>This message's own rail colour, or null to use the role's or the transcript's.</summary>
			public Color? RailColorOverride { get; set; }

			/// <summary>This message's own background, or null to use its role's.</summary>
			public Color? BackgroundOverride { get; set; }

			/// <summary>
			/// Explicit per-message Markdown request, or <c>null</c> (the default) to inherit
			/// <see cref="ChatRoleStyle.Markdown"/>. Tri-state rather than a bool so a caller can render
			/// native markup in a markdown role, force markdown in a plain role, and hand the decision
			/// back to the role — see <see cref="SetMarkdownMode(ChatMessageId, bool?)"/>.
			/// </summary>
			public bool? MarkdownOverride { get; set; }

			/// <summary>
			/// Whether this message currently has a footer (an actions row and/or a status row) rendered
			/// as siblings of the panel. There is no separate footer container control — the two rows are
			/// inserted directly after the panel — so footer-presence is derived from the two row fields.
			/// </summary>
			public bool HasFooter => ActionsToolbar != null || StatusBar != null;
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

			// FOLDED LIKE A TOOL ROW, AND RAILED: reasoning is read on demand, as tool output is, and the
			// rail sets it apart from the answer beside it at a glance.
			_roleStyles[ChatRole.Thinking] = new ChatRoleStyle
			{
				ColorRole = ColorRole.Warning,
				HeaderStyle = CollapsibleHeaderStyle.Borderless,
				Collapsible = true,
				StartCollapsed = true,
				Rail = true,
				Markdown = false,
				Header = static (_, author) => author ?? "Thinking"
			};
		}

		#region Role style API

		/// <summary>
		/// Sets the visual style used for messages of the given role.
		/// </summary>
		/// <remarks>
		/// This affects messages added <em>after</em> the call only; already-added message panels keep
		/// the style they were built with. To bring an existing conversation onto the new styles — after
		/// a theme switch, say — set the role styles and then call <see cref="RestyleMessages"/>.
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

		/// <summary>
		/// Re-applies the current role styles to every message already in the transcript, and re-renders
		/// each body under the current <see cref="Configuration.MarkdownStyle.Default"/>.
		/// </summary>
		/// <remarks>
		/// <para>
		/// A role style is copied into a message's panel when the message is BUILT, and its body is
		/// rendered once from the Markdown style in force at that moment. So a theme switch reaches only
		/// the messages added after it: everything already on screen keeps the old theme's colours. This
		/// is the way to fix that up, and it is why <see cref="SetRoleStyle"/> alone does not — a host
		/// cannot re-add the old messages, because the control exposes no way to read their content back.
		/// </para>
		/// <para>
		/// PER-MESSAGE STATE SURVIVES. Only what came from the role style is re-applied. A message's own
		/// Markdown mode, rail, compact footer, status, actions and author header are its own, not the
		/// role's, and are left alone — as is whether the user has it expanded or collapsed, so a restyle
		/// never folds a conversation up under someone mid-read. That last one is why
		/// <see cref="ChatRoleStyle.StartCollapsed"/> is deliberately not re-applied here: it describes how
		/// a message STARTS, and these have already started.
		/// </para>
		/// <para>
		/// A style that no longer sets a background or a border colour CLEARS the one the panel carries,
		/// rather than leaving the old value standing: a role that dropped its background has to lose it.
		/// </para>
		/// <para>
		/// This mutates the control's children and MUST be called on the UI thread, like the other
		/// mutators here; background callers marshal via
		/// <c>windowSystem.EnqueueOnUIThread(() =&gt; chat.RestyleMessages())</c> — see CLAUDE.md Rule 13.
		/// </para>
		/// </remarks>
		public void RestyleMessages()
		{
			foreach (var entry in _order)
			{
				var style = GetRoleStyle(entry.Role);
				var panel = entry.Panel;

				// The header is composed from the style's factory and gradient, so it follows the theme
				// too — but the AUTHOR the message was added with is still its own and is passed back in.
				// A HEADER THE HOST SET IS THE HOST'S — a live status, a summary — and the role's factory
				// knows nothing of it; only a header the role composed is re-composed.
				panel.Title = entry.HeaderOverride ?? ComposeHeader(style, entry.Role, entry.Author);
				panel.ShowHeader = style.ShowHeader;
				panel.HeaderStyle = style.HeaderStyle;
				panel.HeaderAlignment = style.HeaderAlignment;
				panel.Collapsible = style.Collapsible;
				panel.ColorRole = style.ColorRole;
				panel.Margin = style.Margin;

				// Null means "no explicit colour", which has to RESET the panel rather than skip it.
				// BorderColor is already nullable; BackgroundColor is not, so the internal nullable hook
				// is what expresses the difference between "transparent" and "unset".
				panel.BorderColor = style.BorderColor;
				// A message's own background outlives a restyle, as its markdown mode does: it was set on
				// the message, not derived from the style being replaced.
				panel.SetBackgroundColorNullable(entry.BackgroundOverride ?? style.Background);
				if (entry.RoleRail != style.Rail)
				{
					entry.RoleRail = style.Rail;
					ApplyGutter(entry);
				}

				panel.AnimationMode = (AnimateMessages && style.Collapsible)
					? CollapsibleAnimationMode.Height
					: CollapsibleAnimationMode.None;

				// IsExpanded is deliberately untouched — see the remarks.

				if (entry.Body != null)
				{
					entry.Body.EnableSelection = style.Selectable ?? MessagesSelectable;

					// Re-render from the buffer so Markdown colours follow the current style. RenderBody
					// reads the message's own MarkdownOverride itself, so the override survives this.
					RenderBody(entry, style);
					RefreshPeek(entry);
				}

				// A thinking message has a spinner and no body: its panel restyles above, and there is
				// nothing to re-render until its first token arrives.
			}

			Invalidate(Invalidation.Relayout);
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

		/// <summary>Gets whether the message with the given id is currently expanded.</summary>
		/// <param name="id">The message id.</param>
		/// <returns><c>true</c> when the message body is visible; <c>false</c> when it is collapsed.</returns>
		/// <exception cref="KeyNotFoundException">No message with the id exists.</exception>
		public bool IsExpanded(ChatMessageId id) => Require(id).Panel.IsExpanded;

		/// <summary>
		/// Gets the message's own Markdown override, exactly as <see cref="SetMarkdownMode"/> left it.
		/// </summary>
		/// <remarks>
		/// Tri-state, mirroring the setter: <c>true</c> forces Markdown, <c>false</c> forces native
		/// markup, and <c>null</c> means the message defers to its role. This reports the SETTING, not
		/// what the body renders as — an untouched message returns <c>null</c> whatever its role does.
		/// Use <see cref="IsMarkdown"/> for the mode actually in effect.
		/// </remarks>
		/// <param name="id">The message id.</param>
		/// <returns>The override, or <c>null</c> when the message inherits its role's setting.</returns>
		/// <exception cref="KeyNotFoundException">No message with the id exists.</exception>
		public bool? GetMarkdownMode(ChatMessageId id) => Require(id).MarkdownOverride;

		/// <summary>
		/// Gives ONE message its own background, over its role's — for a message that is a different
		/// kind of thing from its neighbours of the same role. Survives <see cref="RestyleMessages"/>.
		/// </summary>
		/// <param name="id">The message id.</param>
		/// <param name="background">The background, or <c>null</c> to return to the role's.</param>
		/// <exception cref="KeyNotFoundException">No message with the id exists.</exception>
		public void SetMessageBackground(ChatMessageId id, Color? background)
		{
			var entry = Require(id);
			entry.BackgroundOverride = background;
			entry.Panel.SetBackgroundColorNullable(background ?? GetRoleStyle(entry.Role).Background);
			Invalidate(Invalidation.Repaint);
		}

		/// <summary>The background <see cref="SetMessageBackground"/> gave a message, or <c>null</c> when
		/// it uses its role's.</summary>
		/// <param name="id">The message id.</param>
		public Color? GetMessageBackground(ChatMessageId id) => Require(id).BackgroundOverride;

		/// <summary>
		/// Gets the rendering mode actually in effect for the message's body: its override when one is
		/// set, otherwise its role's <see cref="ChatRoleStyle.Markdown"/>.
		/// </summary>
		/// <remarks>
		/// This is the expression the render path itself evaluates, so it cannot drift from what is on
		/// screen. With no override it follows the role style live — restyling the role changes the
		/// answer for every message that inherits it.
		/// </remarks>
		/// <param name="id">The message id.</param>
		/// <returns><c>true</c> when the body renders as Markdown; <c>false</c> when it renders as native markup.</returns>
		/// <exception cref="KeyNotFoundException">No message with the id exists.</exception>
		public bool IsMarkdown(ChatMessageId id)
		{
			var entry = Require(id);
			return entry.MarkdownOverride ?? GetRoleStyle(entry.Role).Markdown;
		}

		#endregion

		#region Message mutation API — collapse state

		/// <summary>
		/// Expands or collapses ONE message, overriding its role's
		/// <see cref="ChatRoleStyle.StartCollapsed"/> default.
		/// </summary>
		/// <remarks>
		/// Collapse state was configurable per ROLE but not per MESSAGE, and the two are not the same
		/// need. A role like "System" is usually worth collapsing — startup notes, diagnostics, noise —
		/// yet the occasional System line matters: a goal reporting that it finished, a warning the
		/// user must act on. Those arrived collapsed behind an "expand…" nobody opens, which made a
		/// finished run look identical to a stalled one.
		///
		/// <para>The capability already existed internally: every message owns a
		/// <see cref="CollapsiblePanel"/> whose <see cref="CollapsiblePanel.IsExpanded"/> is public and
		/// settable, and this control already listens for its changes. Only the accessor was missing —
		/// <c>PanelForTest</c> was marked test-only — so callers had no way to reach it.</para>
		/// </remarks>
		/// <param name="id">The message id.</param>
		/// <param name="expanded"><c>true</c> to expand; <c>false</c> to collapse.</param>
		/// <exception cref="KeyNotFoundException">No message with the id exists.</exception>
		public void SetExpanded(ChatMessageId id, bool expanded) =>
			Require(id).Panel.IsExpanded = expanded;

		/// <summary>
		/// Replaces ONE message's header text after creation.
		/// </summary>
		/// <remarks>
		/// The header is otherwise composed once, when the message is added
		/// (<c>ComposeHeader</c>), from the role style's factory and the author string — so a host
		/// whose header should reflect CHANGING state (a step's status, its elapsed time, a live
		/// spinner) had no way to express it and was forced into a separate status row below the
		/// content.
		///
		/// <para>Folding that state into the header is what turns a three-line step — header,
		/// result, status — into two. Combined with an inline <c>[spinner]</c> tag the same header
		/// carries the running indicator, so a step does not change shape when it finishes.</para>
		///
		/// <para>The text is used verbatim: the role style's header factory is NOT re-applied, since
		/// the caller supplying a full header already knows what it wants to say.</para>
		/// </remarks>
		/// <param name="id">The message id.</param>
		/// <param name="header">The new header text. Markup is honoured.</param>
		/// <exception cref="KeyNotFoundException">No message with the id exists.</exception>
		public void SetHeader(ChatMessageId id, string header)
		{
			var entry = Require(id);
			entry.HeaderOverride = header;
			entry.Panel.Title = header;
		}

		/// <summary>
		/// Renders ONE message's footer compactly: no separator rule above the status/actions rows.
		/// The trailing blank line below them is KEPT — it separates consecutive rows.
		/// </summary>
		/// <remarks>
		/// For hosts that log many short, low-information rows — a per-step tool trace, a file list —
		/// where the divider and the trailing blank cost more vertical space than the row's content.
		/// A five-step sequence otherwise pays ten lines of chrome for five lines of substance.
		///
		/// <para>Per-message rather than a property on the rows themselves, because
		/// <c>ApplyFooterSeparator</c> and <c>ApplyFooterSpacer</c> re-derive both on every status
		/// update: setting <c>ShowAboveLine</c> or a margin directly is silently overwritten on the
		/// next <see cref="SetStatus(ChatMessageId, string, NotificationSeverity?)"/> call.</para>
		///
		/// <para>Off by default, so existing transcripts render unchanged. Apply it before or after
		/// setting a status; the next footer rebuild honours it either way.</para>
		/// </remarks>
		/// <param name="id">The message id.</param>
		/// <param name="compact"><c>true</c> to drop the rule and the trailing blank.</param>
		/// <exception cref="KeyNotFoundException">No message with the id exists.</exception>
		public void SetCompactFooter(ChatMessageId id, bool compact)
		{
			var entry = Require(id);
			entry.CompactFooter = compact;
			ApplyFooterSpacer(entry);
			ApplyFooterSeparator(entry);
		}

		/// <summary>
		/// Overrides ONE message's body rendering mode, independently of its role's
		/// <see cref="ChatRoleStyle.Markdown"/>.
		/// </summary>
		/// <remarks>
		/// A role decides markdown-or-markup for every message it owns, so a host with ONE message
		/// carrying rich native markup — a diff with per-file colour and intra-line highlight, a
		/// pre-coloured table — had to move it to a different role to render it correctly, losing the
		/// role's header, colour and collapse behaviour with it.
		///
		/// <para>Tri-state: <c>false</c> renders the body as native markup, <c>true</c> renders it as
		/// Markdown, and <c>null</c> (the default) defers to the role — so a caller can undo an override
		/// without tracking what the role's value would have been. Mirrors
		/// <see cref="SetMessageRail(ChatMessageId, bool?)"/>.</para>
		///
		/// <para>Off by default: an untouched message keeps <c>null</c> and renders exactly as it does
		/// today.</para>
		///
		/// <para>The override is read on every render, so it survives
		/// <see cref="UpdateMessage"/> and streaming <see cref="Append(ChatMessageId, string)"/> calls —
		/// a message that starts as markup does not revert to markdown on its next token.</para>
		/// </remarks>
		/// <param name="id">The message id.</param>
		/// <param name="markdown">
		/// <c>true</c> to force Markdown, <c>false</c> to render native markup, <c>null</c> to inherit
		/// the role's setting.
		/// </param>
		/// <exception cref="KeyNotFoundException">No message with the id exists.</exception>
		public void SetMarkdownMode(ChatMessageId id, bool? markdown)
		{
			var entry = Require(id);
			if (entry.MarkdownOverride == markdown)
				return;

			entry.MarkdownOverride = markdown;
			RenderBody(entry, GetRoleStyle(entry.Role));
			RefreshPeek(entry);
			Invalidate(Invalidation.Relayout);
		}

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

			// A ROLE THAT RAILS its messages reserves the gutter from the first frame; a role that does
			// not leaves the message exactly as it was, so nothing changes for the roles that never asked.
			entry.RoleRail = style.Rail;
			if (style.Rail is true)
				ApplyGutter(entry);

			AddControl(panel);

			// Collapsed fade-peek: keep a preview row in sync with the panel's expanded state, and seed it now
			// when the message starts collapsed.
			entry.Panel.ExpandedChanged += (_, expanded) => OnMessageExpandedChanged(entry, expanded);
			if (!entry.Panel.IsExpanded)
				MaybeAddPeek(entry);

			// Seed per-role default actions (an explicit AddMessage(..., actions, ...) overload overrides these).
			if (style.DefaultActions is { Count: > 0 } defaults)
				SetActions(id, defaults);

			return id;
		}

		/// <summary>
		/// Adds a new message and seeds its footer actions and/or status row at creation time.
		/// </summary>
		/// <remarks>
		/// This overload delegates to <see cref="AddMessage(ChatRole, string, string?, bool)"/> (which already
		/// seeds the role's <see cref="ChatRoleStyle.DefaultActions"/>). When <paramref name="actions"/> is non-<c>null</c>
		/// it replaces those defaults; when <c>null</c> the role defaults are kept. When <paramref name="status"/> is
		/// non-<c>null</c> the message's status row is set.
		/// <para>
		/// Like the other reactive methods, this must be called on the UI thread; background callers should marshal via
		/// <c>windowSystem.EnqueueOnUIThread(...)</c> — see CLAUDE.md Rule 13.
		/// </para>
		/// </remarks>
		/// <param name="role">The role of the message author.</param>
		/// <param name="content">The initial message content (markdown or plain text per the role style).</param>
		/// <param name="author">An optional author name that overrides the role's default header label.</param>
		/// <param name="actions">
		/// Explicit footer actions for this message. When non-<c>null</c> these override the role's
		/// <see cref="ChatRoleStyle.DefaultActions"/>; when <c>null</c> the role defaults are kept.
		/// </param>
		/// <param name="status">An optional initial status row for this message. <c>null</c> adds no status row.</param>
		/// <returns>The id of the newly added message.</returns>
		public ChatMessageId AddMessage(ChatRole role, string content, string? author,
			System.Collections.Generic.IEnumerable<ChatMessageAction>? actions, ChatMessageStatus? status)
		{
			var id = AddMessage(role, content, author); // existing overload — already seeds role DefaultActions
			if (actions != null) SetActions(id, actions); // explicit actions override the role defaults
			if (status != null) SetStatus(id, status);
			return id;
		}

		/// <summary>
		/// Adds a new message, seeding its footer actions, status row and body-rendering mode at creation.
		/// </summary>
		/// <remarks>
		/// A separate overload rather than an optional parameter on the five-argument version: adding an
		/// optional parameter is source-compatible but changes the compiled method signature, so a
		/// consumer assembly built against the old one would fail at runtime with
		/// <c>MissingMethodException</c> until recompiled. The library has real third-party NuGet users,
		/// so new surface is added, never modified.
		/// <para>Like the other reactive methods, this must be called on the UI thread — see CLAUDE.md Rule 13.</para>
		/// </remarks>
		/// <param name="role">The role of the message author.</param>
		/// <param name="content">The initial message content.</param>
		/// <param name="author">An optional author name that overrides the role's default header label.</param>
		/// <param name="actions">
		/// Explicit footer actions. Non-<c>null</c> overrides the role's
		/// <see cref="ChatRoleStyle.DefaultActions"/>; <c>null</c> keeps them.
		/// </param>
		/// <param name="status">An optional initial status row. <c>null</c> adds no status row.</param>
		/// <param name="markdown">
		/// Per-message body-rendering override: <c>false</c> renders native markup, <c>true</c> forces
		/// Markdown, <c>null</c> defers to the role. Seeding it here means a body of rich markup never
		/// renders as Markdown even once — equivalent to calling
		/// <see cref="SetMarkdownMode(ChatMessageId, bool?)"/> immediately afterwards.
		/// </param>
		/// <returns>The id of the newly added message.</returns>
		public ChatMessageId AddMessage(ChatRole role, string content, string? author,
			System.Collections.Generic.IEnumerable<ChatMessageAction>? actions, ChatMessageStatus? status,
			bool? markdown)
		{
			var id = AddMessage(role, content, author, actions, status);
			if (markdown != null) SetMarkdownMode(id, markdown);
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
			RefreshPeek(entry);
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
			RefreshPeek(entry);
		}

		/// <summary>
		/// Removes the message with the given id from the transcript. No-op if the id is unknown.
		/// </summary>
		/// <remarks>
		/// A message is not just its panel: its actions toolbar, status row and collapsed peek row are
		/// inserted as SIBLINGS of the panel (see <c>InsertActionsRow</c>, <c>InsertStatusRow</c> and
		/// <c>MaybeAddPeek</c>). All of them are torn down here, otherwise a removed message leaves its
		/// footer chrome behind as orphan rows attributed to the message above.
		/// <para>The footer spacer and separator are not controls — they are the bottom margin and
		/// <c>ShowAboveLine</c> carried by those rows — so removing the rows removes them too.</para>
		/// <para>Mutates the control's children and MUST run on the UI thread (see CLAUDE.md Rule 13).</para>
		/// </remarks>
		/// <param name="id">The message id to remove.</param>
		public void RemoveMessage(ChatMessageId id)
		{
			if (!_byId.TryGetValue(id, out var entry))
				return;

			entry.Spinner?.Stop();
			entry.Spinner = null;

			// Siblings first, then the panel itself.
			if (entry.StatusBar != null)
			{
				RemoveControl(entry.StatusBar);
				entry.StatusBar = null;
			}

			if (entry.ActionsToolbar != null)
			{
				RemoveControl(entry.ActionsToolbar);
				entry.ActionsToolbar = null;
			}

			if (entry.PeekRow != null)
			{
				RemoveControl(entry.PeekRow);
				entry.PeekRow = null;
			}

			RemoveControl(entry.Panel);
			_byId.Remove(id);
			_order.Remove(entry);
			Invalidate(Invalidation.Relayout);
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
		/// has been streamed into it via <see cref="AddMessage(ChatRole, string, string?, bool)"/>/<see cref="Append(ChatMessageId, string)"/>/<see cref="UpdateMessage"/>.
		/// Used by tests to verify per-message (multi-in-flight) streaming independently of markdown rendering.
		/// </summary>
		internal string BodyTextForTest(ChatMessageId id) => Require(id).Buffer.ToString();

		/// <summary>
		/// Returns the message body control's rendered text (test-only seam). Markdown rendering wraps the
		/// content in a <c>[markdown]…[/]</c> region, so this distinguishes the two paths by their real
		/// output rather than by re-evaluating the mode expression.
		/// </summary>
		internal string? RenderedBodyTextForTest(ChatMessageId id) => Require(id).Body?.Text;

		/// <summary>Returns the message's explicit Markdown override (legacy test-only alias for <see cref="GetMarkdownMode"/>).</summary>
		internal bool? MarkdownOverrideForTest(ChatMessageId id) => GetMarkdownMode(id);

		/// <summary>
		/// Sets a message's Markdown override WITHOUT re-rendering, unlike
		/// <see cref="SetMarkdownMode"/>. Lets a test put the rendered body deliberately out of sync
		/// with what the style now says, so an assertion can prove that a later call actually
		/// re-rendered rather than finding the work already done for it.
		/// </summary>
		internal void SetMarkdownModeWithoutRenderForTest(ChatMessageId id, bool? markdown)
			=> Require(id).MarkdownOverride = markdown;

		/// <summary>
		/// Returns the rendering mode actually used for the message's body (legacy test-only alias for
		/// <see cref="IsMarkdown"/>).
		/// </summary>
		internal bool EffectiveMarkdownForTest(ChatMessageId id) => IsMarkdown(id);

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
		/// <see cref="AddMessage(ChatRole, string, string?, bool)"/>.
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

			// HIDE an empty body rather than rendering it as a blank line. A host that clears a
			// message's content — a step that has not produced output yet, a row whose whole content
			// lives in its header — otherwise pays a blank line for nothing, and with the footer
			// spacer below it that reads as a DOUBLE gap between consecutive rows.
			//
			// Visible, not removal: the control is reused the moment content arrives, and the layout
			// already skips invisible children before measuring them (GridLayout's IsVisible checks),
			// so a hidden body contributes no height.
			entry.Body.Visible = content.Length > 0;
			if (content.Length == 0)
				return;

			// The message's own override wins; null defers to the role. RenderBody re-runs from the
			// buffer on every append/update, so reading the override HERE is what makes it survive
			// streaming — a message that starts as markup must not revert to markdown on its next token.
			// ONE ENTRY, NOT A LINE EACH, in both dialects. The markup parser carries an open style across
			// the newlines of a single entry; split into lines first, each line parsed alone and a colour
			// opened on one line — "[red]a\nb[/]" — was lost on the next. The markdown path already
			// hands over its body whole for the same reason.
			if (entry.MarkdownOverride ?? style.Markdown)
				entry.Body.SetMarkdown(content);
			else
				entry.Body.SetContent(new List<string> { content.Replace("\r\n", "\n") });
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
			ChatRole.Thinking => "Thinking",
			_ => role.ToString()
		};

		#endregion
	}
}
