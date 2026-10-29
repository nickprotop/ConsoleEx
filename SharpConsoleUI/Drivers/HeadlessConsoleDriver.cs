// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Core;
using SharpConsoleUI.Layout;
using Size = SharpConsoleUI.Helpers.Size;

namespace SharpConsoleUI.Drivers;

/// <summary>
/// A headless console driver that captures output instead of writing to the real console.
/// Useful for testing, CI/CD pipelines, and scenarios where no terminal is available.
/// Provides inspection methods for validating rendering output.
/// </summary>
public class HeadlessConsoleDriver : IConsoleDriver, IDisposable
{
	private readonly object _consoleLock = new();
	private readonly List<string> _outputHistory = new();
	private Size _screenSize;
	private Point _cursorPosition;
	private bool _cursorVisible;
	private ConsoleWindowSystem? _windowSystem;
	private ConsoleBuffer? _consoleBuffer;
	private bool _disposed;

	/// <summary>
	/// Gets the history of output operations (legacy, no longer populated).
	/// </summary>
	public IReadOnlyList<string> OutputHistory => _outputHistory;

	/// <summary>
	/// Gets the number of output operations recorded (legacy, no longer populated).
	/// </summary>
	public int WriteCallCount => _outputHistory.Count;

	/// <summary>
	/// Gets the total number of bytes written across all calls.
	/// </summary>
	public int TotalBytesWritten => _outputHistory.Sum(s => s.Length);

	/// <summary>
	/// Gets the concatenated output from all calls.
	/// </summary>
	public string FullOutput => string.Join("", _outputHistory);

	/// <summary>
	/// Gets the number of times <see cref="SetCursorPosition"/> has been called.
	/// </summary>
	public int SetCursorPositionCallCount { get; private set; }

	/// <summary>
	/// Gets the number of times <see cref="SetCursorVisible"/> has been called.
	/// </summary>
	public int SetCursorVisibleCallCount { get; private set; }

	/// <summary>
	/// Gets the number of times <see cref="SetCursorShape(CursorShape)"/> has been called
	/// (the two-arg overload delegates to it, so it counts both).
	/// </summary>
	public int SetCursorShapeCallCount { get; private set; }

	/// <summary>
	/// Gets or sets the cursor position.
	/// </summary>
	public Point CursorPosition
	{
		get => _cursorPosition;
		set => _cursorPosition = value;
	}

	/// <summary>
	/// Gets or sets the cursor visibility.
	/// </summary>
	public bool CursorVisible
	{
		get => _cursorVisible;
		set => _cursorVisible = value;
	}

	/// <inheritdoc />
	public event EventHandler<ConsoleKeyInfo>? KeyPressed;
	/// <inheritdoc />
	public event EventHandler<string>? Paste;
	/// <inheritdoc />
	public event IConsoleDriver.MouseEventHandler? MouseEvent;
	/// <inheritdoc />
	public event EventHandler<Size>? ScreenResized;

	/// <summary>
	/// Creates a new headless console driver with default size 200x50.
	/// </summary>
	public HeadlessConsoleDriver() : this(200, 50)
	{
	}

	/// <summary>
	/// Creates a new headless console driver with specified size.
	/// </summary>
	public HeadlessConsoleDriver(int width, int height)
	{
		_screenSize = new Size(width, height);
	}

	/// <summary>
	/// Gets the screen size.
	/// </summary>
	public Size ScreenSize => _screenSize;

	/// <summary>
	/// Clears the output history.
	/// </summary>
	public void Clear()
	{
		_outputHistory.Clear();
	}

	/// <summary>
	/// Flushes the console buffer, triggering actual rendering with diagnostics capture.
	/// </summary>
	public void Flush()
	{
		_consoleBuffer?.Render();
	}

	/// <summary>
	/// Starts the driver (no-op for headless).
	/// </summary>
	public void Start()
	{
		// No-op for headless driver
	}

	/// <summary>
	/// Stops the driver (no-op for headless).
	/// </summary>
	public void Stop()
	{
		// No-op for headless driver
	}

	/// <summary>
	/// Sets the cursor position.
	/// </summary>
	public void SetCursorPosition(int x, int y)
	{
		SetCursorPositionCallCount++;
		_cursorPosition = new Point(x, y);
	}

	/// <summary>
	/// Sets the cursor visibility.
	/// </summary>
	public void SetCursorVisible(bool visible)
	{
		SetCursorVisibleCallCount++;
		_cursorVisible = visible;
	}

