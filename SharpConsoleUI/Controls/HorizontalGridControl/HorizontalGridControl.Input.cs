// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Events;
using SharpConsoleUI.Drivers;

using SharpConsoleUI.Extensions;
namespace SharpConsoleUI.Controls
{
	public partial class HorizontalGridControl
	{
		/// <inheritdoc/>
		public bool ProcessKey(ConsoleKeyInfo key)
		{

			// Let focused content try to handle the key first (including Tab for nested containers)
			if (_focusedContent != null && _focusedContent.ProcessKey(key))
			{
				return true; // Child handled it (e.g., inner grid's Tab navigation)
			}

			// Child didn't handle it - now handle Tab at this level
			if (key.Key == ConsoleKey.Tab)
			{
				// Build a properly ordered list of interactive controls
				List<ColumnContainer> tabColumns;
				List<SplitterControl> tabSplitters;
				Dictionary<IInteractiveControl, int> tabSplitterControls;
				lock (_gridLock)
				{
					tabColumns = new List<ColumnContainer>(_columns);
					tabSplitters = new List<SplitterControl>(_splitters);
					tabSplitterControls = new Dictionary<IInteractiveControl, int>(_splitterControls);
				}

				var orderedInteractiveControls = new List<IInteractiveControl>();

				// Start by collecting all the interactive controls from columns and their associated splitters
				var columnControls = new Dictionary<int, List<IInteractiveControl>>();

				// First, gather all interactive controls by column
				for (int i = 0; i < tabColumns.Count; i++)
				{
					var column = tabColumns[i];
					if (!column.Visible) continue;

					var interactiveContents = column.GetInteractiveContents();

					if (!columnControls.ContainsKey(i))
					{
						columnControls[i] = new List<IInteractiveControl>();
					}

					columnControls[i].AddRange(interactiveContents);

					// Find if this column has a splitter to the right
					var splitter = tabSplitters.FirstOrDefault(s => tabSplitterControls[s] == i);
					if (splitter != null && splitter.Visible)
					{
						// Add the splitter right after this column's controls
						columnControls[i].Add(splitter);
					}
				}

				// Now flatten the dictionary into a single ordered list
				for (int i = 0; i < tabColumns.Count; i++)
				{
					if (columnControls.ContainsKey(i))
					{
						orderedInteractiveControls.AddRange(columnControls[i]);
					}
				}

				// Filter to only include controls that can actually receive focus
				orderedInteractiveControls = orderedInteractiveControls
					.Where(c => c is not IFocusableControl fc || fc.CanReceiveFocus)
					.ToList();

				// If we have no interactive controls, exit
				if (orderedInteractiveControls.Count == 0)
				{
					return false;
				}

				// Handle tabbing through the ordered list
				if (_focusedContent == null)
				{
					_focusedContent = orderedInteractiveControls.First();
				}
				else
				{
					// Unfocus current control using SetFocus for consistent focus handling
					if (_focusedContent is IFocusableControl currentFocusable)
					{
						currentFocusable.SetFocus(false, FocusReason.Keyboard);
					}
					else
					{
						_focusedContent.HasFocus = false;
					}

					// If it's from columns dictionary, invalidate its container
					if (_interactiveContents.ContainsKey(_focusedContent))
					{
						_interactiveContents[_focusedContent].Invalidate(true);
					}

					int index = orderedInteractiveControls.IndexOf(_focusedContent);

					// Determine the next control based on tab direction
					if (key.Modifiers.HasFlag(ConsoleModifiers.Shift))
					{
						if (index == 0)
						{
							return false; // Exit control backward
						}
						index--;
					}
					else
					{
						if (index == orderedInteractiveControls.Count - 1)
						{
							return false; // Exit control forward
						}
						index++;
					}

					_focusedContent = orderedInteractiveControls[index];
				}

				// Set focus on the new control using SetFocus for consistent focus handling
				if (_focusedContent is IFocusableControl newFocusable)
				{
					newFocusable.SetFocus(true, FocusReason.Keyboard);
				}
				else
				{
					_focusedContent.HasFocus = true;
				}

				// If it's from columns dictionary, invalidate its container
				if (_interactiveContents.ContainsKey(_focusedContent))
				{
					_interactiveContents[_focusedContent].Invalidate(true);
				}

				Container?.Invalidate(true);
				return true;
			}

			return false;
		}

		/// <inheritdoc/>
		public void SetFocus(bool focus, FocusReason reason = FocusReason.Programmatic)
		{
			// Note: _focusFromBackward should be set before calling this method
			// if backward focus selection is needed
			bool hadFocus = HasFocus;
			HasFocus = focus;

			// Notify parent Window if focus state actually changed
			if (hadFocus != focus)
			{
				this.NotifyParentWindowOfFocusChange(focus);
			}
		}

