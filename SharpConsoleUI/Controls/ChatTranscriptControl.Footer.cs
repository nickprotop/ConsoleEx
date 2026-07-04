// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Core;
using SharpConsoleUI.Themes;

namespace SharpConsoleUI.Controls
{
	public partial class ChatTranscriptControl
	{
		#region Actions row API

		/// <summary>
		/// Raised after a non-toggle message action has been dispatched (its click handler has run).
		/// Toggle-variant actions raise <c>ActionToggled</c> instead.
		/// </summary>
		public event System.EventHandler<ChatActionEventArgs>? ActionInvoked;

		/// <summary>
		/// Raised after a <see cref="ChatActionVariant.Toggle"/> action's pressed state changes — whether via
		/// a click, a handler's <see cref="ChatActionContext.SetPressed"/>, or a programmatic
		/// <see cref="SetActionState"/> call. The event args carry the new pressed state.
		/// </summary>
		public event System.EventHandler<ChatActionToggledEventArgs>? ActionToggled;

		/// <summary>
		/// Replaces the actions row of a message with the given set of actions, rebuilding the toolbar.
		/// An empty set removes the actions row (and the footer, if no status row remains). No-op if the
		/// id is unknown.
		/// </summary>
		/// <remarks>
		/// The actions row is a toolbar of buttons rendered as a <em>sibling</em> of the message panel,
		/// inserted immediately after it and <em>above</em> any status row. Mutates the control's children
		/// and MUST run on the UI thread (see CLAUDE.md Rule 13).
		/// </remarks>
		/// <param name="id">The target message id.</param>
		/// <param name="actions">The actions to display; an empty sequence clears the row.</param>
		public void SetActions(ChatMessageId id, System.Collections.Generic.IEnumerable<ChatMessageAction> actions)
		{
			if (!_byId.TryGetValue(id, out var entry))
				return;

			entry.Actions.Clear();
			entry.Actions.AddRange(actions);
			RebuildActionsToolbar(entry);
		}

		/// <summary>Appends a single action to a message's actions row, creating the row if needed. No-op if the id is unknown.</summary>
		/// <param name="id">The target message id.</param>
		/// <param name="action">The action to append.</param>
		public void AddAction(ChatMessageId id, ChatMessageAction action)
		{
			if (!_byId.TryGetValue(id, out var entry))
				return;

			entry.Actions.Add(action);
			RebuildActionsToolbar(entry);
		}

		/// <summary>Removes the action(s) with the given action id from a message's actions row. Removes the row (and footer) if it becomes empty. No-op if the id is unknown.</summary>
		/// <param name="id">The target message id.</param>
		/// <param name="actionId">The <see cref="ChatMessageAction.Id"/> of the action(s) to remove.</param>
		public void RemoveAction(ChatMessageId id, string actionId)
		{
			if (!_byId.TryGetValue(id, out var entry))
				return;

			entry.Actions.RemoveAll(a => a.Id == actionId);
			RebuildActionsToolbar(entry);
		}

		/// <summary>Removes a message's actions row entirely (and the footer, if no status row remains). No-op if the id is unknown.</summary>
		/// <param name="id">The target message id.</param>
		public void ClearActions(ChatMessageId id)
		{
			if (!_byId.TryGetValue(id, out var entry))
				return;

			entry.Actions.Clear();
			RebuildActionsToolbar(entry);
		}

		/// <summary>Enables or disables the action(s) with the given action id, rebuilding the row. No-op if the id is unknown.</summary>
		/// <param name="id">The target message id.</param>
		/// <param name="actionId">The <see cref="ChatMessageAction.Id"/> of the action(s) to toggle.</param>
		/// <param name="enabled">The new enabled state.</param>
		public void SetActionEnabled(ChatMessageId id, string actionId, bool enabled)
		{
			if (!_byId.TryGetValue(id, out var entry))
				return;

			bool changed = false;
			for (int i = 0; i < entry.Actions.Count; i++)
			{
				if (entry.Actions[i].Id == actionId && entry.Actions[i].Enabled != enabled)
				{
					entry.Actions[i] = entry.Actions[i] with { Enabled = enabled };
					changed = true;
				}
			}

			if (changed)
				RebuildActionsToolbar(entry);
		}

		#endregion

		#region Status row API

