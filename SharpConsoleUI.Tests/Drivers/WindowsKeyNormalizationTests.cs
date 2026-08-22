// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using SharpConsoleUI.Drivers;
using Xunit;

namespace SharpConsoleUI.Tests.Drivers;

/// <summary>
/// Covers <see cref="NetConsoleDriver.NormalizeKey"/>, which fills in the <see cref="ConsoleKey"/>
/// that <c>ENABLE_VIRTUAL_TERMINAL_INPUT</c> leaves unset on Windows.
/// </summary>
/// <remarks>
/// Under VT input mode <see cref="Console.ReadKey(bool)"/> reports <see cref="ConsoleKey.None"/> for
/// ordinary characters and sets only <see cref="ConsoleKeyInfo.KeyChar"/>. Measured by A/B on one
/// Windows console with the same injected keystroke: VT off gives <c>Key=W, KeyChar=0x77</c>, VT on
/// gives <c>Key=None, KeyChar=0x77</c>. So <c>keyInfo.Key == ConsoleKey.W</c> — the obvious test, and
/// the one that works on Unix — was never true on Windows, silently breaking any application
/// comparing against a letter or digit key.
///
/// <para>These run on every platform: the method is pure, and the mapping it must produce is the same
/// one the Unix parser already applies.</para>
/// </remarks>
public class WindowsKeyNormalizationTests
{
	private static ConsoleKeyInfo VtStyle(char c, ConsoleModifiers modifiers = 0) =>
		// What VT input mode actually delivers: the character, and no Key.
		new(c, ConsoleKey.None,
			(modifiers & ConsoleModifiers.Shift) != 0,
			(modifiers & ConsoleModifiers.Alt) != 0,
			(modifiers & ConsoleModifiers.Control) != 0);

	#region The gap being closed

	[Theory]
	[InlineData('w', ConsoleKey.W)]
	[InlineData('W', ConsoleKey.W)]
	[InlineData('a', ConsoleKey.A)]
	[InlineData('z', ConsoleKey.Z)]
	[InlineData('Q', ConsoleKey.Q)]
	public void Letters_GetTheirConsoleKey(char c, ConsoleKey expected)
	{
		var result = NetConsoleDriver.NormalizeKey(VtStyle(c));

		Assert.Equal(expected, result.Key);
		Assert.Equal(c, result.KeyChar); // the raw character must survive untouched
	}

	[Theory]
	[InlineData('0', ConsoleKey.D0)]
	[InlineData('5', ConsoleKey.D5)]
	[InlineData('9', ConsoleKey.D9)]
	public void Digits_GetTheirConsoleKey(char c, ConsoleKey expected)
	{
		var result = NetConsoleDriver.NormalizeKey(VtStyle(c));

		Assert.Equal(expected, result.Key);
		Assert.Equal(c, result.KeyChar);
	}

	[Fact]
	public void Modifiers_ArePreserved()
	{
		var result = NetConsoleDriver.NormalizeKey(VtStyle('w', ConsoleModifiers.Shift | ConsoleModifiers.Alt));

		Assert.Equal(ConsoleKey.W, result.Key);
		Assert.True(result.Modifiers.HasFlag(ConsoleModifiers.Shift));
		Assert.True(result.Modifiers.HasFlag(ConsoleModifiers.Alt));
		Assert.False(result.Modifiers.HasFlag(ConsoleModifiers.Control));
	}

	/// <summary>Windows must now report what Unix already reported for the same keystroke.</summary>
	[Theory]
	[InlineData('w')]
	[InlineData('W')]
	[InlineData('7')]
	[InlineData(' ')]
	[InlineData('.')]
	public void AgreesWithTheUnixParser(char c)
	{
		var windows = NetConsoleDriver.NormalizeKey(VtStyle(c));
		var unix = SharpConsoleUI.Drivers.Input.AnsiInputParser.CharToConsoleKey(c);

		Assert.Equal(unix, windows.Key);
	}

	#endregion

	#region What must not change

