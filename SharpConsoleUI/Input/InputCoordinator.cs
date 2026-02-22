// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Core;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Logging;
using System.Drawing;
using Color = Spectre.Console.Color;

namespace SharpConsoleUI.Input
{
	/// <summary>
	/// Coordinates all input processing for the console window system.
	/// Handles keyboard input, mouse events, window dragging, and resizing operations.
	/// Extracted from ConsoleWindowSystem to reduce complexity and improve maintainability.
	/// </summary>
	public class InputCoordinator
	{
		private readonly IConsoleDriver _consoleDriver;
		private readonly InputStateService _inputStateService;
		private readonly WindowStateService _windowStateService;
		private readonly ILogService _logService;
		private readonly ConsoleWindowSystem _context;

		/// <summary>
		/// Initializes a new instance of the InputCoordinator class.
		/// </summary>
		/// <param name="consoleDriver">Console driver for event subscription.</param>
		/// <param name="inputStateService">Service managing keyboard input state.</param>
		/// <param name="windowStateService">Service managing window drag/resize state.</param>
		/// <param name="logService">Logging service for debug output.</param>
		/// <param name="context">Window system context for operations requiring access to window collection and rendering.</param>
		public InputCoordinator(
			IConsoleDriver consoleDriver,
			InputStateService inputStateService,
			WindowStateService windowStateService,
			ILogService logService,
			ConsoleWindowSystem context)
		{
			_consoleDriver = consoleDriver ?? throw new ArgumentNullException(nameof(consoleDriver));
			_inputStateService = inputStateService ?? throw new ArgumentNullException(nameof(inputStateService));
			_windowStateService = windowStateService ?? throw new ArgumentNullException(nameof(windowStateService));
			_logService = logService ?? throw new ArgumentNullException(nameof(logService));
			_context = context ?? throw new ArgumentNullException(nameof(context));
		}

		/// <summary>
		/// Registers event handlers for mouse and keyboard input.
		/// Must be called after ConsoleWindowSystem initialization.
		/// </summary>
		public void RegisterEventHandlers(EventHandler<ConsoleKeyInfo> keyPressedHandler)
		{
			_consoleDriver.KeyPressed += keyPressedHandler;
			_consoleDriver.MouseEvent += HandleMouseEvent;
		}

		/// <summary>
		/// Unregisters event handlers for mouse and keyboard input.
		/// Must be called during ConsoleWindowSystem cleanup.
		/// </summary>
		public void UnregisterEventHandlers(EventHandler<ConsoleKeyInfo> keyPressedHandler)
		{
			_consoleDriver.KeyPressed -= keyPressedHandler;
			_consoleDriver.MouseEvent -= HandleMouseEvent;
		}

		/// <summary>
		/// Processes all pending keyboard input from the input queue.
		/// Routes input to appropriate handlers based on key combinations and active window state.
		/// </summary>
		public void ProcessInput()
		{
			ConsoleKeyInfo? key;
			while ((key = _inputStateService.DequeueKey()) != null)
			{
				var keyInfo = key.Value;

				// Check for Start menu shortcut first
				if (HandleStartMenuShortcut(keyInfo))
				{
					continue;
				}

				// Check for window cycling (Ctrl+T)
				if ((keyInfo.Modifiers & ConsoleModifiers.Control) != 0 && keyInfo.Key == ConsoleKey.T)
				{
					_context.CycleActiveWindow();
				}
				// Check for exit (Ctrl+Q)
				else if ((keyInfo.Modifiers & ConsoleModifiers.Control) != 0 && keyInfo.Key == ConsoleKey.Q)
				{
					_context.RequestExit(0);
				}
				// Route to active window
				else if (_context.ActiveWindow != null)
				{
					bool handled = _context.ActiveWindow.EventDispatcher?.ProcessInput(keyInfo) ?? false;

					if (!handled)
					{
						// Try window resize (Shift+arrows)
						if ((keyInfo.Modifiers & ConsoleModifiers.Shift) != 0 && _context.ActiveWindow.IsResizable)
						{
							handled = HandleResizeInput(keyInfo);
						}
						// Try window move (Ctrl+arrows)
						else if ((keyInfo.Modifiers & ConsoleModifiers.Control) != 0 && _context.ActiveWindow.IsMovable)
						{
							handled = HandleMoveInput(keyInfo);
						}
						// Try Alt+1-9 window selection
						else if ((keyInfo.Modifiers & ConsoleModifiers.Alt) != 0)
						{
							handled = HandleAltInput(keyInfo);
						}
					}
				}
			}
		}

