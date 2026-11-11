// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SharpConsoleUI.Drivers.Input;
using Xunit;

namespace SharpConsoleUI.Tests.Drivers;

/// <summary>
/// The two sequence families a terminal uses to report a modified key that has no control code of
/// its own: xterm's <c>modifyOtherKeys</c> (<c>CSI 27;mod;code~</c>) and the Kitty/fixterms
/// "CSI u" form (<c>CSI code;mod u</c>).
/// </summary>
/// <remarks>
/// <para>
/// A control byte carries no case, so Ctrl+O and Ctrl+Shift+O are the same byte (0x0F) on a pty
/// and are genuinely indistinguishable there (issue #95). These sequences exist precisely to carry
/// the modifier the control code cannot, and a terminal sending one is reporting a keystroke the
/// user made.
/// </para>
/// <para>
/// Both were previously decoded to <c>UnknownSequenceEvent</c> and dropped: modifyOtherKeys fell
/// through <c>DispatchTilde</c>'s default, and the <c>u</c> final byte had no case at all. A key
/// the user pressed produced no event, which reads as the application ignoring the keyboard. The
/// library does not enable either protocol, but it does not have to for these to arrive: the user
/// can set <c>modifyOtherKeys</c> in their terminal, and another program sharing the terminal can
/// turn it on and leave it on.
/// </para>
/// </remarks>
public class ModifiedKeySequenceTests
{
	private static List<InputEvent> Decode(string sequence)
	{
		var parser = new AnsiInputParser();
		byte[] bytes = Encoding.ASCII.GetBytes(sequence);
		var events = parser.Parse(bytes, bytes.Length);
		events.AddRange(parser.Flush());
		return events;
	}

	private static ConsoleKeyInfo SingleKey(string sequence)
	{
		var keys = Decode(sequence).OfType<KeyInputEvent>().ToList();
		Assert.Single(keys);
		return keys[0].KeyInfo;
	}

	#region modifyOtherKeys: CSI 27 ; mod ; code ~

	/// <summary>
	/// The reporter's keystroke, in the form a terminal uses when it CAN tell the two apart.
	/// Modifier 5 is Ctrl, 6 is Ctrl+Shift.
	/// </summary>
	[Fact]
	public void ModifyOtherKeys_SeparatesCtrlFromCtrlShift()
	{
		var ctrlO = SingleKey("\x1b[27;5;111~");
		var ctrlShiftO = SingleKey("\x1b[27;6;79~");

		Assert.Equal(ConsoleKey.O, ctrlO.Key);
		Assert.True(ctrlO.Modifiers.HasFlag(ConsoleModifiers.Control));
		Assert.False(ctrlO.Modifiers.HasFlag(ConsoleModifiers.Shift));

		Assert.Equal(ConsoleKey.O, ctrlShiftO.Key);
		Assert.True(ctrlShiftO.Modifiers.HasFlag(ConsoleModifiers.Control));
		Assert.True(ctrlShiftO.Modifiers.HasFlag(ConsoleModifiers.Shift));

		Assert.NotEqual(ctrlO.Modifiers, ctrlShiftO.Modifiers);
	}

	/// <summary>The xterm modifier encoding, which is the same one the arrow keys already use.</summary>
	[Theory]
	[InlineData(2, false, false, true)]    // Shift
	[InlineData(3, false, true, false)]    // Alt
	[InlineData(5, true, false, false)]    // Ctrl
	[InlineData(6, true, false, true)]     // Ctrl+Shift
	[InlineData(7, true, true, false)]     // Ctrl+Alt
	[InlineData(8, true, true, true)]      // Ctrl+Alt+Shift
	public void ModifyOtherKeys_DecodesTheModifierCode(int mod, bool ctrl, bool alt, bool shift)
	{
		var key = SingleKey($"\x1b[27;{mod};111~");

		Assert.Equal(ConsoleKey.O, key.Key);
		Assert.Equal(ctrl, key.Modifiers.HasFlag(ConsoleModifiers.Control));
		Assert.Equal(alt, key.Modifiers.HasFlag(ConsoleModifiers.Alt));
		Assert.Equal(shift, key.Modifiers.HasFlag(ConsoleModifiers.Shift));
	}

	/// <summary>Modifier 1 is "no modifiers", which is still a keystroke.</summary>
	[Fact]
	public void ModifyOtherKeys_NoModifiers_IsStillAKey()
	{
		var key = SingleKey("\x1b[27;1;111~");

		Assert.Equal(ConsoleKey.O, key.Key);
		Assert.Equal((ConsoleModifiers)0, key.Modifiers);
	}

	/// <summary>
	/// The character the key produced must survive, since the typing paths consume it.
	/// </summary>
	[Fact]
	public void ModifyOtherKeys_KeepsTheCharacter()
	{
		Assert.Equal('o', SingleKey("\x1b[27;5;111~").KeyChar);
		Assert.Equal('O', SingleKey("\x1b[27;6;79~").KeyChar);
	}

	/// <summary>
	/// Keys that have their own named sequence must keep reporting it, so this cannot be a general
	/// "any code becomes a letter" rule.
	/// </summary>
	[Theory]
	[InlineData(13, ConsoleKey.Enter)]
	[InlineData(9, ConsoleKey.Tab)]
	[InlineData(27, ConsoleKey.Escape)]
	[InlineData(32, ConsoleKey.Spacebar)]
	public void ModifyOtherKeys_NamedKeys_GetTheirConsoleKey(int code, ConsoleKey expected)
	{
		var key = SingleKey($"\x1b[27;5;{code}~");

		Assert.Equal(expected, key.Key);
		Assert.True(key.Modifiers.HasFlag(ConsoleModifiers.Control));
	}

