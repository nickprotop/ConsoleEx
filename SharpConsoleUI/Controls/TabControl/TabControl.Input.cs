// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Parsing;
using System.Linq;

namespace SharpConsoleUI.Controls
{
	public partial class TabControl
	{
		#region IMouseAwareControl Implementation

		/// <inheritdoc/>
		public bool WantsMouseEvents => true;

		/// <inheritdoc/>
		public bool CanFocusWithMouse => false; // TabControl itself not focusable

		#pragma warning disable CS0067 // Event never raised (interface requirement)
		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseClick;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseDoubleClick;


		/// <summary>
		/// Occurs when the control is right-clicked with the mouse.
		/// </summary>
		public event EventHandler<MouseEventArgs>? MouseRightClick;
		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseEnter;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseLeave;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseMove;
		#pragma warning restore CS0067

		/// <summary>
		/// Returns the tab index at the given control-relative X position on the header row,
		/// or -1 if the position does not fall on any tab.
		/// </summary>
		private int GetTabIndexAtX(int clickX)
		{
			List<TabPage> snapshot;
			lock (_tabLock) { snapshot = _tabPages.ToList(); }
			return GetTabIndexAtX(clickX, snapshot);
		}

		private int GetTabIndexAtX(int clickX, List<TabPage> tabs)
		{
			int currentX = Margin.Left;
			for (int i = 0; i < tabs.Count; i++)
			{
				int innerWidth = MarkupParser.StripLength(tabs[i].Title) + 2 + (tabs[i].IsClosable ? 1 : 0);
				if (clickX >= currentX && clickX < currentX + innerWidth)
					return i;
				currentX += innerWidth + 1; // + separator
			}
			return -1;
		}

		/// <inheritdoc/>
		public bool ProcessMouseEvent(MouseEventArgs args)
		{
			List<TabPage> snapshot;
			lock (_tabLock) { snapshot = _tabPages.ToList(); }

			// Handle right-click
			if (args.HasFlag(MouseFlags.Button3Clicked))
			{
				if (_selectOnRightClick && args.Position.Y == Margin.Top)
				{
					int tabIndex = GetTabIndexAtX(args.Position.X, snapshot);
					if (tabIndex >= 0)
					{
						ActiveTabIndex = tabIndex;
					}
				}
				MouseRightClick?.Invoke(this, args);
				return true;
			}

			// Only handle clicks on tab headers (account for top margin)
			if (args.Position.Y == Margin.Top)
			{
				// Calculate which tab was clicked (account for left margin)
				int clickX = args.Position.X;
				int tabIndex = GetTabIndexAtX(clickX, snapshot);

				if (tabIndex >= 0 && args.HasFlag(MouseFlags.Button1Clicked))
				{
					// Check if click landed on the close button
					int currentX = Margin.Left;
					for (int j = 0; j < tabIndex; j++)
						currentX += MarkupParser.StripLength(snapshot[j].Title) + 2 + (snapshot[j].IsClosable ? 1 : 0) + 1;

					if (snapshot[tabIndex].IsClosable && clickX == currentX + MarkupParser.StripLength(snapshot[tabIndex].Title) + 2)
					{
						TabCloseRequested?.Invoke(this, new TabEventArgs(snapshot[tabIndex], tabIndex));
						args.Handled = true;
						return true;
					}
					ActiveTabIndex = tabIndex;
					return true;
				}
			}

			// Content clicks handled by child controls automatically
			return false;
		}

		#endregion

		#region IInteractiveControl / IFocusableControl / IFocusableContainerWithHeader Implementation

		private bool _hasFocus;

		/// <inheritdoc/>
		public bool HasFocus
		{
			get => _hasFocus;
			set
			{
				if (_hasFocus == value) return;
				_hasFocus = value;
				OnPropertyChanged();
				Invalidate(true);
			}
		}

		/// <inheritdoc/>
		public bool IsEnabled { get; set; } = true;

		/// <inheritdoc/>
		// Left/Right change the active tab; everything else is unhandled so Tab/Shift+Tab
		// propagate to SwitchFocus and land on the active tab's content controls.
		public bool ProcessKey(ConsoleKeyInfo key)
		{
			if (!_hasFocus) return false;

			if (key.Key == ConsoleKey.LeftArrow || key.Key == ConsoleKey.RightArrow)
			{
				lock (_tabLock)
				{
					if (_tabPages.Count == 0) return false;
					if (key.Key == ConsoleKey.RightArrow)
						ActiveTabIndex = (_activeTabIndex + 1) % _tabPages.Count;
					else
						ActiveTabIndex = (_activeTabIndex - 1 + _tabPages.Count) % _tabPages.Count;
				}
				return true;
			}

			return false;
		}

		// IFocusableControl — the header row is a real Tab focus stop.
		/// <inheritdoc/>
		public bool CanReceiveFocus { get { lock (_tabLock) { return _tabPages.Count > 0; } } }

		/// <inheritdoc/>
		public void SetFocus(bool focus, FocusReason reason = FocusReason.Programmatic)
		{
			HasFocus = focus;
		}

#pragma warning disable CS0067
		/// <inheritdoc/>
		public event EventHandler? GotFocus;
		/// <inheritdoc/>
		public event EventHandler? LostFocus;
#pragma warning restore CS0067

		#endregion
	}
}
