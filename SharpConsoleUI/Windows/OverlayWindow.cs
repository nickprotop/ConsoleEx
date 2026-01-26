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
		// Configure to cover desktop but NOT status bars
		var dimensions = windowSystem.DesktopDimensions;

		// Adjust for status bars so overlay doesn't block them
		int top = 0;
		int height = dimensions.Height;

		// If top status bar is shown, start below it
		if (windowSystem.Options.StatusBar.ShowTopStatus)
		{
			top = 1;
			height--;
		}

		// If bottom status bar is shown, don't cover it
		if (windowSystem.Options.StatusBar.ShowBottomStatus)
		{
			height--;
		}

		Left = 0;
		Top = top;
		Width = dimensions.Width;
		Height = height;

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
		// DEBUG: Log unhandled click in overlay
		System.IO.File.AppendAllText("/tmp/overlay_mouse_debug.log",
			$"[{DateTime.Now:HH:mm:ss.fff}] OverlayWindow.OnUnhandledMouseClick: DISMISSING\n" +
			$"  DismissOnClickOutside: {_dismissOnClickOutside}\n" +
			$"  WindowPosition: ({e.WindowPosition.X}, {e.WindowPosition.Y})\n\n");

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
