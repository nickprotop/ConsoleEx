// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text;
using SharpConsoleUI.Controls.Terminal;
using SharpConsoleUI.Drawing;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Security
{
	/// <summary>
	/// Demonstrates that C1 control characters (U+0080-U+009F) bypass the
	/// TextSanitizer boundary when sent through the VT100Machine -> CopyFrom
	/// path. These tests prove the vulnerability exists so a fix can be verified.
	/// </summary>
	public class VT100MachineBypassTests
	{
		[Fact]
		public void VT100Machine_C1CsiViaUtf8_ReachesScreenBuffer()
		{
			// U+009B (CSI as C1 control) encoded as UTF-8: 0xC2 0x9B
			// Followed by "31m" which would be "set foreground red"
			// Then visible text, then reset
			var payload = new byte[]
			{
				(byte)'A',
				0xC2, 0x9B,  // U+009B = CSI via C1
				(byte)'3', (byte)'1', (byte)'m',  // SGR red
				(byte)'B',
			};

			var vt = new VT100Machine(40, 5);
			vt.Process(payload);

			// The VT100Machine may consume the CSI internally (processed as a real CSI sequence
			// and SGR applied) or store the raw C1 char — either is acceptable on the screen
			// buffer. What matters is the security boundary: verify no C1 controls reach the
			// host buffer via CopyFrom.
			var hostBuffer = new CharacterBuffer(40, 5);
			hostBuffer.CopyFrom(vt.Screen, new LayoutRect(0, 0, 40, 5), 0, 0);

			for (int x = 0; x < 40; x++)
			{
				var cell = hostBuffer.GetCell(x, 0);
				Assert.False(
					TextSanitizer.IsUnsafeRune(cell.Character) && cell.Character.Value != ' ',
					$"Unsafe rune U+{cell.Character.Value:X4} at column {x} reached host buffer via CopyFrom");
			}
		}

		[Fact]
		public void VT100Machine_BiDiOverrideViaUtf8_ReachesScreenBuffer()
		{
			// U+202E (Right-to-Left Override) in UTF-8: 0xE2 0x80 0xAE
			var payload = new byte[]
			{
				(byte)'a', (byte)'d', (byte)'m', (byte)'i', (byte)'n',
				0xE2, 0x80, 0xAE,  // U+202E RLO
				(byte)'r', (byte)'e', (byte)'s', (byte)'u',
			};

			var vt = new VT100Machine(40, 5);
			vt.Process(payload);

			var hostBuffer = new CharacterBuffer(40, 5);
			hostBuffer.CopyFrom(vt.Screen, new LayoutRect(0, 0, 40, 5), 0, 0);

			// Check for BiDi override in host buffer
			for (int x = 0; x < 40; x++)
			{
				var cell = hostBuffer.GetCell(x, 0);
				int cp = cell.Character.Value;
				Assert.False(
					cp >= 0x202A && cp <= 0x202E,
					$"BiDi override U+{cp:X4} at column {x} reached host buffer (Trojan Source risk)");
			}
		}

		[Fact]
		public void VT100Machine_OscViaC1_DoesNotReachHostBuffer()
		{
			// U+009D (OSC as C1) followed by clipboard set payload
			// UTF-8: 0xC2 0x9D
			var payload = new byte[]
			{
				(byte)'H', (byte)'i',
				0xC2, 0x9D,  // U+009D = OSC
				(byte)'5', (byte)'2', (byte)';', (byte)'c', (byte)';',
				(byte)'S', (byte)'G', (byte)'V', (byte)'s',  // base64 "Hes"
				0xC2, 0x9C,  // U+009C = ST (String Terminator)
				(byte)'X',
			};

			var vt = new VT100Machine(40, 5);
			vt.Process(payload);

			var hostBuffer = new CharacterBuffer(40, 5);
			hostBuffer.CopyFrom(vt.Screen, new LayoutRect(0, 0, 40, 5), 0, 0);

			for (int x = 0; x < 40; x++)
			{
				var cell = hostBuffer.GetCell(x, 0);
				Assert.False(
					cell.Character.Value >= 0x0080 && cell.Character.Value <= 0x009F,
					$"C1 control U+{cell.Character.Value:X4} at column {x} reached host buffer");
			}
		}
	}
}