	/// <summary>
	/// A key the console already identified is returned untouched — this must never overwrite a
	/// correct Key (VT input off, or a console that resolved the key itself).
	/// </summary>
	[Theory]
	[InlineData(ConsoleKey.Enter, '\r')]
	[InlineData(ConsoleKey.Tab, '\t')]
	[InlineData(ConsoleKey.Escape, '\x1b')]
	[InlineData(ConsoleKey.UpArrow, '\0')]
	[InlineData(ConsoleKey.F5, '\0')]
	[InlineData(ConsoleKey.W, 'w')]
	public void AlreadyIdentifiedKeys_PassThroughUnchanged(ConsoleKey key, char keyChar)
	{
		var original = new ConsoleKeyInfo(keyChar, key, false, false, false);

		var result = NetConsoleDriver.NormalizeKey(original);

		Assert.Equal(original.Key, result.Key);
		Assert.Equal(original.KeyChar, result.KeyChar);
		Assert.Equal(original.Modifiers, result.Modifiers);
	}

	/// <summary>
	/// Ctrl+letter is rebuilt earlier in the input loop from control characters 0x01-0x1A and never
	/// reaches this method; if one ever did, it already carries its Key and must pass through.
	/// </summary>
	[Fact]
	public void ControlCombinations_PassThroughUnchanged()
	{
		var ctrlQ = new ConsoleKeyInfo('', ConsoleKey.Q, false, false, true);

		var result = NetConsoleDriver.NormalizeKey(ctrlQ);

		Assert.Equal(ConsoleKey.Q, result.Key);
		Assert.Equal('', result.KeyChar);
		Assert.True(result.Modifiers.HasFlag(ConsoleModifiers.Control));
	}

	/// <summary>
	/// Characters with no sensible mapping keep the character and stay unidentified. Reporting
	/// NoName would be no more useful than None, and the raw character is what the CJK and
	/// bracketed-paste paths consume (issue #42).
	/// </summary>
	[Theory]
	[InlineData('中')]
	[InlineData('П')]
	[InlineData('é')]
	[InlineData('\0')]   // no character at all — nothing to map from
	public void UnmappableCharacters_KeepTheirCharacter(char c)
	{
		var result = NetConsoleDriver.NormalizeKey(VtStyle(c));

		Assert.Equal(c, result.KeyChar);
		Assert.Equal(ConsoleKey.None, result.Key);
	}

	/// <summary>
	/// A pasted or typed non-ASCII character must arrive with its character intact — this is the
	/// path issue #42 fixed, and normalization must not disturb it.
	/// </summary>
	[Fact]
	public void CjkText_SurvivesNormalizationCharacterForCharacter()
	{
		const string sample = "中文Привет";

		foreach (char c in sample)
		{
			var result = NetConsoleDriver.NormalizeKey(VtStyle(c));
			Assert.Equal(c, result.KeyChar);
		}
	}

	#endregion

	#region Alt+key (the ESC branch)

	/// <summary>What VT input delivers as the character following an ESC.</summary>
	private static ConsoleKeyInfo AfterEsc(char c) => VtStyle(c);

	/// <summary>
	/// Alt+letter must report its <see cref="ConsoleKey"/> too.
	/// </summary>
	/// <remarks>
	/// Under VT input Alt+X arrives as ESC then X, handled by a branch UPSTREAM of the ordinary
	/// normalization — so the first fix did not reach it. Measured on Windows before and after that
	/// fix, unchanged both times: <c>Alt+w -> Key=None, Mods=Alt, KeyChar=0x77</c>. Unix already
	/// reported <c>Key=W</c> here, via <c>AnsiInputParser.ProcessEscape</c>.
	/// </remarks>
	[Theory]
	[InlineData('w', ConsoleKey.W)]
	[InlineData('f', ConsoleKey.F)]
	[InlineData('7', ConsoleKey.D7)]
	[InlineData('.', ConsoleKey.OemPeriod)]
	public void AltKey_GetsItsConsoleKey(char c, ConsoleKey expected)
	{
		var result = NetConsoleDriver.BuildAltKey(AfterEsc(c));

		Assert.Equal(expected, result.Key);
		Assert.Equal(c, result.KeyChar);
		Assert.True(result.Modifiers.HasFlag(ConsoleModifiers.Alt));
	}