		/// <summary>
		/// Sets (or replaces) the status row for a message: a single left-aligned status line tinted by
		/// the optional severity. Creates the message's footer lazily if needed. No-op if the id is unknown.
		/// </summary>
		/// <remarks>
		/// The status row is rendered as a non-sticky, transparent <see cref="StatusBarControl"/> inserted
		/// as a <em>sibling</em> of the message panel (right after it), so it survives the message body
		/// collapsing. Mutates the control's children and MUST run on the UI thread (see CLAUDE.md Rule 13).
		/// </remarks>
		/// <param name="id">The target message id.</param>
		/// <param name="text">The status text.</param>
		/// <param name="severity">Optional severity that tints the status text color.</param>
		public void SetStatus(ChatMessageId id, string text, NotificationSeverity? severity = null)
			=> SetStatus(id, new ChatMessageStatus(text, severity));

		/// <summary>
		/// Sets (or replaces) the status row for a message from a full <see cref="ChatMessageStatus"/>
		/// (text + severity plus optional left/center/right region items). No-op if the id is unknown.
		/// </summary>
		/// <remarks>
		/// The status row is rendered as a non-sticky, transparent <see cref="StatusBarControl"/> inserted
		/// as a <em>sibling</em> of the message panel (right after it), so it survives the message body
		/// collapsing. Mutates the control's children and MUST run on the UI thread (see CLAUDE.md Rule 13).
		/// </remarks>
		/// <param name="id">The target message id.</param>
		/// <param name="status">The status content to display.</param>
		public void SetStatus(ChatMessageId id, ChatMessageStatus status)
		{
			if (!_byId.TryGetValue(id, out var entry))
				return;

			if (entry.StatusBar == null)
			{
				var bar = new StatusBarControl(stickyBottom: false)
				{
					BackgroundColor = null,
					Outline = false
				};
				entry.StatusBar = bar;
				InsertStatusRow(entry, bar);
			}

			RebuildStatusBar(entry.StatusBar, status);
			Invalidate(Invalidation.Relayout);
		}

		/// <summary>
		/// Removes the status row from a message, and removes the footer if no other footer row (actions)
		/// remains. No-op if the id is unknown or the message has no status row.
		/// </summary>
		/// <remarks>Mutates the control's children and MUST run on the UI thread (see CLAUDE.md Rule 13).</remarks>
		/// <param name="id">The target message id.</param>
		public void ClearStatus(ChatMessageId id)
		{
			if (!_byId.TryGetValue(id, out var entry) || entry.StatusBar == null)
				return;

			RemoveControl(entry.StatusBar);
			entry.StatusBar = null;
			Invalidate(Invalidation.Relayout);
		}

		#endregion

		#region Footer test seams

		/// <summary>Returns the message's status-row <see cref="StatusBarControl"/>, or <c>null</c> when it has none (test-only seam).</summary>
		internal StatusBarControl? StatusBarForTest(ChatMessageId id) => Require(id).StatusBar;

		/// <summary>Returns whether the message currently has a footer row (actions and/or status) rendered as a sibling of its panel (test-only seam).</summary>
		internal bool HasFooterForTest(ChatMessageId id) => Require(id).HasFooter;

		/// <summary>Returns the message's actions-row <see cref="ToolbarControl"/>, or <c>null</c> when it has none (test-only seam).</summary>
		internal ToolbarControl? ActionsToolbarForTest(ChatMessageId id) => Require(id).ActionsToolbar;

		/// <summary>Returns the number of action buttons currently in the message's actions row (0 when there is none) (test-only seam).</summary>
		internal int ActionButtonCountForTest(ChatMessageId id) => Require(id).ActionsToolbar?.Items.Count ?? 0;

		/// <summary>Dispatches the action with the given action id as if its button were clicked (test-only seam that drives the real dispatch path).</summary>
		internal void InvokeActionForTest(ChatMessageId id, string actionId)
		{
			var entry = Require(id);
			var action = entry.Actions.Find(a => a.Id == actionId);
			if (action != null)
				DispatchAction(entry, action);
		}

		/// <summary>Returns the current pressed state of the message's toggle action with the given id (false when unknown) (test-only seam).</summary>
		internal bool ActionPressedForTest(ChatMessageId id, string actionId)
		{
			var a = Require(id).Actions.Find(x => x.Id == actionId);
			return a?.IsPressed ?? false;
		}

		#endregion

		#region Footer helpers

