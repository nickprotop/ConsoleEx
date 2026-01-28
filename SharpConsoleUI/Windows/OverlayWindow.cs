using Spectre.Console;
using System.Drawing;

namespace SharpConsoleUI.Windows;

/// <summary>
/// Specialized full-screen overlay window for menus, popups, and modal overlays.
/// Automatically handles click-outside-to-dismiss and background dimming.
/// </summary>
public class OverlayWindow : Window
{
	private bool _dismissOnClickOutside = true;
	private Action? _onDismiss;

	/// <summary>
	/// Initializes a new OverlayWindow that covers the desktop area (but not status bars).
	/// </summary>
	/// <param name="windowSystem">The console window system.</param>
	public OverlayWindow(ConsoleWindowSystem windowSystem) : base(windowSystem)
	{
		// Configure to cover the entire desktop area
		// Window positions are in desktop-relative coordinates, not screen-absolute
		// So (0,0) means the top-left of the desktop area (below status bars)
		var dimensions = windowSystem.DesktopDimensions;

		Left = 0;
		Top = 0;
		Width = dimensions.Width;
		Height = dimensions.Height;

		// Remove all window chrome
		BorderStyle = BorderStyle.None;
		Mode = WindowMode.Modal;
		IsResizable = false;
		IsMovable = false;
		ShowTitle = false;
		IsMinimizable = false;
		IsMaximizable = false;
		ShowCloseButton = false;

		// Default to dark dimmed background
		BackgroundColor = Spectre.Console.Color.Grey11;

		// Set up event handlers
		KeyPressed += OnKeyPressed;
		UnhandledMouseClick += OnUnhandledMouseClick;
	}

	/// <summary>
	/// Sets the background color for the overlay (typically dark/dimmed).
	/// </summary>
	/// <param name="color">The background color.</param>
	public void SetOverlayBackground(Spectre.Console.Color color)
	{
		BackgroundColor = color;
	}

	/// <summary>
	/// Enables/disables click-outside-to-dismiss behavior.
	/// </summary>
	/// <param name="enable">True to enable dismissal on outside clicks.</param>
	/// <param name="onDismiss">Optional callback invoked when overlay is dismissed.</param>
	public void SetDismissOnClickOutside(bool enable, Action? onDismiss = null)
	{
		_dismissOnClickOutside = enable;
		_onDismiss = onDismiss;
	}

	/// <summary>
	/// Handles keyboard events, implementing Escape-to-dismiss logic.
	/// </summary>
	private void OnKeyPressed(object? sender, KeyPressedEventArgs e)
	{
		// Escape always dismisses overlay
		// Accept Escape even if handled by controls (e.g., menu unfocus)
		if (e.KeyInfo.Key == ConsoleKey.Escape)
		{
			Dismiss();
			e.Handled = true;
		}
	}

	/// <summary>
	/// Handles unhandled mouse clicks (clicks on empty space).
	/// Dismisses the overlay if DismissOnClickOutside is enabled.
	/// </summary>
	private void OnUnhandledMouseClick(object? sender, Events.MouseEventArgs e)
	{
		if (_dismissOnClickOutside)
		{
			Dismiss();
		}
	}

	/// <summary>
	/// Dismisses the overlay by invoking callbacks and closing with invalidation.
	/// </summary>
	public void Dismiss()
	{
		_onDismiss?.Invoke();
		CloseAndInvalidate();
	}

	/// <summary>
	/// Closes the overlay and forces full screen redraw.
	/// </summary>
	public void CloseAndInvalidate()
	{
		// Force all underlying windows to redraw
		var windowSystem = GetConsoleWindowSystem;
		if (windowSystem != null)
		{
			foreach (var window in windowSystem.Windows.Values)
			{
				if (window != this)
				{
					window.IsDirty = true;
				}
			}
		}

		Close();
	}
}
