// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Logging;

namespace SharpConsoleUI.Core
{
	/// <summary>
	/// Manages panel state and visibility.
	/// </summary>
	public class PanelStateService
	{
		private readonly ILogService _logService;
		private readonly Func<ConsoleWindowSystem> _getWindowSystem;

		// Panel references
		private Panel.Panel? _topPanel;
		private Panel.Panel? _bottomPanel;

		// Visibility
		private bool _showTopPanel = true;
		private bool _showBottomPanel = true;

		/// <summary>
		/// Initializes a new instance of the PanelStateService class.
		/// </summary>
		/// <param name="logService">Service for debug logging.</param>
		/// <param name="getWindowSystem">Function to get the window system (lazy to avoid circular dependency).</param>
		public PanelStateService(ILogService logService, Func<ConsoleWindowSystem> getWindowSystem)
		{
			_logService = logService ?? throw new ArgumentNullException(nameof(logService));
			_getWindowSystem = getWindowSystem ?? throw new ArgumentNullException(nameof(getWindowSystem));
		}

		#region Panel Ownership

		/// <summary>
		/// Gets the top panel (desktop bar) if configured, or null.
		/// </summary>
		public Panel.Panel? TopPanel => _topPanel;

		/// <summary>
		/// Gets the bottom panel (desktop bar) if configured, or null.
		/// </summary>
		public Panel.Panel? BottomPanel => _bottomPanel;

		/// <summary>
		/// Gets whether any panel needs to be redrawn.
		/// </summary>
		public bool IsDirty =>
			(_topPanel?.IsDirty ?? false)
			|| (_bottomPanel?.IsDirty ?? false);

		/// <summary>
		/// Marks both panels as dirty, forcing a re-render on the next frame.
		/// </summary>
		public void MarkDirty()
		{
			_topPanel?.MarkDirty();
			_bottomPanel?.MarkDirty();
		}

		#endregion

		#region Visibility

		/// <summary>
		/// Gets or sets whether the top panel is visible.
		/// Changing this affects desktop dimensions and triggers window invalidation.
		/// </summary>
		public bool ShowTopPanel
		{
			get => _showTopPanel;
			set
			{
				if (_showTopPanel != value)
				{
					_showTopPanel = value;
					if (_topPanel != null)
						_topPanel.Visible = value;
					var ws = _getWindowSystem();
					ws.Render.InvalidateAllWindows();
				}
			}
		}

		/// <summary>
		/// Gets or sets whether the bottom panel is visible.
		/// Changing this affects desktop dimensions and triggers window invalidation.
		/// </summary>
		public bool ShowBottomPanel
		{
			get => _showBottomPanel;
			set
			{
				if (_showBottomPanel != value)
				{
					_showBottomPanel = value;
					if (_bottomPanel != null)
						_bottomPanel.Visible = value;
					var ws = _getWindowSystem();
					ws.Render.InvalidateAllWindows();
				}
			}
		}

		#endregion

		#region Status Text (convenience)

		/// <summary>
		/// Sets the text of the first StatusTextElement in the top panel.
		/// Convenience shorthand — equivalent to finding the element and setting .Text directly.
		/// </summary>
		public string TopStatus
		{
			set
			{
				if (_topPanel?.FindElement<Panel.StatusTextElement>("statustext") is { } el)
					el.Text = value ?? "";
			}
		}

		/// <summary>
		/// Sets the text of the first StatusTextElement in the bottom panel.
		/// Convenience shorthand — equivalent to finding the element and setting .Text directly.
		/// </summary>
		public string BottomStatus
		{
			set
			{
				if (_bottomPanel?.FindElement<Panel.StatusTextElement>("statustext") is { } el)
					el.Text = value ?? "";
			}
		}

		#endregion

		#region Panel Initialization

		/// <summary>
		/// Initializes panels from configuration options.
		/// User-supplied config replaces the default panel entirely.
		/// When no config is provided, sensible defaults are created.
		/// </summary>
		/// <param name="options">The window system configuration options.</param>
		public void InitializePanels(ConsoleWindowSystemOptions options)
		{
			var ws = _getWindowSystem();

			// Top panel: user config or default (status text + clock)
			if (options.TopPanelConfig != null)
			{
				var builder = options.TopPanelConfig(new Panel.PanelBuilder());
				_topPanel = builder.Build();
			}
			else
			{
				var appName = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name ?? "SharpConsoleUI";
				_topPanel = new Panel.PanelBuilder()
					.Left(new Panel.StatusTextElement($"[bold]{appName}[/]"))
					.Right(new Panel.ClockElement { Format = "HH:mm:ss" })
					.Build();
			}
			_topPanel.WindowSystem = ws;

			// Bottom panel: user config or default (status text + task bar)
			if (options.BottomPanelConfig != null)
			{
				var builder = options.BottomPanelConfig(new Panel.PanelBuilder());
				_bottomPanel = builder.Build();
			}
			else
			{
				_bottomPanel = new Panel.PanelBuilder()
					.Left(new Panel.StatusTextElement(""))
					.Center(new Panel.TaskBarElement())
					.Build();
			}
			_bottomPanel.WindowSystem = ws;

			// Apply initial visibility from options
			_showTopPanel = options.ShowTopPanel;
			_showBottomPanel = options.ShowBottomPanel;
			_topPanel.Visible = _showTopPanel;
			_bottomPanel.Visible = _showBottomPanel;
		}

		#endregion
	}
}
