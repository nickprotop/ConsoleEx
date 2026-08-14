// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Linq;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Events;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Parsing;

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

		private int GetTabIndexAtX(int clickX, List<TabPage> tabs) =>
			GetTabIndexAtX(clickX, HeaderStrip(tabs));

		private int GetTabIndexAtX(int clickX, TabStripLayout strip)
		{
			for (int drawn = 0; drawn < strip.Count; drawn++)
			{
				int start = Margin.Left + strip.Offsets[drawn];
				if (clickX >= start && clickX < start + strip.Widths[drawn])
					return strip.First + drawn;
			}
			return -1;
		}

		/// <summary>
		/// The header row as it was last painted. It has to come from the same <see cref="LayoutStrip"/>
		/// the paint uses, or a scrolled strip is hit-tested against tab positions nobody can see — the
		/// click would land on whichever tab happens to sit at that X when counting from index 0.
		/// </summary>
		private TabStripLayout HeaderStrip(IReadOnlyList<TabPage> tabs) =>
			LayoutStrip(tabs, ActiveTabIndex, ActualWidth - Margin.Left - Margin.Right);

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
				var strip = HeaderStrip(snapshot);
				int tabIndex = GetTabIndexAtX(clickX, strip);

				if (tabIndex >= 0 && args.HasFlag(MouseFlags.Button1Clicked))
				{
					// Check if click landed on the close button, at the far end of that tab's own cells
					int currentX = Margin.Left + strip.Offsets[tabIndex - strip.First];

					if (snapshot[tabIndex].IsClosable && clickX == currentX + MarkupParser.StripLength(snapshot[tabIndex].Title) + 2)
					{
						TabCloseRequested?.Invoke(this, new TabEventArgs(snapshot[tabIndex], tabIndex));
						args.Handled = true;
						return true;
					}
					ActiveTabIndex = tabIndex;
					args.Handled = true;
					return true;
				}
			}

			// Content clicks handled by child controls automatically
			return false;
		}

		#endregion

		#region IInteractiveControl / IFocusableControl / IFocusableContainerWithHeader Implementation


		/// <inheritdoc/>
		public bool HasFocus
		{
			get => ComputeHasFocus();
		}

		/// <inheritdoc/>
		public bool IsEnabled { get; set; } = true;

		/// <inheritdoc/>
		// Left/Right change the active tab; everything else is unhandled so Tab/Shift+Tab
		// propagate to SwitchFocus and land on the active tab's content controls.
		public bool ProcessKey(ConsoleKeyInfo key)
		{
			if (!IsEnabled || !(ComputeHasFocus())) return false;

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

		#endregion
	}
}
