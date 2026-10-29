// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Drawing;
using System.Text;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Drivers.Input;
using SharpConsoleUI.Helpers;
using Xunit;

namespace SharpConsoleUI.Tests.Drivers;

/// <summary>
/// Both ways into the SGR decoder, held to the same answers.
/// </summary>
/// <remarks>
/// <para>
/// A terminal's mouse reports reach the decoder by two routes, chosen by
/// <c>_useDirectAnsi</c> - which is false on Windows and also on Unix when raw
/// mode cannot be entered, so neither route is one platform's alone. One reads
/// <c>/dev/tty</c> raw and hands <see cref="AnsiInputParser"/> the bytes; the
/// other reads through <c>Console.ReadKey</c>, reassembles the escape sequence
/// a character at a time, and hands the same parser those characters through
/// <see cref="SequenceHelper.GetMouse"/>.
/// </para>
/// <para>
/// One decoder, two ways of feeding it, so what these tests hold is the
/// bridge: that a sequence reassembled from <c>ConsoleKeyInfo</c> decodes to
/// what the same sequence read as bytes does. Both answer with one flag per
/// entry, since both are the parser's own answer.
/// </para>
/// <para>
/// Parity alone is not enough and must not be all this asserts. Two routes to
/// one decoder agree by construction, including when that decoder is wrong, so
/// <see cref="The_decoded_meaning_is_what_the_protocol_says"/> pins absolute
/// values: which buttons, which modifiers, where.
/// </para>
/// </remarks>
public class MouseDecoderParityTests
{
	/// <summary>Every SGR report worth holding both decoders to.</summary>
	/// <remarks>
	/// Button, modifier and wheel bits are independent in the protocol, so the
	/// corpus walks each axis rather than sampling combinations at random.
	/// </remarks>
	public static TheoryData<string, string> Corpus() => new()
	{
		// sequence,      what it means
		{ "<0;10;5M",   "left press" },
		{ "<0;10;5m",   "left release" },
		{ "<1;10;5M",   "middle press" },
		{ "<2;10;5M",   "right press" },
		{ "<2;10;5m",   "right release" },
		{ "<32;10;5M",  "left drag" },
		{ "<33;10;5M",  "middle drag" },
		{ "<34;10;5M",  "right drag" },
		{ "<35;10;5M",  "motion, no button" },
		{ "<64;10;5M",  "wheel up" },
		{ "<65;10;5M",  "wheel down" },
		{ "<4;10;5M",   "shift + left press" },
		{ "<8;10;5M",   "alt + left press" },
		{ "<16;10;5M",  "ctrl + left press" },
		{ "<28;10;5M",  "shift + alt + ctrl + left press" },
		{ "<0;1;1M",    "top-left corner" },
		{ "<0;200;60M", "far from the origin" },
	};

	[Theory]
	[MemberData(nameof(Corpus))]
	public void Both_decoders_read_the_same_buttons_and_modifiers(string sequence, string meaning)
	{
		var windows = Windows(sequence);
		var unix = Unix(sequence);

		// Guard: a corpus entry that decodes to nothing on BOTH sides would pass
		// this test by saying nothing at all.
		Assert.True(Bits(windows.Flags) != 0 || Bits(unix.Flags) != 0,
			$"{meaning} ({sequence}): neither decoder produced any flags");

		Assert.True(Same(windows.Flags, unix.Flags),
			$"{meaning} ({sequence}): windows read [{Show(windows.Flags)}], "
			+ $"unix read [{Show(unix.Flags)}]");
	}

	[Theory]
	[MemberData(nameof(Corpus))]
	public void Both_decoders_read_the_same_position(string sequence, string meaning)
	{
		// SGR is 1-based and the library is 0-based, so an off-by-one here lands
		// every click one cell up and left on one platform only.
		Assert.Equal(Unix(sequence).Position, Windows(sequence).Position);
	}