	/// <summary>
	/// Gets the blink behavior passed to the most recent <see cref="SetCursorShape(CursorShape, CursorBlink)"/> call.
	/// </summary>
	public CursorBlink LastCursorBlink { get; private set; } = CursorBlink.Blinking;

	/// <summary>
	/// Sets the cursor shape (no-op for headless).
	/// </summary>
	public void SetCursorShape(CursorShape shape)
	{
		SetCursorShapeCallCount++;
		// No-op for headless driver
	}

	/// <summary>
	/// Sets the cursor shape and blink (records the blink for inspection; otherwise a no-op).
	/// </summary>
	public void SetCursorShape(CursorShape shape, CursorBlink blink)
	{
		LastCursorBlink = blink;
		SetCursorShape(shape);
	}

	/// <summary>
	/// Resets the cursor shape (no-op for headless).
	/// </summary>
	public void ResetCursorShape()
	{
		// No-op for headless driver
	}

	/// <summary>
	/// Initializes the driver with a reference to the window system.
	/// </summary>
	public void Initialize(ConsoleWindowSystem windowSystem)
	{
		_windowSystem = windowSystem;

		// Create ConsoleBuffer for buffered rendering with thread-safe lock
		_consoleBuffer = new ConsoleBuffer(_screenSize.Width, _screenSize.Height, windowSystem.Options, _consoleLock)
		{
			// This driver captures output; it has no console to write to. Without this the buffer's
			// Console.Out fallback would paint rendered frames into the process's real stdout — into
			// the caller's data when stdout is redirected, which is exactly what the Windows
			// redirected-stdio refusal exists to prevent.
			SuppressConsoleOutput = true
		};

		// Connect diagnostics to ConsoleBuffer
		if (windowSystem.RenderingDiagnostics != null)
		{
			_consoleBuffer.Diagnostics = windowSystem.RenderingDiagnostics;
		}
	}

	/// <inheritdoc />
	public void SetNarrowCell(int x, int y, char character, Color fg, Color bg)
	{
		_consoleBuffer?.SetNarrowCell(x, y, character, fg, bg);
	}

	/// <inheritdoc />
	public void FillCells(int x, int y, int width, char character, Color fg, Color bg)
	{
		_consoleBuffer?.FillCells(x, y, width, character, fg, bg);
	}

	/// <inheritdoc />
	public void WriteBufferRegion(int destX, int destY, CharacterBuffer source, int srcX, int srcY, int width, Color fallbackBg)
	{
		// Route through ConsoleBuffer for double-buffered rendering
		_consoleBuffer?.SetCellsFromBuffer(destX, destY, source, srcX, srcY, width, fallbackBg);
	}

	/// <inheritdoc/>
	/// <inheritdoc/>
	public long FrameNumber => _consoleBuffer?.FrameNumber ?? 0;

	/// <inheritdoc/>
	public Diagnostics.Snapshots.ConsoleBufferSnapshot? CaptureComposed() =>
		_consoleBuffer?.CaptureComposed();

	/// <summary>
	/// Returns the count of dirty characters from the ConsoleBuffer.
	/// </summary>
	public int GetDirtyCharacterCount()
	{
		return _consoleBuffer?.GetDirtyCharacterCount() ?? 0;
	}

	/// <summary>
	/// Simulates a key press event.
	/// </summary>
	public void SimulateKeyPress(ConsoleKeyInfo keyInfo)
	{
		KeyPressed?.Invoke(this, keyInfo);
	}

	/// <summary>
	/// Simulates a mouse event.
	/// </summary>
	public void SimulateMouseEvent(List<MouseFlags> flags, Point point)
	{
		// Counted the same way the real driver counts it, so a test driving this
		// one gets the same answer from IsDispatchingMouse.
		_mouseDispatch.While(() => MouseEvent?.Invoke(this, flags, point));
	}

	private readonly Input.MouseDispatchCount _mouseDispatch = new();

	/// <summary>Whether a mouse handler is running on any thread.</summary>
	/// <remarks>
	/// Dispatch is synchronous on the calling thread, so this is true only while
	/// a handler is actually executing - which is exactly the window in which
	/// "nothing is queued" would otherwise read as nothing left to do.
	/// </remarks>
	public bool IsDispatchingMouse => _mouseDispatch.IsDispatching;

