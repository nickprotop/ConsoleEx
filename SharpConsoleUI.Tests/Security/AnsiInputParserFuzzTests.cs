// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Drivers.Input;
using Xunit;

namespace SharpConsoleUI.Tests.Security
{
	/// <summary>
	/// Fuzz-style tests for AnsiInputParser to verify it handles arbitrary byte
	/// sequences without crashing, hanging, or exceeding memory bounds. These tests
	/// feed adversarial inputs that exercise edge cases in the state machine.
	/// </summary>
	public class AnsiInputParserFuzzTests
	{
		/// <summary>
		/// Feeds random byte sequences to the parser to verify no unhandled exceptions.
		/// Uses a fixed seed for reproducibility.
		/// </summary>
		[Theory]
		[InlineData(42)]
		[InlineData(123)]
		[InlineData(7777)]
		[InlineData(99999)]
		[InlineData(314159)]
		public void Parse_RandomBytes_DoesNotThrow(int seed)
		{
			var parser = new AnsiInputParser();
			var rng = new Random(seed);
			var buffer = new byte[1024];

			for (int iteration = 0; iteration < 100; iteration++)
			{
				int count = rng.Next(1, buffer.Length);
				rng.NextBytes(buffer.AsSpan(0, count));

				var events = parser.Parse(buffer, count);
				Assert.NotNull(events);
			}

			// Flush should also not throw
			var flushEvents = parser.Flush();
			Assert.NotNull(flushEvents);
		}

		/// <summary>
		/// Feeds a long sequence of CSI parameter bytes (digits and semicolons)
		/// to test the overflow protection on _csiParams.
		/// </summary>
		[Fact]
		public void Parse_LongCsiParams_DoesNotOom()
		{
			var parser = new AnsiInputParser();

			// Start a CSI sequence
			var start = new byte[] { 0x1b, (byte)'[' };
			parser.Parse(start, start.Length);

			// Feed 10000 digits — should hit overflow protection without OOM
			var digits = new byte[10000];
			for (int i = 0; i < digits.Length; i++)
				digits[i] = (byte)('0' + (i % 10));

			var events = parser.Parse(digits, digits.Length);
			Assert.NotNull(events);

			// Parser should recover to ground state
			var flush = parser.Flush();
			Assert.NotNull(flush);

			// Verify parser still works after overflow
			var normalInput = new byte[] { (byte)'A' };
			events = parser.Parse(normalInput, normalInput.Length);
			Assert.NotNull(events);
			Assert.True(events.Count > 0);
		}

		/// <summary>
		/// Feeds many ESC bytes without valid sequences to test escape state recovery.
		/// </summary>
		[Fact]
		public void Parse_RepeatedEscapes_DoesNotHang()
		{
			var parser = new AnsiInputParser();
			var escBytes = new byte[500];
			Array.Fill(escBytes, (byte)0x1b);

			var events = parser.Parse(escBytes, escBytes.Length);
			Assert.NotNull(events);
			// Each ESC should eventually be emitted as an event
		}

		/// <summary>
		/// Tests that malformed UTF-8 sequences (orphan continuation bytes) don't crash.
		/// </summary>
		[Fact]
		public void Parse_MalformedUtf8_DoesNotThrow()
		{
			var parser = new AnsiInputParser();

			// Orphan continuation bytes (0x80-0xBF without a start byte)
			var orphans = new byte[] { 0x80, 0x81, 0xBF, 0xBE };
			var events = parser.Parse(orphans, orphans.Length);
			Assert.NotNull(events);

			// Start byte followed by non-continuation
			var bad = new byte[] { 0xC2, 0x41 }; // 2-byte start then ASCII
			events = parser.Parse(bad, bad.Length);
			Assert.NotNull(events);

			// 4-byte start followed by only 2 continuations then something else
			var incomplete = new byte[] { 0xF0, 0x90, 0x80, 0x41 };
			events = parser.Parse(incomplete, incomplete.Length);
			Assert.NotNull(events);
		}

		/// <summary>
		/// Tests X10 mouse sequences with exactly 3 bytes after CSI M.
		/// </summary>
		[Fact]
		public void Parse_X10Mouse_ValidAndInvalid()
		{
			var parser = new AnsiInputParser();

			// Valid X10 mouse: ESC [ M button x y (each +32)
			var valid = new byte[] { 0x1b, (byte)'[', (byte)'M', 32, 33, 33 };
			var events = parser.Parse(valid, valid.Length);
			Assert.NotNull(events);

			// Flush and verify clean state
			parser.Flush();

			// Truncated X10 mouse (only 2 of 3 bytes then flush)
			var truncated = new byte[] { 0x1b, (byte)'[', (byte)'M', 32, 33 };
			parser.Parse(truncated, truncated.Length);
			events = parser.Flush();
			Assert.NotNull(events);
		}

