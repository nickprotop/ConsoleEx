// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using System.Threading;
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



		// QUESTION: What's the difference between isButtonClicked and isButtonPressed?
		// Some clarity or comments would be handy, here.
		// It also seems like some enforcement of valid states might be a good idea.
		/// <summary>The auto-repeat for a held button.</summary>
		/// <remarks>
		/// <para>
		/// Still static, because <see cref="GetMouse"/> and <see cref="Reset"/> are
		/// public static and cannot become instance members without breaking every
		/// caller. What changed is that one object owns the state rather than five
		/// loose fields: a press, a release and a reset are now three calls on it,
		/// and the cancellation discipline is the same code the Unix reader uses.
		/// </para>
		/// <para>
		/// The hazard the old comment described - a new driver in the same process
		/// inheriting a live loop from the previous one - is answered by
		/// <see cref="Reset"/> disposing this and starting a fresh one, which the
		/// driver already calls on shutdown.
		/// </para>
		/// </remarks>
		private static Drivers.Input.ContinuousPress _press = new();

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
			// Decoded by the one parser rather than a second implementation of the
			// same protocol. This used to carry its own SGR state machine, and the
			// two drifted: the Windows path emitted no drag flag at all until #45,
			// and the modifier-mask bug had to be found and fixed once per decoder.
			//
			// The bridge is cheap because SGR is pure ASCII. The Windows input loop
			// collects an escape sequence as ConsoleKeyInfo, and those characters
			// ARE the bytes the parser wants.
			//
			// What stays here is the continuous-press lifecycle below: that is not
			// decoding, it is the terminal's missing auto-repeat, and only the code
			// holding the input knows a button is still physically down.
			var decoded = Drivers.Input.AnsiInputParser.DecodeSgrMouse(GetKeyCharArray(cki));

			pos = decoded.Position;

			// One flag per entry, as the parser produced them, rather than OR'd into
			// a single value. The recogniser strips modifiers and compares for
			// EQUALITY, so a drag collapsed into one value - Pressed, Dragged and
			// ReportMousePosition together - matched no button constant and read as
			// no button held, on Windows only.
			mouseFlags = decoded.Flags;

			if (mouseFlags.Count == 0)
			{
				// Not an SGR mouse report. An empty answer rather than an
				// exception, as before: the caller tests the flags.
				return;
			}

			// The lifecycle below reasons about one value, so the flags are folded
			// together for it alone. What goes OUT is the list above.
			MouseFlags buttonState = 0;
			foreach (var flag in mouseFlags) buttonState |= flag;

			const MouseFlags modifierMask = MouseFlags.ButtonShift | MouseFlags.ButtonAlt | MouseFlags.ButtonCtrl;
			var actionState = buttonState & ~modifierMask;

			// Report what the terminal said, and nothing else.
			//
			// Clicks, double-clicks and triple-clicks are NOT derived here. They are
			// gestures - judgements about timing and position - made once, for every
			// platform, by MouseGestureRecognizer in the input coordinator. This used
			// to be a ~130-line state machine with five pieces of static state and two
			// background expiry timers, duplicated almost line for line in
			// UnixStdinReader. Two copies could disagree, and did; worse, a press
			// inside the click window was REPLACED by the derived double-click rather
			// than accompanied by it, so anything watching for presses lost half of a
			// fast click run.
			//
			// The continuous-press loop stays: it is not a gesture, it is the
			// terminal's missing auto-repeat, and only the code reading input knows a
			// button is still physically down.
			//
			// Nothing is written back into mouseFlags here. It holds the parser's
			// own entries, one flag each, and overwriting the first with the
			// folded value left a list whose head carried every bit and whose
			// tail repeated them - so a caller testing Contains for one flag
			// could not find it.

			if (actionState == MouseFlags.Button1Pressed
				|| actionState == MouseFlags.Button2Pressed
				|| actionState == MouseFlags.Button3Pressed
				|| actionState == MouseFlags.Button4Pressed)
			{
				// Begin replaces any repeat already running, so a press of a button
				// already held - the terminal restating a drag - cannot stack a
				// second loop on the first.
				_press.Begin(buttonState, pos, continuousButtonPressedHandler);
			}
			else if (actionState == MouseFlags.Button1Released
				|| actionState == MouseFlags.Button2Released
				|| actionState == MouseFlags.Button3Released
				|| actionState == MouseFlags.Button4Released)
			{
				_press.End(Drivers.Input.ContinuousPress.PressFor(actionState));
			}
			else if (buttonState.HasFlag(MouseFlags.ReportMousePosition))
			{
				// So a held button repeats where the pointer is now.
				_press.Moved(pos);
			}

			// The modifier bits are already in the parser's entries, so there is
			// nothing to re-apply: SetControlKeyStates existed to put them back
			// onto a value the old state machine had stripped them from.

			//foreach (var mf in mouseFlags) {
			//}
		}







		/// <summary>
		/// Clears the mouse click/press state machine and stops any continuous-press loop.
		/// </summary>
		/// <remarks>
		/// This state is process-wide, so a driver shutting down — or a test constructing a fresh
		/// console window system — should call this to avoid inheriting a previous owner's
		/// half-finished click sequence or a still-running press loop.
		/// </remarks>
		public static void Reset()
		{
			// Swapped atomically, then disposed. GetMouse reads this field on the
			// input thread, and Reset runs on whichever thread is stopping the
			// driver - which does not wait for the reader - so a plain
			// dispose-then-assign leaves a window where the input thread can call
			// Begin on an instance that has just been disposed.
			//
			// Disposed and replaced rather than cleared, so a fresh owner cannot
			// inherit a running loop from the one before it.
			var previous = Interlocked.Exchange(ref _press, new Drivers.Input.ContinuousPress());

			previous.Dispose();
		}

	}
}
