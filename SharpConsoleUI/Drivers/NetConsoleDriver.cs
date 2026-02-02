// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Helpers;
using SharpConsoleUI.Themes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Size = SharpConsoleUI.Helpers.Size;

namespace SharpConsoleUI.Drivers
{
	/// <summary>
	/// Specifies the rendering mode for console output.
	/// </summary>
	public enum RenderMode
	{
		/// <summary>
		/// Writes directly to the console without buffering.
		/// </summary>
		/// <remarks>
		/// This mode writes each output operation immediately to the console,
		/// which may result in visible flickering during complex updates.
		/// </remarks>
		Direct,

		/// <summary>
		/// Uses double-buffering for smoother rendering.
		/// </summary>
		/// <remarks>
		/// This mode accumulates changes in a buffer and renders them all at once,
		/// only updating portions of the screen that have changed. This provides
		/// smoother visual updates and is recommended for most applications.
		/// </remarks>
		Buffer
	}

	/// <summary>
	/// Provides a cross-platform console driver implementation using .NET Console APIs.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This driver supports both Windows and Unix-like platforms, handling platform-specific
	/// console mode configuration for mouse input, virtual terminal processing, and other features.
	/// </para>
	/// <para>
	/// On Windows, the driver configures console modes using Win32 API calls to enable
	/// virtual terminal input/output and mouse reporting.
	/// </para>
	/// <para>
	/// Mouse events are parsed from ANSI escape sequences in both X10 and SGR formats,
	/// supporting button presses, releases, clicks, double-clicks, triple-clicks, and wheel events.
	/// </para>
	/// </remarks>
	public class NetConsoleDriver : IConsoleDriver
	{
		private const uint DISABLE_NEWLINE_AUTO_RETURN = 8;
		private const int DoubleClickTime = 500;
		private const uint ENABLE_ECHO_INPUT = 4;
		private const uint ENABLE_EXTENDED_FLAGS = 128;
		private const uint ENABLE_INSERT_MODE = 32;
		private const uint ENABLE_LINE_INPUT = 2;
		private const uint ENABLE_LVB_GRID_WORLDWIDE = 10;
		private const uint ENABLE_MOUSE_INPUT = 16;

		// Input modes.
		private const uint ENABLE_PROCESSED_INPUT = 1;

		// Output modes.
		private const uint ENABLE_PROCESSED_OUTPUT = 1;

		private const uint ENABLE_QUICK_EDIT_MODE = 64;
		private const uint ENABLE_VIRTUAL_TERMINAL_INPUT = 512;
		private const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 4;
		private const uint ENABLE_WINDOW_INPUT = 8;
		private const uint ENABLE_WRAP_AT_EOL_OUTPUT = 2;
		private const int STD_ERROR_HANDLE = -12;
		private const int STD_INPUT_HANDLE = -10;
		private const int STD_OUTPUT_HANDLE = -11;

		// ===== FIX TOGGLES =====

		private ConsoleWindowSystem? _consoleWindowSystem;
		private object? _consoleLock; // Shared lock for thread-safe Console I/O
		private readonly nint _errorHandle;
		private readonly nint _inputHandle;
		private readonly uint _originalErrorConsoleMode;
		private readonly uint _originalInputConsoleMode;
		private readonly uint _originalOutputConsoleMode;
		private readonly nint _outputHandle;
		private ConsoleBuffer? _consoleBuffer;
		private MouseFlags _lastButton;
		private Point? _lastClickPosition;
		private DateTime _lastClickTime;
		private int _lastConsoleHeight;
		private int _lastConsoleWidth;
		private bool _running = false;
		private EventHandler? _processExitHandler;

