// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using System.Text;

namespace SharpConsoleUI.Drivers.Input
{
	/// <summary>
	/// Stateful byte-level ANSI input parser. Converts raw stdin bytes into InputEvent records.
	/// Handles partial sequences across read boundaries via internal pending buffer.
	/// </summary>
	/// <remarks>
	/// State machine: Ground → Escape → CsiParam/Ss3 → dispatch.
	/// Ports logic from NetConsoleDriver.MapAnsiToConsoleKeyInfo and SequenceHelper.GetMouse
	/// but operates on raw bytes instead of ConsoleKeyInfo, avoiding Console.ReadKey overhead.
	/// </remarks>
	internal class AnsiInputParser
	{
		private const int MaxPendingBytes = 256;
		private const int Utf8MaxBytes = 4;
		private const int X10MouseByteCount = 3; // button, x, y

		private enum State
		{
			Ground,
			Escape,
			CsiParam,
			Ss3,
			Utf8,
			X10Mouse // Collecting 3 raw bytes after CSI M (legacy X10 format)
		}

		private State _state = State.Ground;
		private readonly List<byte> _pending = new(MaxPendingBytes);
		private readonly StringBuilder _csiParams = new(32);
		private bool _csiIsSgr; // CSI sequence started with '<' (SGR mouse)
		private int _utf8Remaining;
		private readonly byte[] _utf8Buffer = new byte[Utf8MaxBytes];
		private int _utf8Index;
		private readonly byte[] _x10MouseBuffer = new byte[X10MouseByteCount];
		private int _x10MouseIndex;

		/// <summary>
		/// Parses raw bytes from stdin into a list of input events.
		/// May return zero events if a partial sequence is still being accumulated.
		/// </summary>
		public List<InputEvent> Parse(ReadOnlySpan<byte> buffer, int bytesRead)
		{
			var events = new List<InputEvent>();

			for (int i = 0; i < bytesRead; i++)
			{
				byte b = buffer[i];

				switch (_state)
				{
					case State.Ground:
						ProcessGround(b, events);
						break;

					case State.Escape:
						ProcessEscape(b, events);
						break;

					case State.CsiParam:
						ProcessCsiParam(b, events);
						break;

					case State.Ss3:
						ProcessSs3(b, events);
						break;

					case State.Utf8:
						ProcessUtf8(b, events);
						break;

					case State.X10Mouse:
						ProcessX10Mouse(b, events);
						break;
				}
			}

			return events;
		}

		/// <summary>
		/// Flushes any pending partial sequence as events.
		/// Call this on ESC timeout (50ms with no follow-up bytes) to emit a plain Escape key.
		/// </summary>
		public List<InputEvent> Flush()
		{
			var events = new List<InputEvent>();

			if (_state == State.Escape)
			{
				// Lone ESC with no follow-up → plain Escape key
				events.Add(new KeyInputEvent(new ConsoleKeyInfo('\x1b', ConsoleKey.Escape, false, false, false)));
				_state = State.Ground;
			}
			else if (_state == State.CsiParam || _state == State.Ss3)
			{
				// Incomplete sequence — emit as unknown
				events.Add(new UnknownSequenceEvent(_pending.ToArray()));
				_pending.Clear();
				_csiParams.Clear();
				_state = State.Ground;
			}
			else if (_state == State.Utf8)
			{
				// Incomplete UTF-8 — emit as unknown
				events.Add(new UnknownSequenceEvent(_utf8Buffer[.._utf8Index]));
				_utf8Index = 0;
				_utf8Remaining = 0;
				_state = State.Ground;
			}
			else if (_state == State.X10Mouse)
			{
				// Incomplete X10 mouse — emit as unknown
				events.Add(new UnknownSequenceEvent(_pending.ToArray()));
				_pending.Clear();
				_x10MouseIndex = 0;
				_state = State.Ground;
			}

			return events;
		}

		/// <summary>
		/// Resets the parser to ground state, discarding any pending data.
		/// </summary>
		public void Reset()
		{
			_state = State.Ground;
			_pending.Clear();
			_csiParams.Clear();
			_csiIsSgr = false;
			_utf8Remaining = 0;
			_utf8Index = 0;
			_x10MouseIndex = 0;
		}