		/// <summary>
		/// Rebuilds (or removes) a message's actions toolbar from its declared <see cref="MessageEntry.Actions"/>.
		/// The toolbar is a sibling of the panel, inserted immediately after it and <em>above</em> any status
		/// row. When there are no actions the toolbar is removed and the footer collapsed if empty.
		/// </summary>
		private void RebuildActionsToolbar(MessageEntry entry)
		{
			// Remove any existing toolbar first so a rebuild always starts clean.
			if (entry.ActionsToolbar != null)
			{
				RemoveControl(entry.ActionsToolbar);
				entry.ActionsToolbar = null;
			}

			if (entry.Actions.Count == 0)
			{
				// No actions row; nothing else to add. Footer presence is derived (status may remain).
				Invalidate(Invalidation.Relayout);
				return;
			}

			var toolbar = new ToolbarControl();

			string? lastGroup = null;
			bool first = true;
			foreach (var action in entry.Actions)
			{
				// Draw a separator between adjacent groups (once groups have been seen).
				if (!first && action.Group != lastGroup && (action.Group != null || lastGroup != null))
					toolbar.AddItem(new ButtonControl { Text = "│", IsEnabled = false });

				lastGroup = action.Group;
				first = false;

				var btn = new ButtonControl
				{
					Text = string.IsNullOrEmpty(action.Icon) ? action.Label : action.Icon + " " + action.Label,
					IsEnabled = action.Enabled,
					ColorRole = ColorRoleFor(action)
				};

				var captured = action;
				btn.Click += (_, __) => DispatchAction(entry, captured);
				toolbar.AddItem(btn);
			}

			entry.ActionsToolbar = toolbar;
			InsertActionsRow(entry, toolbar);
			Invalidate(Invalidation.Relayout);
		}

		/// <summary>
		/// Inserts the actions toolbar as a sibling of the message panel, immediately after it — which puts
		/// it above any status row (the status row is either inserted after the toolbar by
		/// <see cref="InsertStatusRow"/>, or pushed down one slot when it already exists here).
		/// </summary>
		private void InsertActionsRow(MessageEntry entry, ToolbarControl toolbar)
		{
			var children = Children; // fresh list
			int panelIndex = -1;
			for (int i = 0; i < children.Count; i++)
			{
				if (ReferenceEquals(children[i], entry.Panel))
				{
					panelIndex = i;
					break;
				}
			}

			// Actions sit directly below the panel and above the status row.
			InsertControl(panelIndex + 1, toolbar);
		}

		/// <summary>
		/// Dispatches a message action: routes toggle-variant actions to the toggle path; for non-toggle
		/// actions it runs the click handlers, raises <see cref="ActionInvoked"/>, and applies
		/// <see cref="ChatActionAfterPress.Hide"/>.
		/// </summary>
		private void DispatchAction(MessageEntry entry, ChatMessageAction action)
		{
			if (action.Variant == ChatActionVariant.Toggle)
			{
				ToggleAction(entry, action);
				return;
			}

			var ctx = new ChatActionContext(
				entry.Id,
				action,
				(text, sev) => SetStatus(entry.Id, text, sev),
				() => ClearActions(entry.Id),
				p => SetActionStateInternal(entry, action.Id, p, fireEvent: true, runOnClick: false));

			action.OnClick?.Invoke(ctx);
			if (action.OnClickAsync != null)
				_ = action.OnClickAsync(ctx); // fire-and-forget; the host owns awaiting/marshaling

			ActionInvoked?.Invoke(this, new ChatActionEventArgs(entry.Id, action));

			if (action.AfterPress == ChatActionAfterPress.Hide)
				ClearActions(entry.Id);
		}

		/// <summary>
		/// Toggle-variant dispatch: the control owns the pressed state (it lives in the action record inside
		/// <see cref="MessageEntry.Actions"/>). A click flips it, restyles the button, runs the click handler
		/// (with a context whose <see cref="ChatActionContext.SetPressed"/> re-enters this path), and raises
		/// <see cref="ActionToggled"/>. <see cref="ChatActionAfterPress.Hide"/> is intentionally ignored for
		/// toggles — a toggle stays in place so it can be flipped back.
		/// </summary>
		private void ToggleAction(MessageEntry entry, ChatMessageAction action)
		{
			SetActionStateInternal(entry, action.Id, !action.IsPressed, fireEvent: true, runOnClick: true);
		}

		/// <summary>
		/// Sets a toggle action's pressed state programmatically. Restyles the button and raises
		/// <see cref="ActionToggled"/>, but does <em>not</em> run the action's click handler (unlike a real
		/// click) — this is a state restore, not a user gesture. No-op if the id or action id is unknown.
		/// </summary>
		/// <remarks>
		/// Mutates the control's children (rebuilds the actions row) and MUST run on the UI thread (see
		/// CLAUDE.md Rule 13).
		/// </remarks>
		/// <param name="id">The target message id.</param>
		/// <param name="actionId">The id of the toggle action to update.</param>
		/// <param name="pressed">The new pressed state.</param>
		public void SetActionState(ChatMessageId id, string actionId, bool pressed)
		{
			if (_byId.TryGetValue(id, out var entry))
				SetActionStateInternal(entry, actionId, pressed, fireEvent: true, runOnClick: false);
		}

