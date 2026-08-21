// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using SharpConsoleUI.Drivers;

// Code from Terminal.Gui - https://github.com/gui-cs/Terminal.Gui

namespace SharpConsoleUI.Helpers
{
	/// <summary>
	/// Provides helper methods and constants for handling ANSI escape sequences,
	/// mouse input parsing, and keyboard input processing in console applications.
	/// </summary>
	/// <remarks>
	/// This class contains functionality adapted from Terminal.Gui for parsing
	/// escape sequences and mouse events from console input.
	/// </remarks>
	public static class SequenceHelper
	{
		/// <summary>
		/// The Control Sequence Introducer (CSI) escape sequence prefix.
		/// </summary>
		public const string CSI = "\u001B[";

		/// <summary>
		/// The escape key character value.
		/// </summary>
		public const char KeyEsc = (char)KeyCode.Esc;

		/// <summary>
		/// The CSI sequence to enable any-event mouse tracking mode.
		/// </summary>
		public static readonly string CSI_EnableAnyEventMouse = CSI + "?1003h";

		/// <summary>
		/// The combined CSI sequence to enable all mouse event tracking modes.
		/// </summary>
		public static readonly string CSI_EnableMouseEvents = CSI_EnableAnyEventMouse + CSI_EnableSgrExtModeMouse;

		/// <summary>
		/// The CSI sequence to enable SGR extended mouse mode.
		/// </summary>
		public static readonly string CSI_EnableSgrExtModeMouse = CSI + "?1006h";

		private static bool _isButtonClicked;

		private static bool _isButtonDoubleClicked;

		// QUESTION: What's the difference between isButtonClicked and isButtonPressed?
		// Some clarity or comments would be handy, here.
		// It also seems like some enforcement of valid states might be a good idea.
		private static bool _isButtonPressed;

		private static bool _isButtonTripleClicked;

		private static MouseFlags? _lastMouseButtonPressed;
		private static Point? _point;
		private static DateTime _lastClickTime = DateTime.MinValue;

		/// <summary>
		/// Gets the C1 control character name for the specified character.
		/// </summary>
		/// <param name="c">The character following an escape sequence to interpret.</param>
		/// <returns>The name of the C1 control character, or an empty string if not recognized.</returns>
		/// <remarks>
		/// These control characters are used in vtXXX terminal emulation.
		/// </remarks>
		public static string GetC1ControlChar(in char c)
		{
			// These control characters are used in the vtXXX emulation.
			return c switch
			{
				'D' => "IND", // Index
				'E' => "NEL", // Next Line
				'H' => "HTS", // Tab Set
				'M' => "RI", // Reverse Index
				'N' => "SS2", // Single Shift Select of G2 Character Set: affects next character only
				'O' => "SS3", // Single Shift Select of G3 Character Set: affects next character only
				'P' => "DCS", // Device Control String
				'V' => "SPA", // Start of Guarded Area
				'W' => "EPA", // End of Guarded Area
				'X' => "SOS", // Start of String
				'Z' => "DECID", // Return Terminal ID Obsolete form of CSI c (DA)
				'[' => "CSI", // Control Sequence Introducer
				'\\' => "ST", // String Terminator
				']' => "OSC", // Operating System Command
				'^' => "PM", // Privacy Message
				'_' => "APC", // Application Program Command
				_ => string.Empty
			};
		}

