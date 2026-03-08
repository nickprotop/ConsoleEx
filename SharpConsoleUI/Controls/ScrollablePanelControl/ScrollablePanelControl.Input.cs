// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Events;
using SharpConsoleUI.Extensions;

namespace SharpConsoleUI.Controls
{
	public partial class ScrollablePanelControl
	{
		#region ProcessKey

		/// <inheritdoc/>
		public bool ProcessKey(ConsoleKeyInfo key)
		{
			var log = GetConsoleWindowSystem?.LogService;
			log?.LogTrace($"ScrollPanel.ProcessKey({key.Key}): _hasFocus={_hasFocus} _isEnabled={_isEnabled} _focusedChild={_focusedChild?.GetType().Name ?? "null"} _focusedChild.HasFocus={(_focusedChild as IFocusableControl)?.HasFocus}", "Focus");

			if (!_hasFocus || !_isEnabled) return false;

			// FIRST: Delegate to focused child if we have one
			if (_focusedChild != null && _focusedChild.ProcessKey(key))
			{
				return true; // Child handled it
			}

			// Handle Escape: unfocus child, enter scroll mode (panel stays focused)
			if (key.Key == ConsoleKey.Escape && _focusedChild != null)
			{
				log?.LogTrace($"ScrollPanel.ProcessKey: Escape → unfocusing child {_focusedChild.GetType().Name}, entering scroll mode", "Focus");
				_lastInternalFocusedChild = _focusedChild;
				if (_focusedChild is IFocusableControl escapeFc)
					escapeFc.SetFocus(false, FocusReason.Programmatic);
				_focusedChild = null;
				Container?.Invalidate(true);
				return true;
			}

			// Handle Escape in scroll mode (no child focused): let it propagate to unfocus panel
			if (key.Key == ConsoleKey.Escape && _focusedChild == null)
			{
				log?.LogTrace("ScrollPanel.ProcessKey: Escape in scroll mode → propagating to parent", "Focus");
				_lastInternalFocusedChild = null;
				return false; // Let parent handle (will unfocus panel)
			}

			// SECOND: Handle Tab navigation through children
			if (key.Key == ConsoleKey.Tab)
			{
				bool shiftPressed = (key.Modifiers & ConsoleModifiers.Shift) != 0;

				// Tab in scroll mode: restore last focused child
				if (_focusedChild == null && _lastInternalFocusedChild != null)
				{
					log?.LogTrace($"ScrollPanel.ProcessKey: Tab in scroll mode → restoring {_lastInternalFocusedChild.GetType().Name}", "Focus");
					_focusedChild = _lastInternalFocusedChild;
					_lastInternalFocusedChild = null;
					if (_focusedChild is IFocusableControl restoreFc)
						restoreFc.SetFocus(true, FocusReason.Keyboard);
					if (_focusedChild is IWindowControl focusedWindow)
						ScrollChildIntoView(focusedWindow);
					Container?.Invalidate(true);
					return true;
				}

				List<IWindowControl> childrenSnapshot;
				lock (_childrenLock) { childrenSnapshot = new List<IWindowControl>(_children); }
				var focusableChildren = childrenSnapshot
					.Where(c => c.Visible && c is IFocusableControl fc && fc.CanReceiveFocus)
					.Cast<IInteractiveControl>()
					.ToList();

				if (focusableChildren.Count > 0)
				{
					int currentIndex = _focusedChild != null ? focusableChildren.IndexOf(_focusedChild) : -1;

					int newIndex;
					if (shiftPressed)
					{
						// Backward
						newIndex = currentIndex - 1;
						if (newIndex < 0)
							return false; // Let Tab propagate to parent
					}
					else
					{
						// Forward
						newIndex = currentIndex + 1;
						if (newIndex >= focusableChildren.Count)
							return false; // Let Tab propagate to parent
					}

					// Unfocus current
					if (_focusedChild is IFocusableControl currentFc)
						currentFc.SetFocus(false, FocusReason.Keyboard);

					// Focus new
					_focusedChild = focusableChildren[newIndex];
					if (_focusedChild is IFocusableControl newFc)
						newFc.SetFocus(true, FocusReason.Keyboard);

					// Scroll newly focused child into view
					if (_focusedChild is IWindowControl newlyFocusedWindow)
						ScrollChildIntoView(newlyFocusedWindow);

					Container?.Invalidate(true);
					return true;
				}
			}

			// THIRD: Handle scrolling keys (only if panel needs scrolling)
			if (NeedsScrolling())
			{
				switch (key.Key)
				{
					case ConsoleKey.UpArrow:
						if (_verticalScrollMode == ScrollMode.Scroll)
						{
							ScrollVerticalBy(-1);
							return true;
						}
						break;

					case ConsoleKey.DownArrow:
						if (_verticalScrollMode == ScrollMode.Scroll)
						{
							ScrollVerticalBy(1);
							return true;
						}
						break;

					case ConsoleKey.PageUp:
						if (_verticalScrollMode == ScrollMode.Scroll)
						{
							ScrollVerticalBy(-_viewportHeight);
							return true;
						}
						break;

					case ConsoleKey.PageDown:
						if (_verticalScrollMode == ScrollMode.Scroll)
						{
							ScrollVerticalBy(_viewportHeight);
							return true;
						}
						break;

					case ConsoleKey.Home:
						if (_verticalScrollMode == ScrollMode.Scroll)
						{
							ScrollVerticalTo(0);
							return true;
						}
						break;

					case ConsoleKey.End:
						if (_verticalScrollMode == ScrollMode.Scroll)
						{
							_autoScroll = true;  // Explicitly re-attach
							ScrollVerticalTo(Math.Max(0, _contentHeight - _viewportHeight));
							return true;
						}
						break;

					case ConsoleKey.LeftArrow:
						if (_horizontalScrollMode == ScrollMode.Scroll)
						{
							ScrollHorizontalBy(-1);
							return true;
						}
						break;

					case ConsoleKey.RightArrow:
						if (_horizontalScrollMode == ScrollMode.Scroll)
						{
							ScrollHorizontalBy(1);
							return true;
						}
						break;
				}
			}

			return false;
		}