		/// <summary>
		/// Initializes a new instance of the <see cref="NetConsoleDriver"/> class with configuration options.
		/// </summary>
		/// <param name="options">Configuration options for the driver.</param>
		/// <exception cref="ArgumentNullException">
		/// Thrown when <paramref name="options"/> is null.
		/// </exception>
		/// <exception cref="ApplicationException">
		/// Thrown when console mode configuration fails on Windows platforms.
		/// </exception>
		public NetConsoleDriver(NetConsoleDriverOptions options)
		{
			Options = options ?? throw new ArgumentNullException(nameof(options));
			RenderMode = options.RenderMode;

			Console.OutputEncoding = Encoding.UTF8;

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				_inputHandle = GetStdHandle(STD_INPUT_HANDLE);

				if (!GetConsoleMode(_inputHandle, out uint mode))
				{
					throw new ApplicationException($"Failed to get input console mode, error code: {GetLastError()}.");
				}

				_originalInputConsoleMode = mode;

				mode |= ENABLE_MOUSE_INPUT;

				if ((mode & ENABLE_VIRTUAL_TERMINAL_INPUT) < ENABLE_VIRTUAL_TERMINAL_INPUT)
				{
					mode |= ENABLE_VIRTUAL_TERMINAL_INPUT;

					if (!SetConsoleMode(_inputHandle, mode))
					{
						throw new ApplicationException($"Failed to set input console mode, error code: {GetLastError()}.");
					}
				}

				_outputHandle = GetStdHandle(STD_OUTPUT_HANDLE);

				if (!GetConsoleMode(_outputHandle, out mode))
				{
					throw new ApplicationException($"Failed to get output console mode, error code: {GetLastError()}.");
				}

				_originalOutputConsoleMode = mode;

				if ((mode & (ENABLE_VIRTUAL_TERMINAL_PROCESSING | DISABLE_NEWLINE_AUTO_RETURN)) < DISABLE_NEWLINE_AUTO_RETURN)
				{
					mode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING | DISABLE_NEWLINE_AUTO_RETURN;

					if (!SetConsoleMode(_outputHandle, mode))
					{
						throw new ApplicationException($"Failed to set output console mode, error code: {GetLastError()}.");
					}
				}

				_errorHandle = GetStdHandle(STD_ERROR_HANDLE);

				if (!GetConsoleMode(_errorHandle, out mode))
				{
					throw new ApplicationException($"Failed to get error console mode, error code: {GetLastError()}.");
				}

				_originalErrorConsoleMode = mode;

				if ((mode & DISABLE_NEWLINE_AUTO_RETURN) < DISABLE_NEWLINE_AUTO_RETURN)
				{
					mode |= DISABLE_NEWLINE_AUTO_RETURN;

					if (!SetConsoleMode(_errorHandle, mode))
					{
						throw new ApplicationException($"Failed to set error console mode, error code: {GetLastError()}.");
					}
				}
			}

			// Register process exit handler for emergency cleanup
			_processExitHandler = OnProcessExit;
			AppDomain.CurrentDomain.ProcessExit += _processExitHandler;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="NetConsoleDriver"/> class with a specific render mode.
		/// </summary>
		/// <param name="renderMode">The rendering mode to use. Defaults to <see cref="RenderMode.Buffer"/>.</param>
		/// <exception cref="ApplicationException">
		/// Thrown when console mode configuration fails on Windows platforms.
		/// </exception>
		public NetConsoleDriver(RenderMode renderMode = RenderMode.Buffer)
			: this(new NetConsoleDriverOptions { RenderMode = renderMode })
		{
		}

		/// <inheritdoc/>
		public event EventHandler<ConsoleKeyInfo>? KeyPressed;

		/// <inheritdoc/>
		public event IConsoleDriver.MouseEventHandler? MouseEvent;

		/// <inheritdoc/>
		public event EventHandler<Size>? ScreenResized;

		/// <summary>
		/// Gets the driver configuration options.
		/// </summary>
		public NetConsoleDriverOptions Options { get; }

		/// <summary>
		/// Gets the rendering mode for console output.
		/// </summary>
		/// <value>The current render mode.</value>
		/// <remarks>
		/// The render mode is set during driver construction and cannot be changed afterward.
		/// </remarks>
		public RenderMode RenderMode { get; }

		/// <inheritdoc/>
		public Size ScreenSize => new Size(Console.WindowWidth, Console.WindowHeight);

		/// <summary>
		/// Restores the console to its original configuration.
		/// </summary>
		/// <remarks>
		/// On Windows, this method restores the original console modes for input, output, and error handles.
		/// This method is automatically called by <see cref="Stop"/>.
		/// </remarks>
		/// <exception cref="ApplicationException">
		/// Thrown when restoring console modes fails on Windows platforms.
		/// </exception>
		public void Cleanup()
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				if (!SetConsoleMode(_inputHandle, _originalInputConsoleMode))
				{
					throw new ApplicationException($"Failed to restore input console mode, error code: {GetLastError()}.");
				}

				if (!SetConsoleMode(_outputHandle, _originalOutputConsoleMode))
				{
					throw new ApplicationException($"Failed to restore output console mode, error code: {GetLastError()}.");
				}