		#region Mouse Event Handling

		/// <summary>
		/// Main mouse event router - handles all mouse events in priority order.
		/// Priority: release → drag/resize movement → press → click → propagation
		/// </summary>
		private void HandleMouseEvent(object sender, List<MouseFlags> flags, Point point)
		{
			// Check for Start button click first
			if (flags.Contains(MouseFlags.Button1Pressed))
			{
				if (_context.StatusBarStateService.HandleStatusBarClick(point.X, point.Y))
				{
					return;
				}
			}

			// Handle mouse button release first (end drag/resize) - highest priority
			if (flags.Contains(MouseFlags.Button1Released))
			{
				if (IsDragging || IsResizing)
				{
					// Invalidate the window that was moved/resized for a final clean redraw
					DragWindow?.Invalidate(true);

					// End interaction via service
					if (IsDragging)
					{
						_logService.LogDebug($"Drag ended: {DragWindow?.Title}", "Interaction");
						_windowStateService.EndDrag();
					}
					else if (IsResizing)
					{
						_logService.LogDebug($"Resize ended: {DragWindow?.Title}", "Interaction");
						_windowStateService.EndResize();
					}

					return;
				}
			}

			// Handle mouse movement during drag/resize operations - second priority
			if ((IsDragging || IsResizing) && DragWindow != null)
			{
				if (IsDragging)
				{
					HandleWindowMove(point);
					return;
				}
				else if (IsResizing)
				{
					HandleWindowResize(point);
					return;
				}
			}

			// Handle mouse button press (start drag/resize) - third priority
			if (flags.Contains(MouseFlags.Button1Pressed) && !IsDragging && !IsResizing)
			{
				var window = GetWindowAtPoint(point);
				if (window != null)
				{
					// Activate the window if it's not already active
					if (window != _context.ActiveWindow)
					{
						_context.SetActiveWindow(window);
					}

					// Check if clicking on an interactive control first
					var contentControl = window.EventDispatcher?.GetControlAtPosition(GeometryHelpers.TranslateToRelative(window, point, _context.DesktopUpperLeft.Y));
					bool clickingOnControl = contentControl is Controls.IMouseAwareControl mouseAware
											  && mouseAware.WantsMouseEvents;

					// Only check resize/drag if NOT clicking on an interactive control
					if (!clickingOnControl)
					{
						// Check if we're starting a resize operation
						var resizeDirection = GetResizeDirection(window, point);
						if (resizeDirection != ResizeDirection.None && window.IsResizable)
						{
							_logService.LogDebug($"Resize started: {window.Title} ({resizeDirection})", "Interaction");
							_windowStateService.StartResize(window, resizeDirection, point);
							return;
						}

						// Check if we're starting a move operation (title bar area)
						if (IsInTitleBar(window, point) && window.IsMovable)
						{
							_logService.LogDebug($"Drag started: {window.Title}", "Interaction");
							_windowStateService.StartDrag(window, point);
							return;
						}
					}
				}
			}

			// Handle mouse clicks for window activation and event propagation - lowest priority
			if (flags.Contains(MouseFlags.Button1Clicked) && !IsDragging && !IsResizing)
			{
				var window = GetWindowAtPoint(point);
				if (window != null)
				{
					// Check if close button was clicked
					if (IsOnCloseButton(window, point))
					{
						_context.CloseWindow(window);
						return;
					}

					// Check if maximize button was clicked
					if (IsOnMaximizeButton(window, point))
					{
						if (window.State == WindowState.Maximized)
						{
							window.Restore();
						}
						else
						{
							window.Maximize();
						}
						return;
					}

					// Check if minimize button was clicked
					if (IsOnMinimizeButton(window, point))
					{
						window.Minimize();
						// If the minimized window was active, activate another window
						if (_context.ActiveWindow == window)
						{
							_context.WindowStateService.ActivateNextNonMinimizedWindow(window);
						}
						return;
					}

					HandleWindowClick(window, flags, point);
					return;
				}
				else
				{
					// Clicked on empty desktop - deactivate active window
					_context.WindowStateService.DeactivateCurrentWindow();
				}
			}

			// Handle other mouse events for active window propagation
			if (_context.ActiveWindow != null && !IsDragging && !IsResizing)
			{
				var windowAtPoint = GetWindowAtPoint(point);
				if (windowAtPoint == _context.ActiveWindow)
				{
					PropagateMouseEventToWindow(_context.ActiveWindow, flags, point);
				}
			}
		}

