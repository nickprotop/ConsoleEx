// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;

namespace SharpConsoleUI.Drivers.Input
{
	/// <summary>
	/// Reads raw bytes from Unix stdin (fd 0) and dispatches parsed events.
	/// Replaces Console.ReadKey-based InputLoop on Unix platforms to eliminate
	/// the echo leak caused by .NET's per-keystroke tcsetattr toggling.
	/// </summary>
	internal class UnixStdinReader
	{
		private const int ReadBufferSize = 256;
		private const int EscTimeoutMs = 50; // vim/tmux convention for ESC vs ESC-prefix
		private const int ContinuousPressIntervalMs = 100;
		private const int DoubleClickTimeMs = 500;
		private const int DuplicateReleaseThresholdMs = 50;

		private readonly Stream _stdin;
		private readonly AnsiInputParser _parser;

		// Mouse state — mirrors SequenceHelper's static state
		private volatile bool _isButtonPressed;
		private MouseFlags _lastMouseButtonPressed;
		private Point _lastMousePoint;
		private bool _isButtonClicked;
		private bool _isButtonDoubleClicked;
		private bool _isButtonTripleClicked;
		private DateTime _lastClickTime = DateTime.MinValue;
		private Point? _lastClickPosition;

		public UnixStdinReader(Stream stdin, AnsiInputParser parser)
		{
			_stdin = stdin ?? throw new ArgumentNullException(nameof(stdin));
			_parser = parser ?? throw new ArgumentNullException(nameof(parser));
		}

		/// <summary>
		/// Blocking read loop that reads from stdin and dispatches events.
		/// Runs on a dedicated background thread.
		/// </summary>
		public void ReadLoop(
			CancellationToken cancellationToken,
			Action<ConsoleKeyInfo> onKey,
			Action<List<MouseFlags>, Point> onMouse,
			Action<MouseFlags, Point> continuousButtonPressedHandler)
		{
			var buffer = new byte[ReadBufferSize];

			while (!cancellationToken.IsCancellationRequested)
			{
				int bytesRead;
				try
				{
					bytesRead = _stdin.Read(buffer, 0, buffer.Length);
				}
				catch (OperationCanceledException)
				{
					break;
				}
				catch (ObjectDisposedException)
				{
					break;
				}
				catch (IOException)
				{
					break;
				}

				if (bytesRead <= 0)
					break;

				var events = _parser.Parse(buffer.AsSpan(), bytesRead);

				// ESC timeout: if we got a lone ESC byte, wait briefly for follow-up
				if (events.Count == 0 && bytesRead == 1 && buffer[0] == 0x1B)
				{
					var escWaitStart = DateTime.UtcNow;
					bool gotMore = false;
					try
					{
						var readTask = _stdin.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
						int remainingMs = Math.Max(1, EscTimeoutMs - (int)(DateTime.UtcNow - escWaitStart).TotalMilliseconds);
						if (readTask.Wait(remainingMs))
						{
							int moreBytes = readTask.Result;
							if (moreBytes > 0)
							{
								events.AddRange(_parser.Parse(buffer.AsSpan(), moreBytes));
								gotMore = true;
							}
						}
					}
					catch { }

					if (!gotMore)
						events.AddRange(_parser.Flush());
				}

				// Dispatch events
				foreach (var evt in events)
				{
					if (cancellationToken.IsCancellationRequested)
						break;

					switch (evt)
					{
						case KeyInputEvent keyEvt:
							onKey(keyEvt.KeyInfo);
							break;

						case MouseInputEvent mouseEvt:
							var flags = ProcessMouseEvent(
								mouseEvt.Flags, mouseEvt.Position,
								onMouse, continuousButtonPressedHandler);
							if (flags != null && flags.Count > 0)
								onMouse(flags, mouseEvt.Position);
							break;
					}
				}
			}
		}