		/// <summary>
		/// Sets focus with direction information for proper child control selection.
		/// </summary>
		/// <param name="focus">Whether to set or remove focus.</param>
		/// <param name="backward">If true, focus last child; if false, focus first child.</param>
		public void SetFocusWithDirection(bool focus, bool backward)
		{
			_focusFromBackward = backward;
			HasFocus = focus;
		}

		private void FocusChanged()
		{
			if (_hasFocus)
			{
				// Only rebuild interactive contents dictionary when columns have changed
				if (_interactiveContentsDirty)
				{
					lock (_gridLock)
					{
						_interactiveContents.Clear();
						foreach (var column in _columns)
						{
							foreach (var interactiveContent in column.GetInteractiveContents())
							{
								_interactiveContents.Add(interactiveContent, column);
							}
						}
						_interactiveContentsDirty = false;
					}
				}

				if (_interactiveContents.Count == 0 && _splitterControls.Count == 0) return;

				if (_focusedContent == null)
				{
					// Find first or last focusable control based on focus direction
					_focusedContent = _focusFromBackward
						? FindLastFocusableControl()
						: FindFirstFocusableControl();
					_focusFromBackward = false; // Reset after use
				}

				// Set focus on the control if it can receive focus
				if (_focusedContent != null)
				{
					var controlToFocus = _focusedContent;
					SetControlFocus(controlToFocus, true);

					// Re-check after SetControlFocus since notifications may re-enter and change state
					if (controlToFocus != null && _interactiveContents.ContainsKey(controlToFocus))
					{
						_interactiveContents[controlToFocus].Invalidate(true);
					}
				}
			}
			else
			{
				// Remove focus from all interactive controls
				if (_interactiveContents.Count > 0 && _focusedContent != null && _interactiveContents.ContainsKey(_focusedContent))
				{
					_interactiveContents[_focusedContent]?.Invalidate(true);
				}

				foreach (var control in _interactiveContents.Keys)
				{
					control.HasFocus = false;
				}

				foreach (var splitterControl in _splitterControls.Keys)
				{
					splitterControl.HasFocus = false;
				}

				_focusedContent = null;
			}
		}

		/// <summary>
		/// Finds the first control that can receive focus
		/// </summary>
		private IInteractiveControl? FindFirstFocusableControl()
		{
			// Check interactive contents first
			foreach (var control in _interactiveContents.Keys)
			{
				if (control is IFocusableControl focusable && focusable.CanReceiveFocus)
				{
					return control;
				}
			}

			// Then check splitters
			foreach (var splitter in _splitterControls.Keys)
			{
				if (splitter is IFocusableControl focusable && focusable.CanReceiveFocus)
				{
					return splitter;
				}
			}

			return null;
		}

		/// <summary>
		/// Finds the last control that can receive focus (for backward tab navigation)
		/// </summary>
		private IInteractiveControl? FindLastFocusableControl()
		{
			IInteractiveControl? lastFocusable = null;

			// Check interactive contents
			foreach (var control in _interactiveContents.Keys)
			{
				if (control is IFocusableControl focusable && focusable.CanReceiveFocus)
				{
					lastFocusable = control;
				}
			}

			// Then check splitters (splitters come after column controls in tab order)
			foreach (var splitter in _splitterControls.Keys)
			{
				if (splitter is IFocusableControl focusable && focusable.CanReceiveFocus)
				{
					lastFocusable = splitter;
				}
			}

			return lastFocusable;
		}

		/// <summary>
		/// Sets focus on a control, checking CanReceiveFocus and using SetFocus when available
		/// </summary>
		private void SetControlFocus(IInteractiveControl control, bool focus)
		{
			if (control is IFocusableControl focusable)
			{
				if (focus && !focusable.CanReceiveFocus)
				{
					return; // Don't set focus if control can't receive it
				}
				focusable.SetFocus(focus, FocusReason.Programmatic);
			}
			else
			{
				control.HasFocus = focus;
			}
		}

		#region IFocusTrackingContainer Implementation

		/// <inheritdoc/>
		public void NotifyChildFocusChanged(IInteractiveControl child, bool hasFocus)
		{
			if (hasFocus)
			{
				if (_focusedContent != null && _focusedContent != child)
				{
					if (_focusedContent is IFocusableControl oldFc)
						oldFc.HasFocus = false;
					else
						_focusedContent.HasFocus = false;
				}

				_focusedContent = child;

				if (!_hasFocus)
				{
					_hasFocus = true;
					GotFocus?.Invoke(this, EventArgs.Empty);
				}
			}
			else if (_focusedContent == child)
			{
				_focusedContent = null;
			}

			Container?.Invalidate(true);
		}

		#endregion
	}
}
