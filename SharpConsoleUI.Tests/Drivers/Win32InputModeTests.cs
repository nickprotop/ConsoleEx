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
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Drivers.Input;
using Xunit;

namespace SharpConsoleUI.Tests.Drivers;

/// <summary>
/// win32-input-mode: <c>ESC [ Vk ; Sc ; Uc ; Kd ; Cs ; Rc _</c>, the raw Windows console key
/// record serialised as an escape sequence.
/// </summary>
/// <remarks>
/// <para>
/// Under <c>ENABLE_VIRTUAL_TERMINAL_INPUT</c> conhost zeroes <c>ControlKeyState</c> on every key
/// record, so Ctrl+Shift+O reaches an application as Ctrl+O and the modifier cannot be recovered
/// afterwards (issue #95). This mode is the only way back: the terminal stops translating to legacy
/// ANSI and reports the record itself, modifiers intact.
/// </para>
/// <para>
/// Windows Terminal and WezTerm honour <c>CSI ?9001h</c>; classic conhost ignores it. Nothing is
/// probed -- the first well-formed report IS the evidence the request was honoured, and until one
/// arrives decoding is unchanged. That makes the fallback the absence of a change rather than a
/// second code path.
/// </para>
/// <para>
/// These run on every platform: the decoder is pure, and parsing a documented sequence does not
/// need the console that produces it.
/// </para>
/// </remarks>
public class Win32InputModeTests
{
	// ControlKeyState bits, as the console reports them.
	private const int RightAlt = 0x0001;
	private const int LeftAlt = 0x0002;
	private const int RightCtrl = 0x0004;
	private const int LeftCtrl = 0x0008;
	private const int ShiftPressed = 0x0010;

	private const int VkO = 0x4F;
	private const int VkReturn = 0x0D;
	private const int VkBack = 0x08;
	private const int VkTab = 0x09;
	private const int VkEscape = 0x1B;
	private const int VkSpace = 0x20;
	private const int VkF1 = 0x70;
	private const int VkLeft = 0x25;
	private const int VkShift = 0x10;
	private const int VkControl = 0x11;

	/// <summary>A win32 report, in the order the mode defines.</summary>
	private static string Report(int vk, int uc, int cs, int kd = 1, int sc = 0, int rc = 1) =>
		$"\x1b[{vk};{sc};{uc};{kd};{cs};{rc}_";

	private static List<InputEvent> Decode(string sequence)
	{
		var parser = new AnsiInputParser();
		byte[] bytes = Encoding.ASCII.GetBytes(sequence);
		var events = parser.Parse(bytes, bytes.Length);
		events.AddRange(parser.Flush());
		return events;
	}

	private static List<ConsoleKeyInfo> Keys(string sequence) =>
		Decode(sequence).OfType<KeyInputEvent>().Select(e => e.KeyInfo).ToList();

	private static ConsoleKeyInfo SingleKey(string sequence)
	{
		var keys = Keys(sequence);
		Assert.Single(keys);
		return keys[0];
	}

	#region The distinction the mode exists for

	/// <summary>The reporter's case, now separable because ControlKeyState survives.</summary>
	[Fact]
	public void CtrlShiftO_IsDistinctFromCtrlO()
	{
		var ctrlO = SingleKey(Report(VkO, 'o', LeftCtrl));
		var ctrlShiftO = SingleKey(Report(VkO, 'O', LeftCtrl | ShiftPressed));

		Assert.Equal(ConsoleKey.O, ctrlO.Key);
		Assert.Equal(ConsoleKey.O, ctrlShiftO.Key);

		Assert.True(ctrlO.Modifiers.HasFlag(ConsoleModifiers.Control));
		Assert.False(ctrlO.Modifiers.HasFlag(ConsoleModifiers.Shift));

		Assert.True(ctrlShiftO.Modifiers.HasFlag(ConsoleModifiers.Control));
		Assert.True(ctrlShiftO.Modifiers.HasFlag(ConsoleModifiers.Shift));

		Assert.NotEqual(ctrlO.Modifiers, ctrlShiftO.Modifiers);
	}

	/// <summary>Either Ctrl or either Alt counts, since the console reports them per side.</summary>
	[Theory]
	[InlineData(LeftCtrl, true, false, false)]
	[InlineData(RightCtrl, true, false, false)]
	[InlineData(LeftAlt, false, true, false)]
	[InlineData(RightAlt, false, true, false)]
	[InlineData(ShiftPressed, false, false, true)]
	[InlineData(LeftCtrl | ShiftPressed, true, false, true)]
	[InlineData(LeftCtrl | LeftAlt | ShiftPressed, true, true, true)]
	[InlineData(0, false, false, false)]
	public void ControlKeyState_MapsToModifiers(int cs, bool ctrl, bool alt, bool shift)
	{
		var key = SingleKey(Report(VkO, 'o', cs));

		Assert.Equal(ctrl, key.Modifiers.HasFlag(ConsoleModifiers.Control));
		Assert.Equal(alt, key.Modifiers.HasFlag(ConsoleModifiers.Alt));
		Assert.Equal(shift, key.Modifiers.HasFlag(ConsoleModifiers.Shift));
	}