		#endregion

		#region Window Interaction Detection

		/// <summary>
		/// Determines resize direction based on mouse position relative to window borders.
		/// </summary>
		private ResizeDirection GetResizeDirection(Window window, Point point)
		{
			// Borderless windows cannot be resized from borders
			if (window.BorderStyle == BorderStyle.None)
			{
				return ResizeDirection.None;
			}

			var relativePoint = GeometryHelpers.TranslateToRelative(window, point, _context.DesktopUpperLeft.Y);

			const int borderThickness = 1;
			const int cornerSize = 3;

			// Check if point is within expanded window bounds for resize detection
			if (relativePoint.X < -borderThickness || relativePoint.X >= window.Width + borderThickness ||
				relativePoint.Y < -borderThickness || relativePoint.Y >= window.Height + borderThickness)
			{
				return ResizeDirection.None;
			}

			// Exclude title bar area (Y=0) from TOP resize detection
			bool onLeftBorder = relativePoint.X < borderThickness;
			bool onRightBorder = relativePoint.X >= window.Width - borderThickness;
			bool onTopBorder = relativePoint.Y < borderThickness && relativePoint.Y != 0;
			bool onBottomBorder = relativePoint.Y >= window.Height - borderThickness;

			bool inTitleBarCorner = relativePoint.Y == 0 &&
									   (relativePoint.X < cornerSize || relativePoint.X >= window.Width - cornerSize);

			// Corner resize areas
			if (relativePoint.Y == 0)
			{
				if (inTitleBarCorner && onLeftBorder) return AllowedDirection(window, ResizeDirection.TopLeft);
				if (inTitleBarCorner && onRightBorder) return AllowedDirection(window, ResizeDirection.TopRight);
			}
			else
			{
				if (onTopBorder && onLeftBorder) return AllowedDirection(window, ResizeDirection.TopLeft);
				if (onTopBorder && onRightBorder) return AllowedDirection(window, ResizeDirection.TopRight);
			}

			if (onBottomBorder && onLeftBorder) return AllowedDirection(window, ResizeDirection.BottomLeft);
			if (onBottomBorder && onRightBorder) return AllowedDirection(window, ResizeDirection.BottomRight);

			// Resize grip at bottom-right
			if (IsOnResizeGrip(window, point)) return AllowedDirection(window, ResizeDirection.BottomRight);

			// Edge resize areas
			if (onTopBorder) return AllowedDirection(window, ResizeDirection.Top);
			if (onBottomBorder) return AllowedDirection(window, ResizeDirection.Bottom);
			if (onLeftBorder) return AllowedDirection(window, ResizeDirection.Left);
			if (onRightBorder) return AllowedDirection(window, ResizeDirection.Right);

			return ResizeDirection.None;
		}

		/// <summary>
		/// Returns <paramref name="direction"/> if it is permitted by the window's
		/// <see cref="Window.AllowedResizeDirections"/> (a border is active when at least one of
		/// its two movement directions is enabled); otherwise <see cref="ResizeDirection.None"/>.
		/// </summary>
		private static ResizeDirection AllowedDirection(Window window, ResizeDirection direction)
		{
			var d = window.AllowedResizeDirections;
			bool topActive    = d.HasFlag(ResizeBorderDirections.TopExpand)    || d.HasFlag(ResizeBorderDirections.TopContract);
			bool bottomActive = d.HasFlag(ResizeBorderDirections.BottomExpand) || d.HasFlag(ResizeBorderDirections.BottomContract);
			bool leftActive   = d.HasFlag(ResizeBorderDirections.LeftExpand)   || d.HasFlag(ResizeBorderDirections.LeftContract);
			bool rightActive  = d.HasFlag(ResizeBorderDirections.RightExpand)  || d.HasFlag(ResizeBorderDirections.RightContract);

			bool allowed = direction switch
			{
				ResizeDirection.Top         => topActive,
				ResizeDirection.Bottom      => bottomActive,
				ResizeDirection.Left        => leftActive,
				ResizeDirection.Right       => rightActive,
				ResizeDirection.TopLeft     => topActive    && leftActive,
				ResizeDirection.TopRight    => topActive    && rightActive,
				ResizeDirection.BottomLeft  => bottomActive && leftActive,
				ResizeDirection.BottomRight => bottomActive && rightActive,
				_                           => true
			};
			return allowed ? direction : ResizeDirection.None;
		}

