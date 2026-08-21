// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpConsoleUI.Core;
using Xunit;

namespace SharpConsoleUI.Tests.Core;

/// <summary>
/// Covers <see cref="PipedInputCapture"/> — reading piped stdin without blocking startup.
/// </summary>
/// <remarks>
/// The bug this replaces: <c>ConsoleWindowSystem</c>'s constructor called
/// <c>Console.In.ReadToEnd()</c>, which never returns while a writer holds stdin open. Any app
/// spawned with an open stdin pipe (agent harnesses, supervisors, <c>tail -f</c> producers) hung
/// before drawing a single frame. These tests pin the reader that fixes it.
/// </remarks>
[Collection("TimingSensitive")]
public class PipedInputCaptureTests
{
	/// <summary>A reader whose end-of-input the test controls, standing in for a held-open pipe.</summary>
	private sealed class BlockingReader : TextReader
	{
		private readonly SemaphoreSlim _available = new(0);
		private readonly System.Collections.Concurrent.ConcurrentQueue<string> _chunks = new();
		private volatile bool _ended;

		public void Push(string text) { _chunks.Enqueue(text); _available.Release(); }

		/// <summary>Signals end-of-input, the thing a held-open pipe never does.</summary>
		public void End() { _ended = true; _available.Release(); }

		public override int Read(char[] buffer, int index, int count)
		{
			while (true)
			{
				if (_chunks.TryDequeue(out var chunk))
				{
					int n = Math.Min(count, chunk.Length);
					chunk.CopyTo(0, buffer, index, n);
					if (n < chunk.Length) _chunks.Enqueue(chunk.Substring(n));
					return n;
				}

				if (_ended) return 0;
				_available.Wait();
			}
		}
	}

	[Fact]
	public void Completes_WithFullText_WhenInputEnds()
	{
		var reader = new BlockingReader();
		var capture = new PipedInputCapture(reader);

		reader.Push("alpha\nbeta\n");
		reader.End();

		Assert.True(capture.Completion.Wait(TimeSpan.FromSeconds(5)), "capture did not complete");
		Assert.Equal("alpha\nbeta\n", capture.Completion.Result);
	}

	[Fact]
	public void ReassemblesText_SplitAcrossManyChunks()
	{
		var reader = new BlockingReader();
		var capture = new PipedInputCapture(reader);

		for (int i = 0; i < 50; i++) reader.Push($"line{i}\n");
		reader.End();

		Assert.True(capture.Completion.Wait(TimeSpan.FromSeconds(5)));
		Assert.Equal(50, capture.Completion.Result.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
		Assert.StartsWith("line0\n", capture.Completion.Result);
		Assert.EndsWith("line49\n", capture.Completion.Result);
	}

	[Fact]
	public void ReadsLargerThanOneChunk()
	{
		// The reader loops in fixed-size chunks; a payload well past one must survive intact.
		var payload = new string('x', 40_000);
		var reader = new BlockingReader();
		var capture = new PipedInputCapture(reader);

		reader.Push(payload);
		reader.End();

		Assert.True(capture.Completion.Wait(TimeSpan.FromSeconds(5)));
		Assert.Equal(payload.Length, capture.Completion.Result.Length);
		Assert.Equal(payload, capture.Completion.Result);
	}

	[Fact]
	public void DoesNotComplete_WhileInputStaysOpen()
	{
		// The defining property: a writer that never closes must not produce completion...
		var reader = new BlockingReader();
		var capture = new PipedInputCapture(reader);
		reader.Push("partial");

		Assert.False(capture.Completion.Wait(TimeSpan.FromMilliseconds(300)));
		Assert.False(capture.IsCompleted);

		// ...and must not lose what already arrived.
		Assert.Equal("partial", capture.PartialText);
	}

	[Fact]
	public void WaitForText_ReturnsPartialText_OnTimeout_RatherThanBlocking()
	{
		var reader = new BlockingReader();
		var capture = new PipedInputCapture(reader);
		reader.Push("half a line");

		// Give the reader a moment to drain the chunk, then bound the wait.
		Assert.True(SpinUntil(() => capture.PartialText.Length > 0, 2000), "text never arrived");

		var sw = Stopwatch.StartNew();
		string text = capture.WaitForText(200);
		sw.Stop();

		Assert.Equal("half a line", text);
		Assert.False(capture.IsCompleted);
		Assert.True(sw.ElapsedMilliseconds < 2000, $"WaitForText blocked for {sw.ElapsedMilliseconds}ms");
	}

	[Fact]
	public void WaitForText_ReturnsImmediately_WithZeroTimeout()
	{
		var reader = new BlockingReader();
		var capture = new PipedInputCapture(reader);

		var sw = Stopwatch.StartNew();
		capture.WaitForText(0);
		sw.Stop();

		Assert.True(sw.ElapsedMilliseconds < 1000, $"zero-timeout wait took {sw.ElapsedMilliseconds}ms");
	}

	[Fact]
	public void WaitForText_ReturnsFullText_WhenInputEndsWithinTheTimeout()
	{
		var reader = new BlockingReader();
		var capture = new PipedInputCapture(reader);

		reader.Push("done\n");
		reader.End();

		Assert.Equal("done\n", capture.WaitForText(5000));
		Assert.True(capture.IsCompleted);
	}

	[Fact]
	public void CompletesWithWhatArrived_WhenTheReaderThrows()
	{
		// A failed read is not a crash: piped input is best-effort, and bytes already read are valid.
		var capture = new PipedInputCapture(new ThrowingReader("kaboom"));

		Assert.True(capture.Completion.Wait(TimeSpan.FromSeconds(5)), "capture did not settle");
		Assert.Equal("before", capture.Completion.Result);
	}

	/// <summary>Yields some text, then fails — a pipe closing mid-read.</summary>
	private sealed class ThrowingReader : TextReader
	{
		private readonly string _message;
		private bool _yielded;

		public ThrowingReader(string message) => _message = message;

		public override int Read(char[] buffer, int index, int count)
		{
			if (!_yielded)
			{
				_yielded = true;
				const string text = "before";
				text.CopyTo(0, buffer, index, text.Length);
				return text.Length;
			}

			throw new IOException(_message);
		}
	}

	private static bool SpinUntil(Func<bool> condition, int timeoutMs)
	{
		var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
		while (DateTime.UtcNow < deadline)
		{
			if (condition()) return true;
			Thread.Sleep(20);
		}
		return condition();
	}
}