				if (!SetConsoleMode(_errorHandle, _originalErrorConsoleMode))
				{
					throw new ApplicationException($"Failed to restore error console mode, error code: {GetLastError()}.");
				}
			}
		}

		/// <inheritdoc/>
		public void Initialize(ConsoleWindowSystem windowSystem)
		{
			_consoleWindowSystem = windowSystem ?? throw new ArgumentNullException(nameof(windowSystem));
			_consoleLock = windowSystem.ConsoleLock;
		}

		/// <inheritdoc/>
		public void Clear()
		{
			switch (RenderMode)
			{
				case RenderMode.Direct:
					Console.Clear();
					break;

				case RenderMode.Buffer:
					_consoleBuffer?.Clear();
					break;
			}
		}

		/// <inheritdoc/>
		public void Flush()
		{
			if (RenderMode.Buffer == RenderMode)
			{
				_consoleBuffer?.Render();
			}
		}

		/// <inheritdoc/>
		public void Start()
		{
			if (RenderMode.Buffer == RenderMode)
			{
				_consoleBuffer = new ConsoleBuffer(Console.WindowWidth, Console.WindowHeight, _consoleWindowSystem?.Options, _consoleLock);
			}

			_running = true;

			// Hide cursor via service (if available) or directly
			if (_consoleWindowSystem != null)
			{
				_consoleWindowSystem.CursorStateService.HideCursor();
				_consoleWindowSystem.CursorStateService.ApplyCursorToConsole(Console.WindowWidth, Console.WindowHeight);
			}
			else
			{
				Console.CursorVisible = false;
			}


			// Enable mouse reporting in proper order: basic -> extended modes -> drag tracking
			Console.Out.Write("\x1b[?1000h");  // Enable basic mouse reporting
			Console.Out.Write("\x1b[?1006h");  // Enable SGR extended mouse mode
			Console.Out.Write("\x1b[?1015h");  // Enable urxvt extended mouse mode
			Console.Out.Write("\x1b[?1002h");  // Enable button event tracking (drag mode)
			Console.Out.Write("\x1b[?1003h");  // Enable any event mouse (motion tracking)

			// Disable autowrap to prevent terminal scroll when writing to bottom-right corner
			Console.Out.Write("\x1b[?7l");

			_lastConsoleWidth = Console.WindowWidth;
			_lastConsoleHeight = Console.WindowHeight;

			var inputTask = Task.Run(InputLoop);
			var resizeTask = Task.Run(ResizeLoop);
		}

		/// <inheritdoc/>
		public void Stop()
		{
			_running = false;

			// Unregister emergency handler - we're doing proper cleanup now
			if (_processExitHandler != null)
			{
				AppDomain.CurrentDomain.ProcessExit -= _processExitHandler;
				_processExitHandler = null;
			}

			// Soft terminal reset first
			var resetSequence = "\x1b[!p";  // Soft reset (RIS)

			// TRIPLE-SEND the mouse disable sequences
			var mouseDisable =
				"\x1b[?1003l" +  // Disable any event mouse
				"\x1b[?1002l" +  // Disable button event tracking
				"\x1b[?1015l" +  // Disable urxvt extended mouse mode
				"\x1b[?1006l" +  // Disable SGR extended mouse mode
				"\x1b[?1000l";   // Disable basic mouse reporting

			var cleanupSequence =
				resetSequence +
				mouseDisable +
				mouseDisable +  // Second time
				mouseDisable +  // Third time
				"\x1b[?7h" +    // Re-enable autowrap
				"\x1b[0m" +     // Reset ANSI attributes
				"\x1b[?25h" +   // Make cursor visible
				"\n";

			// Write to /dev/tty on Unix
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
			    RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ||
			    RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
			{
				try
				{
					using var tty = new System.IO.FileStream("/dev/tty",
						System.IO.FileMode.Open,
						System.IO.FileAccess.Write,
						System.IO.FileShare.ReadWrite);
					var bytes = Encoding.UTF8.GetBytes(cleanupSequence);
					tty.Write(bytes, 0, bytes.Length);
					tty.Flush();
					tty.Write(bytes, 0, bytes.Length);
					tty.Flush();
				}
				catch
				{
					Console.Error.Write(cleanupSequence);
					Console.Error.Flush();
				}
			}
			else
			{
				Console.Error.Write(cleanupSequence);
				Console.Error.Flush();
			}

			Console.Out.Write(cleanupSequence);
			Console.Out.Flush();
			Console.ResetColor();

			Thread.Sleep(50);

			Cleanup();

			Console.Clear();

			// Restore cursor visibility on shutdown
			SetCursorVisible(true);
			ResetCursorShape();
		}

		/// <inheritdoc/>
		public void SetCursorPosition(int x, int y)
		{
			Console.SetCursorPosition(x, y);
		}

		/// <inheritdoc/>
		public void SetCursorVisible(bool visible)
		{
			Console.CursorVisible = visible;
		}

		/// <inheritdoc/>
		public void SetCursorShape(Core.CursorShape shape)
		{
			// ANSI escape sequence for cursor shape: ESC [ n SP q
			// Shape codes: 1=blinking block, 2=steady block, 3=blinking underline,
			// 4=steady underline, 5=blinking bar, 6=steady bar
			int shapeCode = shape switch
			{
				Core.CursorShape.Block => 2,        // Steady block
				Core.CursorShape.Underline => 4,    // Steady underline
				Core.CursorShape.VerticalBar => 6,  // Steady bar
				Core.CursorShape.Hidden => 0,       // Will be handled by SetCursorVisible
				_ => 2  // Default to steady block
			};

			if (shapeCode > 0)
			{
				Console.Write($"\x1b[{shapeCode} q");
			}
		}

		/// <inheritdoc/>
		public void ResetCursorShape()
		{
			// Reset to default: ESC [ 0 SP q
			Console.Write("\x1b[0 q");
		}

		/// <inheritdoc/>
		public void WriteToConsole(int x, int y, string value)
		{
			switch (RenderMode)
			{
				case RenderMode.Direct:
					Console.SetCursorPosition(x, y);
					Console.Write(value);
					break;


				case RenderMode.Buffer:
					// DIAGNOSTIC: Log any write containing box-drawing char at problematic lines
					if ((y == 3 || y == 10 || y == 14 || y == 67) && value.Contains('─'))
					{
						var logValue = value.Replace("\x1b", "<ESC>");
					}
					_consoleBuffer?.AddContent(x, y, value);
					break;
			}
		}

		/// <summary>
		/// Gets the count of dirty characters in the rendering buffer.
		/// </summary>
		/// <returns>The number of dirty characters, or 0 if not using buffered rendering.</returns>
		public int GetDirtyCharacterCount()
		{
			return RenderMode == RenderMode.Buffer && _consoleBuffer != null
				? _consoleBuffer.GetDirtyCharacterCount()
				: 0;
		}

		/// <summary>
		/// Emergency cleanup handler called on process exit.
		/// </summary>
		/// <remarks>
		/// This handler ensures console cleanup even during forceful termination,
		/// crashes, or abnormal exits. Errors are swallowed since process is exiting.
		/// </remarks>
		private void OnProcessExit(object? sender, EventArgs e)
		{
			try
			{
				EmergencyCleanup();
			}
			catch
			{
				// Ignore all errors during emergency cleanup - process is exiting anyway
			}
		}

		/// <summary>
		/// Performs minimal essential cleanup when process is terminating.
		/// </summary>
		/// <remarks>
		/// Only executes critical escape sequences to restore terminal state.
		/// Does not clear screen or perform complex operations.
		/// All errors are swallowed to ensure cleanup completes.
		/// </remarks>
		private void EmergencyCleanup()
		{
			try
			{
				// Soft terminal reset first
				var resetSequence = "\x1b[!p";  // Soft reset (RIS)

				// TRIPLE-SEND the mouse disable sequences
				var mouseDisable =
					"\x1b[?1003l" +  // Disable any event mouse
					"\x1b[?1002l" +  // Disable button event tracking
					"\x1b[?1015l" +  // Disable urxvt extended mouse mode
					"\x1b[?1006l" +  // Disable SGR extended mouse mode
					"\x1b[?1000l";   // Disable basic mouse reporting

				var cleanupSequence =
					resetSequence +
					mouseDisable +
					mouseDisable +  // Second time
					mouseDisable +  // Third time
					"\x1b[?7h" +    // Re-enable autowrap
					"\x1b[0m" +     // Reset ANSI attributes
					"\x1b[?25h" +   // Make cursor visible
					"\n";

				// Write to /dev/tty on Unix
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
				    RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ||
				    RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
				{
					try
					{
						using var tty = new System.IO.FileStream("/dev/tty",
							System.IO.FileMode.Open,
							System.IO.FileAccess.Write,
							System.IO.FileShare.ReadWrite);
						var bytes = Encoding.UTF8.GetBytes(cleanupSequence);
						tty.Write(bytes, 0, bytes.Length);
						tty.Flush();
						tty.Write(bytes, 0, bytes.Length);
						tty.Flush();
					}
					catch
					{
						Console.Error.Write(cleanupSequence);
						Console.Error.Flush();
					}
				}
				else
				{
					Console.Error.Write(cleanupSequence);
					Console.Error.Flush();
				}

				Console.Out.Write(cleanupSequence);
				Console.Out.Flush();
				Console.ResetColor();

				try { Console.CursorVisible = true; } catch { }

				Thread.Sleep(50);
			}
			catch
			{
				// Swallow all errors - exiting anyway
			}
		}

		[DllImport("kernel32.dll")]
		private static extern bool GetConsoleMode(nint hConsoleHandle, out uint lpMode);

		[DllImport("kernel32.dll")]
		private static extern uint GetLastError();

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern nint GetStdHandle(int nStdHandle);

		[DllImport("kernel32.dll")]
		private static extern bool SetConsoleMode(nint hConsoleHandle, uint dwMode);

		private void InputLoop()
		{
			while (_running == true)
			{
				// CRITICAL: Acquire lock BEFORE checking Console.KeyAvailable to prevent
				// concurrent Console I/O from corrupting ANSI sequence parsing
				lock (_consoleLock ?? new object())
				{
					if (Console.KeyAvailable)
					{
						var key = Console.ReadKey(true);

						List<ConsoleKeyInfo> consoleKeyInfoSequence = new List<ConsoleKeyInfo>();
						consoleKeyInfoSequence.Add(key);

					// Normalize Ctrl+Space: Terminals often send this as Ctrl+2 (D2) with NUL character (0x00)
					if (key.Key == ConsoleKey.D2 && (key.Modifiers & ConsoleModifiers.Control) != 0 && key.KeyChar == '\0')
					{
						var normalizedKey = new ConsoleKeyInfo(
							' ',                          // Spacebar character
							ConsoleKey.Spacebar,          // Spacebar key
							false,                        // shift
							false,                        // alt
							true                          // ctrl
						);
						KeyPressed?.Invoke(this, normalizedKey);
						continue;
					}

					// Map control characters
					if (key.KeyChar >= '\u0001' && key.KeyChar <= '\u001A' && key.KeyChar != '\t' && key.KeyChar != '\r')
					{
						// Convert control character to corresponding letter (A-Z)
						char baseChar = (char)(key.KeyChar + 'A' - 1);

						// Create new ConsoleKeyInfo with the mapped key
						var mappedKey = new ConsoleKeyInfo(
							key.KeyChar,  // Keep original control character
							(ConsoleKey)baseChar, // Map to corresponding letter's ConsoleKey
							false,  // shift
							false,  // alt
							true    // ctrl
						);

						KeyPressed?.Invoke(this, mappedKey);
						continue;
					}

					switch (key.KeyChar)
					{
						case '\b': // Backspace
							KeyPressed?.Invoke(this, new ConsoleKeyInfo('\b', ConsoleKey.Backspace, (key.Modifiers & ConsoleModifiers.Shift) != 0, (key.Modifiers & ConsoleModifiers.Alt) != 0, (key.Modifiers & ConsoleModifiers.Control) != 0));
							continue;
						case '\x7f': // Backspace
							KeyPressed?.Invoke(this, new ConsoleKeyInfo('\x7f', ConsoleKey.Backspace, (key.Modifiers & ConsoleModifiers.Shift) != 0, (key.Modifiers & ConsoleModifiers.Alt) != 0, (key.Modifiers & ConsoleModifiers.Control) != 0));
							continue;
						case '\t': // Tab
							KeyPressed?.Invoke(this, new ConsoleKeyInfo('\t', ConsoleKey.Tab, (key.Modifiers & ConsoleModifiers.Shift) != 0, (key.Modifiers & ConsoleModifiers.Alt) != 0, (key.Modifiers & ConsoleModifiers.Control) != 0));
							continue;
						case '\r': // Enter
							KeyPressed?.Invoke(this, new ConsoleKeyInfo('\r', ConsoleKey.Enter, (key.Modifiers & ConsoleModifiers.Shift) != 0, (key.Modifiers & ConsoleModifiers.Alt) != 0, (key.Modifiers & ConsoleModifiers.Control) != 0));
							continue;
						case ' ': // Spacebar
							KeyPressed?.Invoke(this, new ConsoleKeyInfo(' ', ConsoleKey.Spacebar, (key.Modifiers & ConsoleModifiers.Shift) != 0, (key.Modifiers & ConsoleModifiers.Alt) != 0, (key.Modifiers & ConsoleModifiers.Control) != 0));
							continue;
					}

					if (key.KeyChar == '\x1b' || key.KeyChar == '\u001b') // ESC character
					{
						if (Console.KeyAvailable)
						{
								var nextKey = Console.ReadKey(true);
								consoleKeyInfoSequence.Add(nextKey);

								if (nextKey.KeyChar == '[' || nextKey.KeyChar == 'O') // Handle both CSI and SS3
								{
									var ansiSequence = new StringBuilder();
									while (Console.KeyAvailable)
									{
										var ansiKey = Console.ReadKey(true);
									consoleKeyInfoSequence.Add(ansiKey);

									if (char.IsLetter(ansiKey.KeyChar) || ansiKey.KeyChar == '~' || ansiKey.KeyChar == '\r' || ansiKey.KeyChar == '\t')
									{
										ansiSequence.Append(ansiKey.KeyChar);
										break;
									}
									ansiSequence.Append(ansiKey.KeyChar);
								}

								// Check if this is a SGR mouse sequence (format: ESC[<button;x;y M/m)
								if (ansiSequence.ToString().StartsWith("<") && (ansiSequence.ToString().EndsWith("M") || ansiSequence.ToString().EndsWith("m")))
								{
									// FIX23: Log mouse sequence detection
									{
										string mouseSeq = ansiSequence.ToString();
									}

									// Use SequenceHelper for SGR mouse parsing (supports unlimited coordinates)
									// Provide proper continuous button press handler for drag operations
									SequenceHelper.GetMouse(consoleKeyInfoSequence.ToArray(), out List<MouseFlags> mouseFlags, out Point pos, 
										(flags, position) => {
											// Handle continuous mouse events (drag operations)
											var continuousFlags = new List<MouseFlags> { flags };
											MouseEvent?.Invoke(this, continuousFlags, position);
										});
									if (mouseFlags.Count > 0)
									{
										MouseEvent?.Invoke(this, mouseFlags, pos);
										continue;
									}
									else
									{
									}
								}

								var consoleKeyInfo = MapAnsiToConsoleKeyInfo(ansiSequence.ToString(), consoleKeyInfoSequence);

								if (consoleKeyInfo.isMouse != true && consoleKeyInfo.consoleKeyInfo.HasValue)
								{
									KeyPressed?.Invoke(this, consoleKeyInfo.consoleKeyInfo.Value);
									continue;
								}
							}
							else
							{
								// This is an Alt + key combination (ESC followed by key)
								var altKey = new ConsoleKeyInfo(
									nextKey.KeyChar,
									nextKey.Key,
									(nextKey.Modifiers & ConsoleModifiers.Shift) != 0,
									true, // Alt is pressed
									(nextKey.Modifiers & ConsoleModifiers.Control) != 0);

								KeyPressed?.Invoke(this, altKey);
								continue;
							}
						}
						else
						{
							// Plain ESC key
							KeyPressed?.Invoke(this, new ConsoleKeyInfo('\x1b', ConsoleKey.Escape, false, false, false));
							continue;
						}
					}
					else
					{
						// Regular key press
						KeyPressed?.Invoke(this, key);
					}
				}
				} // End lock(_consoleLock) - all Console I/O now protected
				Thread.Sleep(10);
			}
		}

		private (ConsoleKeyInfo? consoleKeyInfo, bool isMouse) MapAnsiToConsoleKeyInfo(string ansiSequence, List<ConsoleKeyInfo> consoleKeyInfoSequence)
		{
			bool shift = false;
			bool alt = false;
			bool ctrl = false;

			string originalSequence = ansiSequence;

			// Check for modifier codes in the sequence
			if (ansiSequence.Length > 1 && char.IsDigit(ansiSequence[0]))
			{
				var parts = ansiSequence.Split(';');
				if (parts.Length > 1)
				{
					// Separate the numeric part from the character part
					string numericPart = new string(parts[1].TakeWhile(char.IsDigit).ToArray());
					string charPart = new string(parts[1].SkipWhile(char.IsDigit).ToArray());

					if (int.TryParse(numericPart, out int modifierCode))
					{
						// ANSI terminal modifiers are 1-based where:
						// 1 = No modifier
						// 2 = Shift
						// 3 = Alt
						// 4 = Shift + Alt
						// 5 = Control
						// 6 = Shift + Control
						// 7 = Alt + Control
						// 8 = Shift + Alt + Control
						modifierCode--; // Convert to 0-based for bit operations
						shift = (modifierCode & 1) != 0;  // Shift is bit 0
						alt = (modifierCode & 2) != 0;    // Alt is bit 1
						ctrl = (modifierCode & 4) != 0;   // Ctrl is bit 2
					}

					// Append the character part to the remaining sequence
					ansiSequence = parts[0] + charPart;
				}
			}

			// Handle the final character in the ANSI sequence
			char finalChar = ansiSequence.Last();
			ansiSequence = ansiSequence.TrimEnd(finalChar);

			switch (finalChar)
			{
				// Enter key variations
				case '\r':
					return (new ConsoleKeyInfo('\r', ConsoleKey.Enter, shift, alt, ctrl), false);

				// Tab key variations
				case '\t':
					return (new ConsoleKeyInfo('\t', ConsoleKey.Tab, shift, alt, ctrl), false);

				case 'I':  // Some terminals send this for Tab
					return (new ConsoleKeyInfo('\t', ConsoleKey.Tab, shift, alt, ctrl), false);

				case 'Z':  // Shift+Tab
					return (new ConsoleKeyInfo('\t', ConsoleKey.Tab, true, alt, ctrl), false);

				// Arrow keys
				case 'A': return (new ConsoleKeyInfo('\0', ConsoleKey.UpArrow, shift, alt, ctrl), false);
				case 'B': return (new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, shift, alt, ctrl), false);
				case 'C': return (new ConsoleKeyInfo('\0', ConsoleKey.RightArrow, shift, alt, ctrl), false);
				case 'D': return (new ConsoleKeyInfo('\0', ConsoleKey.LeftArrow, shift, alt, ctrl), false);

				// Navigation keys
				case 'H': return (new ConsoleKeyInfo('\x1b', ConsoleKey.Home, shift, alt, ctrl), false);
				case 'F': return (new ConsoleKeyInfo('\x1b', ConsoleKey.End, shift, alt, ctrl), false);
				case 'E': return (new ConsoleKeyInfo('\x1b', ConsoleKey.Clear, shift, alt, ctrl), false);

				// Function keys (CSI O)
				case 'P': return (new ConsoleKeyInfo('\0', ConsoleKey.F1, shift, alt, ctrl), false);
				case 'Q': return (new ConsoleKeyInfo('\0', ConsoleKey.F2, shift, alt, ctrl), false);
				case 'R': return (new ConsoleKeyInfo('\0', ConsoleKey.F3, shift, alt, ctrl), false);
				case 'S': return (new ConsoleKeyInfo('\0', ConsoleKey.F4, shift, alt, ctrl), false);

				case '~':
					switch (ansiSequence)
					{
						// Navigation keys
						case "1": return (new ConsoleKeyInfo('\x1b', ConsoleKey.Home, shift, alt, ctrl), false);
						case "2": return (new ConsoleKeyInfo('\x1b', ConsoleKey.Insert, shift, alt, ctrl), false);
						case "3": return (new ConsoleKeyInfo('\x1b', ConsoleKey.Delete, shift, alt, ctrl), false);
						case "4": return (new ConsoleKeyInfo('\x1b', ConsoleKey.End, shift, alt, ctrl), false);
						case "5": return (new ConsoleKeyInfo('\x1b', ConsoleKey.PageUp, shift, alt, ctrl), false);
						case "6": return (new ConsoleKeyInfo('\x1b', ConsoleKey.PageDown, shift, alt, ctrl), false);

						// Function keys
						case "11": return (new ConsoleKeyInfo('\0', ConsoleKey.F1, shift, alt, ctrl), false);
						case "12": return (new ConsoleKeyInfo('\0', ConsoleKey.F2, shift, alt, ctrl), false);
						case "13": return (new ConsoleKeyInfo('\0', ConsoleKey.F3, shift, alt, ctrl), false);
						case "14": return (new ConsoleKeyInfo('\0', ConsoleKey.F4, shift, alt, ctrl), false);
						case "15": return (new ConsoleKeyInfo('\0', ConsoleKey.F5, shift, alt, ctrl), false);
						case "17": return (new ConsoleKeyInfo('\0', ConsoleKey.F6, shift, alt, ctrl), false);
						case "18": return (new ConsoleKeyInfo('\0', ConsoleKey.F7, shift, alt, ctrl), false);
						case "19": return (new ConsoleKeyInfo('\0', ConsoleKey.F8, shift, alt, ctrl), false);
						case "20": return (new ConsoleKeyInfo('\0', ConsoleKey.F9, shift, alt, ctrl), false);
						case "21": return (new ConsoleKeyInfo('\0', ConsoleKey.F10, shift, alt, ctrl), false);
						case "23": return (new ConsoleKeyInfo('\0', ConsoleKey.F11, shift, alt, ctrl), false);
						case "24": return (new ConsoleKeyInfo('\0', ConsoleKey.F12, shift, alt, ctrl), false);
					}
					break;

				// Media/Special keys  
				case 'M': // Mouse events - legacy X10 format (ESC [ M <button> <x> <y>)
					// Read the remaining mouse data for X10 format
					if (Console.KeyAvailable)
					{
						var button = Console.ReadKey(true);
						consoleKeyInfoSequence.Add(button);

						if (Console.KeyAvailable)
						{
							var xPos = Console.ReadKey(true);
							consoleKeyInfoSequence.Add(xPos);

							if (Console.KeyAvailable)
							{
								var yPos = Console.ReadKey(true);
								consoleKeyInfoSequence.Add(yPos);

								if (ParseMouseSequence(consoleKeyInfoSequence.ToArray(), out List<MouseFlags> mouseFlags, out Point pos))
								{
									MouseEvent?.Invoke(this, mouseFlags, pos);
								}
							}
						}
					}
					break;
			}

			return (null, false);
		}

		private bool ParseMouseSequence(ConsoleKeyInfo[] sequence, out List<MouseFlags> mouseFlags, out Point position)
		{
			mouseFlags = new List<MouseFlags>();
			position = new Point(0, 0);

			// We need at least 6 bytes (ESC [ M <button> <x> <y>)
			if (sequence.Length < 6)
				return false;

			try
			{
				// The button info is in the 4th byte (index 3)
				// Subtract 32 as per ANSI mouse protocol
				int buttonCode = sequence[3].KeyChar - 32;

				// Extract coordinates (subtract 32 from the raw values as per ANSI mouse protocol)
				// NOTE: This X10 format is limited to coordinates 0-222 (255-32-1)
				// For coordinates beyond this range, SGR mode should be used instead
				position.X = Math.Max(0, sequence[4].KeyChar - 33); // Convert to 0-based (32 + 1)
				position.Y = Math.Max(0, sequence[5].KeyChar - 33); // Convert to 0-based (32 + 1)

				// Set modifier flags first
				if ((buttonCode & 0x04) != 0) mouseFlags.Add(MouseFlags.ButtonShift);
				if ((buttonCode & 0x08) != 0) mouseFlags.Add(MouseFlags.ButtonAlt);
				if ((buttonCode & 0x10) != 0) mouseFlags.Add(MouseFlags.ButtonCtrl);

				// Check for mouse motion
				bool motion = (buttonCode & 0x20) != 0;
				if (motion)
				{
					mouseFlags.Add(MouseFlags.ReportMousePosition);
					// For motion events, the bottom 2 bits indicate which button is being dragged (if any)
					int dragButton = buttonCode & 0x03;
					switch (dragButton)
					{
						case 0: mouseFlags.Add(MouseFlags.Button1Pressed); break;
						case 1: mouseFlags.Add(MouseFlags.Button2Pressed); break;
						case 2: mouseFlags.Add(MouseFlags.Button3Pressed); break;
					}
					MouseEvent?.Invoke(this, mouseFlags, position); // Raise the MouseEvent
					return true;
				}

				// Check for button release events
				bool isRelease = (buttonCode & 0x03) == 3;
				int buttonNumber = (buttonCode >> 2) & 0x03;

				// Handle button events
				switch (buttonNumber)
				{
					case 0: // Button 1 (Left button)
						if (!isRelease)
						{
							mouseFlags.Add(MouseFlags.Button1Pressed);
							_lastButton = MouseFlags.Button1Pressed;
						}
					else if (_lastButton == MouseFlags.Button1Pressed)
					{
						// SAFEGUARD: Ignore duplicate release events (< 50ms = driver bug)
						// The console mouse driver sometimes generates duplicate Button1Released events
						var timeSinceLastClick = (DateTime.Now - _lastClickTime).TotalMilliseconds;
						if (_lastClickPosition?.X == position.X &&
							_lastClickPosition?.Y == position.Y &&
							timeSinceLastClick < 50)
						{
							// Duplicate event, ignore it
							return true;
						}

						mouseFlags.Add(MouseFlags.Button1Released);
						mouseFlags.Add(MouseFlags.Button1Clicked);

						// Check for double/triple click
						if (_lastClickPosition?.X == position.X &&
							_lastClickPosition?.Y == position.Y &&
							timeSinceLastClick < DoubleClickTime)
						{
							if (mouseFlags.Contains(MouseFlags.Button1DoubleClicked))
								mouseFlags.Add(MouseFlags.Button1TripleClicked);
							else
								mouseFlags.Add(MouseFlags.Button1DoubleClicked);
						}

						_lastClickPosition = position;
						_lastClickTime = DateTime.Now;
					}
						break;

					case 1: // Button 2 (Middle button)
						if (!isRelease)
						{
							mouseFlags.Add(MouseFlags.Button2Pressed);
							_lastButton = MouseFlags.Button2Pressed;
						}
						else if (_lastButton == MouseFlags.Button2Pressed)
						{
							mouseFlags.Add(MouseFlags.Button2Released);
							mouseFlags.Add(MouseFlags.Button2Clicked);
						}
						break;

					case 2: // Button 3 (Right button)
						if (!isRelease)
						{
							mouseFlags.Add(MouseFlags.Button3Pressed);
							_lastButton = MouseFlags.Button3Pressed;
						}
						else if (_lastButton == MouseFlags.Button3Pressed)
						{
							mouseFlags.Add(MouseFlags.Button3Released);
							mouseFlags.Add(MouseFlags.Button3Clicked);
						}
						break;

					case 3: // Special cases - includes wheel events
						if ((buttonCode & 0x40) != 0) // This bit indicates wheel event
						{
							if ((buttonCode & 0x01) != 0)
							{
								mouseFlags.Add(MouseFlags.WheeledUp);
								if ((buttonCode & 0x10) != 0) // Ctrl is pressed
									mouseFlags.Add(MouseFlags.WheeledLeft);
							}
							else
							{
								mouseFlags.Add(MouseFlags.WheeledDown);
								if ((buttonCode & 0x10) != 0) // Ctrl is pressed
									mouseFlags.Add(MouseFlags.WheeledRight);
							}
						}
						break;
				}

				return true;
			}
			catch
			{
				return false;
			}
		}

		private void ResizeLoop()
		{
			while (_running == true)
			{
				if (Console.WindowWidth != _lastConsoleWidth || Console.WindowHeight != _lastConsoleHeight)
				{
					if (RenderMode.Buffer == RenderMode)
					{
						_consoleBuffer!.Lock = true;
						_consoleBuffer = new ConsoleBuffer(Console.WindowWidth, Console.WindowHeight, _consoleWindowSystem?.Options, _consoleLock);
						_consoleBuffer.Lock = false;
					}

					ScreenResized?.Invoke(this, ScreenSize);

					_lastConsoleWidth = Console.WindowWidth;
					_lastConsoleHeight = Console.WindowHeight;
				}
				Thread.Sleep(50);
			}
		}
	}
}