	[Fact]
	public void Neither_decoder_turns_a_drag_report_into_a_held_button()
	{
		// A motion report carries the held button beside ReportMousePosition, and
		// the recogniser deliberately does NOT take that as a press: doing so
		// moves the remembered press position to the pointer, and the release
		// then reads as a click at the end of a drag.
		//
		// The two paths used to disagree here by accident - Windows OR'd its
		// flags into one value that matched no button constant, Unix sent them
		// as a list that did - and the behaviour is now the same on both,
		// deliberately.
		const string drag = "<32;10;5M";

		var windows = new SharpConsoleUI.Input.MouseGestureRecognizer();
		var w = Windows(drag);
		windows.Recognize(w.Flags, w.Position);

		var unix = new SharpConsoleUI.Input.MouseGestureRecognizer();
		var u = Unix(drag);
		unix.Recognize(u.Flags, u.Position);

		Assert.False(unix.IsButtonDown, "a drag report is not a press");
		Assert.False(windows.IsButtonDown, "windows must agree with unix");
	}

	/// <summary>What the Windows path makes of a sequence.</summary>
	private static (List<MouseFlags> Flags, Point Position) Windows(string sequence)
	{
		// Static state, so a previous case's press would otherwise be inherited.
		SequenceHelper.Reset();

		SequenceHelper.GetMouse(
			sequence.Select(c => new ConsoleKeyInfo(c, ConsoleKey.NoName, false, false, false)).ToArray(),
			out var flags, out Point position, (_, _) => { });

		return (flags, position);
	}

	/// <summary>What the Unix path makes of the same sequence.</summary>
	private static (List<MouseFlags> Flags, Point Position) Unix(string sequence)
	{
		var parser = new AnsiInputParser();
		var bytes = Encoding.ASCII.GetBytes("\x1b[" + sequence);

		var mouse = parser.Parse(bytes.AsSpan(), bytes.Length)
			.OfType<MouseInputEvent>()
			.FirstOrDefault();

		return mouse != null
			? (mouse.Flags, mouse.Position)
			: (new List<MouseFlags>(), Point.Empty);
	}

	/// <summary>
	/// Whether two flag sets carry the same facts, whatever shape they arrive in.
	/// </summary>
	/// <remarks>
	/// One side lists each flag separately and the other ORs them into a single
	/// value, so the sets are compared as the union of their bits. That is the
	/// MEANING the two decoders must agree on; the shape is a separate question.
	/// </remarks>
	private static bool Same(List<MouseFlags> a, List<MouseFlags> b) => Bits(a) == Bits(b);

	private static MouseFlags Bits(List<MouseFlags> flags)
	{
		MouseFlags all = 0;
		foreach (var f in flags) all |= f;

		// AllEvents is every bit set, so masking it out would erase the answer.
		// One decoder seeds its list with it and then overwrites that entry, so
		// it never survives to here; the guard is only for a list that still
		// carries the seed alone.
		return all == MouseFlags.AllEvents ? 0 : all;
	}

	private static string Show(List<MouseFlags> flags) => string.Join("|", flags);

	[Fact]
	public void The_windows_input_can_be_fed_to_the_unix_decoder()
	{
		// The bridge the unification needs: Windows collects an escape sequence
		// as ConsoleKeyInfo, and SGR is pure ASCII, so the bytes a byte-parser
		// wants are one encode away. If this holds for the whole corpus there is
		// no technical reason for a second decoder.
		foreach (var row in Corpus())
		{
			string sequence = (string)row[0];
			string meaning = (string)row[1];

			var asKeys = sequence
				.Select(c => new ConsoleKeyInfo(c, ConsoleKey.NoName, false, false, false))
				.ToArray();

			// Exactly what the Windows loop holds, turned into bytes.
			var bytes = Encoding.ASCII.GetBytes("\x1b[" + new string(asKeys.Select(k => k.KeyChar).ToArray()));

			var mouse = new AnsiInputParser().Parse(bytes.AsSpan(), bytes.Length)
				.OfType<MouseInputEvent>()
				.FirstOrDefault();

			Assert.True(mouse != null, $"{meaning} ({sequence}) did not decode through the byte parser");
			Assert.Equal(Unix(sequence).Position, mouse!.Position);
		}
	}