		/// <summary>
		/// Parses an escape sequence from a character array and extracts its components.
		/// </summary>
		/// <param name="kChar">The character array containing the escape sequence.</param>
		/// <returns>
		/// A tuple containing:
		/// <list type="bullet">
		/// <item><description>c1Control: The C1 control character name (e.g., "CSI", "ESC").</description></item>
		/// <item><description>code: Any additional code characters in the sequence.</description></item>
		/// <item><description>values: The numeric parameter values separated by semicolons.</description></item>
		/// <item><description>terminating: The terminating character(s) of the sequence.</description></item>
		/// </list>
		/// </returns>
		public static (string? c1Control, string? code, string[]? values, string? terminating) GetEscapeResult(char[] kChar)
		{
			if (kChar is null || kChar.Length == 0 || (kChar.Length == 1 && kChar[0] != KeyEsc))
			{
				return (null, null, null, null);
			}

			if (kChar[0] != KeyEsc)
			{
				throw new InvalidOperationException("Invalid escape character!");
			}

			if (kChar.Length == 1)
			{
				return ("ESC", null, null, null);
			}

			if (kChar.Length == 2)
			{
				return ("ESC", null, null, kChar[1].ToString());
			}

			string c1Control = GetC1ControlChar(kChar[1]);
			string? code = null;
			int nSep = kChar.Count(static x => x == ';') + 1;
			var values = new string[nSep];
			var valueIdx = 0;
			var terminating = string.Empty;

			for (var i = 2; i < kChar.Length; i++)
			{
				char c = kChar[i];

				if (char.IsDigit(c))
				{
					// PERF: Ouch
					values[valueIdx] += c.ToString();
				}
				else if (c == ';')
				{
					valueIdx++;
				}
				else if (valueIdx == nSep - 1 || i == kChar.Length - 1)
				{
					// PERF: Ouch
					terminating += c.ToString();
				}
				else
				{
					// PERF: Ouch
					code += c.ToString();
				}
			}

			return (c1Control, code, values, terminating);
		}

		/// <summary>
		/// Converts an array of <see cref="ConsoleKeyInfo"/> to a character array.
		/// </summary>
		/// <param name="cki">The array of console key information.</param>
		/// <returns>An array of characters extracted from the key information.</returns>
		public static char[] GetKeyCharArray(ConsoleKeyInfo[] cki)
		{
			char[] kChar = [];
			var length = 0;

			foreach (ConsoleKeyInfo kc in cki)
			{
				length++;
				Array.Resize(ref kChar, length);
				kChar[length - 1] = kc.KeyChar;
			}

			return kChar;
		}