	#endregion

	#region Key spelling must not change

	/// <summary>
	/// Named keys keep the <see cref="ConsoleKey"/> this library already reports for them.
	/// </summary>
	/// <remarks>
	/// Deliberately NOT the kitty spelling, where Ctrl+H is Ctrl+'h' rather than Backspace. Five
	/// controls test <see cref="ConsoleKey.Backspace"/> directly, and adopters test these keys too,
	/// so a key must not change identity just because the terminal chose a richer encoding.
	/// </remarks>
	[Theory]
	[InlineData(VkReturn, '\r', ConsoleKey.Enter)]
	[InlineData(VkBack, '\b', ConsoleKey.Backspace)]
	[InlineData(VkTab, '\t', ConsoleKey.Tab)]
	[InlineData(VkEscape, '\x1b', ConsoleKey.Escape)]
	[InlineData(VkSpace, ' ', ConsoleKey.Spacebar)]
	[InlineData(VkF1, 0, ConsoleKey.F1)]
	[InlineData(VkLeft, 0, ConsoleKey.LeftArrow)]
	public void NamedKeys_KeepTheirConsoleKey(int vk, char uc, ConsoleKey expected)
	{
		Assert.Equal(expected, SingleKey(Report(vk, uc, 0)).Key);
	}

	/// <summary>
	/// Ctrl+H is Backspace here, as it has always been on this input path -- the modifier does not
	/// change which key was pressed.
	/// </summary>
	[Fact]
	public void CtrlBackspace_IsStillBackspace()
	{
		var key = SingleKey(Report(VkBack, '\b', LeftCtrl));

		Assert.Equal(ConsoleKey.Backspace, key.Key);
		Assert.True(key.Modifiers.HasFlag(ConsoleModifiers.Control));
	}

	/// <summary>Letters and digits report their own key, and the typed character survives.</summary>
	[Theory]
	[InlineData(0x41, 'a', ConsoleKey.A)]
	[InlineData(0x5A, 'z', ConsoleKey.Z)]
	[InlineData(0x30, '0', ConsoleKey.D0)]
	[InlineData(0x39, '9', ConsoleKey.D9)]
	public void LettersAndDigits_ReportTheirKeyAndCharacter(int vk, char uc, ConsoleKey expected)
	{
		var key = SingleKey(Report(vk, uc, 0));

		Assert.Equal(expected, key.Key);
		Assert.Equal(uc, key.KeyChar);
	}

	#endregion

	#region What must not reach the application

	/// <summary>
	/// Key-UP reports are dropped. Without this every keystroke fires twice, which is the loudest
	/// possible regression: typing one character inserts two.
	/// </summary>
	[Fact]
	public void KeyUp_ProducesNoEvent()
	{
		Assert.Empty(Keys(Report(VkO, 'o', 0, kd: 0)));
	}

	/// <summary>A down/up pair is one keystroke, not two.</summary>
	[Fact]
	public void DownThenUp_IsOneKeystroke()
	{
		var keys = Keys(Report(VkO, 'o', 0, kd: 1) + Report(VkO, 'o', 0, kd: 0));

		Assert.Single(keys);
		Assert.Equal(ConsoleKey.O, keys[0].Key);
	}

	/// <summary>
	/// A bare modifier press types nothing and must not surface as a keystroke; the application
	/// learns about Shift from the next real key's modifiers.
	/// </summary>
	[Theory]
	[InlineData(VkShift)]
	[InlineData(VkControl)]
	public void BareModifierPress_ProducesNoEvent(int vk)
	{
		Assert.Empty(Keys(Report(vk, 0, ShiftPressed)));
	}

	#endregion

	#region Malformed reports

	/// <summary>
	/// A report that is not well-formed must not become a keystroke. This is the same rule that
	/// keeps a cursor-position reply from being delivered as F3.
	/// </summary>
	[Theory]
	[InlineData("\x1b[_")]                      // no fields
	[InlineData("\x1b[79_")]                    // too few fields
	[InlineData("\x1b[79;0;111;1_")]            // still too few
	[InlineData("\x1b[79;0;111;1;8;0_")]        // repeat count below 1
	public void MalformedReports_ProduceNoKey(string sequence)
	{
		Assert.Empty(Decode(sequence).OfType<KeyInputEvent>());
	}