	/// <summary>What each sequence must decode to, whatever decodes it.</summary>
	/// <remarks>
	/// Parity alone stopped being enough the moment both paths started sharing a
	/// decoder: breaking it breaks both sides equally, and they go on agreeing
	/// with each other about the wrong answer. These pin the ABSOLUTE meaning, so
	/// the corpus still fails when the shared decode is wrong.
	/// </remarks>
	public static TheoryData<string, MouseFlags> Expected() => new()
	{
		{ "<0;10;5M",  MouseFlags.Button1Pressed },
		{ "<0;10;5m",  MouseFlags.Button1Released },
		{ "<1;10;5M",  MouseFlags.Button2Pressed },
		{ "<2;10;5M",  MouseFlags.Button3Pressed },
		{ "<64;10;5M", MouseFlags.WheeledUp },
		{ "<65;10;5M", MouseFlags.WheeledDown },
		{ "<4;10;5M",  MouseFlags.Button1Pressed | MouseFlags.ButtonShift },
		{ "<8;10;5M",  MouseFlags.Button1Pressed | MouseFlags.ButtonAlt },
		{ "<16;10;5M", MouseFlags.Button1Pressed | MouseFlags.ButtonCtrl },
		{ "<32;10;5M", MouseFlags.Button1Pressed | MouseFlags.Button1Dragged
					 | MouseFlags.ReportMousePosition },
	};

	[Theory]
	[MemberData(nameof(Expected))]
	public void The_decoded_meaning_is_what_the_protocol_says(string sequence, MouseFlags expected)
	{
		Assert.Equal(expected, Bits(Unix(sequence).Flags));
		Assert.Equal(expected, Bits(Windows(sequence).Flags));
	}

	/// <summary>
	/// A press that names no button holds no button down.
	/// </summary>
	/// <remarks>
	/// Button bits 3 is "no button". X10 reuses it to mean a release, having no
	/// terminator to say so with; SGR has one, so there the bits mean what they
	/// say. Reading the X10 rule on a press reports a button the terminal never
	/// named AND a release where the terminator says press, which leaves a
	/// control believing a button is held that is not.
	/// <para>The motion branch of the same decode treats these bits as no button
	/// already. A RELEASE is the deliberate exception, covered below.</para>
	/// </remarks>
	[Fact]
	public void A_press_that_names_no_button_holds_none()
	{
		var flags = Bits(Unix("<3;10;5M").Flags);

		foreach (var invented in new[]
		{
			MouseFlags.Button1Pressed, MouseFlags.Button1Released,
			MouseFlags.Button2Pressed, MouseFlags.Button2Released,
			MouseFlags.Button3Pressed, MouseFlags.Button3Released,
		})
		{
			Assert.False(flags.HasFlag(invented),
				$"the report named no button, and {invented} says one: {flags}");
		}
	}

	/// <summary>
	/// A release that names no button still ends what a button started.
	/// </summary>
	/// <remarks>
	/// The deliberate asymmetry with the press above, and not a decode of the
	/// bits: there is no button-agnostic release in <see cref="MouseFlags"/>, and
	/// ending a drag, releasing a window capture and deriving a click are all
	/// keyed on <see cref="MouseFlags.Button1Released"/>. A release naming nothing
	/// would end none of them, which is a stuck drag rather than a tidy decode.
	/// A press has no such obligation, so it names nothing.
	/// </remarks>
	[Fact]
	public void A_release_that_names_no_button_still_releases()
	{
		var flags = Bits(Unix("<3;10;5m").Flags);

		Assert.True(flags.HasFlag(MouseFlags.Button1Released),
			$"nothing here would end a drag: {flags}");
	}