		private void ProcessGround(byte b, List<InputEvent> events)
		{
			// Ctrl+Space (Ctrl+@) = NUL byte — must check before the ESC/control ranges
			// KeyChar must be '\0' to match Console.ReadKey behavior (not ' ')
			// so that isControl/isTypingKey checks work correctly downstream
			if (b == 0x00)
			{
				events.Add(new KeyInputEvent(new ConsoleKeyInfo('\0', ConsoleKey.Spacebar, false, false, true)));
				return;
			}

			if (b == 0x1B)
			{
				_state = State.Escape;
				_pending.Clear();
				_pending.Add(b);
				return;
			}

			// Ctrl+Letter: 0x01-0x1A (except Tab=0x09, Enter=0x0D)
			if (b >= 0x01 && b <= 0x1A)
			{
				switch (b)
				{
					case 0x09: // Tab
						events.Add(new KeyInputEvent(new ConsoleKeyInfo('\t', ConsoleKey.Tab, false, false, false)));
						return;
					case 0x0D: // Enter (CR)
						events.Add(new KeyInputEvent(new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false)));
						return;
					case 0x0A: // Newline (LF) — treat as Enter in raw mode
						events.Add(new KeyInputEvent(new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false)));
						return;
					case 0x08: // Ctrl+H / Backspace
						events.Add(new KeyInputEvent(new ConsoleKeyInfo('\b', ConsoleKey.Backspace, false, false, false)));
						return;
					default:
						// Ctrl+Letter: map 0x01→A, 0x02→B, etc.
						char baseChar = (char)(b + 'A' - 1);
						events.Add(new KeyInputEvent(new ConsoleKeyInfo(
							(char)b,
							(ConsoleKey)baseChar,
							false, false, true)));
						return;
				}
			}

			// DEL (0x7F) → Backspace
			if (b == 0x7F)
			{
				events.Add(new KeyInputEvent(new ConsoleKeyInfo('\x7f', ConsoleKey.Backspace, false, false, false)));
				return;
			}

			// Printable ASCII
			if (b >= 0x20 && b <= 0x7E)
			{
				char c = (char)b;
				var key = CharToConsoleKey(c);
				events.Add(new KeyInputEvent(new ConsoleKeyInfo(c, key, char.IsUpper(c), false, false)));
				return;
			}

			// UTF-8 multi-byte start
			if (b >= 0xC0)
			{
				_utf8Index = 0;
				_utf8Buffer[_utf8Index++] = b;

				if (b < 0xE0) _utf8Remaining = 1;       // 2-byte sequence
				else if (b < 0xF0) _utf8Remaining = 2;   // 3-byte sequence
				else _utf8Remaining = 3;                  // 4-byte sequence

				_state = State.Utf8;
				return;
			}