		/// <summary>
		/// Parses mouse input from console key information and extracts mouse state.
		/// </summary>
		/// <param name="cki">The array of console key information containing mouse data.</param>
		/// <param name="mouseFlags">Output list of mouse flags indicating the current mouse state.</param>
		/// <param name="pos">Output position of the mouse cursor.</param>
		/// <param name="continuousButtonPressedHandler">Handler to invoke for continuous button press events.</param>
		public static void GetMouse(ConsoleKeyInfo[] cki, out List<MouseFlags> mouseFlags, out Point pos, Action<MouseFlags, Point> continuousButtonPressedHandler)
		{
			MouseFlags buttonState = 0;
			pos = Point.Empty;
			var buttonCode = 0;
			var foundButtonCode = false;
			var foundPoint = 0;
			string value = string.Empty;
			char[] kChar = GetKeyCharArray(cki);

			// PERF: This loop could benefit from use of Spans and other strategies to avoid copies.
			for (var i = 0; i < kChar.Length; i++)
			{
				// PERF: Copy
				char c = kChar[i];

				if (c == '<')
				{
					foundButtonCode = true;
				}
				else if (foundButtonCode && c != ';')
				{
					// PERF: Ouch
					value += c.ToString();
				}
				else if (c == ';')
				{
					if (foundButtonCode)
					{
						foundButtonCode = false;
						buttonCode = int.Parse(value);
					}

					if (foundPoint == 1)
					{
						pos.X = int.Parse(value) - 1;
					}

					value = string.Empty;
					foundPoint++;
				}
				else if (foundPoint > 0 && c != 'm' && c != 'M')
				{
					value += c.ToString();
				}
				else if (c == 'm' || c == 'M')
				{
					//pos.Y = int.Parse (value) + Console.WindowTop - 1;
					pos.Y = int.Parse(value) - 1;

					if ((buttonCode & 0x40) != 0)
					{
						// Wheel codes: xterm maps a modifier held during a vertical wheel tick to a
						// synthetic horizontal tilt (WheeledLeft/Right) rather than a plain modifier bit,
						// so this table is deliberately kept as its own hardcoded lookup.
						switch (buttonCode)
						{
							case 64:
								buttonState = MouseFlags.WheeledUp;
								break;

							case 65:
								buttonState = MouseFlags.WheeledDown;
								break;

							case 68:
							case 72:
							case 80:
								buttonState = MouseFlags.WheeledLeft; // Shift/Alt/Ctrl+WheeledUp

								break;

							case 69:
							case 73:
							case 81:
								buttonState = MouseFlags.WheeledRight; // Shift/Alt/Ctrl+WheeledDown

								break;
						}
					}
					else
					{
						// Base button + modifiers computed from the SGR bit layout (bits 0-1 = button,
						// 0x04/0x08/0x10 = Shift/Alt/Ctrl, 0x20 = motion) instead of a hand-enumerated
						// case table: the old table only listed specific button+modifier combinations
						// and silently dropped any code it didn't recognize (e.g. a bare Shift+Click was
						// code 4, present in neither switch), leaving buttonState at its default 0 and
						// the click never recognized at all.
						switch (buttonCode & 0x03)
						{
							case 0:
								buttonState = c == 'M' ? MouseFlags.Button1Pressed : MouseFlags.Button1Released;
								break;

							case 1:
								buttonState = c == 'M' ? MouseFlags.Button2Pressed : MouseFlags.Button2Released;
								break;

							case 2:
								buttonState = c == 'M' ? MouseFlags.Button3Pressed : MouseFlags.Button3Released;
								break;

							case 3:
								if ((buttonCode & 0x20) != 0)
									buttonState = MouseFlags.ReportMousePosition;
								break;
						}

						if ((buttonCode & 0x20) != 0) buttonState |= MouseFlags.ReportMousePosition;
						if ((buttonCode & 0x04) != 0) buttonState |= MouseFlags.ButtonShift;
						if ((buttonCode & 0x08) != 0) buttonState |= MouseFlags.ButtonAlt;
						if ((buttonCode & 0x10) != 0) buttonState |= MouseFlags.ButtonCtrl;
					}
				}
			}

			// Consistency with the Unix AnsiInputParser path: an SGR motion-while-button-held report
			// (motion bit 0x20 set, a real button 0/1/2 in the low 2 bits, not a wheel 0x40) must also
			// surface ButtonNDragged. Without this the Windows ReadKey path emits only
			// ButtonNPressed|ReportMousePosition, so drag-aware controls (selection autoscroll,
			// splitters) never see Button1Dragged and behave differently than on Unix (#45).
			if ((buttonCode & 0x20) != 0 && (buttonCode & 0x40) == 0)
			{
				switch (buttonCode & 0x03)
				{
					case 0: buttonState |= MouseFlags.Button1Dragged; break;
					case 1: buttonState |= MouseFlags.Button2Dragged; break;
					case 2: buttonState |= MouseFlags.Button3Dragged; break;
				}
			}

			mouseFlags = [MouseFlags.AllEvents];

			// buttonState mixes the button/wheel action with Shift/Alt/Ctrl (OR'd in by the modifier switch
			// above), so every `buttonState == Button1Pressed`-style check below fails whenever a modifier
			// is held — same class of bug ExtractButtonState fixed on the Unix path (UnixStdinReader.cs).
			// SetControlKeyStates() re-applies the modifier bits onto the final output further down, so
			// only the state-machine's recognition needs the stripped value.
			const MouseFlags modifierMask = MouseFlags.ButtonShift | MouseFlags.ButtonAlt | MouseFlags.ButtonCtrl;
			var actionState = buttonState & ~modifierMask;

			if (_lastMouseButtonPressed != null
				&& !_isButtonPressed
				&& !buttonState.HasFlag(MouseFlags.ReportMousePosition)
				&& !buttonState.HasFlag(MouseFlags.Button1Released)
				&& !buttonState.HasFlag(MouseFlags.Button2Released)
				&& !buttonState.HasFlag(MouseFlags.Button3Released)
				&& !buttonState.HasFlag(MouseFlags.Button4Released))
			{
				_lastMouseButtonPressed = null;
				_isButtonPressed = false;
			}

			if ((!_isButtonClicked
				 && !_isButtonDoubleClicked
				 && (actionState == MouseFlags.Button1Pressed
					 || actionState == MouseFlags.Button2Pressed
					 || actionState == MouseFlags.Button3Pressed
					 || actionState == MouseFlags.Button4Pressed)
				 && _lastMouseButtonPressed is null)
				|| (_isButtonPressed && _lastMouseButtonPressed is { } && buttonState.HasFlag(MouseFlags.ReportMousePosition)))
			{
				mouseFlags[0] = buttonState;
				_lastMouseButtonPressed = buttonState;
				_isButtonPressed = true;

				_point = pos;

				if ((mouseFlags[0] & MouseFlags.ReportMousePosition) == 0)
				{
					Task.Run(
							async () => await ProcessContinuousButtonPressedAsync(
											buttonState,
											continuousButtonPressedHandler));
				}
				else if (mouseFlags[0].HasFlag(MouseFlags.ReportMousePosition))
				{
					_point = pos;

					// The isButtonPressed must always be true, otherwise we can lose the feature
					// If mouse flags has ReportMousePosition this feature won't run
					// but is always prepared with the new location
					//isButtonPressed = false;
				}
			}
			else if (_isButtonDoubleClicked
					 && (actionState == MouseFlags.Button1Pressed
						 || actionState == MouseFlags.Button2Pressed
						 || actionState == MouseFlags.Button3Pressed
						 || actionState == MouseFlags.Button4Pressed))
			{
				mouseFlags[0] = GetButtonTripleClicked(actionState);
				_isButtonDoubleClicked = false;
				_isButtonTripleClicked = true;
			}
			else if (_isButtonClicked
					 && (actionState == MouseFlags.Button1Pressed
						 || actionState == MouseFlags.Button2Pressed
						 || actionState == MouseFlags.Button3Pressed
						 || actionState == MouseFlags.Button4Pressed))
			{
				mouseFlags[0] = GetButtonDoubleClicked(actionState);
				_isButtonClicked = false;
				_isButtonDoubleClicked = true;

				Task.Run(async () => await ProcessButtonDoubleClickedAsync());
			}

			else if (!_isButtonClicked
					 && !_isButtonDoubleClicked
					 && (actionState == MouseFlags.Button1Released
						 || actionState == MouseFlags.Button2Released
						 || actionState == MouseFlags.Button3Released
						 || actionState == MouseFlags.Button4Released))
			{
				mouseFlags[0] = buttonState;
				_isButtonPressed = false;

				if (_isButtonTripleClicked)
				{
					_isButtonTripleClicked = false;
				}
				else if (pos.X == _point?.X && pos.Y == _point?.Y)
				{
					// SAFEGUARD: Ignore duplicate release events (< 50ms = driver bug)
					var timeSinceLastClick = (DateTime.Now - _lastClickTime).TotalMilliseconds;
					if (timeSinceLastClick >= 50)
					{
						mouseFlags.Add(GetButtonClicked(actionState));
						_isButtonClicked = true;
						Task.Run(async () => await ProcessButtonClickedAsync());
						_lastClickTime = DateTime.Now;
					}
				}

				_point = pos;

			}
			else if (actionState == MouseFlags.WheeledUp)
			{
				mouseFlags[0] = MouseFlags.WheeledUp;
			}
			else if (actionState == MouseFlags.WheeledDown)
			{
				mouseFlags[0] = MouseFlags.WheeledDown;
			}
			else if (actionState == MouseFlags.WheeledLeft)
			{
				mouseFlags[0] = MouseFlags.WheeledLeft;
			}
			else if (actionState == MouseFlags.WheeledRight)
			{
				mouseFlags[0] = MouseFlags.WheeledRight;
			}
			else if (actionState == MouseFlags.ReportMousePosition)
			{
				mouseFlags[0] = MouseFlags.ReportMousePosition;
			}
			else
			{
				mouseFlags[0] = buttonState;

				//foreach (var flag in buttonState.GetUniqueFlags()) {
				//	mouseFlag [0] |= flag;
				//}
			}

			mouseFlags[0] = SetControlKeyStates(buttonState, mouseFlags[0]);

			//buttonState = mouseFlags;

			//foreach (var mf in mouseFlags) {
			//}
		}