	#endregion

	#region CSI u: CSI code ; mod u

	[Fact]
	public void CsiU_SeparatesCtrlFromCtrlShift()
	{
		var ctrlO = SingleKey("\x1b[111;5u");
		var ctrlShiftO = SingleKey("\x1b[111;6u");

		Assert.Equal(ConsoleKey.O, ctrlO.Key);
		Assert.True(ctrlO.Modifiers.HasFlag(ConsoleModifiers.Control));
		Assert.False(ctrlO.Modifiers.HasFlag(ConsoleModifiers.Shift));

		Assert.Equal(ConsoleKey.O, ctrlShiftO.Key);
		Assert.True(ctrlShiftO.Modifiers.HasFlag(ConsoleModifiers.Control));
		Assert.True(ctrlShiftO.Modifiers.HasFlag(ConsoleModifiers.Shift));
	}

	/// <summary>A bare <c>CSI code u</c> carries no modifier section and is an unmodified key.</summary>
	[Fact]
	public void CsiU_WithoutModifier_IsStillAKey()
	{
		var key = SingleKey("\x1b[111u");

		Assert.Equal(ConsoleKey.O, key.Key);
		Assert.Equal((ConsoleModifiers)0, key.Modifiers);
		Assert.Equal('o', key.KeyChar);
	}

	/// <summary>
	/// Kitty appends further sections (event type, associated text) after the modifier. The extra
	/// parameters must not stop the key being decoded.
	/// </summary>
	[Fact]
	public void CsiU_WithExtraParameters_StillDecodes()
	{
		var key = SingleKey("\x1b[111;6;111u");

		Assert.Equal(ConsoleKey.O, key.Key);
		Assert.True(key.Modifiers.HasFlag(ConsoleModifiers.Control));
		Assert.True(key.Modifiers.HasFlag(ConsoleModifiers.Shift));
	}

	#endregion

	#region Agreement and non-regression

	/// <summary>
	/// The three ways a terminal can report Ctrl+O must agree on the key. Only the legacy control
	/// byte cannot carry Shift, which is the whole reason the other two exist.
	/// </summary>
	[Fact]
	public void AllThreeEncodingsOfCtrlO_AgreeOnTheKey()
	{
		var legacy = Decode("\u000f").OfType<KeyInputEvent>().Single().KeyInfo;
		var modifyOther = SingleKey("\x1b[27;5;111~");
		var csiU = SingleKey("\x1b[111;5u");

		Assert.Equal(ConsoleKey.O, legacy.Key);
		Assert.Equal(ConsoleKey.O, modifyOther.Key);
		Assert.Equal(ConsoleKey.O, csiU.Key);

		foreach (var k in new[] { legacy, modifyOther, csiU })
			Assert.True(k.Modifiers.HasFlag(ConsoleModifiers.Control));
	}

	/// <summary>
	/// A malformed sequence must not be turned into a modified keystroke the user never made.
	/// </summary>
	/// <remarks>
	/// A letter is not a valid CSI parameter byte, so the parser abandons the sequence and the
	/// letters re-emerge as the ordinary typed characters they are -- long-standing behaviour, and
	/// not something these forms change. What matters here is the narrower claim: nothing invents a
	/// Ctrl- or Shift-modified key out of a sequence it could not parse.
	/// </remarks>
	[Theory]
	[InlineData("\x1b[27;5;~")]       // no code
	[InlineData("\x1b[27;5;abc~")]    // non-numeric code
	[InlineData("\x1b[abc;5u")]       // non-numeric code
	public void MalformedSequences_AreNotTurnedIntoModifiedKeys(string sequence)
	{
		var keys = Decode(sequence).OfType<KeyInputEvent>();

		Assert.Empty(keys.Where(k => k.KeyInfo.Modifiers != 0));
	}

	/// <summary>
	/// The tilde sequences that already worked must keep working: 27 is a specific prefix, not a
	/// new interpretation of every tilde sequence.
	/// </summary>
	[Theory]
	[InlineData("\x1b[5~", ConsoleKey.PageUp)]
	[InlineData("\x1b[6~", ConsoleKey.PageDown)]
	[InlineData("\x1b[3~", ConsoleKey.Delete)]
	[InlineData("\x1b[15~", ConsoleKey.F5)]
	[InlineData("\x1b[24~", ConsoleKey.F12)]
	public void OrdinaryTildeSequences_StillDecode(string sequence, ConsoleKey expected)
	{
		Assert.Equal(expected, SingleKey(sequence).Key);
	}

	/// <summary>Escape itself reports code 27 as a KEY, not as a modifyOtherKeys prefix.</summary>
	[Fact]
	public void EscapeKey_IsNotMistakenForAModifyOtherKeysPrefix()
	{
		// CSI 27 ~ has no modifier/code sections: it is not the three-parameter form.
		var events = Decode("\x1b[27~");

		Assert.Empty(events.OfType<KeyInputEvent>().Where(k => k.KeyInfo.Key == ConsoleKey.O));
	}

	#endregion
}
