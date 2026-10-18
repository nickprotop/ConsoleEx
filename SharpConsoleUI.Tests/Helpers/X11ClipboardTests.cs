using System.Diagnostics;
using SharpConsoleUI.Helpers;
using Xunit;

namespace SharpConsoleUI.Tests.Helpers;

/// <summary>
/// The X11 clipboard over libX11, between two real connections to the running display.
/// </summary>
/// <remarks>
/// <para>A PRIVATE SELECTION PER TEST, never CLIPBOARD: the protocol is the same, and running the suite does not
/// replace what the person running it had copied.</para>
///
/// <para>Each test does nothing without a display and libX11 — a headless CI runner, macOS, Windows.</para>
/// </remarks>
[Collection("EnvSerial")]
public class X11ClipboardTests
{
	/// <summary>
	/// Whether a real display answers: probed ONCE by opening a private selection, because DISPLAY being set is not
	/// the same as libX11 being installed or the server accepting us (a stale DISPLAY, a missing xauth cookie).
	/// </summary>
	internal static readonly bool DisplayAvailable = ProbeDisplay();

	private static bool ProbeDisplay()
	{
		try
		{
			using var probe = X11Clipboard.TryOpen("SHARPCONSOLEUI_PROBE_" + Guid.NewGuid().ToString("N"));
			return probe is not null;
		}
		catch
		{
			return false;
		}
	}

	private static string NewSelection() => "SHARPCONSOLEUI_TEST_" + Guid.NewGuid().ToString("N");

	private static bool TryOpenPair(out X11Clipboard owner, out X11Clipboard reader)
	{
		var selection = NewSelection();
		owner = X11Clipboard.TryOpen(selection)!;
		reader = owner is null ? null! : X11Clipboard.TryOpen(selection)!;
		if (owner is null || reader is null)
		{
			owner?.Dispose();
			return false;
		}
		return true;
	}

	/// <summary>
	/// SETTING IS POSTED, so a read straight after may reach the server before the ownership did. Waits for the reader
	/// to see <paramref name="expected"/>, and answers what it last saw.
	/// </summary>
	private static string? ReadUntil(X11Clipboard reader, string expected)
	{
		string? seen = null;
		var deadline = DateTime.UtcNow.AddSeconds(5);
		while (DateTime.UtcNow < deadline)
		{
			seen = reader.GetText(2000);
			if (seen == expected)
				break;
			Thread.Sleep(20);
		}
		return seen;
	}

	[X11Fact]
	public void ACopy_IsReadByAnotherClient_AsTyped()
	{
		if (!TryOpenPair(out var owner, out var reader))
			return;
		using (owner)
		using (reader)
		{
			const string text = "copied — Ωμέγα 日本語 🎉\nsecond line";
			owner.SetText(text);

			Assert.Equal(text, ReadUntil(reader, text));
		}
	}

	[X11Fact]
	public void TheOwner_ReadsItsOwnCopy()
	{
		if (!TryOpenPair(out var owner, out var reader))
			return;
		using (owner)
		using (reader)
		{
			owner.SetText("mine");
			Assert.Equal("mine", ReadUntil(owner, "mine"));
		}
	}

	/// <summary>ANOTHER CLIENT'S COPY REPLACES OURS: a paste here then gets theirs, not the text we last served.</summary>
	[X11Fact]
	public void AnotherClientsCopy_ReplacesOurs()
	{
		if (!TryOpenPair(out var first, out var second))
			return;
		using (first)
		using (second)
		{
			first.SetText("first");
			Assert.Equal("first", ReadUntil(second, "first"));

			second.SetText("second");
			Assert.Equal("second", ReadUntil(first, "second"));
		}
	}

	/// <summary>
	/// A COPY LARGER THAN ONE PROPERTY CROSSES IN PIECES (INCR), both ways — a requestor may refuse a single property
	/// that size, as Tk does with "selection property too large".
	/// </summary>
	[X11Fact]
	public void ALargeCopy_CrossesInPieces_Whole()
	{
		if (!TryOpenPair(out var owner, out var reader))
			return;
		using (owner)
		using (reader)
		{
			var text = "START" + string.Concat(Enumerable.Range(0, 300_000).Select(i => (char)('α' + i % 24))) + "END";
			owner.SetText(text);

			var read = ReadUntil(reader, text);
			Assert.NotNull(read);
			Assert.Equal(text.Length, read!.Length);
			Assert.Equal(text, read);
			Assert.Equal(1, owner.IncrementalSends);
		}
	}

