// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace SharpConsoleUI.Helpers
{
	/// <summary>
	/// The system clipboard over the X11 protocol, talking to libX11 directly — no xclip, xsel or wl-copy.
	/// </summary>
	/// <remarks>
	/// <para>WHY IT EXISTS: a Linux desktop without one of those tools had no system clipboard at all, only OSC 52,
	/// which VTE terminals (GNOME Terminal, Ptyxis, Console) ignore. libX11 ships with every X11 desktop and with
	/// Xwayland, which GNOME and KDE run under Wayland and bridge to the Wayland clipboard — so this reaches native
	/// Wayland apps too, with nothing installed.</para>
	///
	/// <para>AN X11 CLIPBOARD IS SERVED, NOT STORED. Copying makes this process the selection OWNER; the text lives
	/// here and is sent to whichever client pastes, on request. So one background thread owns the connection for
	/// the whole run and answers those requests, and before the process exits the text is handed to the desktop's
	/// clipboard manager (the ICCCM SAVE_TARGETS handshake) so it outlives the app.</para>
	///
	/// <para>ONE THREAD TOUCHES THE DISPLAY. Xlib connections are not thread-safe without XInitThreads, which must
	/// precede every other Xlib call in the process — not something a library can guarantee. Other threads post
	/// work to the owning thread and wake it through a pipe it polls beside the X socket.</para>
	///
	/// <para>NOTHING HERE MAY REACH THE TERMINAL OR END THE PROCESS. Xlib's default error handlers print to stderr
	/// — on top of the UI — and call exit(). Quiet handlers replace them; a lost connection marks this clipboard
	/// broken and the helper falls back to its in-process buffer.</para>
	/// </remarks>
	internal sealed unsafe class X11Clipboard : IDisposable
	{
		private const string LibX11 = "libX11.so.6";
		private const string LibC = "libc";

		private const int PropertyNotify = 28;
		private const int SelectionClear = 29;
		private const int SelectionRequest = 30;
		private const int SelectionNotify = 31;
		private const int PropertyNewValue = 0;
		private const int PropertyDelete = 1;
		private const int PropModeReplace = 0;
		private const nint PropertyChangeMask = 1 << 22;
		private const nint XaAtom = 4;
		private const nint XaString = 31;
		private const nint None = 0;
		private const nint CurrentTime = 0;
		private const short PollIn = 1;

		/// <summary>How long a read waits for the owner's answer before giving up on its own side.</summary>
		private const int ReadDeadlineMs = 2000;

		/// <summary>How long an incremental send may wait for the requestor to take a chunk before it is dropped.</summary>
		private const int SendIdleMs = 5000;

		/// <summary>How long a SAVE_TARGETS handoff may take before the process exits without it.</summary>
		internal const int DefaultHandoffMs = 1000;

		private readonly ConcurrentQueue<Action> _work = new();
		private readonly Thread _thread;
		private readonly ManualResetEventSlim _started = new(false);
		private volatile bool _broken;
		private volatile bool _stopping;
		private bool _opened;
		private readonly string _selectionName;

		// Owned by the X thread from here down.
		private nint _display;
		private nint _window;
		private int _wakeRead = -1;
		private int _wakeWrite = -1;
		private int _chunkBytes;
		private nint _clipboard, _targets, _multiple, _atomPair, _utf8, _textPlainUtf8, _textPlain, _text, _incr;
		private nint _clipboardManager, _saveTargets, _property;
		private string? _owned;
		private PendingRead? _read;
		private readonly Queue<TaskCompletionSource<string?>> _queuedReads = new();
		private (TaskCompletionSource<bool> Done, long Deadline)? _handoff;
		private readonly Dictionary<(nint Window, nint Property), Transfer> _sending = new();

		/// <summary>An incremental (INCR) send in progress: the text, and how much of it the requestor has taken.</summary>
		private sealed class Transfer
		{
			public required byte[] Data { get; init; }
			public required nint Type { get; init; }
			public int Offset { get; set; }
			public bool Finished { get; set; }
			public long Deadline { get; set; }
		}

		private sealed class PendingRead
		{
			public required TaskCompletionSource<string?> Done { get; init; }
			public required nint Target { get; set; }
			public long Deadline { get; set; }
			public MemoryStream? Incremental { get; set; }

			/// <summary>Callers that arrived while this read was in flight; they get the same answer.</summary>
			public List<TaskCompletionSource<string?>>? Joined { get; set; }
		}

		private X11Clipboard(string selection)
		{
			_selectionName = selection;
			_thread = new Thread(Run) { IsBackground = true, Name = "SharpConsoleUI X11 clipboard" };
		}

		/// <summary>
		/// Connects to the display named by <c>DISPLAY</c>. Null when there is none, libX11 is not installed, or the
		/// connection fails — the caller then uses another backend. Never throws.
		/// </summary>
		/// <param name="selection">
		/// The selection to serve. CLIPBOARD is the one every desktop pastes from; a test names a private one, so the
		/// same protocol runs without replacing what the person running the tests had copied.
		/// </param>
		/// <param name="timeoutMs">How long to wait for the connection.</param>
		public static X11Clipboard? TryOpen(string selection = "CLIPBOARD", int timeoutMs = 2000)
		{
			if (!OperatingSystem.IsLinux() && !OperatingSystem.IsFreeBSD())
				return null;
			if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")))
				return null;

			try
			{
				var clipboard = new X11Clipboard(selection);
				clipboard._thread.Start();
				if (!clipboard._started.Wait(timeoutMs) || !clipboard._opened)
				{
					clipboard._stopping = true;
					return null;
				}
				return clipboard;
			}
			catch
			{
				return null;
			}
		}

		/// <summary>How many sends went in INCR pieces. For tests: a reader that accepts one large property cannot tell.</summary>
		internal int IncrementalSends => Volatile.Read(ref _incrementalSends);

		private volatile int _answerDelayMs;

		/// <summary>Test-only: makes this owner take <paramref name="ms"/> to answer a conversion request.</summary>
		internal void DelayAnswersForTest(int ms) => _answerDelayMs = ms;

		private int _incrementalSends;

		/// <summary>Whether the connection has failed; a broken clipboard does nothing and reads nothing.</summary>
		public bool IsBroken => _broken;

		/// <summary>
		/// Makes this process the clipboard owner, serving <paramref name="text"/> to whoever pastes. Returns at once;
		/// the ownership is taken on the X thread.
		/// </summary>
		public void SetText(string text) => Post(() => TakeOwnership(text));

		/// <summary>
		/// Reads the clipboard: this process's own text when it owns it, otherwise the owner's answer. Null when
		/// nothing answered within <paramref name="timeoutMs"/> or the connection is broken; empty when the clipboard
		/// holds no text.
		/// </summary>
		public string? GetText(int timeoutMs)
		{
			if (_broken)
				return null;
			var done = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
			if (!Post(() => BeginRead(done)))
				return null;
			try
			{
				if (done.Task.Wait(timeoutMs))
					return done.Task.Result;

				// GIVING UP HERE LEAVES THE CONVERSION RUNNING, deliberately. The request is with the owner and
				// the server will deliver its answer whatever this caller does; tearing it down would throw away
				// the very text the next paste wants, and starting a second conversion into the same property
				// while the first is outstanding makes the two answers race. The read stays in the slot and
				// keeps collecting — see BeginRead, where the next caller joins it instead of queueing.
				return null;
			}
			catch
			{
				return null;
			}
		}

		/// <summary>
		/// Hands the text this process still owns to the desktop's clipboard manager, so a paste after the process
		/// has exited still finds it. Waits at most <paramref name="timeoutMs"/>; answers whether a manager took it.
		/// </summary>
		public bool HandOffBeforeExit(int timeoutMs = DefaultHandoffMs)
		{
			if (_broken)
				return false;
			var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
			if (!Post(() => BeginHandoff(done, timeoutMs)))
				return false;
			try
			{
				return done.Task.Wait(timeoutMs + 200) && done.Task.Result;
			}
			catch
			{
				return false;
			}
		}

		public void Dispose()
		{
			_stopping = true;
			Wake();
			if (Thread.CurrentThread != _thread)
				_thread.Join(2000);
		}

		private bool Post(Action work)
		{
			if (_broken || _stopping)
				return false;
			_work.Enqueue(work);
			Wake();
			return true;
		}

		private void Wake()
		{
			if (_wakeWrite < 0)
				return;
			byte b = 1;
			write(_wakeWrite, &b, 1);
		}

		#region X thread

		private void Run()
		{
			try
			{
				if (!Open())
				{
					_started.Set();
					return;
				}
				_opened = true;
				_started.Set();
				Loop();
			}
			catch
			{
				// The thread must never take the process down; the clipboard just stops working.
				_broken = true;
			}
			finally
			{
				_started.Set();
				FailOutstanding();
				Close();
			}
		}

		private bool Open()
		{
			try
			{
				InstallQuietHandlers();
				_display = XOpenDisplay(nint.Zero);
			}
			catch (DllNotFoundException)
			{
				return false;
			}
			catch (EntryPointNotFoundException)
			{
				return false;
			}
			if (_display == nint.Zero)
				return false;

			QuietIoExit(_display);

			int* fds = stackalloc int[2];
			if (pipe(fds) != 0)
			{
				XCloseDisplay(_display);
				_display = nint.Zero;
				return false;
			}
			_wakeRead = fds[0];
			_wakeWrite = fds[1];

			_window = XCreateSimpleWindow(_display, XDefaultRootWindow(_display), 0, 0, 1, 1, 0, 0, 0);
			XSelectInput(_display, _window, PropertyChangeMask);

			_clipboard = Intern(_selectionName);
			_targets = Intern("TARGETS");
			_multiple = Intern("MULTIPLE");
			_atomPair = Intern("ATOM_PAIR");
			_utf8 = Intern("UTF8_STRING");
			_textPlainUtf8 = Intern("text/plain;charset=utf-8");
			_textPlain = Intern("text/plain");
			_text = Intern("TEXT");
			_incr = Intern("INCR");
			_clipboardManager = Intern("CLIPBOARD_MANAGER");
			_saveTargets = Intern("SAVE_TARGETS");
			_property = Intern("SHARPCONSOLEUI_CLIPBOARD");

			// THE LARGEST PIECE SENT IN ONE PROPERTY: the core request size, not the BIG-REQUESTS one. A requestor may
			// refuse a property larger than that outright — Tk answers "selection property too large" — so anything
			// bigger goes by INCR, in pieces of this size.
			_chunkBytes = (int)Math.Clamp((long)XMaxRequestSize(_display) * 4 - 100, 4096, 256 * 1024);

			XFlush(_display);
			return true;
		}

		private void Loop()
		{
			var fds = stackalloc PollFd[2];
			byte* drain = stackalloc byte[64];

			while (!_stopping && !_broken)
			{
				while (_work.TryDequeue(out var work))
					work();

				while (!_broken && XPending(_display) > 0)
				{
					XEvent ev;
					XNextEvent(_display, &ev);
					Handle(&ev);
				}
				if (_broken)
					break;
				XFlush(_display);
				ExpireDeadlines();

				if (!_work.IsEmpty)
					continue;

				fds[0] = new PollFd { Fd = XConnectionNumber(_display), Events = PollIn };
				fds[1] = new PollFd { Fd = _wakeRead, Events = PollIn };
				poll(fds, 2, NextTimeoutMs());

				if ((fds[1].Revents & PollIn) != 0)
					read(_wakeRead, drain, 64);
			}
		}

		private void Handle(XEvent* ev)
		{
			switch (ev->Type)
			{
				case SelectionRequest:
					// A SLOW OWNER, ON PURPOSE (tests only): a real browser or editor can take longer to answer a
					// conversion than a paste is willing to wait, and that is the case the abandoned-read handling
					// exists for. Zero in every normal run, so nothing is delayed in production.
					if (_answerDelayMs > 0)
						Thread.Sleep(_answerDelayMs);
					Answer((XSelectionRequestEvent*)ev);
					break;
				case SelectionClear:
					// ANOTHER CLIENT COPIED: it owns the clipboard now, and this process's text is no longer the answer.
					if (((XSelectionClearEvent*)ev)->Selection == _clipboard)
						_owned = null;
					break;
				case SelectionNotify:
					Notified((XSelectionEvent*)ev);
					break;
				case PropertyNotify:
					PropertyChanged((XPropertyEvent*)ev);
					break;
			}
		}

		private void TakeOwnership(string text)
		{
			XSetSelectionOwner(_display, _clipboard, _window, CurrentTime);
			_owned = XGetSelectionOwner(_display, _clipboard) == _window ? text : null;
		}

		private void Answer(XSelectionRequestEvent* request)
		{
			// AN OBSOLETE REQUESTOR NAMES NO PROPERTY; ICCCM says to use the target atom as the property then.
			nint property = request->Property == None ? request->Target : request->Property;

			bool served = request->Selection == _clipboard && _owned != null
				&& (request->Target == _multiple
					? AnswerMultiple(request->Requestor, property)
					: Write(request->Requestor, request->Target, property));

			var reply = new XSelectionEvent
			{
				Type = SelectionNotify,
				Display = _display,
				Requestor = request->Requestor,
				Selection = request->Selection,
				Target = request->Target,
				Property = served ? property : None,
				Time = request->Time,
			};
			XSendEvent(_display, request->Requestor, 0, 0, &reply);
		}

		/// <summary>
		/// Answers a MULTIPLE request: the requestor's property holds (target, property) pairs, each answered in turn,
		/// with a pair's property set to None where that target cannot be given. Clipboard managers saving the text
		/// on exit commonly ask this way.
		/// </summary>
		private bool AnswerMultiple(nint requestor, nint property)
		{
			nint type;
			int format;
			nuint count, after;
			byte* data;
			if (XGetWindowProperty(_display, requestor, property, 0, int.MaxValue / 4, 0, _atomPair,
					&type, &format, &count, &after, &data) != 0 || data == null)
				return false;
			try
			{
				if (format != 32)
					return false;
				var pairs = (nint*)data;
				for (nuint i = 0; i + 1 < count; i += 2)
				{
					if (!Write(requestor, pairs[i], pairs[i + 1]))
						pairs[i + 1] = None;
				}
				XChangeProperty(_display, requestor, property, _atomPair, 32, PropModeReplace, data, (int)count);
				return true;
			}
			finally
			{
				XFree((nint)data);
			}
		}

		private bool Write(nint requestor, nint target, nint property)
		{
			if (_owned == null)
				return false;

			if (target == _targets)
			{
				nint* offered = stackalloc nint[] { _targets, _multiple, _utf8, _textPlainUtf8, XaString, _text, _textPlain };
				XChangeProperty(_display, requestor, property, XaAtom, 32, PropModeReplace, (byte*)offered, 7);
				return true;
			}

			byte[] bytes;
			nint type;
			if (target == _utf8 || target == _textPlainUtf8)
			{
				bytes = Encoding.UTF8.GetBytes(_owned);
				type = target;
			}
			else if (target == XaString || target == _text || target == _textPlain)
			{
				// STRING IS LATIN-1 BY DEFINITION; a character outside it becomes '?'. Every modern client asks for
				// UTF8_STRING first, so this reaches only the old ones.
				bytes = Encoding.Latin1.GetBytes(_owned);
				type = XaString;
			}
			else
			{
				return false;
			}

			if (bytes.Length > _chunkBytes)
			{
				BeginIncrementalSend(requestor, property, type, bytes);
				return true;
			}
			fixed (byte* p = bytes)
				XChangeProperty(_display, requestor, property, type, 8, PropModeReplace, p, bytes.Length);
			return true;
		}

		/// <summary>
		/// Starts an INCR send: the property gets type INCR and the total size, and each time the requestor deletes it
		/// the next piece is written, ending with an empty one.
		/// </summary>
		/// <remarks>
		/// THE REQUESTOR'S DELETES ARE WATCHED ON ITS WINDOW, so this connection selects property events there. The
		/// transfer keeps the bytes it started with: a copy made meanwhile changes what the next paste gets, not the
		/// one already under way.
		/// </remarks>
		private void BeginIncrementalSend(nint requestor, nint property, nint type, byte[] bytes)
		{
			Interlocked.Increment(ref _incrementalSends);
			XSelectInput(_display, requestor, PropertyChangeMask);
			nint size = bytes.Length;
			XChangeProperty(_display, requestor, property, _incr, 32, PropModeReplace, (byte*)&size, 1);
			_sending[(requestor, property)] = new Transfer
			{
				Data = bytes,
				Type = type,
				Deadline = Environment.TickCount64 + SendIdleMs,
			};
		}

		private void SendNextPiece(nint requestor, nint property, Transfer transfer)
		{
			if (transfer.Finished)
			{
				EndTransfer(requestor, property);
				return;
			}
			int length = Math.Min(_chunkBytes, transfer.Data.Length - transfer.Offset);
			fixed (byte* p = transfer.Data)
				XChangeProperty(_display, requestor, property, transfer.Type, 8, PropModeReplace, p + transfer.Offset, length);
			transfer.Offset += length;

			// THE EMPTY PIECE IS THE END MARKER, and it is the one just written when nothing was left to send.
			transfer.Finished = length == 0;
			transfer.Deadline = Environment.TickCount64 + SendIdleMs;
		}

		private void EndTransfer(nint requestor, nint property)
		{
			_sending.Remove((requestor, property));

			// STOP WATCHING A WINDOW NOTHING IS BEING SENT TO: its other property changes would keep waking this thread.
			foreach (var key in _sending.Keys)
			{
				if (key.Window == requestor)
					return;
			}
			if (requestor != _window)
				XSelectInput(_display, requestor, 0);
		}

		private void BeginRead(TaskCompletionSource<string?> done)
		{
			// THIS PROCESS'S OWN COPY IS ANSWERED HERE: asking the server would only route the request back to us.
			if (_owned != null && XGetSelectionOwner(_display, _clipboard) == _window)
			{
				done.TrySetResult(_owned);
				return;
			}
			if (XGetSelectionOwner(_display, _clipboard) == None)
			{
				done.TrySetResult(string.Empty);
				return;
			}

			// ONE CONVERSION AT A TIME: they share the property they land in, so a second request issued while
			// one is outstanding would have the two answers race for it.
			//
			// A READ ALREADY RUNNING IS THIS READ'S ANSWER TOO. The clipboard is one value, so a caller arriving
			// now wants exactly what the conversion in flight is fetching: it joins that read and is completed
			// with it. Queueing instead meant a paste whose predecessor had timed out waited for a WHOLE SECOND
			// conversion after the first finished — so the key did nothing, twice, until the owner answered and
			// the backlog drained. Joining also keeps a burst of pastes to a single round trip.
			if (_read is { } running)
			{
				running.Joined ??= new List<TaskCompletionSource<string?>>();
				running.Joined.Add(done);
				return;
			}
			_read = new PendingRead { Done = done, Target = _utf8 };
			Convert(_read);
		}

		private void Convert(PendingRead read)
		{
			read.Deadline = Environment.TickCount64 + ReadDeadlineMs;
			XDeleteProperty(_display, _window, _property);
			XConvertSelection(_display, _clipboard, read.Target, _property, _window, CurrentTime);
		}

		private void Notified(XSelectionEvent* ev)
		{
			if (ev->Selection == _clipboardManager)
			{
				if (_handoff is { } handoff)
				{
					handoff.Done.TrySetResult(ev->Property != None);
					_handoff = null;
				}
				return;
			}

			if (_read is not { } read || ev->Selection != _clipboard || ev->Requestor != _window)
				return;

			if (ev->Property == None)
			{
				// UTF8_STRING REFUSED: an old owner may still give Latin-1 STRING.
				if (read.Target == _utf8)
				{
					read.Target = XaString;
					Convert(read);
				}
				else
				{
					FinishRead(string.Empty);
				}
				return;
			}

			if (!TakeProperty(out var type, out var bytes))
			{
				FinishRead(null);
				return;
			}

			if (type == _incr)
			{
				// INCREMENTAL: the owner sends the text in chunks, each written to the property after this side
				// deletes the previous one — which TakeProperty just did — and ends with an empty chunk.
				read.Incremental = new MemoryStream();
				read.Deadline = Environment.TickCount64 + ReadDeadlineMs;
				return;
			}
			FinishRead(Decode(read.Target, bytes));
		}

		private void PropertyChanged(XPropertyEvent* ev)
		{
			// A REQUESTOR TOOK A PIECE of an incremental send: the next one goes in its place.
			if (ev->State == PropertyDelete && _sending.TryGetValue((ev->Window, ev->Atom), out var transfer))
			{
				SendNextPiece(ev->Window, ev->Atom, transfer);
				return;
			}

			if (_read is not { Incremental: { } chunks } read
				|| ev->Window != _window || ev->Atom != _property || ev->State != PropertyNewValue)
				return;

			if (!TakeProperty(out _, out var bytes))
			{
				FinishRead(null);
				return;
			}
			if (bytes.Length == 0)
			{
				FinishRead(Decode(read.Target, chunks.ToArray()));
				return;
			}
			chunks.Write(bytes);
			read.Deadline = Environment.TickCount64 + ReadDeadlineMs;
		}

		private bool TakeProperty(out nint type, out byte[] bytes)
		{
			nint actualType;
			int format;
			nuint count, after;
			byte* data;
			bytes = Array.Empty<byte>();
			type = None;
			if (XGetWindowProperty(_display, _window, _property, 0, int.MaxValue / 4, 1, 0,
					&actualType, &format, &count, &after, &data) != 0)
				return false;
			try
			{
				type = actualType;
				if (data != null && count > 0)
				{
					// The item count is in units of the format; format-32 items are C longs in memory, not 4 bytes.
					long size = format switch { 8 => (long)count, 16 => (long)count * 2, _ => (long)count * sizeof(nint) };
					bytes = new ReadOnlySpan<byte>(data, (int)Math.Min(size, int.MaxValue)).ToArray();
				}
				return true;
			}
			finally
			{
				if (data != null)
					XFree((nint)data);
			}
		}

		private string Decode(nint target, byte[] bytes) =>
			target == XaString ? Encoding.Latin1.GetString(bytes) : Encoding.UTF8.GetString(bytes);

		private void FinishRead(string? text)
		{
			var read = _read;
			_read = null;
			read?.Done.TrySetResult(text);

			// EVERY CALLER THAT JOINED gets this same answer — including ones whose own wait has already run out,
			// where the TrySetResult is simply a no-op.
			if (read?.Joined is { } joined)
			{
				foreach (var waiter in joined)
					waiter.TrySetResult(text);
			}

			if (_queuedReads.TryDequeue(out var next))
				BeginRead(next);
		}

		private void BeginHandoff(TaskCompletionSource<bool> done, int timeoutMs)
		{
			// NOTHING TO HAND OVER unless this process still owns the clipboard and a manager is running to take it.
			if (_owned == null || XGetSelectionOwner(_display, _clipboard) != _window
				|| XGetSelectionOwner(_display, _clipboardManager) == None)
			{
				done.TrySetResult(false);
				return;
			}
			_handoff = (done, Environment.TickCount64 + timeoutMs);
			XConvertSelection(_display, _clipboardManager, _saveTargets, _property, _window, CurrentTime);
		}

		private void ExpireDeadlines()
		{
			long now = Environment.TickCount64;
			if (_read is { } read && now >= read.Deadline)
				FinishRead(null);
			if (_handoff is { } handoff && now >= handoff.Deadline)
			{
				handoff.Done.TrySetResult(false);
				_handoff = null;
			}

			// A REQUESTOR THAT STOPPED TAKING PIECES — it exited, or gave up — is not waited on forever.
			List<(nint, nint)>? stale = null;
			foreach (var (key, transfer) in _sending)
			{
				if (now >= transfer.Deadline)
					(stale ??= new()).Add(key);
			}
			if (stale != null)
			{
				foreach (var (window, property) in stale)
					EndTransfer(window, property);
			}
		}

		private int NextTimeoutMs()
		{
			long next = long.MaxValue;
			if (_read is { } read)
				next = Math.Min(next, read.Deadline);
			if (_handoff is { } handoff)
				next = Math.Min(next, handoff.Deadline);
			foreach (var transfer in _sending.Values)
				next = Math.Min(next, transfer.Deadline);
			if (next == long.MaxValue)
				return -1;
			return (int)Math.Clamp(next - Environment.TickCount64, 0, int.MaxValue);
		}

		private void FailOutstanding()
		{
			_read?.Done.TrySetResult(null);
			if (_read?.Joined is { } joined)
			{
				foreach (var waiter in joined)
					waiter.TrySetResult(null);
			}
			_read = null;
			while (_queuedReads.TryDequeue(out var queued))
				queued.TrySetResult(null);
			_handoff?.Done.TrySetResult(false);
			_handoff = null;
			while (_work.TryDequeue(out _)) { }
		}

		private void Close()
		{
			// AFTER A LOST CONNECTION Xlib's state is unusable; closing it would only fail again.
			if (_display != nint.Zero && !_broken)
			{
				try
				{
					if (_window != nint.Zero)
						XDestroyWindow(_display, _window);
					XCloseDisplay(_display);
				}
				catch
				{
				}
			}
			_display = nint.Zero;
			_broken = true;
			int wakeRead = _wakeRead, wakeWrite = _wakeWrite;
			_wakeRead = _wakeWrite = -1;
			if (wakeRead >= 0) close(wakeRead);
			if (wakeWrite >= 0) close(wakeWrite);
		}

		private nint Intern(string name)
		{
			byte[] bytes = Encoding.ASCII.GetBytes(name + '\0');
			fixed (byte* p = bytes)
				return XInternAtom(_display, p, 0);
		}

		#endregion

		#region Error handlers

		// THE CONNECTION THAT LOST ITS SERVER. Xlib reports an I/O error per display, through a handler that cannot be
		// told which clipboard it belongs to; there is one connection per process in practice.
		private static volatile X11Clipboard? _current;

		private static int _handlersInstalled;

		private static void InstallQuietHandlers()
		{
			if (Interlocked.Exchange(ref _handlersInstalled, 1) != 0)
				return;
			XSetErrorHandler(&OnProtocolError);
			XSetIOErrorHandler(&OnIoError);
		}

		/// <summary>
		/// Replaces Xlib's exit() after an I/O error, where libX11 offers it (1.7 and later). Without it a lost
		/// connection still ends the process, as it does for every Xlib client; with it the clipboard is marked broken.
		/// </summary>
		private void QuietIoExit(nint display)
		{
			_current = this;
			try
			{
				if (NativeLibrary.TryLoad(LibX11, out var lib)
					&& NativeLibrary.TryGetExport(lib, "XSetIOErrorExitHandler", out var export))
				{
					var setExitHandler = (delegate* unmanaged<nint, delegate* unmanaged<nint, nint, void>, nint, void>)export;
					setExitHandler(display, &OnIoExit, nint.Zero);
				}
			}
			catch
			{
			}
		}

		// A PROTOCOL ERROR — typically a requestor window that vanished before its answer was written — is ignored:
		// the default handler would print it over the UI and exit.
		[UnmanagedCallersOnly]
		private static int OnProtocolError(nint display, nint error) => 0;

		[UnmanagedCallersOnly]
		private static int OnIoError(nint display)
		{
			if (_current is { } clipboard)
				clipboard._broken = true;
			return 0;
		}

		[UnmanagedCallersOnly]
		private static void OnIoExit(nint display, nint userData)
		{
			if (_current is { } clipboard)
				clipboard._broken = true;
		}

		#endregion

		#region Native

		[StructLayout(LayoutKind.Sequential, Size = 24 * 8)]
		private struct XEvent
		{
			public int Type;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct XSelectionRequestEvent
		{
			public int Type;
			public nuint Serial;
			public int SendEvent;
			public nint Display;
			public nint Owner;
			public nint Requestor;
			public nint Selection;
			public nint Target;
			public nint Property;
			public nint Time;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct XSelectionEvent
		{
			public int Type;
			public nuint Serial;
			public int SendEvent;
			public nint Display;
			public nint Requestor;
			public nint Selection;
			public nint Target;
			public nint Property;
			public nint Time;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct XSelectionClearEvent
		{
			public int Type;
			public nuint Serial;
			public int SendEvent;
			public nint Display;
			public nint Window;
			public nint Selection;
			public nint Time;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct XPropertyEvent
		{
			public int Type;
			public nuint Serial;
			public int SendEvent;
			public nint Display;
			public nint Window;
			public nint Atom;
			public nint Time;
			public int State;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct PollFd
		{
			public int Fd;
			public short Events;
			public short Revents;
		}

		[DllImport(LibX11)] private static extern nint XOpenDisplay(nint name);
		[DllImport(LibX11)] private static extern int XCloseDisplay(nint display);
		[DllImport(LibX11)] private static extern nint XDefaultRootWindow(nint display);
		[DllImport(LibX11)] private static extern nint XCreateSimpleWindow(nint display, nint parent, int x, int y, uint width, uint height, uint borderWidth, nuint border, nuint background);
		[DllImport(LibX11)] private static extern int XDestroyWindow(nint display, nint window);
		[DllImport(LibX11)] private static extern int XSelectInput(nint display, nint window, nint mask);
		[DllImport(LibX11)] private static extern nint XInternAtom(nint display, byte* name, int onlyIfExists);
		[DllImport(LibX11)] private static extern int XSetSelectionOwner(nint display, nint selection, nint owner, nint time);
		[DllImport(LibX11)] private static extern nint XGetSelectionOwner(nint display, nint selection);
		[DllImport(LibX11)] private static extern int XConvertSelection(nint display, nint selection, nint target, nint property, nint requestor, nint time);
		[DllImport(LibX11)] private static extern int XChangeProperty(nint display, nint window, nint property, nint type, int format, int mode, byte* data, int count);
		[DllImport(LibX11)] private static extern int XDeleteProperty(nint display, nint window, nint property);
		[DllImport(LibX11)] private static extern int XGetWindowProperty(nint display, nint window, nint property, nint offset, nint length, int delete, nint requestedType, nint* actualType, int* actualFormat, nuint* count, nuint* bytesAfter, byte** data);
		[DllImport(LibX11)] private static extern int XFree(nint data);
		[DllImport(LibX11)] private static extern int XSendEvent(nint display, nint window, int propagate, nint mask, void* ev);
		[DllImport(LibX11)] private static extern int XPending(nint display);
		[DllImport(LibX11)] private static extern int XNextEvent(nint display, void* ev);
		[DllImport(LibX11)] private static extern int XFlush(nint display);
		[DllImport(LibX11)] private static extern int XConnectionNumber(nint display);
		[DllImport(LibX11)] private static extern nint XMaxRequestSize(nint display);
		[DllImport(LibX11)] private static extern nint XExtendedMaxRequestSize(nint display);
		[DllImport(LibX11)] private static extern nint XSetErrorHandler(delegate* unmanaged<nint, nint, int> handler);
		[DllImport(LibX11)] private static extern nint XSetIOErrorHandler(delegate* unmanaged<nint, int> handler);

		[DllImport(LibC)] private static extern int pipe(int* fds);
		[DllImport(LibC)] private static extern int poll(PollFd* fds, nuint count, int timeoutMs);
		[DllImport(LibC)] private static extern nint read(int fd, byte* buffer, nint count);
		[DllImport(LibC)] private static extern nint write(int fd, byte* buffer, nint count);
		[DllImport(LibC)] private static extern int close(int fd);

		#endregion
	}
}