		/// <summary>
		/// Clamps a resize delta to zero when the movement direction is not permitted.
		/// </summary>
		private static int ClampResizeDelta(int delta, bool allowNegative, bool allowPositive)
		{
			if (delta < 0 && !allowNegative) return 0;
			if (delta > 0 && !allowPositive) return 0;
			return delta;
		}

		/// <summary>
		/// Checks if a point is in the draggable title bar area.
		/// </summary>
		private bool IsInTitleBar(Window window, Point point)
		{
			if (window.BorderStyle == BorderStyle.None)
			{
				return false;
			}

			var relativePoint = GeometryHelpers.TranslateToRelative(window, point, _context.DesktopUpperLeft.Y);

			// Must be in the top row
			if (relativePoint.Y != 0)
				return false;

			// Must be within window bounds
			if (relativePoint.X < 0 || relativePoint.X >= window.Width)
				return false;

			// Exclude left corner for resize
			const int cornerSize = 3;
			if (relativePoint.X < cornerSize)
				return false;

			// Exclude button area on the right
			int closeButtonWidth = window.IsClosable ? 3 : 0;
			int maximizeButtonWidth = window.IsMaximizable ? 3 : 0;
			int minimizeButtonWidth = window.IsMinimizable ? 3 : 0;
			int rightExcludeWidth = 1 + closeButtonWidth + maximizeButtonWidth + minimizeButtonWidth;

			if (relativePoint.X >= window.Width - rightExcludeWidth)
				return false;

			return true;
		}

		/// <summary>
		/// Checks if a point is on the window close button.
		/// </summary>
		private bool IsOnCloseButton(Window window, Point point)
		{
			if (!window.IsClosable)
				return false;

			var relativePoint = GeometryHelpers.TranslateToRelative(window, point, _context.DesktopUpperLeft.Y);

			if (relativePoint.Y != 0)
				return false;

			int closeButtonStart = window.Width - 4;
			int closeButtonEnd = window.Width - 2;

			return relativePoint.X >= closeButtonStart && relativePoint.X <= closeButtonEnd;
		}

		/// <summary>
		/// Checks if a point is on the window maximize button.
		/// </summary>
		private bool IsOnMaximizeButton(Window window, Point point)
		{
			if (!window.IsMaximizable)
				return false;

			var relativePoint = GeometryHelpers.TranslateToRelative(window, point, _context.DesktopUpperLeft.Y);

			if (relativePoint.Y != 0)
				return false;

			int offset = 0;
			if (window.IsClosable) offset += 3;

			int maximizeButtonStart = window.Width - 4 - offset;
			int maximizeButtonEnd = window.Width - 2 - offset;

			return relativePoint.X >= maximizeButtonStart && relativePoint.X <= maximizeButtonEnd;
		}

		/// <summary>
		/// Checks if a point is on the window minimize button.
		/// </summary>
		private bool IsOnMinimizeButton(Window window, Point point)
		{
			if (!window.IsMinimizable)
				return false;

			var relativePoint = GeometryHelpers.TranslateToRelative(window, point, _context.DesktopUpperLeft.Y);

			if (relativePoint.Y != 0)
				return false;

			int offset = 0;
			if (window.IsClosable) offset += 3;
			if (window.IsMaximizable) offset += 3;

			int minimizeButtonStart = window.Width - 4 - offset;
			int minimizeButtonEnd = window.Width - 2 - offset;

			return relativePoint.X >= minimizeButtonStart && relativePoint.X <= minimizeButtonEnd;
		}

		/// <summary>
		/// Checks if a point is on the resize grip (bottom-right corner).
		/// </summary>
		private bool IsOnResizeGrip(Window window, Point point)
		{
			if (!window.IsResizable)
				return false;

			var relativePoint = GeometryHelpers.TranslateToRelative(window, point, _context.DesktopUpperLeft.Y);

			return relativePoint.X == window.Width - 1 && relativePoint.Y == window.Height - 1;
		}

