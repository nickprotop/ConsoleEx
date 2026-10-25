// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Drivers.Input;
using Xunit;
using Xunit.Abstractions;

namespace SharpConsoleUI.Tests.Drivers;

/// <summary>
/// Rapid clicking must keep producing presses.
/// </summary>
/// <remarks>
/// Reported from a game: the fire button "stalls" when you click fast and works again
/// when you move the mouse. These drive the real Unix decoder with real SGR bytes,
/// because the bug lives below the flag level - a test that hands controls
/// hand-built flags cannot see it.
/// </remarks>
public class UnixRapidClickTests
{
	private readonly ITestOutputHelper _out;

	public UnixRapidClickTests(ITestOutputHelper outHelper) => _out = outHelper;

	/// <summary>Feeds a scripted sequence of byte chunks, pausing between them.</summary>
	private sealed class PacedStream : Stream
	{
		private readonly (string Seq, int Delay)[] _script;
		private int _index;

		public PacedStream((string, int)[] script) => _script = script;

		public override int Read(byte[] buffer, int offset, int count)
		{
			if (_index >= _script.Length)
			{
				Thread.Sleep(20);
				return 0;
			}

			var (seq, delay) = _script[_index++];
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

	/// <summary>Clicks <paramref name="clicks"/> times at one spot and returns what was dispatched.</summary>
	private List<List<MouseFlags>> Click(int clicks, int gapMs)
	{
		var script = new List<(string, int)>();
		for (int i = 0; i < clicks; i++)
		{
			script.Add(("\u001b[<0;10;5M", i == 0 ? 0 : gapMs));   // press
			script.Add(("\u001b[<0;10;5m", 5));                     // release
		}

		var reader = new UnixStdinReader(new PacedStream(script.ToArray()), new AnsiInputParser());
		using var cts = new CancellationTokenSource(
			TimeSpan.FromMilliseconds(1000 + clicks * (gapMs + 40)));

		var dispatched = new List<List<MouseFlags>>();
		try
		{
			reader.ReadLoop(cts.Token,
				_ => { },
				(flags, _) => { lock (dispatched) dispatched.Add(new List<MouseFlags>(flags)); },
				_ => { },
				(_, _) => { });
		}
		catch (OperationCanceledException)
		{
			// The reader runs until the token trips; that is how the script ends.
		}

		lock (dispatched)
		{
			foreach (var d in dispatched) _out.WriteLine(string.Join("|", d));
			return new List<List<MouseFlags>>(dispatched);
		}
	}

	private static int Presses(List<List<MouseFlags>> dispatched) => dispatched.Count(
		f => f.Contains(MouseFlags.Button1Pressed)
			|| f.Contains(MouseFlags.Button1DoubleClicked)
			|| f.Contains(MouseFlags.Button1TripleClicked));

	[Fact]
	public void SlowClicking_ProducesAPressPerClick()
	{
		// The control case: well outside the 300ms click window, every click is
		// a clean press/release pair.
		var dispatched = Click(clicks: 4, gapMs: 400);

		Assert.Equal(4, Presses(dispatched));
	}

	[Fact]
	public void RapidClicking_StillProducesAPressPerClick()
	{
		// The reported bug. Inside the click window the decoder reports the second
		// press as DoubleClicked and the third as TripleClicked - which is fine, as
		// long as one of them arrives per click and the sequence does not stall.
		var dispatched = Click(clicks: 6, gapMs: 30);

		Assert.Equal(6, Presses(dispatched));
	}

	[Fact]
	public void RapidClicking_DoesNotStallPartwayThrough()
	{
		// The specific failure: it works for the first few and then stops, because
		// a suppressed release leaves the press state set and every later press is
		// rejected by the "no button currently down" guard.
		var dispatched = Click(clicks: 10, gapMs: 25);

		Assert.Equal(10, Presses(dispatched));
	}
}
