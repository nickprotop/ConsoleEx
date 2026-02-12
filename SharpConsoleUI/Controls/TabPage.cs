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
	/// Represents a single tab page within a <see cref="TabControl"/>.
	/// Contains a title and an embedded <see cref="ScrollablePanelControl"/> for content.
	/// This is a data class, not a control — the TabControl manages its lifecycle.
	/// </summary>
	public class TabPage
	{
		private string _title;
		private bool _isEnabled = true;
		private bool _isVisible = true;

		/// <summary>
		/// Initializes a new instance of the <see cref="TabPage"/> class.
		/// </summary>
		/// <param name="title">The title to display in the tab header. Supports Spectre.Console markup.</param>
		public TabPage(string title)
		{
			_title = title;
			Content = new ScrollablePanelControl
			{
				ShowScrollbar = true,
				VerticalScrollMode = ScrollMode.Scroll,
				HorizontalScrollMode = ScrollMode.None
			};
		}

		/// <summary>
		/// Gets or sets the tab title displayed in the tab strip header.
		/// Supports Spectre.Console markup (e.g. "[bold]Tab[/]", "[red]Errors[/]").
		/// </summary>
		public string Title
		{
			get => _title;
			set
			{
				_title = value;
				Owner?.Invalidate();
			}
		}

		/// <summary>
		/// Gets or sets whether this tab page is enabled.
		/// Disabled tabs cannot be selected via keyboard or mouse.
		/// </summary>
		public bool IsEnabled
		{
			get => _isEnabled;
			set
			{
				_isEnabled = value;
				Owner?.Invalidate();
			}
		}

		/// <summary>
		/// Gets or sets whether this tab page is visible in the tab strip.
		/// Hidden tabs are not rendered and cannot be navigated to.
		/// </summary>
		public bool IsVisible
		{
			get => _isVisible;
			set
			{
				_isVisible = value;
				Owner?.Invalidate();
			}
		}

		/// <summary>
		/// Gets or sets an arbitrary tag object for application use.
		/// </summary>
		public object? Tag { get; set; }

		/// <summary>
		/// Gets the embedded scrollable panel that hosts this tab page's content.
		/// </summary>
		public ScrollablePanelControl Content { get; }

		/// <summary>
		/// Gets or sets the owning TabControl. Set internally when added/removed.
		/// </summary>
		internal TabControl? Owner { get; set; }

		/// <summary>
		/// Adds a child control to this tab page's content panel.
		/// </summary>
		/// <param name="control">The control to add.</param>
		public void AddControl(IWindowControl control)
		{
			Content.AddControl(control);
		}

		/// <summary>
		/// Removes a child control from this tab page's content panel.
		/// </summary>
		/// <param name="control">The control to remove.</param>
		public void RemoveControl(IWindowControl control)
		{
			Content.RemoveControl(control);
		}
	}
}
