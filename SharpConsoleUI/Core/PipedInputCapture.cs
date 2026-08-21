// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text;

namespace SharpConsoleUI.Core
{
	/// <summary>
	/// Reads text piped into the application via stdin, off the startup path.
	/// </summary>
	/// <remarks>
	/// Redirected stdin has no single correct deadline: a finite pipe (<c>echo x | app</c>) ends at
	/// once, while a live producer (<c>tail -f log | app</c>) never does. Reading it inline would
	/// therefore block construction for an unbounded time — before the application can draw anything —
	/// so the read runs on a background thread and callers wait on it only as long as they choose.
	///
	/// <para>The text accumulates incrementally rather than in one <c>ReadToEnd</c>, so a caller that
	/// gives up waiting still receives everything that arrived before the deadline.</para>
	/// </remarks>
	internal sealed class PipedInputCapture
	{
		/// <summary>Read granularity. Large enough to be cheap, small enough to publish promptly.</summary>
		private const int ReadChunkSize = 4096;

		private readonly TaskCompletionSource<string> _completion =
			new(TaskCreationOptions.RunContinuationsAsynchronously);

		private readonly StringBuilder _buffer = new();
		private readonly object _sync = new();

		/// <summary>Starts a capture reading from <paramref name="reader"/> on a background thread.</summary>
		/// <param name="reader">The stdin reader. Owned by the caller.</param>
		public PipedInputCapture(System.IO.TextReader reader)
		{
			// A dedicated background thread, not the thread pool: the read blocks for an unbounded
			// time, which is exactly what a pool thread must not do. Background so it can never keep
			// the process alive after the application exits.
			var thread = new Thread(() => ReadLoop(reader))
			{
				IsBackground = true,
				Name = "SharpConsoleUI.PipedInput"
			};
			thread.Start();
		}

		/// <summary>Completes with the full text once stdin reaches end-of-input.</summary>
		public Task<string> Completion => _completion.Task;

		/// <summary>Whether the capture has finished (reached end-of-input or failed).</summary>
		public bool IsCompleted => _completion.Task.IsCompleted;

		/// <summary>
		/// The text received so far. Safe to read at any time and from any thread; returns the complete
		/// text once <see cref="Completion"/> has finished.
		/// </summary>
		public string PartialText
		{
			get { lock (_sync) return _buffer.ToString(); }
		}

		/// <summary>
		/// Waits up to <paramref name="timeoutMs"/> for the capture to finish, then returns the text —
		/// complete if it finished in time, partial if it did not.
		/// </summary>
		/// <param name="timeoutMs">
		/// Milliseconds to wait. Zero returns immediately with whatever has arrived;
		/// <see cref="Timeout.Infinite"/> waits for end-of-input.
		/// </param>
		public string WaitForText(int timeoutMs)
		{
			if (timeoutMs != 0)
			{
				// Wait() on the task rather than the thread: it observes completion without
				// rethrowing, and a faulted read is reported as the text captured before it failed.
				try { _completion.Task.Wait(timeoutMs); }
				catch (AggregateException) { /* read failed — fall through to the partial text */ }
			}

			return PartialText;
		}

		private void ReadLoop(System.IO.TextReader reader)
		{
			var chunk = new char[ReadChunkSize];

			try
			{
				while (true)
				{
					int read = reader.Read(chunk, 0, chunk.Length);
					if (read <= 0) break; // end of input

					lock (_sync) _buffer.Append(chunk, 0, read);
				}

				_completion.TrySetResult(PartialText);
			}
			catch (Exception)
			{
				// stdin read failed (closed handle, encoding error). Whatever arrived before the
				// failure is still valid input, so complete with it rather than faulting: callers
				// treat piped input as best-effort, and the historical behaviour swallowed errors too.
				_completion.TrySetResult(PartialText);
			}
		}
	}
}