		/// <summary>
		/// Finds the topmost window at the specified point.
		/// </summary>
		private Window? GetWindowAtPoint(Point point)
		{
			return _context.GetWindowAtPoint(point);
		}

		#endregion

		#region Drag and Resize Operations

		/// <summary>
		/// Handles window movement during drag operation.
		/// </summary>
		private void HandleWindowMove(Point currentMousePos)
		{
			var currentDrag = _windowStateService.CurrentDrag;
			if (currentDrag == null) return;

			var window = currentDrag.Window;
			int deltaX = currentMousePos.X - currentDrag.StartMousePos.X;
			int deltaY = currentMousePos.Y - currentDrag.StartMousePos.Y;

			int newLeft = currentDrag.StartWindowPos.X + deltaX;
			int newTop = currentDrag.StartWindowPos.Y + deltaY;

			// Constrain to desktop bounds
			var desktopDimensions = _context.DesktopDimensions;
			newLeft = Math.Max(0, Math.Min(newLeft, desktopDimensions.Width - window.Width));
			newTop = Math.Max(0, Math.Min(newTop, desktopDimensions.Height - window.Height));

			_context.Positioning.MoveWindowTo(window, newLeft, newTop);
		}

		/// <summary>
		/// Handles window resizing during resize operation.
		/// </summary>
		private void HandleWindowResize(Point currentMousePos)
		{
			var currentResize = _windowStateService.CurrentResize;
			if (currentResize == null) return;

			var window = currentResize.Window;
			int deltaX = currentMousePos.X - currentResize.StartMousePos.X;
			int deltaY = currentMousePos.Y - currentResize.StartMousePos.Y;

			int newLeft = currentResize.StartWindowPos.X;
			int newTop = currentResize.StartWindowPos.Y;
			int newWidth = currentResize.StartWindowSize.Width;
			int newHeight = currentResize.StartWindowSize.Height;

			// Apply resize based on direction, clamping each axis to allowed movement directions
			var dirs = window.AllowedResizeDirections;
			switch (currentResize.Direction)
			{
				case ResizeDirection.Left:
					// deltaX < 0 = left border expands left; deltaX > 0 = contracts right
					deltaX = ClampResizeDelta(deltaX, dirs.HasFlag(ResizeBorderDirections.LeftExpand), dirs.HasFlag(ResizeBorderDirections.LeftContract));
					newLeft += deltaX;
					newWidth -= deltaX;
					break;
				case ResizeDirection.Right:
					// deltaX > 0 = right border expands right; deltaX < 0 = contracts left
					deltaX = ClampResizeDelta(deltaX, dirs.HasFlag(ResizeBorderDirections.RightContract), dirs.HasFlag(ResizeBorderDirections.RightExpand));
					newWidth += deltaX;
					break;
				case ResizeDirection.Top:
					// deltaY < 0 = top border expands up; deltaY > 0 = contracts down
					deltaY = ClampResizeDelta(deltaY, dirs.HasFlag(ResizeBorderDirections.TopExpand), dirs.HasFlag(ResizeBorderDirections.TopContract));
					newTop += deltaY;
					newHeight -= deltaY;
					break;
				case ResizeDirection.Bottom:
					// deltaY > 0 = bottom border expands down; deltaY < 0 = contracts up
					deltaY = ClampResizeDelta(deltaY, dirs.HasFlag(ResizeBorderDirections.BottomContract), dirs.HasFlag(ResizeBorderDirections.BottomExpand));
					newHeight += deltaY;
					break;
				case ResizeDirection.TopLeft:
					deltaX = ClampResizeDelta(deltaX, dirs.HasFlag(ResizeBorderDirections.LeftExpand),   dirs.HasFlag(ResizeBorderDirections.LeftContract));
					deltaY = ClampResizeDelta(deltaY, dirs.HasFlag(ResizeBorderDirections.TopExpand),    dirs.HasFlag(ResizeBorderDirections.TopContract));
					newLeft += deltaX;
					newWidth -= deltaX;
					newTop += deltaY;
					newHeight -= deltaY;
					break;
				case ResizeDirection.TopRight:
					deltaX = ClampResizeDelta(deltaX, dirs.HasFlag(ResizeBorderDirections.RightContract), dirs.HasFlag(ResizeBorderDirections.RightExpand));
					deltaY = ClampResizeDelta(deltaY, dirs.HasFlag(ResizeBorderDirections.TopExpand),     dirs.HasFlag(ResizeBorderDirections.TopContract));
					newWidth += deltaX;
					newTop += deltaY;
					newHeight -= deltaY;
					break;
				case ResizeDirection.BottomLeft:
					deltaX = ClampResizeDelta(deltaX, dirs.HasFlag(ResizeBorderDirections.LeftExpand),    dirs.HasFlag(ResizeBorderDirections.LeftContract));
					deltaY = ClampResizeDelta(deltaY, dirs.HasFlag(ResizeBorderDirections.BottomContract), dirs.HasFlag(ResizeBorderDirections.BottomExpand));
					newLeft += deltaX;
					newWidth -= deltaX;
					newHeight += deltaY;
					break;
				case ResizeDirection.BottomRight:
					deltaX = ClampResizeDelta(deltaX, dirs.HasFlag(ResizeBorderDirections.RightContract), dirs.HasFlag(ResizeBorderDirections.RightExpand));
					deltaY = ClampResizeDelta(deltaY, dirs.HasFlag(ResizeBorderDirections.BottomContract), dirs.HasFlag(ResizeBorderDirections.BottomExpand));
					newWidth += deltaX;
					newHeight += deltaY;
					break;
			}

			// Apply minimum size constraints
			const int minWidth = 10;
			const int minHeight = 3;

			if (newWidth < minWidth)
			{
				if (currentResize.Direction == ResizeDirection.Left ||
					currentResize.Direction == ResizeDirection.TopLeft ||
					currentResize.Direction == ResizeDirection.BottomLeft)
				{
					newLeft = newLeft + newWidth - minWidth;
				}
				newWidth = minWidth;
			}

			if (newHeight < minHeight)
			{
				if (currentResize.Direction == ResizeDirection.Top ||
					currentResize.Direction == ResizeDirection.TopLeft ||
					currentResize.Direction == ResizeDirection.TopRight)
				{
					newTop = newTop + newHeight - minHeight;
				}
				newHeight = minHeight;
			}

			// Constrain to desktop bounds
			var desktopDimensions = _context.DesktopDimensions;
			newLeft = Math.Max(0, Math.Min(newLeft, desktopDimensions.Width - newWidth));
			newTop = Math.Max(0, Math.Min(newTop, desktopDimensions.Height - newHeight));

			_context.Positioning.ResizeWindowTo(window, newLeft, newTop, newWidth, newHeight);
		}

