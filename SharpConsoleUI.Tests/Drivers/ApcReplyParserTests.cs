// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using System.Text;
using SharpConsoleUI.Drivers.Input;
using Xunit;

namespace SharpConsoleUI.Tests.Drivers;

/// <summary>
/// A terminal's APC replies must never reach the application as input.
/// </summary>
/// <remarks>
/// <para>
/// Kitty answers a graphics command with <c>ESC _ G i=&lt;id&gt;;OK ESC \</c>. The introducer is
/// <c>ESC _</c>, and <c>_</c> is printable, so the reply used to fall into the "ESC plus printable
/// is Alt+key" rule and was delivered as the keystrokes <c>_ G i = 3 1 ; O K</c>.
/// </para>
/// <para>
/// Found in a real full-screen Kitty: the reply typed itself into a composer and was sent to a
/// model as part of the user's message, and a portal that closed on any key closed the moment its
/// image finished uploading.
/// </para>
/// </remarks>
public class ApcReplyParserTests
{
	private static List<InputEvent> Parse(string ascii)
	{
		var parser = new AnsiInputParser();
		var bytes = Encoding.UTF8.GetBytes(ascii);
		return parser.Parse(bytes.AsSpan(), bytes.Length);
	}

	private static string Typed(IEnumerable<InputEvent> events)
		=> new string(events.OfType<KeyInputEvent>()
			.Select(e => e.KeyInfo.KeyChar)
			.Where(c => c != '\0')
			.ToArray());

	/// <summary>The reply Kitty actually sends, verbatim.</summary>
	private const string KittyOkReply = "\x1b_Gi=31;OK\x1b\\";

	#region The reply itself

	[Fact]
	public void AKittyGraphicsReply_RaisesNothing()
	{
		Assert.Empty(Parse(KittyOkReply));
	}

	/// <summary>An error reply arrives even when the command asked to be left alone (q=2).</summary>
	[Fact]
	public void AnErrorReply_RaisesNothing()
	{
		Assert.Empty(Parse("\x1b_Gi=31;ENOENT:No such file\x1b\\"));
	}

	/// <summary>The 8-bit form: 0x9F introduces, 0x9C terminates, with no ESC involved.</summary>
	[Fact]
	public void TheEightBitForm_RaisesNothing()
	{
		var parser = new AnsiInputParser();
		var bytes = new List<byte> { 0x9F };
		bytes.AddRange(Encoding.ASCII.GetBytes("Gi=31;OK"));
		bytes.Add(0x9C);

		var array = bytes.ToArray();
		Assert.Empty(parser.Parse(array.AsSpan(), array.Length));
	}

	#endregion

	#region Keys around it

	[Fact]
	public void KeysBeforeAndAfterTheReply_ArriveUnchanged()
	{
		var events = Parse($"ab{KittyOkReply}cd");

		Assert.Equal("abcd", Typed(events));
	}

	/// <summary>
	/// A reply between two keystrokes must not swallow the second or hold it back: the key after it
	/// is delivered in the same parse, not left pending.
	/// </summary>
	[Fact]
	public void AReplyBetweenTwoKeys_DelaysNeither()
	{
		var events = Parse($"x{KittyOkReply}y");

		Assert.Equal("xy", Typed(events));
		Assert.Empty(events.OfType<PasteInputEvent>());
	}

	/// <summary>It is not a paste either: nothing in the reply reaches the application at all.</summary>
	[Fact]
	public void AReply_IsNotDeliveredAsAPaste()
	{
		var events = Parse($"a{KittyOkReply}b");

		Assert.Empty(events.OfType<PasteInputEvent>());
	}

	#endregion

	#region Split across reads

	/// <summary>
	/// A reply arrives in whatever pieces the read happens to return. Split anywhere, it must still
	/// be consumed whole: a half-swallowed reply resumes mid-payload and delivers the tail as keys,
	/// which is the bug itself.
	/// </summary>
	[Theory]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(3)]
	[InlineData(5)]
	[InlineData(8)]
	[InlineData(11)]
	public void AReplySplitAcrossTwoReads_IsStillConsumedWhole(int splitAt)
	{
		string whole = $"a{KittyOkReply}b";
		var parser = new AnsiInputParser();
		var all = new List<InputEvent>();

		foreach (string part in new[] { whole[..splitAt], whole[splitAt..] })
		{
			var bytes = Encoding.UTF8.GetBytes(part);
			all.AddRange(parser.Parse(bytes.AsSpan(), bytes.Length));
		}

		Assert.Equal("ab", Typed(all));
	}

	/// <summary>One byte per read, the worst case a slow pipe can produce.</summary>
	[Fact]
	public void AReplyFedOneByteAtATime_IsStillConsumedWhole()
	{
		string whole = $"a{KittyOkReply}b";
		var parser = new AnsiInputParser();
		var all = new List<InputEvent>();

		foreach (byte b in Encoding.UTF8.GetBytes(whole))
		{
			var one = new[] { b };
			all.AddRange(parser.Parse(one.AsSpan(), 1));
		}

		Assert.Equal("ab", Typed(all));
	}

	#endregion

	#region Not over-eager

	/// <summary>
	/// Alt+underscore is a real keystroke a user can type, and it is ESC _ with no APC payload
	/// after it. It is now read as an APC introducer, which is the deliberate trade: a terminal
	/// reply arriving as text is a correctness bug, and Alt+_ is a chord nothing binds.
	/// </summary>
	[Fact]
	public void TheIntroducerIsTreatedAsAReply_NotAsAltUnderscore()
	{
		var events = Parse("\x1b_Gi=1;OK\x1b\\");

		Assert.Empty(events);
	}

	/// <summary>Other ESC-plus-printable chords still work: only '_' changed meaning.</summary>
	[Fact]
	public void OtherAltChords_StillArrive()
	{
		// "\x1ba" would be the SINGLE character U+01BA: C# reads \x greedily, so the escape
		// swallows the 'a'. \u001b pins the escape at one character.
		var events = Parse("\u001ba").OfType<KeyInputEvent>().ToList();

		Assert.Single(events);
		Assert.Equal('a', events[0].KeyInfo.KeyChar);
		Assert.True(events[0].KeyInfo.Modifiers.HasFlag(System.ConsoleModifiers.Alt));
	}

	#endregion
}