		/// <summary>
		/// Processes raw SGR mouse flags through a state machine that mirrors SequenceHelper.GetMouse.
		/// Generates Clicked/DoubleClicked/TripleClicked flags, manages continuous press handler lifecycle.
		/// Returns the flags to dispatch, or null to suppress the event.
		/// </summary>
		private List<MouseFlags>? ProcessMouseEvent(
			List<MouseFlags> rawFlags,
			Point position,
			Action<List<MouseFlags>, Point> onMouse,
			Action<MouseFlags, Point> continuousButtonPressedHandler)
		{
			var buttonState = rawFlags.Count > 0 ? rawFlags[0] : (MouseFlags)0;

			// --- Button press ---
			if (!_isButtonClicked && !_isButtonDoubleClicked &&
				(buttonState == MouseFlags.Button1Pressed ||
				 buttonState == MouseFlags.Button2Pressed ||
				 buttonState == MouseFlags.Button3Pressed) &&
				_lastMouseButtonPressed == 0)
			{
				_lastMouseButtonPressed = buttonState;
				_isButtonPressed = true;
				_lastMousePoint = position;

				// Start continuous press loop (stops when _isButtonPressed becomes false)
				var capturedFlag = buttonState;
				_ = Task.Run(async () =>
				{
					while (_isButtonPressed)
					{
						await Task.Delay(ContinuousPressIntervalMs);
						if (_isButtonPressed && _lastMouseButtonPressed != 0)
						{
							continuousButtonPressedHandler(capturedFlag, _lastMousePoint);
						}
					}
				});

				return new List<MouseFlags> { buttonState };
			}

			// --- Button press during double-click window → triple click ---
			if (_isButtonDoubleClicked &&
				(buttonState == MouseFlags.Button1Pressed ||
				 buttonState == MouseFlags.Button2Pressed ||
				 buttonState == MouseFlags.Button3Pressed))
			{
				var tripleFlag = GetButtonTripleClicked(buttonState);
				_isButtonDoubleClicked = false;
				_isButtonTripleClicked = true;
				return new List<MouseFlags> { tripleFlag };
			}

			// --- Button press during click window → double click ---
			if (_isButtonClicked &&
				(buttonState == MouseFlags.Button1Pressed ||
				 buttonState == MouseFlags.Button2Pressed ||
				 buttonState == MouseFlags.Button3Pressed))
			{
				var doubleFlag = GetButtonDoubleClicked(buttonState);
				_isButtonClicked = false;
				_isButtonDoubleClicked = true;
				_ = Task.Run(async () =>
				{
					await Task.Delay(300);
					_isButtonDoubleClicked = false;
				});
				return new List<MouseFlags> { doubleFlag };
			}

			// --- Button release ---
			if (!_isButtonClicked && !_isButtonDoubleClicked &&
				(buttonState == MouseFlags.Button1Released ||
				 buttonState == MouseFlags.Button2Released ||
				 buttonState == MouseFlags.Button3Released))
			{
				_isButtonPressed = false; // Stops the continuous press loop

				if (_isButtonTripleClicked)
				{
					_isButtonTripleClicked = false;
				}
				else if (position.X == _lastMousePoint.X && position.Y == _lastMousePoint.Y)
				{
					// Release at same position → click
					var timeSinceLastClick = (DateTime.Now - _lastClickTime).TotalMilliseconds;

					// Duplicate release guard
					if (_lastClickPosition?.X == position.X &&
						_lastClickPosition?.Y == position.Y &&
						timeSinceLastClick < DuplicateReleaseThresholdMs)
					{
						_lastMouseButtonPressed = 0;
						return null; // Suppress duplicate
					}

					var clickFlag = GetButtonClicked(buttonState);
					_isButtonClicked = true;
					_lastClickPosition = position;
					_lastClickTime = DateTime.Now;

					_ = Task.Run(async () =>
					{
						await Task.Delay(300);
						_isButtonClicked = false;
					});

					_lastMouseButtonPressed = 0;
					return new List<MouseFlags> { buttonState, clickFlag };
				}

				_lastMouseButtonPressed = 0;
				return new List<MouseFlags> { buttonState };
			}

			// --- Motion with button held (drag) ---
			if (_isButtonPressed && _lastMouseButtonPressed != 0 &&
				rawFlags.Contains(MouseFlags.ReportMousePosition))
			{
				_lastMousePoint = position;
				return new List<MouseFlags>(rawFlags);
			}

			// --- Motion without button (hover) ---
			if (rawFlags.Contains(MouseFlags.ReportMousePosition))
			{
				return new List<MouseFlags>(rawFlags);
			}

			// --- Wheel events ---
			if (rawFlags.Contains(MouseFlags.WheeledUp) ||
				rawFlags.Contains(MouseFlags.WheeledDown) ||
				rawFlags.Contains(MouseFlags.WheeledLeft) ||
				rawFlags.Contains(MouseFlags.WheeledRight))
			{
				return new List<MouseFlags>(rawFlags);
			}

			// Unhandled — pass through
			return new List<MouseFlags>(rawFlags);
		}

		private static MouseFlags GetButtonClicked(MouseFlags released)
		{
			return released switch
			{
				MouseFlags.Button1Released => MouseFlags.Button1Clicked,
				MouseFlags.Button2Released => MouseFlags.Button2Clicked,
				MouseFlags.Button3Released => MouseFlags.Button3Clicked,
				_ => MouseFlags.AllEvents
			};
		}

		private static MouseFlags GetButtonDoubleClicked(MouseFlags pressed)
		{
			return pressed switch
			{
				MouseFlags.Button1Pressed => MouseFlags.Button1DoubleClicked,
				MouseFlags.Button2Pressed => MouseFlags.Button2DoubleClicked,
				MouseFlags.Button3Pressed => MouseFlags.Button3DoubleClicked,
				_ => MouseFlags.AllEvents
			};
		}

		private static MouseFlags GetButtonTripleClicked(MouseFlags pressed)
		{
			return pressed switch
			{
				MouseFlags.Button1Pressed => MouseFlags.Button1TripleClicked,
				MouseFlags.Button2Pressed => MouseFlags.Button2TripleClicked,
				MouseFlags.Button3Pressed => MouseFlags.Button3TripleClicked,
				_ => MouseFlags.AllEvents
			};
		}
	}
}
