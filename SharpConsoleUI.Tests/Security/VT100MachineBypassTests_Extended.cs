// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls.Terminal;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using Xunit;

namespace SharpConsoleUI.Tests.Security
{
	/// <summary>
	/// Extended tests for C1 controls that may NOT be consumed by the VT100
	/// state machine and could end up stored as cell characters.
	/// </summary>
	public class VT100MachineBypassTests_Extended
	{
		[Theory]
		[InlineData(0x80)]  // PAD
		[InlineData(0x81)]  // HOP
		[InlineData(0x82)]  // BPH
		[InlineData(0x83)]  // NBH
		[InlineData(0x86)]  // SSA
		[InlineData(0x87)]  // ESA
		[InlineData(0x88)]  // HTS
		[InlineData(0x89)]  // HTJ
		[InlineData(0x8A)]  // VTS
		[InlineData(0x8B)]  // PLD
		[InlineData(0x8C)]  // PLU
		[InlineData(0x8D)]  // RI
		[InlineData(0x8E)]  // SS2
		[InlineData(0x8F)]  // SS3
		[InlineData(0x91)]  // PU1
		[InlineData(0x92)]  // PU2
		[InlineData(0x93)]  // STS
		[InlineData(0x94)]  // CCH
		[InlineData(0x95)]  // MW
		[InlineData(0x96)]  // SPA
		[InlineData(0x97)]  // EPA
		[InlineData(0x99)]  // (undefined)
		[InlineData(0x9A)]  // SCI
		public void VT100Machine_UnhandledC1Control_DoesNotReachHostBuffer(int c1Value)
		{
			// Encode the C1 control as UTF-8 (all are 0xC2 0x80-0x9F)
			byte hi = 0xC2;
			byte lo = (byte)c1Value;
			var payload = new byte[] { (byte)'A', hi, lo, (byte)'B' };

			var vt = new VT100Machine(40, 5);
			vt.Process(payload);

			var hostBuffer = new CharacterBuffer(40, 5);
			hostBuffer.CopyFrom(vt.Screen, new LayoutRect(0, 0, 40, 5), 0, 0);

			for (int x = 0; x < 40; x++)
			{
				var cell = hostBuffer.GetCell(x, 0);
				int cp = cell.Character.Value;
				if (cp == 0x20 && x > 5) break;

				Assert.False(
					cp >= 0x0080 && cp <= 0x009F,
					$"C1 control U+{cp:X4} (from input U+{c1Value:X4}) stored in host buffer at column {x}");
			}
		}

		[Fact]
		public void VT100Machine_BiDiOverride_DoesNotReachHostBuffer()
		{
			// U+202E (RLO) = UTF-8: E2 80 AE
			var payload = new byte[] { (byte)'H', (byte)'i', 0xE2, 0x80, 0xAE, (byte)'X' };

			var vt = new VT100Machine(40, 5);
			vt.Process(payload);

			var hostBuffer = new CharacterBuffer(40, 5);
			hostBuffer.CopyFrom(vt.Screen, new LayoutRect(0, 0, 40, 5), 0, 0);

			for (int x = 0; x < 40; x++)
			{
				var cell = hostBuffer.GetCell(x, 0);
				Assert.False(
					TextSanitizer.IsUnsafeRune(cell.Character) &&
					cell.Character != new System.Text.Rune(' '),
					$"Unsafe rune U+{cell.Character.Value:X4} at column {x} in host buffer");
			}
		}
	}
}
