using ConsoleEx.Helpers;
using ConsoleEx.Themes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleEx.Drivers
{
	public enum RenderMode
	{
		Direct,
		Buffer
	}

	public class NetConsoleDriver : IConsoleDriver
	{
		private const uint DISABLE_NEWLINE_AUTO_RETURN = 8;
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

		private readonly nint _errorHandle;
		private readonly nint _inputHandle;
		private readonly uint _originalErrorConsoleMode;
		private readonly uint _originalInputConsoleMode;
		private readonly uint _originalOutputConsoleMode;
		private readonly nint _outputHandle;

		private ConsoleWindowSystem? _consoleWindowSystem;
		private int _lastConsoleHeight;
		private int _lastConsoleWidth;
		private bool _running = false;

		public NetConsoleDriver(ConsoleWindowSystem consoleWindowSystem)
		{
			_consoleWindowSystem = consoleWindowSystem;

			Console.OutputEncoding = Encoding.UTF8;

			_inputHandle = GetStdHandle(STD_INPUT_HANDLE);

			if (!GetConsoleMode(_inputHandle, out uint mode))
			{
				throw new ApplicationException($"Failed to get input console mode, error code: {GetLastError()}.");
			}

			_originalInputConsoleMode = mode;

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

		public event EventHandler<ConsoleKeyInfo>? KeyPressed;

		public event EventHandler<Size>? ScreenResized;

		public Size ScreenSize => new Size(Console.WindowWidth, Console.WindowHeight);

		private RenderMode _renderMode { get; set; } = RenderMode.Direct;

		public void Cleanup()
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

		public void Clear()
		{
			Console.Clear();
		}

		public void Start()
		{
			_running = true;

			Console.CursorVisible = false;

			_lastConsoleWidth = Console.WindowWidth;
			_lastConsoleHeight = Console.WindowHeight;

			var inputTask = Task.Run(InputLoop);
			var resizeTask = Task.Run(ResizeLoop);
		}

		public void Stop()
		{
			_running = false;

			Cleanup();

			Console.Clear();

			Console.CursorVisible = true;
		}

		public void WriteToConsole(int x, int y, string value)
		{
			switch (_renderMode)
			{
				case RenderMode.Direct:
					Console.SetCursorPosition(x, y);
					Console.Write(value);
					break;
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

		/*
		private void InputLoop()
		{
			while (_running == true)
			{
				if (Console.KeyAvailable)
				{
					var key = Console.ReadKey(true);

					KeyPressed?.Invoke(this, key);
				}
				Thread.Sleep(10);
			}
		}
		*/

		private void InputLoop()
		{
			while (_running == true)
			{
				if (Console.KeyAvailable)
				{
					var key = Console.ReadKey(true);

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
							KeyPressed?.Invoke(this, new ConsoleKeyInfo('\b', ConsoleKey.Backspace, false, false, false));
							continue;
						case '\x7f': // Delete
							KeyPressed?.Invoke(this, new ConsoleKeyInfo('\x7f', ConsoleKey.Delete, false, false, false));
							continue;
						case '\t': // Tab
							KeyPressed?.Invoke(this, new ConsoleKeyInfo('\t', ConsoleKey.Tab, false, false, false));
							continue;
						case '\r': // Enter
							KeyPressed?.Invoke(this, new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false));
							continue;
					}

					if (key.KeyChar == '\x1b') // ESC character
					{
						if (Console.KeyAvailable)
						{
							var nextKey = Console.ReadKey(true);
							if (nextKey.KeyChar == '[' || nextKey.KeyChar == 'O') // Handle both CSI and SS3
							{
								var ansiSequence = new StringBuilder();
								while (Console.KeyAvailable)
								{
									var ansiKey = Console.ReadKey(true);
									if (char.IsLetter(ansiKey.KeyChar) || ansiKey.KeyChar == '~' || ansiKey.KeyChar == '\r' || ansiKey.KeyChar == '\t')
									{
										ansiSequence.Append(ansiKey.KeyChar);
										break;
									}
									ansiSequence.Append(ansiKey.KeyChar);
								}

								var consoleKeyInfo = MapAnsiToConsoleKeyInfo(ansiSequence.ToString());
								if (consoleKeyInfo.HasValue)
								{
									KeyPressed?.Invoke(this, consoleKeyInfo.Value);
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

					// Regular key press
					KeyPressed?.Invoke(this, key);
				}
				Thread.Sleep(10);
			}
		}

		private ConsoleKeyInfo? MapAnsiToConsoleKeyInfo(string ansiSequence)
		{
			bool shift = false;
			bool alt = false;
			bool ctrl = false;

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
					return new ConsoleKeyInfo('\r', ConsoleKey.Enter, shift, alt, ctrl);

				// Tab key variations
				case '\t':
					return new ConsoleKeyInfo('\t', ConsoleKey.Tab, shift, alt, ctrl);

				case 'I':  // Some terminals send this for Tab
					return new ConsoleKeyInfo('\t', ConsoleKey.Tab, shift, alt, ctrl);

				case 'Z':  // Shift+Tab
					return new ConsoleKeyInfo('\t', ConsoleKey.Tab, true, alt, ctrl);

				// Arrow keys
				case 'A': return new ConsoleKeyInfo('\0', ConsoleKey.UpArrow, shift, alt, ctrl);
				case 'B': return new ConsoleKeyInfo('\0', ConsoleKey.DownArrow, shift, alt, ctrl);
				case 'C': return new ConsoleKeyInfo('\0', ConsoleKey.RightArrow, shift, alt, ctrl);
				case 'D': return new ConsoleKeyInfo('\0', ConsoleKey.LeftArrow, shift, alt, ctrl);

				// Navigation keys
				case 'H': return new ConsoleKeyInfo('\x1b', ConsoleKey.Home, shift, alt, ctrl);
				case 'F': return new ConsoleKeyInfo('\x1b', ConsoleKey.End, shift, alt, ctrl);
				case 'E': return new ConsoleKeyInfo('\x1b', ConsoleKey.Clear, shift, alt, ctrl);

				// Function keys (CSI O)
				case 'P': return new ConsoleKeyInfo('\0', ConsoleKey.F1, shift, alt, ctrl);
				case 'Q': return new ConsoleKeyInfo('\0', ConsoleKey.F2, shift, alt, ctrl);
				case 'R': return new ConsoleKeyInfo('\0', ConsoleKey.F3, shift, alt, ctrl);
				case 'S': return new ConsoleKeyInfo('\0', ConsoleKey.F4, shift, alt, ctrl);

				case '~':
					switch (ansiSequence)
					{
						// Navigation keys
						case "1": return new ConsoleKeyInfo('\x1b', ConsoleKey.Home, shift, alt, ctrl);
						case "2": return new ConsoleKeyInfo('\x1b', ConsoleKey.Insert, shift, alt, ctrl);
						case "3": return new ConsoleKeyInfo('\x1b', ConsoleKey.Delete, shift, alt, ctrl);
						case "4": return new ConsoleKeyInfo('\x1b', ConsoleKey.End, shift, alt, ctrl);
						case "5": return new ConsoleKeyInfo('\x1b', ConsoleKey.PageUp, shift, alt, ctrl);
						case "6": return new ConsoleKeyInfo('\x1b', ConsoleKey.PageDown, shift, alt, ctrl);

						// Function keys
						case "11": return new ConsoleKeyInfo('\0', ConsoleKey.F1, shift, alt, ctrl);
						case "12": return new ConsoleKeyInfo('\0', ConsoleKey.F2, shift, alt, ctrl);
						case "13": return new ConsoleKeyInfo('\0', ConsoleKey.F3, shift, alt, ctrl);
						case "14": return new ConsoleKeyInfo('\0', ConsoleKey.F4, shift, alt, ctrl);
						case "15": return new ConsoleKeyInfo('\0', ConsoleKey.F5, shift, alt, ctrl);
						case "17": return new ConsoleKeyInfo('\0', ConsoleKey.F6, shift, alt, ctrl);
						case "18": return new ConsoleKeyInfo('\0', ConsoleKey.F7, shift, alt, ctrl);
						case "19": return new ConsoleKeyInfo('\0', ConsoleKey.F8, shift, alt, ctrl);
						case "20": return new ConsoleKeyInfo('\0', ConsoleKey.F9, shift, alt, ctrl);
						case "21": return new ConsoleKeyInfo('\0', ConsoleKey.F10, shift, alt, ctrl);
						case "23": return new ConsoleKeyInfo('\0', ConsoleKey.F11, shift, alt, ctrl);
						case "24": return new ConsoleKeyInfo('\0', ConsoleKey.F12, shift, alt, ctrl);
					}
					break;

				// Media/Special keys
				case 'M': // Mouse events (if needed)
					break;
			}

			return null;
		}

		private void ResizeLoop()
		{
			while (_running == true)
			{
				if (Console.WindowWidth != _lastConsoleWidth || Console.WindowHeight != _lastConsoleHeight)
				{
					ScreenResized?.Invoke(this, ScreenSize);

					_lastConsoleWidth = Console.WindowWidth;
					_lastConsoleHeight = Console.WindowHeight;
				}
				Thread.Sleep(50);
			}
		}
	}
}