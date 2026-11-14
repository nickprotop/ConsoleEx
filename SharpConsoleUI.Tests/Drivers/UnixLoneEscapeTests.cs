// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using SharpConsoleUI.Drivers.Input;
using Xunit;
using Xunit.Abstractions;

namespace SharpConsoleUI.Tests.Drivers;

/// <summary>
/// Every press of a lone Escape must be delivered on its own, promptly.
/// </summary>
/// <remarks>
/// Found from cxlog: after any Escape, the NEXT Escape press was not delivered until a third key
/// arrived, so "Esc to clear, Esc to exit" needed three presses. The first lone ESC goes through the
/// 50ms timeout and is flushed; the ReadAsync left pending by that timeout then captures the second
/// ESC, which was parsed into the parser's pending state without its own timeout and sat there until
/// more input came. These drive the real Unix reader with real bytes, paced like a typist.
/// </remarks>
public class UnixLoneEscapeTests
{
	private readonly ITestOutputHelper _out;

	public UnixLoneEscapeTests(ITestOutputHelper outHelper) => _out = outHelper;

	/// <summary>
	/// Feeds byte chunks after delays, then BLOCKS (like a terminal waiting for the next key)
	/// until the test cancels - so nothing is flushed by an end of stream.
	/// </summary>
	private sealed class TypistStream : Stream
	{
		private readonly (string Seq, int DelayMs)[] _script;
		private readonly CancellationToken _ct;
		private int _index;

		public TypistStream((string, int)[] script, CancellationToken ct)
		{
			_script = script;
			_ct = ct;
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			int i = Interlocked.Increment(ref _index) - 1;
			if (i >= _script.Length)
			{
				_ct.WaitHandle.WaitOne();
				return 0;
			}

			var (seq, delay) = _script[i];
			if (delay > 0) Thread.Sleep(delay);
			byte[] bytes = System.Text.Encoding.ASCII.GetBytes(seq);
			Array.Copy(bytes, 0, buffer, offset, bytes.Length);
			return bytes.Length;
		}

		public override bool CanRead => true;
		public override bool CanSeek => false;
		public override bool CanWrite => false;
		public override long Length => 0;
		public override long Position { get => 0; set { } }
		public override void Flush() { }
		public override long Seek(long offset, SeekOrigin origin) => 0;
		public override void SetLength(long value) { }
		public override void Write(byte[] buffer, int offset, int count) { }
	}

	/// <summary>Runs the reader over the script and returns each key with the time it was dispatched.</summary>
	private List<(ConsoleKeyInfo Key, long AtMs)> Run((string, int)[] script, int runMs)
	{
		using var cts = new CancellationTokenSource(runMs);
		var reader = new UnixStdinReader(new TypistStream(script, cts.Token), new AnsiInputParser());
		var sw = Stopwatch.StartNew();
		var keys = new List<(ConsoleKeyInfo, long)>();

		var thread = new Thread(() =>
		{
			try
			{
				reader.ReadLoop(cts.Token,
					k => { lock (keys) keys.Add((k, sw.ElapsedMilliseconds)); },
					(_, _) => { }, _ => { }, (_, _) => { });
			}
			catch (OperationCanceledException) { }
		}) { IsBackground = true };
		thread.Start();
		thread.Join(runMs + 2000);

		lock (keys)
		{
			foreach (var (k, at) in keys)
				_out.WriteLine($"{at,5}ms  {k.Key} mods={k.Modifiers}");
			return keys.ToList();
		}
	}

	[Fact]
	public void SecondLoneEscape_IsDelivered_WithoutWaitingForAnotherKey()
	{
		// ESC at ~0ms, ESC at ~300ms, then nothing. Both must arrive, the second well before the run ends.
		var keys = Run(new[] { ("\u001b", 0), ("\u001b", 300) }, runMs: 1200);

		Assert.Equal(2, keys.Count(k => k.Key.Key == ConsoleKey.Escape && k.Key.Modifiers == 0));
		Assert.True(keys[1].AtMs < 800, $"second Escape arrived at {keys[1].AtMs}ms; expected ~350ms");
	}

	[Fact]
	public void RepeatedLoneEscapes_AreEachDeliveredBeforeTheNextPress()
	{
		// Four presses 250ms apart: each must be dispatched before the next one is typed.
		var script = new[] { ("\u001b", 0), ("\u001b", 250), ("\u001b", 250), ("\u001b", 250) };
		var keys = Run(script, runMs: 1800);

		var escapes = keys.Where(k => k.Key.Key == ConsoleKey.Escape && k.Key.Modifiers == 0).ToList();
		Assert.Equal(4, escapes.Count);
		for (int i = 0; i < escapes.Count; i++)
		{
			long typedAt = 250L * i;
			Assert.True(escapes[i].AtMs < typedAt + 200,
				$"Escape #{i + 1} typed at ~{typedAt}ms was dispatched at {escapes[i].AtMs}ms");
		}
	}

	[Fact]
	public void LoneEscape_ThenLetter_DeliversBothAsPlainKeys()
	{
		// Control case for the existing path: a timed-out ESC followed later by a letter.
		var keys = Run(new[] { ("\u001b", 0), ("a", 300) }, runMs: 1000);

		Assert.Equal(2, keys.Count);
		Assert.Equal(ConsoleKey.Escape, keys[0].Key.Key);
		Assert.Equal(0, (int)keys[0].Key.Modifiers);
		Assert.Equal('a', keys[1].Key.KeyChar);
		Assert.Equal(0, (int)(keys[1].Key.Modifiers & ConsoleModifiers.Alt));
	}

	[Fact]
	public void EscapeFollowedQuickly_IsStillAnAltChord()
	{
		// The timeout exists for this: ESC immediately followed by a key is Alt+key, not two keys.
		var keys = Run(new[] { ("\u001bx", 0) }, runMs: 600);

		Assert.Single(keys);
		Assert.Equal(ConsoleModifiers.Alt, keys[0].Key.Modifiers & ConsoleModifiers.Alt);
	}
}