		#endregion

		#region Keyboard Input Handlers

		/// <summary>
		/// Handles keyboard-based window movement (Ctrl+arrows, Ctrl+X).
		/// </summary>
		private bool HandleMoveInput(ConsoleKeyInfo key)
		{
			var activeWindow = _context.ActiveWindow;
			if (activeWindow == null) return false;

			switch (key.Key)
			{
				case ConsoleKey.UpArrow:
					_context.Positioning.MoveWindowBy(activeWindow, 0, -1);
					return true;

				case ConsoleKey.DownArrow:
					_context.Positioning.MoveWindowBy(activeWindow, 0, 1);
					return true;

				case ConsoleKey.LeftArrow:
					_context.Positioning.MoveWindowBy(activeWindow, -1, 0);
					return true;

				case ConsoleKey.RightArrow:
					_context.Positioning.MoveWindowBy(activeWindow, 1, 0);
					return true;

				case ConsoleKey.X:
					_context.CloseWindow(activeWindow);
					return true;

				default:
					return false;
			}
		}

		/// <summary>
		/// Handles keyboard-based window resizing (Shift+arrows).
		/// </summary>
		private bool HandleResizeInput(ConsoleKeyInfo key)
		{
			var w = _context.ActiveWindow;
			if (w == null) return false;

			var dirs = w.AllowedResizeDirections;

			// Each arrow key moves the natural border outward (expand only)
			switch (key.Key)
			{
				case ConsoleKey.UpArrow:
					if (!dirs.HasFlag(ResizeBorderDirections.TopExpand)) return false;
					_context.Positioning.ResizeWindowTo(w, w.Left, w.Top - 1, w.Width, w.Height + 1);
					return true;

				case ConsoleKey.DownArrow:
					if (!dirs.HasFlag(ResizeBorderDirections.BottomExpand)) return false;
					_context.Positioning.ResizeWindowTo(w, w.Left, w.Top, w.Width, w.Height + 1);
					return true;

				case ConsoleKey.LeftArrow:
					if (!dirs.HasFlag(ResizeBorderDirections.LeftExpand)) return false;
					_context.Positioning.ResizeWindowTo(w, w.Left - 1, w.Top, w.Width + 1, w.Height);
					return true;

				case ConsoleKey.RightArrow:
					if (!dirs.HasFlag(ResizeBorderDirections.RightExpand)) return false;
					_context.Positioning.ResizeWindowTo(w, w.Left, w.Top, w.Width + 1, w.Height);
					return true;

				default:
					return false;
			}
		}