		/// <summary>
		/// Updates a toggle action's pressed state in place (via <c>with { IsPressed = pressed }</c>), rebuilds
		/// the toolbar so the button restyles, optionally runs the click handler, and optionally raises
		/// <see cref="ActionToggled"/>.
		/// </summary>
		private void SetActionStateInternal(MessageEntry entry, string actionId, bool pressed, bool fireEvent, bool runOnClick)
		{
			ChatMessageAction? updated = null;
			for (int i = 0; i < entry.Actions.Count; i++)
			{
				if (entry.Actions[i].Id == actionId)
				{
					updated = entry.Actions[i] with { IsPressed = pressed };
					entry.Actions[i] = updated;
					break;
				}
			}

			if (updated == null)
				return;

			// Rebuild so the button picks up its pressed styling (ColorRoleFor considers IsPressed for toggles).
			RebuildActionsToolbar(entry);

			if (runOnClick && (updated.OnClick != null || updated.OnClickAsync != null))
			{
				var ctx = new ChatActionContext(
					entry.Id,
					updated,
					(text, sev) => SetStatus(entry.Id, text, sev),
					() => ClearActions(entry.Id),
					p => SetActionStateInternal(entry, actionId, p, fireEvent: true, runOnClick: false));

				updated.OnClick?.Invoke(ctx);
				if (updated.OnClickAsync != null)
					_ = updated.OnClickAsync(ctx); // fire-and-forget; the host owns awaiting/marshaling
			}

			if (fireEvent)
				ActionToggled?.Invoke(this, new ChatActionToggledEventArgs(entry.Id, updated, pressed));
		}

		/// <summary>Maps an action's variant to the <see cref="ColorRole"/> used to style its button. A
		/// pressed toggle adopts the accent role; an unpressed toggle is neutral.</summary>
		private static ColorRole ColorRoleFor(ChatMessageAction action) => action.Variant switch
		{
			ChatActionVariant.Primary => ColorRole.Primary,
			ChatActionVariant.Danger => ColorRole.Danger,
			ChatActionVariant.Toggle => action.IsPressed ? ColorRole.Primary : ColorRole.Default,
			_ => ColorRole.Default
		};

		/// <summary>
		/// Inserts the status row as a sibling of the message panel, right after it (below any actions
		/// row). The footer rows are siblings of the panel — never children of it — so they survive the
		/// message body collapsing.
		/// </summary>
		private void InsertStatusRow(MessageEntry entry, StatusBarControl bar)
		{
			var children = Children; // fresh list
			int panelIndex = -1;
			for (int i = 0; i < children.Count; i++)
			{
				if (ReferenceEquals(children[i], entry.Panel))
				{
					panelIndex = i;
					break;
				}
			}

			// Status sits below the actions row (inserted immediately after the panel); place it after the
			// actions row when present, otherwise directly after the panel.
			int insertIndex = panelIndex + 1;
			if (entry.ActionsToolbar != null)
			{
				for (int i = panelIndex + 1; i < children.Count; i++)
				{
					if (ReferenceEquals(children[i], entry.ActionsToolbar))
					{
						insertIndex = i + 1;
						break;
					}
				}
			}

			InsertControl(insertIndex, bar);
		}

		/// <summary>
		/// Rebuilds a status bar's items from a <see cref="ChatMessageStatus"/>. Clears existing items and
		/// adds the primary text (tinted by severity) plus any optional region items.
		/// </summary>
		private static void RebuildStatusBar(StatusBarControl bar, ChatMessageStatus status)
		{
			bar.ClearAll();

			Color? color = status.Severity?.Severity switch
			{
				NotificationSeverityEnum.Success => Color.Green,
				NotificationSeverityEnum.Warning => Color.Yellow,
				NotificationSeverityEnum.Danger => Color.Red,
				NotificationSeverityEnum.Info => Color.Blue,
				_ => (Color?)null,
			};

			bar.AddLeft(new StatusBarItem { Label = status.Text, LabelForeground = color });

			if (status.Left != null)
				foreach (var item in status.Left)
					bar.AddLeft(item);
			if (status.Center != null)
				foreach (var item in status.Center)
					bar.AddCenter(item);
			if (status.Right != null)
				foreach (var item in status.Right)
					bar.AddRight(item);
		}

		#endregion
	}
}
