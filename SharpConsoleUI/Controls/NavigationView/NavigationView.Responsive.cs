// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Animation;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Extensions;

namespace SharpConsoleUI.Controls
{
	public partial class NavigationView
	{
		private IAnimation? _widthAnimation;
		private bool _isAnimatingWidth;

		#region Display Mode Resolution

		/// <summary>
		/// Resolves the effective display mode based on the configured mode and available width.
		/// </summary>
		/// <param name="availableWidth">The available width in characters.</param>
		/// <returns>The resolved display mode (never <see cref="NavigationViewDisplayMode.Auto"/>).</returns>
		internal NavigationViewDisplayMode ResolveDisplayMode(int availableWidth)
		{
			if (_paneDisplayMode != NavigationViewDisplayMode.Auto)
				return _paneDisplayMode;

			if (availableWidth >= _expandedThreshold)
				return NavigationViewDisplayMode.Expanded;
			if (availableWidth >= _compactThreshold)
				return NavigationViewDisplayMode.Compact;
			return NavigationViewDisplayMode.Minimal;
		}

		/// <summary>
		/// Checks the available width, resolves the display mode, and applies it if changed.
		/// </summary>
		/// <param name="availableWidth">The available width in characters.</param>
		internal void CheckAndApplyDisplayMode(int availableWidth)
		{
			_lastKnownWidth = availableWidth;
			var newMode = ResolveDisplayMode(availableWidth);
			if (newMode != _currentDisplayMode)
			{
				ApplyDisplayMode(newMode);
			}
		}

		#endregion

		#region Display Mode Application

		/// <summary>
		/// Applies the specified display mode, updating nav column width, item formatting,
		/// and content header.
		/// </summary>
		/// <param name="newMode">The display mode to apply.</param>
		internal void ApplyDisplayMode(NavigationViewDisplayMode newMode)
		{
			var oldMode = _currentDisplayMode;
			int oldWidth = _effectiveNavWidth;

			_currentDisplayMode = newMode;

			int newWidth = newMode switch
			{
				NavigationViewDisplayMode.Expanded => _navPaneWidth,
				NavigationViewDisplayMode.Compact => _compactPaneWidth,
				NavigationViewDisplayMode.Minimal => 0,
				_ => _navPaneWidth
			};

			// Update pane header for the new mode
			UpdatePaneHeaderForMode(newMode);

			// Close any open portal when leaving a mode that had it open
			if ((oldMode == NavigationViewDisplayMode.Minimal || oldMode == NavigationViewDisplayMode.Compact)
				&& _isPortalOpen)
			{
				CloseNavigationPortal();
			}

			// Update item formatting and content header
			RefreshAllItemMarkupForMode();
			UpdateContentHeaderForMode();

			// Always set the effective width immediately for layout
			_effectiveNavWidth = newWidth;

			// Animate or apply width change
			if (oldWidth != newWidth)
			{
				AnimateNavWidth(oldWidth, newWidth);
			}
			else
			{
				// Width didn't change, just sync controls
				SyncInternalControls();
			}

			DisplayModeChanged?.Invoke(this, newMode);
		}

		#endregion

		#region Width Animation

		/// <summary>
		/// Animates the navigation pane width from one value to another, or applies instantly
		/// if animations are disabled.
		/// </summary>
		private void AnimateNavWidth(int fromWidth, int toWidth)
		{
			// Cancel any in-progress animation
			if (_widthAnimation != null)
			{
				GetConsoleWindowSystem?.Animations.Cancel(_widthAnimation);
				_widthAnimation = null;
				_isAnimatingWidth = false;
			}

			if (!_animateTransitions || GetConsoleWindowSystem == null)
			{
				// Instant — _effectiveNavWidth already set by ApplyDisplayMode
				_navColumn.Width = toWidth;
				this.GetParentWindow()?.ForceRebuildLayout();
				Invalidate(true);
				return;
			}

			_isAnimatingWidth = true;

			_widthAnimation = GetConsoleWindowSystem.Animations.Animate(
				from: fromWidth,
				to: toWidth,
				duration: TimeSpan.FromMilliseconds(ControlDefaults.NavigationViewTransitionDurationMs),
				easing: EasingFunctions.EaseInOut,
				onUpdate: w =>
				{
					_navColumn.Width = w;
					this.GetParentWindow()?.ForceRebuildLayout();
					Invalidate(true);
				},
				onComplete: () =>
				{
					_widthAnimation = null;
					_isAnimatingWidth = false;
					// Ensure final state is exact
					_navColumn.Width = toWidth;
					RefreshAllItemMarkupForMode();
					this.GetParentWindow()?.ForceRebuildLayout();
					Invalidate(true);
				}
			);
		}

		#endregion

		#region Pane Header Updates

		/// <summary>
		/// Updates the pane header for the current display mode.
		/// In Expanded mode, shows the user-configured pane header text.
		/// In Compact mode, shows a hamburger button that opens the full nav as a portal.
		/// In Minimal mode, the pane header is hidden (nav column has zero width).
		/// </summary>
		private void UpdatePaneHeaderForMode(NavigationViewDisplayMode mode)
		{
			switch (mode)
			{
				case NavigationViewDisplayMode.Expanded:
					// Restore original pane header
					_paneHeader.Visible = _paneHeaderText != null;
					if (_paneHeaderText != null)
					{
						_paneHeader.SetContent(new List<string> { _paneHeaderText, "" });
						_paneHeader.Margin = new Margin(0, 1, 0, 0);
					}
					break;

				case NavigationViewDisplayMode.Compact:
					// Show hamburger button centered in compact width, with bottom spacing
					_paneHeader.Visible = true;
					int totalWidth = _compactPaneWidth;
					int hamburgerWidth = 1; // single char
					int leftPad = Math.Max(0, (totalWidth - hamburgerWidth) / 2);
					int rightPad = Math.Max(0, totalWidth - hamburgerWidth - leftPad);
					var padded = new string(' ', leftPad)
						+ ControlDefaults.NavigationViewHamburgerChar
						+ new string(' ', rightPad);
					_paneHeader.SetContent(new List<string> { $"[bold white]{padded}[/]" });
					_paneHeader.Margin = new Margin(0, 1, 0, 1);
					break;

				case NavigationViewDisplayMode.Minimal:
					// Nav column hidden — pane header not visible
					_paneHeader.Visible = false;
					break;
			}
		}

		#endregion

		#region Content Header Updates

		/// <summary>
		/// Updates the content header text based on the current display mode.
		/// Only Minimal mode prepends a hamburger character (since the nav column is hidden).
		/// Compact mode has its own hamburger in the nav pane header.
		/// </summary>
		private void UpdateContentHeaderForMode()
		{
			if (!_showContentHeader) return;

			lock (_itemsLock)
			{
				if (_selectedIndex >= 0 && _selectedIndex < _items.Count)
				{
					var item = _items[_selectedIndex];
					string titleMarkup = _currentDisplayMode == NavigationViewDisplayMode.Minimal
						? $"[bold white]{ControlDefaults.NavigationViewHamburgerChar} {item.Text}[/]"
						: $"[bold white]{item.Text}[/]";

					var headerLines = new List<string> { titleMarkup };
					if (item.Subtitle != null)
						headerLines.Add($"[dim]{item.Subtitle}[/]");
					_contentHeader.SetContent(headerLines);
				}
			}
		}

		#endregion
	}
}
