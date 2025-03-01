// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using ConsoleEx.Helpers;
using ConsoleEx.Services.NotificationsService;
using ConsoleEx.Themes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Size = ConsoleEx.Helpers.Size;

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

		private readonly ConsoleWindowSystem? _consoleWindowSystem;
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

		public NetConsoleDriver(ConsoleWindowSystem consoleWindowSystem)
		{
			_consoleWindowSystem = consoleWindowSystem;

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
		}

		public event EventHandler<ConsoleKeyInfo>? KeyPressed;

		public event IConsoleDriver.MouseEventHandler? MouseEvent;

		public event EventHandler<Size>? ScreenResized;

		public RenderMode RenderMode { get; set; } = RenderMode.Direct;

		public Size ScreenSize => new Size(Console.WindowWidth, Console.WindowHeight);

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

		public void Flush()
		{
			if (RenderMode.Buffer == RenderMode)
			{
				_consoleBuffer?.Render();
			}
		}

		public void Start()
		{
			if (RenderMode.Buffer == RenderMode)
			{
				_consoleBuffer = new ConsoleBuffer(Console.WindowWidth, Console.WindowHeight);
			}

			_running = true;

			Console.CursorVisible = false;

			Console.Out.Write(SequenceHelper.CSI_EnableMouseEvents);

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
			switch (RenderMode)
			{
				case RenderMode.Direct:
					Console.SetCursorPosition(x, y);
					Console.Write(value);
					break;

				case RenderMode.Buffer:
					_consoleBuffer?.AddContent(x, y, value);
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

		private void InputLoop()
		{
			while (_running == true)
			{
				if (Console.KeyAvailable)
				{
					var key = Console.ReadKey(true);

					List<ConsoleKeyInfo> consoleKeyInfoSequence = new List<ConsoleKeyInfo>();
					consoleKeyInfoSequence.Add(key);

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
						case '\x7f': // Backspace
							KeyPressed?.Invoke(this, new ConsoleKeyInfo('\x7f', ConsoleKey.Backspace, false, false, false));
							continue;
						case '\t': // Tab
							KeyPressed?.Invoke(this, new ConsoleKeyInfo('\t', ConsoleKey.Tab, false, false, false));
							continue;
						case '\r': // Enter
							KeyPressed?.Invoke(this, new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false));
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
				case 'M': // Mouse events (if needed)
						  // We already have the initial ESC [ M sequence in consoleKeyInfoSequence
						  // Need to read 3 more bytes: button+mask, x coord, y coord
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
									MouseEvent?.Invoke(this, mouseFlags, pos); // Raise the MouseEvent
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
				position.X = sequence[4].KeyChar - 32;
				position.Y = sequence[5].KeyChar - 32;

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
							mouseFlags.Add(MouseFlags.Button1Released);
							mouseFlags.Add(MouseFlags.Button1Clicked);

							// Check for double/triple click
							if (_lastClickPosition?.X == position.X &&
								_lastClickPosition?.Y == position.Y &&
								(DateTime.Now - _lastClickTime).TotalMilliseconds < DoubleClickTime)
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
						_consoleBuffer = new ConsoleBuffer(Console.WindowWidth, Console.WindowHeight);
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