	/// <summary>
	/// A letter where a parameter belongs is not a CSI parameter byte at all, so the parser
	/// abandons the sequence upstream of this decoder and the characters re-emerge as the ordinary
	/// typing they resemble -- long-standing behaviour, shared with every other CSI form. The
	/// narrower claim is what matters: no modified keystroke is invented from it.
	/// </summary>
	[Fact]
	public void NonNumericParameters_DoNotInventAModifiedKey()
	{
		var keys = Keys("\x1b[abc;0;111;1;8;1_");

		Assert.Empty(keys.Where(k => k.Modifiers != 0));
	}

	/// <summary>
	/// A report whose Vk maps to nothing and whose character is zero is not a keystroke either --
	/// there is neither a key to name nor a character to insert.
	/// </summary>
	[Fact]
	public void UnmappableReportWithNoCharacter_ProducesNoKey()
	{
		Assert.Empty(Keys(Report(0x00, 0, 0)));
	}

	/// <summary>
	/// A character with no virtual key still types: IME commits and dead-key results arrive this
	/// way, with Vk 0 and a real codepoint.
	/// </summary>
	[Fact]
	public void CharacterWithoutVirtualKey_StillTypes()
	{
		var key = SingleKey(Report(0x00, 'e', 0));

		Assert.Equal('e', key.KeyChar);
	}

	#endregion

	#region Coexistence with the sequences already parsed

	/// <summary>
	/// The APC replies this parser already swallows must keep being swallowed. Both end in
	/// <c>_</c>-adjacent territory, and a graphics reply is not a keystroke.
	/// </summary>
	[Fact]
	public void KittyGraphicsReply_IsStillNotAKeystroke()
	{
		Assert.Empty(Decode("\x1b_Gi=1;OK\x1b\\").OfType<KeyInputEvent>());
	}

	/// <summary>Ordinary CSI keys are untouched by adding a new final byte.</summary>
	[Theory]
	[InlineData("\x1b[A", ConsoleKey.UpArrow)]
	[InlineData("\x1b[5~", ConsoleKey.PageUp)]
	[InlineData("\x1b[1;5A", ConsoleKey.UpArrow)]
	public void OrdinaryCsiKeys_StillDecode(string sequence, ConsoleKey expected)
	{
		Assert.Equal(expected, SingleKey(sequence).Key);
	}

	/// <summary>
	/// All three encodings of Ctrl+Shift+O must agree, so an application written against one
	/// terminal behaves the same on another.
	/// </summary>
	[Fact]
	public void AgreesWithTheExtendedSequences()
	{
		var win32 = SingleKey(Report(VkO, 'O', LeftCtrl | ShiftPressed));
		var modifyOther = SingleKey("\x1b[27;6;79~");
		var csiU = SingleKey("\x1b[111;6u");

		foreach (var k in new[] { win32, modifyOther, csiU })
		{
			Assert.Equal(ConsoleKey.O, k.Key);
			Assert.True(k.Modifiers.HasFlag(ConsoleModifiers.Control));
			Assert.True(k.Modifiers.HasFlag(ConsoleModifiers.Shift));
		}
	}

	#endregion

	#region The Windows sequence reader

	/// <summary>
	/// The Windows reader must stop at <c>_</c>. It breaks on a letter, <c>~</c>, CR or Tab, none of
	/// which a win32 report contains, so without this it reads to its cap on every keystroke and
	/// swallows whatever follows.
	/// </summary>
	[Fact]
	public void WindowsReader_StopsAtTheReportTerminator()
	{
		// What follows the ESC[ of a report, then a key that must NOT be eaten.
		var source = new ScriptedKeys("79;0;111;1;8;1_" + "x");
		var consumed = new List<ConsoleKeyInfo>();

		string sequence = NetConsoleDriver.ReadAnsiSequence(source, consumed);

		Assert.Equal("79;0;111;1;8;1_", sequence);
		Assert.Equal('x', source.Remaining);
	}

	private sealed class ScriptedKeys : NetConsoleDriver.IConsoleKeySource
	{
		private readonly string _script;
		private int _index;

		public ScriptedKeys(string script) => _script = script;

		/// <summary>The first character not consumed, so a reader that overruns is visible.</summary>
		public char Remaining => _index < _script.Length ? _script[_index] : '\0';

		public bool TryReadKey(out ConsoleKeyInfo key)
		{
			if (_index >= _script.Length)
			{
				key = default;
				return false;
			}

			key = new ConsoleKeyInfo(_script[_index++], ConsoleKey.None, false, false, false);
			return true;
		}
	}

	#endregion
}