	/// <summary>
	/// Shift is taken from the character's case, matching the Unix parser, so both backends agree
	/// about Alt+W.
	/// </summary>
	[Theory]
	[InlineData('W', true)]
	[InlineData('w', false)]
	public void AltKey_ReportsShiftFromCharacterCase_LikeUnix(char c, bool expectShift)
	{
		var result = NetConsoleDriver.BuildAltKey(AfterEsc(c));

		Assert.Equal(expectShift, result.Modifiers.HasFlag(ConsoleModifiers.Shift));
		Assert.Equal(ConsoleKey.W, result.Key);
	}

	/// <summary>The two input backends must report the same ConsoleKey for the same Alt+key.</summary>
	[Theory]
	[InlineData('w')]
	[InlineData('W')]
	[InlineData('f')]
	[InlineData('3')]
	[InlineData('-')]
	public void AltKey_AgreesWithTheUnixParser(char c)
	{
		var windows = NetConsoleDriver.BuildAltKey(AfterEsc(c));
		var unix = SharpConsoleUI.Drivers.Input.AnsiInputParser.CharToConsoleKey(c);

		Assert.Equal(unix, windows.Key);
		// Unix sets shift from char.IsUpper at the same point; Windows must match.
		Assert.Equal(char.IsUpper(c), windows.Modifiers.HasFlag(ConsoleModifiers.Shift));
	}

	[Fact]
	public void AltKey_PreservesControlModifier()
	{
		var ctrlAltW = new ConsoleKeyInfo('\u0017', ConsoleKey.W, false, false, true);

		var result = NetConsoleDriver.BuildAltKey(ctrlAltW);

		Assert.Equal(ConsoleKey.W, result.Key);
		Assert.True(result.Modifiers.HasFlag(ConsoleModifiers.Control));
		Assert.True(result.Modifiers.HasFlag(ConsoleModifiers.Alt));
	}

	/// <summary>
	/// Shift is inferred only for ASCII, because that is the only range where Unix takes the Alt path
	/// at all.
	/// </summary>
	/// <remarks>
	/// <c>ProcessEscape</c> gates on <c>b &gt;= 0x20 &amp;&amp; b &lt;= 0x7E</c>, so its
	/// <c>char.IsUpper</c> only ever sees ASCII. <see cref="char.IsUpper(char)"/> is Unicode-aware, so
	/// applying it unscoped reported Shift for Alt+CYRILLIC-CAPITAL — an event Unix never produces,
	/// since ESC followed by a multi-byte character takes the control-character path there. Inferring
	/// Shift outside ASCII invents a divergence instead of removing one.
	/// </remarks>
	[Theory]
	[InlineData('П')]  // Cyrillic capital — uppercase, but outside the range Unix treats as Alt+key
	[InlineData('Ä')]  // Latin-1 capital, likewise
	public void AltKey_DoesNotInferShift_ForNonAsciiUppercase(char c)
	{
		var result = NetConsoleDriver.BuildAltKey(AfterEsc(c));

		Assert.False(result.Modifiers.HasFlag(ConsoleModifiers.Shift));
		Assert.Equal(c, result.KeyChar);
		Assert.True(result.Modifiers.HasFlag(ConsoleModifiers.Alt));
	}

	/// <summary>An Alt+key whose character has no mapping keeps the character and stays unidentified.</summary>
	[Fact]
	public void AltKey_UnmappableCharacter_KeepsItsCharacter()
	{
		var result = NetConsoleDriver.BuildAltKey(AfterEsc('中'));

		Assert.Equal('中', result.KeyChar);
		Assert.Equal(ConsoleKey.None, result.Key);
		Assert.True(result.Modifiers.HasFlag(ConsoleModifiers.Alt));
	}

	/// <summary>A key that already resolved must not be overwritten here either.</summary>
	[Fact]
	public void AltKey_AlreadyIdentifiedKey_PassesThrough()
	{
		var already = new ConsoleKeyInfo('w', ConsoleKey.W, false, false, false);

		var result = NetConsoleDriver.BuildAltKey(already);

		Assert.Equal(ConsoleKey.W, result.Key);
		Assert.Equal('w', result.KeyChar);
		Assert.True(result.Modifiers.HasFlag(ConsoleModifiers.Alt));
	}

	#endregion
}