		#endregion

		#region IFocusableControl Implementation

		/// <inheritdoc/>
		/// <summary>
		/// ScrollablePanel is focusable when it has anything to interact with:
		/// either scrollable content or focusable children. The panel acts as an
		/// opaque focus container — it owns its children's focus lifecycle entirely.
		/// </summary>
		public bool CanReceiveFocus
		{
			get
			{
				if (!Visible || !_isEnabled) return false;

				bool needsScrolling = NeedsScrolling();
				bool hasFocusableChildren = HasFocusableChildren();

				var result = needsScrolling || hasFocusableChildren;
				GetConsoleWindowSystem?.LogService?.LogTrace($"ScrollPanel.CanReceiveFocus: needsScrolling={needsScrolling} hasFocusableChildren={hasFocusableChildren} result={result}", "Focus");
				return result;
			}
		}

		/// <inheritdoc/>
		public void SetFocusWithDirection(bool focus, bool backward)
		{
			_focusFromBackward = backward;
			SetFocus(focus, FocusReason.Keyboard);
		}

		/// <inheritdoc/>
		public void SetFocus(bool focus, FocusReason reason = FocusReason.Programmatic)
		{
			var log = GetConsoleWindowSystem?.LogService;
			log?.LogTrace($"ScrollPanel.SetFocus({focus}, {reason}): _hasFocus={_hasFocus} _focusedChild={_focusedChild?.GetType().Name ?? "null"}", "Focus");

			if (_hasFocus == focus) return;

			var hadFocus = _hasFocus;
			_hasFocus = focus;

			if (focus)
			{
				// Getting focus - find first/last focusable child if we have any
				List<IWindowControl> childrenSnap;
				lock (_childrenLock) { childrenSnap = new List<IWindowControl>(_children); }
				var focusableChildren = childrenSnap
					.Where(c => c.Visible && c is IFocusableControl fc && fc.CanReceiveFocus)
					.ToList();

				if (focusableChildren.Any())
				{
					// Focus first or last child based on direction
					_focusedChild = _focusFromBackward
						? focusableChildren.Last() as IInteractiveControl
						: focusableChildren.First() as IInteractiveControl;

					log?.LogTrace($"ScrollPanel.SetFocus: delegating to child {_focusedChild?.GetType().Name} (backward={_focusFromBackward})", "Focus");

					if (_focusedChild is IFocusableControl fc)
					{
						if (_focusedChild is IDirectionalFocusControl dfc)
							dfc.SetFocusWithDirection(true, _focusFromBackward);
						else
							fc.SetFocus(true, reason);
					}
				}
				else
				{
					log?.LogTrace("ScrollPanel.SetFocus: no focusable children, panel focused for scrolling", "Focus");
				}

				GotFocus?.Invoke(this, EventArgs.Empty);
			}
			else
			{
				// Losing focus - unfocus any focused child
				if (_focusedChild != null && _focusedChild is IFocusableControl fc)
				{
					log?.LogTrace($"ScrollPanel.SetFocus(false): unfocusing child {_focusedChild.GetType().Name}", "Focus");
					fc.SetFocus(false, reason);
				}
				_focusedChild = null;
				_lastInternalFocusedChild = null;

				LostFocus?.Invoke(this, EventArgs.Empty);
			}

			Container?.Invalidate(true);

			// Notify parent Window if focus state actually changed
			if (hadFocus != focus)
			{
				this.NotifyParentWindowOfFocusChange(focus);
			}
		}

		#endregion
	}
}
