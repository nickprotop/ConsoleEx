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

		private readonly Stream _stdin;
		private readonly AnsiInputParser _parser;

		// Mouse state. Only what is needed to repeat a held button; clicks and
		// double-clicks are derived once, upstream, by MouseGestureRecognizer.
		private volatile bool _isButtonPressed;
		private readonly object _mouseStateLock = new();
		private MouseFlags _lastMouseButtonPressed;
		private Point _lastMousePoint;

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
			Action<string> onPaste,
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

				// ESC timeout: if we got a lone ESC byte, wait briefly for follow-up.
				// We use ReadAsync + Wait(timeout). If the timeout expires, we MUST
				// await the ReadAsync to completion before continuing so that the
				// pending async read doesn't race with the next synchronous Read.
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
						else
						{
							// Timeout expired — flush ESC as standalone key, but the
							// ReadAsync is still pending and will capture the next byte.
							// We must await it so we don't lose that byte.
							events.AddRange(_parser.Flush());
							gotMore = true; // skip second Flush below

							// Dispatch the standalone ESC now
							foreach (var evt in events)
							{
								if (cancellationToken.IsCancellationRequested) break;
								if (evt is KeyInputEvent keyEvt)
									onKey(keyEvt.KeyInfo);
								else if (evt is PasteInputEvent pasteEvt)
									onPaste(pasteEvt.Text);
							}
							events.Clear();

							// Now wait for the pending ReadAsync to complete — this will
							// capture the next keypress that would otherwise be lost
							try
							{
								int moreBytes = readTask.GetAwaiter().GetResult();
								if (moreBytes > 0)
									events.AddRange(_parser.Parse(buffer.AsSpan(), moreBytes));
							}
							catch { }
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

						case PasteInputEvent pasteEvt:
							onPaste(pasteEvt.Text);
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
		/// Reports what the terminal said, and nothing else.
		/// </summary>
		/// <remarks>
		/// <para>
		/// Clicks, double-clicks, triple-clicks and drags are NOT derived here. They
		/// are gestures - judgements about timing and position - and they are made
		/// once, for every platform, by <c>MouseGestureRecognizer</c> in the input
		/// coordinator. This method only reports presses, releases, motion and wheel.
		/// </para>
		/// <para>
		/// This used to be a ~130-line state machine mirroring SequenceHelper's, with
		/// five pieces of state and two background expiry timers. Having two copies of
		/// that logic meant they could disagree, and they did. Worse, a press inside
		/// the click window was REPLACED by the derived double-click rather than
		/// accompanied by it, so a control watching for presses silently lost half of
		/// a fast click run.
		/// </para>
		/// <para>
		/// The continuous-press loop stays, because it is not a gesture: it is the
		/// terminal's missing auto-repeat, and only the thing reading stdin knows a
		/// button is still physically down.
		/// </para>
		/// </remarks>
		private List<MouseFlags>? ProcessMouseEvent(
			List<MouseFlags> rawFlags,
			Point position,
			Action<List<MouseFlags>, Point> onMouse,
			Action<MouseFlags, Point> continuousButtonPressedHandler)
		{
			// The BUTTON flag, not rawFlags[0]: AnsiInputParser prepends modifier flags
			// (ButtonShift/ButtonAlt/ButtonCtrl) before the button, so rawFlags[0] is the
			// modifier whenever one is held.
			var buttonState = ExtractButtonState(rawFlags);

			if (buttonState == MouseFlags.Button1Pressed ||
				buttonState == MouseFlags.Button2Pressed ||
				buttonState == MouseFlags.Button3Pressed)
			{
				bool alreadyDown;
				lock (_mouseStateLock)
				{
					alreadyDown = _lastMouseButtonPressed == buttonState;
					_lastMouseButtonPressed = buttonState;
					_lastMousePoint = position;
				}

				// A second press of a button already held is the terminal repeating
				// itself mid-drag; one repeat loop is enough.
				if (!alreadyDown)
				{
					_isButtonPressed = true;
					StartContinuousPress(buttonState, continuousButtonPressedHandler);
				}

				return new List<MouseFlags>(rawFlags);
			}

			if (buttonState == MouseFlags.Button1Released ||
				buttonState == MouseFlags.Button2Released ||
				buttonState == MouseFlags.Button3Released)
			{
				_isButtonPressed = false;        // stops the continuous loop
				lock (_mouseStateLock) _lastMouseButtonPressed = 0;
				return new List<MouseFlags>(rawFlags);
			}

			// Motion keeps the last point current so a held button repeats at the
			// pointer's position rather than where it went down.
			if (rawFlags.Contains(MouseFlags.ReportMousePosition))
			{
				lock (_mouseStateLock) _lastMousePoint = position;
			}

			return new List<MouseFlags>(rawFlags);
		}

		/// <summary>
		/// Repeats a held button so it keeps scrolling, which a terminal never does.
		/// </summary>
		private void StartContinuousPress(
			MouseFlags button, Action<MouseFlags, Point> continuousButtonPressedHandler)
		{
			_ = Task.Run(async () =>
			{
				while (_isButtonPressed)
				{
					await Task.Delay(ContinuousPressIntervalMs);

					Point point;
					MouseFlags held;
					lock (_mouseStateLock)
					{
						point = _lastMousePoint;
						held = _lastMouseButtonPressed;
					}

					if (_isButtonPressed && held != 0)
						continuousButtonPressedHandler(button, point);
				}
			});
		}

		/// <summary>
		/// Returns the button flag from a raw SGR flag set, ignoring any modifier flags that precede it.
		/// </summary>
		private static MouseFlags ExtractButtonState(List<MouseFlags> rawFlags)
		{
			for (int i = 0; i < rawFlags.Count; i++)
			{
				if (!IsModifier(rawFlags[i]))
					return rawFlags[i];
			}
			return (MouseFlags)0;
		}

		/// <summary>Returns just the modifier flags (Shift/Alt/Ctrl) present in a raw SGR flag set.</summary>
		private static List<MouseFlags> ExtractModifiers(List<MouseFlags> rawFlags)
		{
			List<MouseFlags>? mods = null;
			for (int i = 0; i < rawFlags.Count; i++)
			{
				if (IsModifier(rawFlags[i]))
					(mods ??= new List<MouseFlags>()).Add(rawFlags[i]);
			}
			return mods ?? EmptyModifiers;
		}

		private static readonly List<MouseFlags> EmptyModifiers = new();

		private static bool IsModifier(MouseFlags flag) =>
			flag == MouseFlags.ButtonShift || flag == MouseFlags.ButtonAlt || flag == MouseFlags.ButtonCtrl;

		/// <summary>
		/// Builds the dispatch list: the modifiers that came in with the event, followed by the
		/// synthesized button/click flags. Keeps Shift/Ctrl visible to controls that key off them.
		/// </summary>
		private static List<MouseFlags> WithModifiers(List<MouseFlags> modifiers, params MouseFlags[] flags)
		{
			var result = new List<MouseFlags>(modifiers.Count + flags.Length);
			result.AddRange(modifiers);
			result.AddRange(flags);
			return result;
		}



	}
}
