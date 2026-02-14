// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Drivers;
using System.Drawing;

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
		public static readonly string CSI_EnableMouseEvents = CSI_EnableAnyEventMouse + CSI_EnableUrxvtExtModeMouse + CSI_EnableSgrExtModeMouse;

		/// <summary>
		/// The CSI sequence to enable SGR extended mouse mode.
		/// </summary>
		public static readonly string CSI_EnableSgrExtModeMouse = CSI + "?1006h";

		/// <summary>
		/// The CSI sequence to enable urxvt extended mouse mode.
		/// </summary>
		public static readonly string CSI_EnableUrxvtExtModeMouse = CSI + "?1015h";

		private static bool _isButtonClicked;

		private static bool _isButtonDoubleClicked;

		//private static MouseFlags? lastMouseButtonReleased;
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

					switch (buttonCode)
					{
						case 0:
						case 8:
						case 16:
						case 24:
						case 32:
						case 36:
						case 40:
						case 48:
						case 56:
							buttonState = c == 'M'
											  ? MouseFlags.Button1Pressed
											  : MouseFlags.Button1Released;

							break;

						case 1:
						case 9:
						case 17:
						case 25:
						case 33:
						case 37:
						case 41:
						case 45:
						case 49:
						case 53:
						case 57:
						case 61:
							buttonState = c == 'M'
											  ? MouseFlags.Button2Pressed
											  : MouseFlags.Button2Released;

							break;

						case 2:
						case 10:
						case 14:
						case 18:
						case 22:
						case 26:
						case 30:
						case 34:
						case 42:
						case 46:
						case 50:
						case 54:
						case 58:
						case 62:
							buttonState = c == 'M'
											  ? MouseFlags.Button3Pressed
											  : MouseFlags.Button3Released;

							break;

						case 35:
						//// Needed for Windows OS
						//if (isButtonPressed && c == 'm'
						//	&& (lastMouseEvent.ButtonState == MouseFlags.Button1Pressed
						//	|| lastMouseEvent.ButtonState == MouseFlags.Button2Pressed
						//	|| lastMouseEvent.ButtonState == MouseFlags.Button3Pressed)) {
						//	switch (lastMouseEvent.ButtonState) {
						//	case MouseFlags.Button1Pressed:
						//		buttonState = MouseFlags.Button1Released;
						//		break;
						//	case MouseFlags.Button2Pressed:
						//		buttonState = MouseFlags.Button2Released;
						//		break;
						//	case MouseFlags.Button3Pressed:
						//		buttonState = MouseFlags.Button3Released;
						//		break;
						//	}
						//} else {
						//	buttonState = MouseFlags.ReportMousePosition;
						//}
						//break;
						case 39:
						case 43:
						case 47:
						case 51:
						case 55:
						case 59:
						case 63:
							buttonState = MouseFlags.ReportMousePosition;

							break;

						case 64:
							buttonState = MouseFlags.WheeledUp;

							break;

						case 65:
							buttonState = MouseFlags.WheeledDown;

							break;

						case 68:
						case 72:
						case 80:
							buttonState = MouseFlags.WheeledLeft; // Shift/Ctrl+WheeledUp

							break;

						case 69:
						case 73:
						case 81:
							buttonState = MouseFlags.WheeledRight; // Shift/Ctrl+WheeledDown

							break;
					}

					// Modifiers.
					switch (buttonCode)
					{
						case 8:
						case 9:
						case 10:
						case 43:
							buttonState |= MouseFlags.ButtonAlt;

							break;

						case 14:
						case 47:
							buttonState |= MouseFlags.ButtonAlt | MouseFlags.ButtonShift;

							break;

						case 16:
						case 17:
						case 18:
						case 51:
							buttonState |= MouseFlags.ButtonCtrl;

							break;

						case 22:
						case 55:
							buttonState |= MouseFlags.ButtonCtrl | MouseFlags.ButtonShift;

							break;

						case 24:
						case 25:
						case 26:
						case 59:
							buttonState |= MouseFlags.ButtonCtrl | MouseFlags.ButtonAlt;

							break;

						case 30:
						case 63:
							buttonState |= MouseFlags.ButtonCtrl | MouseFlags.ButtonShift | MouseFlags.ButtonAlt;

							break;

						case 32:
						case 33:
						case 34:
							buttonState |= MouseFlags.ReportMousePosition;

							break;

						case 36:
						case 37:
							buttonState |= MouseFlags.ReportMousePosition | MouseFlags.ButtonShift;

							break;

						case 39:
						case 68:
						case 69:
							buttonState |= MouseFlags.ButtonShift;

							break;

						case 40:
						case 41:
						case 42:
							buttonState |= MouseFlags.ReportMousePosition | MouseFlags.ButtonAlt;

							break;

						case 45:
						case 46:
							buttonState |= MouseFlags.ReportMousePosition | MouseFlags.ButtonAlt | MouseFlags.ButtonShift;

							break;

						case 48:
						case 49:
						case 50:
							buttonState |= MouseFlags.ReportMousePosition | MouseFlags.ButtonCtrl;

							break;

						case 53:
						case 54:
							buttonState |= MouseFlags.ReportMousePosition | MouseFlags.ButtonCtrl | MouseFlags.ButtonShift;

							break;

						case 56:
						case 57:
						case 58:
							buttonState |= MouseFlags.ReportMousePosition | MouseFlags.ButtonCtrl | MouseFlags.ButtonAlt;

							break;

						case 61:
						case 62:
							buttonState |= MouseFlags.ReportMousePosition | MouseFlags.ButtonCtrl | MouseFlags.ButtonShift | MouseFlags.ButtonAlt;

							break;
					}
				}
			}

			mouseFlags = [MouseFlags.AllEvents];

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
				 && (buttonState == MouseFlags.Button1Pressed
					 || buttonState == MouseFlags.Button2Pressed
					 || buttonState == MouseFlags.Button3Pressed
					 || buttonState == MouseFlags.Button4Pressed)
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
					 && (buttonState == MouseFlags.Button1Pressed
						 || buttonState == MouseFlags.Button2Pressed
						 || buttonState == MouseFlags.Button3Pressed
						 || buttonState == MouseFlags.Button4Pressed))
			{
				mouseFlags[0] = GetButtonTripleClicked(buttonState);
				_isButtonDoubleClicked = false;
				_isButtonTripleClicked = true;
			}
			else if (_isButtonClicked
					 && (buttonState == MouseFlags.Button1Pressed
						 || buttonState == MouseFlags.Button2Pressed
						 || buttonState == MouseFlags.Button3Pressed
						 || buttonState == MouseFlags.Button4Pressed))
			{
				mouseFlags[0] = GetButtonDoubleClicked(buttonState);
				_isButtonClicked = false;
				_isButtonDoubleClicked = true;

				Task.Run(async () => await ProcessButtonDoubleClickedAsync());
			}

			//else if (isButtonReleased && !isButtonClicked && buttonState == MouseFlags.ReportMousePosition) {
			//	mouseFlag [0] = GetButtonClicked ((MouseFlags)lastMouseButtonReleased);
			//	lastMouseButtonReleased = null;
			//	isButtonReleased = false;
			//	isButtonClicked = true;
			//	Application.MainLoop.AddIdle (() => {
			//		Task.Run (async () => await ProcessButtonClickedAsync ());
			//		return false;
			//	});

			//}
			else if (!_isButtonClicked
					 && !_isButtonDoubleClicked
					 && (buttonState == MouseFlags.Button1Released
						 || buttonState == MouseFlags.Button2Released
						 || buttonState == MouseFlags.Button3Released
						 || buttonState == MouseFlags.Button4Released))
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
						mouseFlags.Add(GetButtonClicked(buttonState));
						_isButtonClicked = true;
						Task.Run(async () => await ProcessButtonClickedAsync());
						_lastClickTime = DateTime.Now;
					}
				}

				_point = pos;

				//if ((lastMouseButtonPressed & MouseFlags.ReportMousePosition) == 0) {
				//	lastMouseButtonReleased = buttonState;
				//	isButtonPressed = false;
				//	isButtonReleased = true;
				//} else {
				//	lastMouseButtonPressed = null;
				//	isButtonPressed = false;
				//}
			}
			else if (buttonState == MouseFlags.WheeledUp)
			{
				mouseFlags[0] = MouseFlags.WheeledUp;
			}
			else if (buttonState == MouseFlags.WheeledDown)
			{
				mouseFlags[0] = MouseFlags.WheeledDown;
			}
			else if (buttonState == MouseFlags.WheeledLeft)
			{
				mouseFlags[0] = MouseFlags.WheeledLeft;
			}
			else if (buttonState == MouseFlags.WheeledRight)
			{
				mouseFlags[0] = MouseFlags.WheeledRight;
			}
			else if (buttonState == MouseFlags.ReportMousePosition)
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
			await Task.Delay(300);
			_isButtonClicked = false;
		}

		private static async Task ProcessButtonDoubleClickedAsync()
		{
			await Task.Delay(300);
			_isButtonDoubleClicked = false;
		}

		private static async Task ProcessContinuousButtonPressedAsync(MouseFlags mouseFlag, Action<MouseFlags, Point> continuousButtonPressedHandler)
		{
			// PERF: Pause and poll in a hot loop.
			// This should be replaced with event dispatch and a synchronization primitive such as AutoResetEvent.
			// Will make a massive difference in responsiveness.
			while (_isButtonPressed)
			{
				await Task.Delay(100);

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