	/// <summary>
	/// A capital M is a press whatever the button bits say.
	/// </summary>
	/// <remarks>
	/// The terminator is the only thing in an SGR report that distinguishes a
	/// press from a release, so a decode that answers the same for both has
	/// stopped reading it.
	/// </remarks>
	[Fact]
	public void The_terminator_still_decides_when_no_button_is_named()
	{
		var press = Bits(Unix("<3;10;5M").Flags);
		var release = Bits(Unix("<3;10;5m").Flags);

		Assert.NotEqual(press, release);
	}

	[Fact]
	public void A_malformed_report_does_not_poison_the_next_one()
	{
		// The decoder is reused rather than allocated per event, and a parser
		// carries state between calls by design: a sequence can span two reads.
		// A truncated one therefore leaves it mid-sequence, and the next decode
		// on that instance returns NOTHING - an intermittent fault depending on
		// what happened to arrive before it. Flushing after every decode is what
		// makes the reuse safe, and this is what proves it.
		Windows("<0;10");       // truncated: no terminator
		Windows("<99;abc;x");   // nonsense
		Windows("");            // empty

		var good = Windows("<0;7;3M");

		Assert.Equal(MouseFlags.Button1Pressed, Bits(good.Flags));
		Assert.Equal(new Point(6, 2), good.Position);
	}

	[Fact]
	public void A_long_sequence_is_refused_rather_than_overrunning()
	{
		// The reused buffer is fixed, so something longer than any real SGR
		// report has to be turned away rather than written past the end.
		var huge = "<" + new string('9', 500) + ";1;1M";

		Assert.Empty(Windows(huge).Flags);

		// And the decoder still works afterwards.
		Assert.Equal(MouseFlags.Button1Pressed, Bits(Windows("<0;7;3M").Flags));
	}

	[Fact]
	public void The_auto_repeat_of_a_held_button_does_not_end_in_a_click()
	{
		// The repeat raises a BARE press - no ReportMousePosition - every
		// hundred milliseconds while a button is held, so a guard that looked
		// only for motion reports let it through, and every drag lasting longer
		// than one interval still ended in a click at the drop point. Which is
		// every real drag.
		var recognizer = new SharpConsoleUI.Input.MouseGestureRecognizer();

		Send(recognizer, "<0;5;5M", 4, 4);     // press
		Send(recognizer, "<32;8;5M", 7, 4);    // dragged away

		// What ContinuousPress raises, at the CURRENT pointer.
		recognizer.Recognize(new List<MouseFlags> { MouseFlags.Button1Pressed }, new Point(7, 4));

		Assert.DoesNotContain(MouseFlags.Button1Clicked, Send(recognizer, "<0;8;5m", 7, 4));
	}

	[Fact]
	public void Holding_after_a_double_click_does_not_become_a_triple_click()
	{
		// Same root cause: each repeat read as a fresh press advanced the click
		// run, so holding the second click of a double-click turned it into a
		// triple a tenth of a second later - word-select becoming line-select
		// under the user's finger.
		var clock = new StepClock();
		var recognizer = new SharpConsoleUI.Input.MouseGestureRecognizer(now: clock.Now);

		Send(recognizer, "<0;5;5M", 4, 4);
		Send(recognizer, "<0;5;5m", 4, 4);
		clock.Advance(200);

		Assert.Contains(MouseFlags.Button1DoubleClicked, Send(recognizer, "<0;5;5M", 4, 4));

		clock.Advance(100);
		var repeat = recognizer.Recognize(
			new List<MouseFlags> { MouseFlags.Button1Pressed }, new Point(4, 4));

		Assert.DoesNotContain(MouseFlags.Button1TripleClicked, repeat);
	}

	[Fact]
	public void A_modifier_on_a_vertical_wheel_stays_a_modifier()
	{
		// Shift+wheel is horizontal scrolling in TableControl, which reads
		// ButtonShift. Rewriting a modified vertical tick as a horizontal tilt
		// - which the old Windows table did - loses that flag and sets
		// ButtonCtrl instead, because WheeledLeft IS ButtonCtrl | WheeledUp.
		var flags = Bits(Unix("<68;10;5M").Flags);

		Assert.True(flags.HasFlag(MouseFlags.ButtonShift), "shift was lost");
		Assert.True(flags.HasFlag(MouseFlags.WheeledUp), "the wheel direction was lost");
		Assert.False(flags.HasFlag(MouseFlags.ButtonCtrl), "ctrl was invented");
	}