	/// <summary>
	/// Simulates a full click: the press and the release a terminal sends.
	/// </summary>
	/// <param name="point">Where to click, in screen coordinates.</param>
	/// <remarks>
	/// <para>
	/// Spelling the pair out by hand is easy to get subtly wrong, and none of it is
	/// what a test is about. The gesture flags - clicked, double, triple - are NOT
	/// added here: <c>MouseGestureRecognizer</c> derives them downstream, once, for
	/// every driver, and a driver that invented its own would disagree with the real
	/// input path.
	/// </para>
	/// <para>
	/// The first click on an inactive window activates it rather than reaching the
	/// control under the cursor, exactly as in a real terminal. Click twice, or
	/// activate the window first, when the control is the target.
	/// </para>
	/// </remarks>
	public void SimulateClick(Point point) => SimulateClick(point, MouseFlags.Button1Pressed);

	/// <summary>
	/// Simulates a full click with a specific button.
	/// </summary>
	/// <param name="point">Where to click, in screen coordinates.</param>
	/// <param name="button">The PRESS flag of the button, such as
	/// <see cref="MouseFlags.Button1Pressed"/>.</param>
	public void SimulateClick(Point point, MouseFlags button)
	{
		SimulateMouseEvent([button], point);
		SimulateMouseEvent([ReleaseFor(button)], point);
	}

	/// <summary>
	/// Simulates two clicks in the same place, close enough in time to be one
	/// double click.
	/// </summary>
	/// <param name="point">Where to click, in screen coordinates.</param>
	/// <remarks>
	/// Four raw events: press, release, press, release. The recognizer derives
	/// <c>Button1DoubleClicked</c> on the SECOND PRESS, which is why a control must
	/// test for the double before the press. Whether the two clicks fall inside the
	/// multi-click window is decided by the recognizer's clock - pass a
	/// <see cref="TimeProvider"/> through
	/// <c>ConsoleWindowSystemOptions.TimeProvider</c> to control it.
	/// </remarks>
	public void SimulateDoubleClick(Point point)
	{
		SimulateClick(point);
		SimulateClick(point);
	}

	/// <summary>The release flag matching a press flag.</summary>
	private static MouseFlags ReleaseFor(MouseFlags press) => press switch
	{
		MouseFlags.Button2Pressed => MouseFlags.Button2Released,
		MouseFlags.Button3Pressed => MouseFlags.Button3Released,
		MouseFlags.Button4Pressed => MouseFlags.Button4Released,
		_ => MouseFlags.Button1Released,
	};

	/// <summary>
	/// Simulates a screen resize event.
	/// </summary>
	/// <remarks>
	/// Rebuilds the console buffer BEFORE raising the event, which is the contract
	/// <see cref="ConsoleWindowSystem"/>'s resize handler relies on - it treats the event
	/// size as "the size the buffer was just rebuilt with" and lays windows out against it.
	/// Without the rebuild the driver reports the new size while every rendered frame still
	/// goes to a buffer of the old one, so a resize appears to succeed and changes nothing.
	/// </remarks>
	public void SimulateScreenResize(int width, int height)
	{
		_screenSize = new Size(width, height);

		if (_windowSystem != null)
		{
			var rebuilt = new ConsoleBuffer(width, height, _windowSystem.Options, _consoleLock)
			{
				SuppressConsoleOutput = true,
			};

			if (_windowSystem.RenderingDiagnostics != null)
				rebuilt.Diagnostics = _windowSystem.RenderingDiagnostics;

			// Keep the frame count monotonic across the rebuild, or a client tracking it
			// would be told the screen went backwards and wait forever for a frame number
			// it had already passed.
			rebuilt.ResumeFrameNumberFrom(_consoleBuffer?.FrameNumber ?? 0);

			_consoleBuffer = rebuilt;
		}

		ScreenResized?.Invoke(this, _screenSize);
	}

	/// <summary>
	/// Clears the output history (alias for Clear()).
	/// </summary>
	public void ClearHistory()
	{
		_outputHistory.Clear();
	}

	/// <summary>
	/// Gets the most recent output string.
	/// </summary>
	public string? GetLastOutput()
	{
		return _outputHistory.Count > 0 ? _outputHistory[^1] : null;
	}

	/// <summary>
	/// Gets output from a specific call index.
	/// </summary>
	public string? GetOutput(int index)
	{
		return index >= 0 && index < _outputHistory.Count ? _outputHistory[index] : null;
	}

	/// <summary>
	/// Disposes of the headless console driver and releases resources.
	/// </summary>
	public void Dispose()
	{
		if (!_disposed)
		{
			_disposed = true;
			_windowSystem = null;
			_consoleBuffer = null;
			_outputHistory.Clear();
			GC.SuppressFinalize(this);
		}
	}
}