			// Unrecognized byte
			events.Add(new UnknownSequenceEvent(new[] { b }));
		}

		private void ProcessEscape(byte b, List<InputEvent> events)
		{
			_pending.Add(b);

			if (b == '[')
			{
				// CSI sequence start
				_state = State.CsiParam;
				_csiParams.Clear();
				_csiIsSgr = false;
				return;
			}

			if (b == 'O')
			{
				// SS3 sequence start (F1-F4)
				_state = State.Ss3;
				return;
			}

			// ESC + printable → Alt+key
			if (b >= 0x20 && b <= 0x7E)
			{
				char c = (char)b;
				var key = CharToConsoleKey(c);
				events.Add(new KeyInputEvent(new ConsoleKeyInfo(c, key, char.IsUpper(c), true, false)));
				_state = State.Ground;
				_pending.Clear();
				return;
			}

			// ESC + control char → emit ESC then re-process the byte
			events.Add(new KeyInputEvent(new ConsoleKeyInfo('\x1b', ConsoleKey.Escape, false, false, false)));
			_state = State.Ground;
			_pending.Clear();
			ProcessGround(b, events);
		}

		private void ProcessCsiParam(byte b, List<InputEvent> events)
		{
			_pending.Add(b);

			// Check for SGR mouse prefix
			if (_csiParams.Length == 0 && b == '<')
			{
				_csiIsSgr = true;
				_csiParams.Append((char)b);
				return;
			}

			// Collect digits, semicolons, and intermediate bytes
			if ((b >= '0' && b <= '9') || b == ';')
			{
				_csiParams.Append((char)b);
				return;
			}

			// Final byte — dispatch
			if (b >= 0x40 && b <= 0x7E)
			{
				DispatchCsi((char)b, events);
				// DispatchCsi may set a new state (e.g. X10Mouse) — don't overwrite it
				if (_state == State.CsiParam)
				{
					_state = State.Ground;
					_pending.Clear();
					_csiParams.Clear();
				}
				return;
			}

			// Overflow protection
			if (_pending.Count > MaxPendingBytes)
			{
				events.Add(new UnknownSequenceEvent(_pending.ToArray()));
				_state = State.Ground;
				_pending.Clear();
				_csiParams.Clear();
			}
		}

		private void ProcessSs3(byte b, List<InputEvent> events)
		{
			_pending.Add(b);
			_state = State.Ground;
			_pending.Clear();

			// SS3 P-S → F1-F4
			switch ((char)b)
			{
				case 'P': events.Add(MakeKey(ConsoleKey.F1, false, false, false)); break;
				case 'Q': events.Add(MakeKey(ConsoleKey.F2, false, false, false)); break;
				case 'R': events.Add(MakeKey(ConsoleKey.F3, false, false, false)); break;
				case 'S': events.Add(MakeKey(ConsoleKey.F4, false, false, false)); break;
				// SS3 A-D → arrow keys (some terminals send these)
				case 'A': events.Add(MakeKey(ConsoleKey.UpArrow, false, false, false)); break;
				case 'B': events.Add(MakeKey(ConsoleKey.DownArrow, false, false, false)); break;
				case 'C': events.Add(MakeKey(ConsoleKey.RightArrow, false, false, false)); break;
				case 'D': events.Add(MakeKey(ConsoleKey.LeftArrow, false, false, false)); break;
				case 'H': events.Add(MakeKey(ConsoleKey.Home, false, false, false)); break;
				case 'F': events.Add(MakeKey(ConsoleKey.End, false, false, false)); break;
				default:
					events.Add(new UnknownSequenceEvent(_pending.ToArray()));
					break;
			}
		}

		private void ProcessUtf8(byte b, List<InputEvent> events)
		{
			// Continuation byte must be 0x80-0xBF
			if ((b & 0xC0) != 0x80)
			{
				// Invalid continuation — emit what we have as unknown, re-process this byte
				events.Add(new UnknownSequenceEvent(_utf8Buffer[.._utf8Index]));
				_utf8Index = 0;
				_utf8Remaining = 0;
				_state = State.Ground;
				ProcessGround(b, events);
				return;
			}

			_utf8Buffer[_utf8Index++] = b;
			_utf8Remaining--;

			if (_utf8Remaining == 0)
			{
				// Complete UTF-8 sequence — decode to char
				string decoded = Encoding.UTF8.GetString(_utf8Buffer, 0, _utf8Index);
				if (decoded.Length > 0)
				{
					char c = decoded[0];
					events.Add(new KeyInputEvent(new ConsoleKeyInfo(c, ConsoleKey.NoName, false, false, false)));
				}
				_utf8Index = 0;
				_state = State.Ground;
			}
		}

		private void ProcessX10Mouse(byte b, List<InputEvent> events)
		{
			_x10MouseBuffer[_x10MouseIndex++] = b;

			if (_x10MouseIndex < X10MouseByteCount)
				return; // Need more bytes

			// All 3 bytes collected: button, x, y (each offset by 32)
			int buttonCode = _x10MouseBuffer[0] - 32;
			int x = Math.Max(0, _x10MouseBuffer[1] - 33); // 1-based + offset 32 → 0-based
			int y = Math.Max(0, _x10MouseBuffer[2] - 33);
			var pos = new Point(x, y);
			var flags = new List<MouseFlags>();

			// Modifier flags
			if ((buttonCode & 0x04) != 0) flags.Add(MouseFlags.ButtonShift);
			if ((buttonCode & 0x08) != 0) flags.Add(MouseFlags.ButtonAlt);
			if ((buttonCode & 0x10) != 0) flags.Add(MouseFlags.ButtonCtrl);

			bool motion = (buttonCode & 0x20) != 0;
			bool wheel = (buttonCode & 0x40) != 0;
			int baseButton = buttonCode & 0x03;

			if (wheel)
			{
				switch (baseButton)
				{
					case 0: flags.Add(MouseFlags.WheeledUp); break;
					case 1: flags.Add(MouseFlags.WheeledDown); break;
					case 2: flags.Add(MouseFlags.WheeledLeft); break;
					case 3: flags.Add(MouseFlags.WheeledRight); break;
				}
			}
			else if (motion)
			{
				flags.Add(MouseFlags.ReportMousePosition);
				switch (baseButton)
				{
					case 0: flags.Add(MouseFlags.Button1Pressed); flags.Add(MouseFlags.Button1Dragged); break;
					case 1: flags.Add(MouseFlags.Button2Pressed); flags.Add(MouseFlags.Button2Dragged); break;
					case 2: flags.Add(MouseFlags.Button3Pressed); flags.Add(MouseFlags.Button3Dragged); break;
				}
			}
			else if (baseButton == 3)
			{
				// X10: button 3 in non-SGR means "button released" (no per-button release info)
				flags.Add(MouseFlags.Button1Released);
			}
			else
			{
				// Button press
				switch (baseButton)
				{
					case 0: flags.Add(MouseFlags.Button1Pressed); break;
					case 1: flags.Add(MouseFlags.Button2Pressed); break;
					case 2: flags.Add(MouseFlags.Button3Pressed); break;
				}
			}

			events.Add(new MouseInputEvent(flags, pos));
			_x10MouseIndex = 0;
			_pending.Clear();
			_state = State.Ground;
		}

		private void DispatchCsi(char finalByte, List<InputEvent> events)
		{
			string paramStr = _csiParams.ToString();

			// SGR mouse: ESC [ < params M/m
			if (_csiIsSgr && (finalByte == 'M' || finalByte == 'm'))
			{
				ParseSgrMouse(paramStr, finalByte == 'M', events);
				return;
			}

			// Parse modifiers from CSI sequences like "1;5A" (Ctrl+Up)
			ParseModifiers(paramStr, out string numericPart, out bool shift, out bool alt, out bool ctrl);

			switch (finalByte)
			{
				// Arrow keys
				case 'A': events.Add(MakeKey(ConsoleKey.UpArrow, shift, alt, ctrl)); break;
				case 'B': events.Add(MakeKey(ConsoleKey.DownArrow, shift, alt, ctrl)); break;
				case 'C': events.Add(MakeKey(ConsoleKey.RightArrow, shift, alt, ctrl)); break;
				case 'D': events.Add(MakeKey(ConsoleKey.LeftArrow, shift, alt, ctrl)); break;

				// Navigation
				case 'H': events.Add(MakeKey(ConsoleKey.Home, shift, alt, ctrl)); break;
				case 'F': events.Add(MakeKey(ConsoleKey.End, shift, alt, ctrl)); break;
				case 'E': events.Add(MakeKey(ConsoleKey.Clear, shift, alt, ctrl)); break;

				// Function keys (CSI format)
				case 'P': events.Add(MakeKey(ConsoleKey.F1, shift, alt, ctrl)); break;
				case 'Q': events.Add(MakeKey(ConsoleKey.F2, shift, alt, ctrl)); break;
				case 'R': events.Add(MakeKey(ConsoleKey.F3, shift, alt, ctrl)); break;
				case 'S': events.Add(MakeKey(ConsoleKey.F4, shift, alt, ctrl)); break;

				// Tab variants
				case 'I': events.Add(new KeyInputEvent(new ConsoleKeyInfo('\t', ConsoleKey.Tab, shift, alt, ctrl))); break;
				case 'Z': events.Add(new KeyInputEvent(new ConsoleKeyInfo('\t', ConsoleKey.Tab, true, alt, ctrl))); break; // Shift+Tab

				// Tilde sequences: ESC [ <number> ~
				case '~':
					DispatchTilde(numericPart, shift, alt, ctrl, events);
					break;

				// Legacy X10 mouse: CSI M <button> <x> <y> (3 raw bytes follow)
				case 'M':
					if (!_csiIsSgr && string.IsNullOrEmpty(paramStr))
					{
						_x10MouseIndex = 0;
						_state = State.X10Mouse;
						// Caller checks _state != CsiParam and skips cleanup
						break;
					}
					events.Add(new UnknownSequenceEvent(_pending.ToArray()));
					break;

				default:
					events.Add(new UnknownSequenceEvent(_pending.ToArray()));
					break;
			}
		}

		private static void DispatchTilde(string numericPart, bool shift, bool alt, bool ctrl, List<InputEvent> events)
		{
			switch (numericPart)
			{
				case "1": events.Add(MakeKey(ConsoleKey.Home, shift, alt, ctrl)); break;
				case "2": events.Add(MakeKey(ConsoleKey.Insert, shift, alt, ctrl)); break;
				case "3": events.Add(MakeKey(ConsoleKey.Delete, shift, alt, ctrl)); break;
				case "4": events.Add(MakeKey(ConsoleKey.End, shift, alt, ctrl)); break;
				case "5": events.Add(MakeKey(ConsoleKey.PageUp, shift, alt, ctrl)); break;
				case "6": events.Add(MakeKey(ConsoleKey.PageDown, shift, alt, ctrl)); break;
				case "11": events.Add(MakeKey(ConsoleKey.F1, shift, alt, ctrl)); break;
				case "12": events.Add(MakeKey(ConsoleKey.F2, shift, alt, ctrl)); break;
				case "13": events.Add(MakeKey(ConsoleKey.F3, shift, alt, ctrl)); break;
				case "14": events.Add(MakeKey(ConsoleKey.F4, shift, alt, ctrl)); break;
				case "15": events.Add(MakeKey(ConsoleKey.F5, shift, alt, ctrl)); break;
				case "17": events.Add(MakeKey(ConsoleKey.F6, shift, alt, ctrl)); break;
				case "18": events.Add(MakeKey(ConsoleKey.F7, shift, alt, ctrl)); break;
				case "19": events.Add(MakeKey(ConsoleKey.F8, shift, alt, ctrl)); break;
				case "20": events.Add(MakeKey(ConsoleKey.F9, shift, alt, ctrl)); break;
				case "21": events.Add(MakeKey(ConsoleKey.F10, shift, alt, ctrl)); break;
				case "23": events.Add(MakeKey(ConsoleKey.F11, shift, alt, ctrl)); break;
				case "24": events.Add(MakeKey(ConsoleKey.F12, shift, alt, ctrl)); break;
				default:
					events.Add(new UnknownSequenceEvent(Encoding.UTF8.GetBytes($"\x1b[{numericPart}~")));
					break;
			}
		}

		private void ParseSgrMouse(string paramStr, bool isPress, List<InputEvent> events)
		{
			// SGR format: ESC [ < buttonCode ; x ; y M/m
			// paramStr includes the '<' prefix
			string data = paramStr.StartsWith('<') ? paramStr[1..] : paramStr;
			var parts = data.Split(';');

			if (parts.Length < 3)
			{
				events.Add(new UnknownSequenceEvent(_pending.ToArray()));
				return;
			}

			if (!int.TryParse(parts[0], out int buttonCode) ||
				!int.TryParse(parts[1], out int x) ||
				!int.TryParse(parts[2], out int y))
			{
				events.Add(new UnknownSequenceEvent(_pending.ToArray()));
				return;
			}

			// SGR coordinates are 1-based, convert to 0-based
			var pos = new Point(x - 1, y - 1);
			var flags = new List<MouseFlags>();

			// Modifier flags from button code
			if ((buttonCode & 0x04) != 0) flags.Add(MouseFlags.ButtonShift);
			if ((buttonCode & 0x08) != 0) flags.Add(MouseFlags.ButtonAlt);
			if ((buttonCode & 0x10) != 0) flags.Add(MouseFlags.ButtonCtrl);

			// Motion flag
			bool motion = (buttonCode & 0x20) != 0;

			// Wheel events
			bool wheel = (buttonCode & 0x40) != 0;

			// Base button (bottom 2 bits, ignoring modifier bits)
			int baseButton = buttonCode & 0x03;

			if (wheel)
			{
				switch (baseButton)
				{
					case 0: flags.Add(MouseFlags.WheeledUp); break;
					case 1: flags.Add(MouseFlags.WheeledDown); break;
					case 2: flags.Add(MouseFlags.WheeledLeft); break;
					case 3: flags.Add(MouseFlags.WheeledRight); break;
				}
			}
			else if (motion)
			{
				flags.Add(MouseFlags.ReportMousePosition);
				switch (baseButton)
				{
					case 0: flags.Add(MouseFlags.Button1Pressed); flags.Add(MouseFlags.Button1Dragged); break;
					case 1: flags.Add(MouseFlags.Button2Pressed); flags.Add(MouseFlags.Button2Dragged); break;
					case 2: flags.Add(MouseFlags.Button3Pressed); flags.Add(MouseFlags.Button3Dragged); break;
					// baseButton 3 = motion with no button
				}
			}
			else
			{
				// Button press/release
				switch (baseButton)
				{
					case 0:
						flags.Add(isPress ? MouseFlags.Button1Pressed : MouseFlags.Button1Released);
						break;
					case 1:
						flags.Add(isPress ? MouseFlags.Button2Pressed : MouseFlags.Button2Released);
						break;
					case 2:
						flags.Add(isPress ? MouseFlags.Button3Pressed : MouseFlags.Button3Released);
						break;
					case 3:
						// Button release (X10 compat — button 3 release means "released")
						flags.Add(MouseFlags.Button1Released);
						break;
				}
			}

			events.Add(new MouseInputEvent(flags, pos));
		}

		/// <summary>
		/// Parses ANSI modifier codes from CSI parameter string.
		/// Format: "number;modifierCode" where modifierCode is 1-based (1=none, 2=Shift, 3=Alt, etc.)
		/// </summary>
		private static void ParseModifiers(string paramStr, out string numericPart, out bool shift, out bool alt, out bool ctrl)
		{
			shift = false;
			alt = false;
			ctrl = false;
			numericPart = paramStr;

			var parts = paramStr.Split(';');
			if (parts.Length >= 2)
			{
				numericPart = parts[0];
				if (int.TryParse(parts[1], out int modCode))
				{
					modCode--; // Convert to 0-based
					shift = (modCode & 1) != 0;
					alt = (modCode & 2) != 0;
					ctrl = (modCode & 4) != 0;
				}
			}
		}

		private static KeyInputEvent MakeKey(ConsoleKey key, bool shift, bool alt, bool ctrl)
		{
			char keyChar = key switch
			{
				ConsoleKey.Home or ConsoleKey.End or ConsoleKey.Insert or
				ConsoleKey.Delete or ConsoleKey.PageUp or ConsoleKey.PageDown or
				ConsoleKey.Clear => '\x1b',
				ConsoleKey.Enter => '\r',
				ConsoleKey.Tab => '\t',
				ConsoleKey.Backspace => '\b',
				ConsoleKey.Spacebar => ' ',
				_ => '\0'
			};
			return new KeyInputEvent(new ConsoleKeyInfo(keyChar, key, shift, alt, ctrl));
		}

		private static ConsoleKey CharToConsoleKey(char c)
		{
			return c switch
			{
				>= 'a' and <= 'z' => (ConsoleKey)(c - 'a' + 'A'), // Map lowercase to ConsoleKey.A-Z
				>= 'A' and <= 'Z' => (ConsoleKey)c,
				>= '0' and <= '9' => (ConsoleKey)c,               // ConsoleKey.D0-D9 = '0'-'9'
				' ' => ConsoleKey.Spacebar,
				'\t' => ConsoleKey.Tab,
				'\r' => ConsoleKey.Enter,
				'\b' => ConsoleKey.Backspace,
				'/' => ConsoleKey.Divide,
				'*' => ConsoleKey.Multiply,
				'-' => ConsoleKey.OemMinus,
				'+' => ConsoleKey.OemPlus,
				'.' => ConsoleKey.OemPeriod,
				',' => ConsoleKey.OemComma,
				';' => ConsoleKey.Oem1,
				'=' => ConsoleKey.OemPlus,
				'[' => ConsoleKey.Oem4,
				']' => ConsoleKey.Oem6,
				'\\' => ConsoleKey.Oem5,
				'\'' => ConsoleKey.Oem7,
				'`' => ConsoleKey.Oem3,
				_ => ConsoleKey.NoName
			};
		}
	}
}
