// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Extensions;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls
{
	public partial class NavigationView
	{
		private bool _isPortalOpen;
		private PortalContentContainer? _portalContent;
		private LayoutNode? _portalNode;

		#region Portal Lifecycle

		/// <summary>
		/// Gets whether the navigation portal overlay is currently open.
		/// </summary>
		public bool IsPortalOpen => _isPortalOpen;

		/// <summary>
		/// Opens the navigation portal overlay for Minimal mode.
		/// Creates a full-height overlay anchored to the left edge containing the full nav list.
		/// </summary>
		internal void OpenNavigationPortal()
		{
			if (_isPortalOpen) return;

			var window = this.GetParentWindow();
			if (window == null) return;

			// Calculate portal bounds: left edge, full height, expanded width
			var bounds = new System.Drawing.Rectangle(
				ActualX,
				ActualY,
				_navPaneWidth,
				ActualHeight
			);

			_portalContent = new PortalContentContainer
			{
				PortalBounds = bounds,
				DismissOnOutsideClick = true,
				BorderStyle = null
			};

			// Create a scrollable panel to host nav items in expanded format
			var portalPanel = new ScrollablePanelControl
			{
				BorderStyle = BorderStyle.None,
				Padding = new Padding(0, 0, 0, 0),
				VerticalAlignment = VerticalAlignment.Fill
			};

			// Add pane header if configured
			if (_paneHeaderText != null)
			{
				var headerControl = new MarkupControl(new List<string> { _paneHeaderText, "" });
				headerControl.Margin = new Margin(0, 1, 0, 0);
				portalPanel.AddControl(headerControl);
			}

			lock (_itemsLock)
			{
				for (int i = 0; i < _items.Count; i++)
				{
					var item = _items[i];
					var markup = FormatNavEntry(item, i == _selectedIndex);
					var control = new MarkupControl(new List<string> { markup });
					control.Wrap = false;

					// Capture index for click handler
					int itemIndex = i;
					control.MouseClick += (_, _) =>
					{
						if (item.ItemType == NavigationItemType.Header)
						{
							ToggleHeaderExpanded(item);
						}
						else if (item.IsEnabled)
						{
							SelectedIndex = itemIndex;
							CloseNavigationPortal();
						}
					};

					portalPanel.AddControl(control);
				}
			}

			_portalContent.AddChild(portalPanel);

			// Focus the scroll panel so wheel events and keyboard navigation
			// work immediately without requiring a click first.
			_portalContent.SetFocusOnFirstChild();

			// Wire dismiss
			_portalContent.DismissRequested += (_, _) =>
			{
				CloseNavigationPortal();
			};

			_portalNode = window.CreatePortal(this, _portalContent);
			_isPortalOpen = true;
			Invalidate(true);
		}

		/// <summary>
		/// Closes the navigation portal overlay and cleans up references.
		/// </summary>
		internal void CloseNavigationPortal()
		{
			if (!_isPortalOpen) return;

			var window = this.GetParentWindow();
			if (window != null && _portalNode != null && _portalContent != null)
			{
				window.RemovePortal(this, _portalNode);
			}

			_portalContent = null;
			_portalNode = null;
			_isPortalOpen = false;
			Invalidate(true);
		}

		#endregion
	}
}