		private static MouseFlags GetButtonClicked(MouseFlags mouseFlag)
		{
			MouseFlags mf = default;

			switch (mouseFlag)
			{
				case MouseFlags.Button1Released:
					mf = MouseFlags.Button1Clicked;

					break;

				case MouseFlags.Button2Released:
					mf = MouseFlags.Button2Clicked;

					break;

				case MouseFlags.Button3Released:
					mf = MouseFlags.Button3Clicked;

					break;
			}

			return mf;
		}

		private static MouseFlags GetButtonDoubleClicked(MouseFlags mouseFlag)
		{
			MouseFlags mf = default;

			switch (mouseFlag)
			{
				case MouseFlags.Button1Pressed:
					mf = MouseFlags.Button1DoubleClicked;

					break;

				case MouseFlags.Button2Pressed:
					mf = MouseFlags.Button2DoubleClicked;

					break;

				case MouseFlags.Button3Pressed:
					mf = MouseFlags.Button3DoubleClicked;

					break;
			}

			return mf;
		}

		private static MouseFlags GetButtonTripleClicked(MouseFlags mouseFlag)
		{
			MouseFlags mf = default;

			switch (mouseFlag)
			{
				case MouseFlags.Button1Pressed:
					mf = MouseFlags.Button1TripleClicked;

					break;

				case MouseFlags.Button2Pressed:
					mf = MouseFlags.Button2TripleClicked;

					break;

				case MouseFlags.Button3Pressed:
					mf = MouseFlags.Button3TripleClicked;

					break;
			}

			return mf;
		}