	/// <summary>A clock a test moves by hand.</summary>
	private sealed class StepClock
	{
		private DateTime _now = DateTime.UtcNow;

		internal DateTime Now() => _now;

		internal void Advance(int milliseconds) => _now = _now.AddMilliseconds(milliseconds);
	}

	[Fact]
	public void A_drag_does_not_end_in_a_click()
	{
		// A motion report carries the held button beside ReportMousePosition, so
		// it reads as a press unless something says otherwise. Taken as one, it
		// moves the remembered press position to wherever the pointer now is,
		// and the release then lands "near" that and becomes a click. Dragging
		// across a control would end in a click on whatever it stopped over.
		var recognizer = new SharpConsoleUI.Input.MouseGestureRecognizer();

		Send(recognizer, "<0;5;5M", 4, 4);     // press
		Send(recognizer, "<32;6;5M", 5, 4);    // drag
		Send(recognizer, "<32;7;5M", 6, 4);
		Send(recognizer, "<32;8;5M", 7, 4);

		var onRelease = Send(recognizer, "<0;8;5m", 7, 4);

		Assert.DoesNotContain(MouseFlags.Button1Clicked, onRelease);
		Assert.Contains(MouseFlags.Button1Released, onRelease);
	}

	[Fact]
	public void A_click_in_one_place_is_still_a_click()
	{
		// The other half: suppressing the drag case must not cost the ordinary one.
		var recognizer = new SharpConsoleUI.Input.MouseGestureRecognizer();

		Send(recognizer, "<0;5;5M", 4, 4);

		Assert.Contains(MouseFlags.Button1Clicked, Send(recognizer, "<0;5;5m", 4, 4));
	}

	/// <summary>Decodes a sequence and puts it through the recogniser.</summary>
	private static List<MouseFlags> Send(
		SharpConsoleUI.Input.MouseGestureRecognizer recognizer, string sequence, int x, int y)
		=> recognizer.Recognize(Unix(sequence).Flags, new Point(x, y));

	/// <summary>Legacy X10 reports, which a terminal sends when it has no SGR.</summary>
	public static TheoryData<int, MouseFlags> X10Corpus() => new()
	{
		{ 0,  MouseFlags.Button1Pressed },
		{ 1,  MouseFlags.Button2Pressed },
		{ 2,  MouseFlags.Button3Pressed },
		{ 3,  MouseFlags.Button1Released },
		{ 4,  MouseFlags.Button1Pressed | MouseFlags.ButtonShift },
		{ 8,  MouseFlags.Button1Pressed | MouseFlags.ButtonAlt },
		{ 16, MouseFlags.Button1Pressed | MouseFlags.ButtonCtrl },
		{ 64, MouseFlags.WheeledUp },
		{ 65, MouseFlags.WheeledDown },
		{ 32, MouseFlags.ReportMousePosition | MouseFlags.Button1Pressed | MouseFlags.Button1Dragged },
	};

	[Theory]
	[MemberData(nameof(X10Corpus))]
	public void X10_decodes_to_what_the_protocol_says(int code, MouseFlags expected)
	{
		// There was a THIRD decoder for this, in the Windows driver, and it
		// disagreed three ways: it read the modifier bits as the button number,
		// so shift+click came out as button 2; the wheel directions were
		// inverted; and its motion branch raised the event itself and then
		// returned true to a caller that raised it again.
		var decoded = AnsiInputParser.DecodeX10Mouse((char)(32 + code), (char)(32 + 8), (char)(32 + 4));

		Assert.Equal(expected, Bits(decoded.Flags));
		Assert.Equal(new Point(7, 3), decoded.Position);
	}
}