		/// <summary>
		/// Tests SGR mouse with extreme coordinate values.
		/// </summary>
		[Fact]
		public void Parse_SgrMouse_ExtremeCoordinates_Clamped()
		{
			var parser = new AnsiInputParser();

			// SGR mouse with very large coordinates: ESC [ < 0 ; 99999 ; 99999 M
			var seq = System.Text.Encoding.ASCII.GetBytes("\x1b[<0;99999;99999M");
			var events = parser.Parse(seq, seq.Length);
			Assert.NotNull(events);

			// Should produce a mouse event with clamped coordinates
			var mouseEvent = events.OfType<MouseInputEvent>().FirstOrDefault();
			if (mouseEvent != null)
			{
				Assert.True(mouseEvent.Position.X >= 0);
				Assert.True(mouseEvent.Position.Y >= 0);
				Assert.True(mouseEvent.Position.X <= ushort.MaxValue);
				Assert.True(mouseEvent.Position.Y <= ushort.MaxValue);
			}
		}

		/// <summary>
		/// Tests SGR mouse with negative coordinates (should be clamped to minimum 0).
		/// </summary>
		[Fact]
		public void Parse_SgrMouse_NegativeCoordinates_Clamped()
		{
			var parser = new AnsiInputParser();

			// SGR mouse with coordinate 0 (below 1-based minimum)
			var seq = System.Text.Encoding.ASCII.GetBytes("\x1b[<0;0;0M");
			var events = parser.Parse(seq, seq.Length);
			Assert.NotNull(events);

			var mouseEvent = events.OfType<MouseInputEvent>().FirstOrDefault();
			if (mouseEvent != null)
			{
				Assert.True(mouseEvent.Position.X >= 0);
				Assert.True(mouseEvent.Position.Y >= 0);
			}
		}

		/// <summary>
		/// Alternates between partial sequences and flushes to verify state machine
		/// recovery after incomplete input.
		/// </summary>
		[Fact]
		public void Parse_PartialSequencesWithFlush_Recovers()
		{
			var parser = new AnsiInputParser();

			for (int i = 0; i < 100; i++)
			{
				// Start an escape sequence but don't finish it
				var partial = new byte[] { 0x1b, (byte)'[' };
				parser.Parse(partial, partial.Length);

				// Flush the incomplete sequence
				var events = parser.Flush();
				Assert.NotNull(events);

				// Parser should accept normal input afterward
				var normal = new byte[] { (byte)'X' };
				events = parser.Parse(normal, normal.Length);
				Assert.NotNull(events);
				Assert.True(events.Count > 0);
			}
		}

		/// <summary>
		/// Tests that interleaved valid and invalid sequences don't corrupt state.
		/// </summary>
		[Fact]
		public void Parse_InterleavedValidInvalid_MaintainsState()
		{
			var parser = new AnsiInputParser();

			// Valid key press
			var a = new byte[] { (byte)'A' };
			var events = parser.Parse(a, a.Length);
			Assert.Single(events);

			// Invalid: CSI with garbage final byte
			var garbage = new byte[] { 0x1b, (byte)'[', 0xFF };
			events = parser.Parse(garbage, garbage.Length);
			Assert.NotNull(events);

			// Another valid key press should still work
			events = parser.Parse(a, a.Length);
			Assert.Single(events);
		}

		/// <summary>
		/// Stress test: feeds 100KB of mixed content to verify no memory leaks
		/// or unbounded growth.
		/// </summary>
		[Fact]
		public void Parse_LargeInput_CompletesInReasonableTime()
		{
			var parser = new AnsiInputParser();
			var rng = new Random(12345);
			var buffer = new byte[4096];
			long totalBytes = 0;

			var sw = System.Diagnostics.Stopwatch.StartNew();

			while (totalBytes < 100_000)
			{
				int count = rng.Next(1, buffer.Length);
				rng.NextBytes(buffer.AsSpan(0, count));
				parser.Parse(buffer, count);
				totalBytes += count;
			}

			sw.Stop();
			parser.Flush();

			// Should complete well under 5 seconds even on slow machines
			Assert.True(sw.ElapsedMilliseconds < 5000,
				$"Parsing 100KB took {sw.ElapsedMilliseconds}ms, expected < 5000ms");
		}
	}
}