	[X11Fact]
	public void ASelectionNobodyOwns_ReadsEmpty()
	{
		var reader = X11Clipboard.TryOpen(NewSelection());
		if (reader is null)
			return;
		using (reader)
			Assert.Equal(string.Empty, reader.GetText(2000));
	}

	/// <summary>
	/// A PASTE THAT GAVE UP MUST NOT COST THE NEXT ONE. An X11 read is a round trip to whichever application
	/// owns the clipboard, and a cold browser or editor can take longer to answer than a paste waits. The first
	/// Ctrl+V then returned nothing — and so did the second, because it queued behind a conversion that was
	/// still outstanding, waiting out a whole further round trip. A caller arriving while a read is in flight
	/// now joins it: the clipboard is one value, so the answer already being fetched is its answer too.
	/// </summary>
	[X11Fact]
	public void APasteDuringASlowOne_LandsWithIt_NotAfterASecondRoundTrip()
	{
		if (!TryOpenPair(out var owner, out var reader))
			return;
		using (owner)
		using (reader)
		{
			owner.SetText("from a slow owner");
			owner.DelayAnswersForTest(700);

			// The first paste gives up: the owner is slower than it waits.
			Assert.Null(reader.GetText(150));

			// The second, with the owner STILL slow, must land when that one answer arrives — the remainder of
			// the 700ms, not another 700 on top. Queueing instead of joining blew well past this bound.
			var clock = Stopwatch.StartNew();
			var text = reader.GetText(900);
			clock.Stop();
			owner.DelayAnswersForTest(0);

			Assert.Equal("from a slow owner", text);
			Assert.True(clock.ElapsedMilliseconds < 700,
				$"took {clock.ElapsedMilliseconds}ms, so it waited for a second conversion instead of joining the first");
		}
	}

	/// <summary>Several pastes during one slow answer collapse onto it, rather than serialising behind it.</summary>
	[X11Fact]
	public void SeveralPastesDuringOneSlowAnswer_AllGetIt()
	{
		if (!TryOpenPair(out var owner, out var reader))
			return;
		using (owner)
		using (reader)
		{
			owner.SetText("shared answer");
			owner.DelayAnswersForTest(600);

			var readers = new Task<string?>[4];
			for (int i = 0; i < readers.Length; i++)
				readers[i] = Task.Run(() => reader.GetText(2000));

			var clock = Stopwatch.StartNew();
			Task.WaitAll(readers);
			clock.Stop();
			owner.DelayAnswersForTest(0);

			Assert.All(readers, r => Assert.Equal("shared answer", r.Result));

			// Four separate conversions at 600ms each would be about 2.4s; one shared is about 600ms.
			Assert.True(clock.ElapsedMilliseconds < 1400,
				$"four pastes took {clock.ElapsedMilliseconds}ms, so they did not share one round trip");
		}
	}

	[Fact]
	public void WithoutADisplay_NothingOpens()
	{
		var saved = Environment.GetEnvironmentVariable("DISPLAY");
		try
		{
			Environment.SetEnvironmentVariable("DISPLAY", null);
			Assert.Null(X11Clipboard.TryOpen(NewSelection()));
		}
		finally
		{
			Environment.SetEnvironmentVariable("DISPLAY", saved);
		}
	}
}

/// <summary>
/// A fact that runs only where libX11 can open the display named by <c>DISPLAY</c>: an X11 desktop, or Xwayland
/// under a Wayland session. Elsewhere — a headless CI runner, macOS, Windows — it reports as SKIPPED.
/// </summary>
/// <remarks>
/// Skipped, not quietly returned. These tests spoke to a real display or did nothing at all, and doing nothing
/// still counted as a pass, so the suite looked green on CI while asserting none of the protocol. A skip says
/// which runs covered it and which did not.
/// </remarks>
internal sealed class X11FactAttribute : Xunit.FactAttribute
{
	public X11FactAttribute()
	{
		if (!X11ClipboardTests.DisplayAvailable)
			Skip = "requires an X11 display (DISPLAY, or Xwayland under Wayland) with libX11";
	}
}