		private static async Task ProcessButtonClickedAsync()
		{
			await Task.Delay(Configuration.ControlDefaults.DefaultDebounceMs);
			_isButtonClicked = false;
		}

		private static async Task ProcessButtonDoubleClickedAsync()
		{
			await Task.Delay(Configuration.ControlDefaults.DefaultDebounceMs);
			_isButtonDoubleClicked = false;
		}

		private static async Task ProcessContinuousButtonPressedAsync(MouseFlags mouseFlag, Action<MouseFlags, Point> continuousButtonPressedHandler)
		{
			// PERF: Pause and poll in a hot loop.
			// This should be replaced with event dispatch and a synchronization primitive such as AutoResetEvent.
			// Will make a massive difference in responsiveness.
			while (_isButtonPressed)
			{
				await Task.Delay(Configuration.ControlDefaults.ContinuousPressIntervalMs);

				if (_isButtonPressed && _lastMouseButtonPressed is { } && (mouseFlag & MouseFlags.ReportMousePosition) == 0)
				{
					continuousButtonPressedHandler(mouseFlag, _point ?? Point.Empty);
				}
			}
		}

		private static MouseFlags SetControlKeyStates(MouseFlags buttonState, MouseFlags mouseFlag)
		{
			if ((buttonState & MouseFlags.ButtonCtrl) != 0 && (mouseFlag & MouseFlags.ButtonCtrl) == 0)
			{
				mouseFlag |= MouseFlags.ButtonCtrl;
			}

			if ((buttonState & MouseFlags.ButtonShift) != 0 && (mouseFlag & MouseFlags.ButtonShift) == 0)
			{
				mouseFlag |= MouseFlags.ButtonShift;
			}

			if ((buttonState & MouseFlags.ButtonAlt) != 0 && (mouseFlag & MouseFlags.ButtonAlt) == 0)
			{
				mouseFlag |= MouseFlags.ButtonAlt;
			}

			return mouseFlag;
		}
	}
}
