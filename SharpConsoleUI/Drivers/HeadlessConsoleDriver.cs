// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Core;
using System.Drawing;
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
	/// Gets the history of all WriteToConsole calls.
	/// </summary>
	public IReadOnlyList<string> OutputHistory => _outputHistory;

	/// <summary>
	/// Gets the number of WriteToConsole calls made.
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
		_cursorPosition = new Point(x, y);
	}

	/// <summary>
	/// Sets the cursor visibility.
	/// </summary>
	public void SetCursorVisible(bool visible)
	{
		_cursorVisible = visible;
	}

	/// <summary>
	/// Sets the cursor shape (no-op for headless).
	/// </summary>
	public void SetCursorShape(CursorShape shape)
	{
		// No-op for headless driver
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
		_consoleBuffer = new ConsoleBuffer(_screenSize.Width, _screenSize.Height, windowSystem.Options, _consoleLock);

		// Connect diagnostics to ConsoleBuffer
		if (windowSystem.RenderingDiagnostics != null)
		{
			_consoleBuffer.Diagnostics = windowSystem.RenderingDiagnostics;
		}
	}

	/// <summary>
	/// Writes content to the console buffer for buffered rendering.
	/// </summary>
	public void WriteToConsole(int x, int y, string value)
	{
		_outputHistory.Add(value);

		// Add content to console buffer for double-buffered rendering
		_consoleBuffer?.AddContent(x, y, value);
	}

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
		MouseEvent?.Invoke(this, flags, point);
	}

	/// <summary>
	/// Simulates a screen resize event.
	/// </summary>
	public void SimulateScreenResize(int width, int height)
	{
		_screenSize = new Size(width, height);
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