		/// <summary>
		/// Handles start menu keyboard shortcut.
		/// </summary>
		private bool HandleStartMenuShortcut(ConsoleKeyInfo key)
		{
			var options = _context.Options.StatusBar;

			// Only handle shortcut if Start button is enabled
			if (!options.ShowStartButton)
				return false;

			if (key.Key == options.StartMenuShortcutKey &&
				key.Modifiers == options.StartMenuShortcutModifiers)
			{
				_context.StatusBarStateService.ShowStartMenu();
				return true;
			}
			return false;
		}

		/// <summary>
		/// Handles Alt+1-9 window selection by index.
		/// </summary>
		private bool HandleAltInput(ConsoleKeyInfo key)
		{
			if (key.KeyChar >= (char)ConsoleKey.D1 && key.KeyChar <= (char)ConsoleKey.D9)
			{
				// Get only top-level windows to match what's displayed in bottom status bar
				var topLevelWindows = _context.Windows.Values
					.Where(w => w.ParentWindow == null)
					.OrderBy(w => w.CreationOrder)
					.ToList();

				int index = key.KeyChar - (char)ConsoleKey.D1;
				if (index < topLevelWindows.Count)
				{
					var newActiveWindow = topLevelWindows[index];
					_context.SetActiveWindow(newActiveWindow);
					if (newActiveWindow.State == WindowState.Minimized)
						newActiveWindow.State = WindowState.Normal;
					return true;
				}
			}
			return false;
		}

		#endregion

	#region Mouse Event Propagation

	/// <summary>
	/// Handles window click for activation and mouse event propagation.
	/// </summary>
	private void HandleWindowClick(Window window, List<MouseFlags> flags, Point point)
	{
		// Block clicks on windows that are blocked by a modal — flash the modal instead
		var blockingModal = _context.ModalStateService.GetBlockingModal(window);
		if (blockingModal != null)
		{
			_windowStateService.FlashWindow(blockingModal);
			return;
		}

		if (window != _context.ActiveWindow)
		{
			// Window is not active - activate it
			_context.SetActiveWindow(window);

			// Special case: OverlayWindow needs mouse events even on first click
			// for click-outside-to-dismiss handling
			if (window is Windows.OverlayWindow)
			{
				PropagateMouseEventToWindow(window, flags, point);
			}
		}
		else
		{
			// Window is already active - propagate the click event
			PropagateMouseEventToWindow(window, flags, point);
		}
	}

	/// <summary>
	/// Propagates a mouse event to the specified window.
	/// </summary>
	private void PropagateMouseEventToWindow(Window window, List<MouseFlags> flags, Point point)
	{
		// Calculate window-relative coordinates
		var windowPosition = GeometryHelpers.TranslateToRelative(window, point, _context.DesktopUpperLeft.Y);

		// Create mouse event arguments
		var mouseArgs = new Events.MouseEventArgs(
			flags,
			windowPosition, // This will be recalculated for control-relative coordinates in the window
			point, // Absolute desktop coordinates
			windowPosition, // Window-relative coordinates
			window
		);

		// Propagate to the window
		window.EventDispatcher?.ProcessMouseEvent(mouseArgs);
	}

	#endregion

		#region Properties (Delegation to Services)

		/// <summary>Delegation to WindowStateService.IsDragging</summary>
		private bool IsDragging => _windowStateService.IsDragging;

		/// <summary>Delegation to WindowStateService.IsResizing</summary>
		private bool IsResizing => _windowStateService.IsResizing;

		/// <summary>Combines drag and resize window into single property</summary>
		private Window? DragWindow => IsDragging ? _windowStateService.CurrentDrag?.Window :
			(IsResizing ? _windowStateService.CurrentResize?.Window : null);

		#endregion
	}
}
