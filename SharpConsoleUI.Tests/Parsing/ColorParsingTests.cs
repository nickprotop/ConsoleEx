using Xunit;
using SharpConsoleUI;

namespace SharpConsoleUI.Tests.Parsing
{
	public class ColorParsingTests
	{
		#region TryFromName

		[Theory]
		[InlineData("red", 255, 0, 0)]
		[InlineData("blue", 0, 0, 255)]
		[InlineData("green", 0, 128, 0)]
		[InlineData("white", 255, 255, 255)]
		[InlineData("black", 0, 0, 0)]
		[InlineData("yellow", 255, 255, 0)]
		public void TryFromName_BasicColors_ReturnsCorrectRgb(string name, byte r, byte g, byte b)
		{
			Assert.True(Color.TryFromName(name, out var color));
			Assert.Equal(new Color(r, g, b), color);
		}

		[Theory]
		[InlineData("RED")]
		[InlineData("Red")]
		[InlineData("rEd")]
		public void TryFromName_CaseInsensitive_Succeeds(string name)
		{
			Assert.True(Color.TryFromName(name, out var color));
			Assert.Equal(Color.Red, color);
		}

		[Fact]
		public void TryFromName_WithUnderscores_Succeeds()
		{
			// "dark_red" → normalized to "darkred" → xterm #52 (95,0,0)
			Assert.True(Color.TryFromName("dark_red", out var color));
			Assert.Equal(new Color(95, 0, 0), color);
		}

		[Theory]
		[InlineData("grey50")]
		[InlineData("gray50")]
		public void TryFromName_GreyGrayAliases_ReturnSameColor(string name)
		{
			Assert.True(Color.TryFromName(name, out var color));
			Assert.Equal(Color.Grey50, color);
		}

		[Fact]
		public void TryFromName_ExtendedColor_SteelBlue()
		{
			Assert.True(Color.TryFromName("steelblue", out var color));
			Assert.Equal(Color.SteelBlue, color);
		}

		[Fact]
		public void TryFromName_ExtendedColor_Cyan1()
		{
			Assert.True(Color.TryFromName("cyan1", out var color));
			Assert.Equal(Color.Cyan1, color);
		}

		[Fact]
		public void TryFromName_InvalidName_ReturnsFalse()
		{
			Assert.False(Color.TryFromName("notacolor", out _));
		}

		[Fact]
		public void TryFromName_EmptyString_ReturnsFalse()
		{
			Assert.False(Color.TryFromName("", out _));
		}

		[Fact]
		public void TryFromName_Whitespace_ReturnsFalse()
		{
			Assert.False(Color.TryFromName("   ", out _));
		}

		#endregion

		#region TryFromHex

		[Fact]
		public void TryFromHex_6Digit_WithHash()
		{
			Assert.True(Color.TryFromHex("#FF0000", out var color));
			Assert.Equal(new Color(255, 0, 0), color);
		}

		[Fact]
		public void TryFromHex_6Digit_WithoutHash()
		{
			Assert.True(Color.TryFromHex("FF0000", out var color));
			Assert.Equal(new Color(255, 0, 0), color);
		}

		[Fact]
		public void TryFromHex_3Digit_ExpandsViaNibbleTimes17()
		{
			Assert.True(Color.TryFromHex("#F00", out var color));
			Assert.Equal(new Color(255, 0, 0), color);
		}

		[Fact]
		public void TryFromHex_Lowercase()
		{
			Assert.True(Color.TryFromHex("#ff00ff", out var color));
			Assert.Equal(new Color(255, 0, 255), color);
		}

		[Fact]
		public void TryFromHex_InvalidChars_ReturnsFalse()
		{
			Assert.False(Color.TryFromHex("#GGG", out _));
		}

		[Fact]
		public void TryFromHex_WrongLength_ReturnsFalse()
		{
			Assert.False(Color.TryFromHex("#FFFF", out _));
		}

		[Fact]
		public void TryFromHex_EmptyString_ReturnsFalse()
		{
			Assert.False(Color.TryFromHex("", out _));
		}

		[Fact]
		public void TryFromHex_Null_ReturnsFalse()
		{
			Assert.False(Color.TryFromHex(null!, out _));
		}

		[Fact]
		public void TryFromHex_3Digit_MixedCase()
		{
			Assert.True(Color.TryFromHex("#fA3", out var color));
			// f=15*17=255, A=10*17=170, 3=3*17=51
			Assert.Equal(new Color(255, 170, 51), color);
		}

		#endregion

		#region FromInt32

		[Theory]
		[InlineData(0, 0, 0, 0)]       // Black
		[InlineData(1, 128, 0, 0)]     // Maroon
		[InlineData(9, 255, 0, 0)]     // Red (bright)
		[InlineData(15, 255, 255, 255)] // White (bright)
		public void FromInt32_BasicColors_CorrectRgb(int index, byte r, byte g, byte b)
		{
			var color = Color.FromInt32(index);
			Assert.Equal(new Color(r, g, b), color);
		}

		[Fact]
		public void FromInt32_CubeCorner16_IsBlack()
		{
			// Index 16 = first cube entry (0,0,0)
			var color = Color.FromInt32(16);
			Assert.Equal(new Color(0, 0, 0), color);
		}

		[Fact]
		public void FromInt32_CubeCorner21_Blue()
		{
			// Index 21 = (0,0,5) → r=0, g=0, b=55+5*40=255
			var color = Color.FromInt32(21);
			Assert.Equal(new Color(0, 0, 255), color);
		}

		[Fact]
		public void FromInt32_CubeCorner196_Red()
		{
			// Index 196 = 196-16=180, r=180/36=5, g=(180%36)/6=0, b=180%6=0
			// r=55+5*40=255, g=0, b=0
			var color = Color.FromInt32(196);
			Assert.Equal(new Color(255, 0, 0), color);
		}

		[Fact]
		public void FromInt32_CubeCorner231_White()
		{
			// Index 231 = 231-16=215, r=215/36=5, g=(215%36)/6=5, b=215%6=5
			// All = 55+5*40=255
			var color = Color.FromInt32(231);
			Assert.Equal(new Color(255, 255, 255), color);
		}

		[Fact]
		public void FromInt32_Grayscale232_DarkGray()
		{
			// Index 232 → 8 + (232-232)*10 = 8
			var color = Color.FromInt32(232);
			Assert.Equal(new Color(8, 8, 8), color);
		}

		[Fact]
		public void FromInt32_Grayscale243_MidGray()
		{
			// Index 243 → 8 + (243-232)*10 = 118
			var color = Color.FromInt32(243);
			Assert.Equal(new Color(118, 118, 118), color);
		}

		[Fact]
		public void FromInt32_Grayscale255_LightGray()
		{
			// Index 255 → 8 + (255-232)*10 = 238
			var color = Color.FromInt32(255);
			Assert.Equal(new Color(238, 238, 238), color);
		}

		[Fact]
		public void FromInt32_NegativeIndex_ThrowsArgumentOutOfRange()
		{
			Assert.Throws<ArgumentOutOfRangeException>(() => Color.FromInt32(-1));
		}

		[Fact]
		public void FromInt32_Index256_ThrowsArgumentOutOfRange()
		{
			Assert.Throws<ArgumentOutOfRangeException>(() => Color.FromInt32(256));
		}

		#endregion

		#region Alpha

		[Fact]
		public void Color_DefaultAlpha_Is255()
		{
			var c = new Color(100, 150, 200);
			Assert.Equal(255, c.A);
		}

		[Fact]
		public void Color_ExplicitAlpha_Stored()
		{
			var c = new Color(100, 150, 200, 128);
			Assert.Equal(128, c.A);
		}

		[Fact]
		public void Color_Equals_IncludesAlpha()
		{
			var opaque = new Color(255, 0, 0, 255);
			var halfTransparent = new Color(255, 0, 0, 128);
			Assert.NotEqual(opaque, halfTransparent);
		}

		[Fact]
		public void TryFromHex_EightDigit_ParsesAlpha()
		{
			Assert.True(Color.TryFromHex("#FF0000FF", out var full));
			Assert.Equal(255, full.A);

			Assert.True(Color.TryFromHex("#FF000080", out var half));
			Assert.Equal(128, half.A);
		}

		[Fact]
		public void Color_ToString_IncludesAlpha_WhenNotOpaque()
		{
			var c = new Color(10, 20, 30, 128);
			Assert.Contains("128", c.ToString());
		}

		[Fact]
		public void Color_ToString_OmitsAlpha_WhenOpaque()
		{
			var c = new Color(10, 20, 30);
			Assert.Equal("Color(10, 20, 30)", c.ToString());
		}

		[Fact]
		public void Transparent_IsNotDefault_AndHasZeroAlpha()
		{
			Assert.Equal(0, Color.Transparent.A);
			Assert.Equal(0, Color.Transparent.R);
			Assert.Equal(0, Color.Transparent.G);
			Assert.Equal(0, Color.Transparent.B);
			Assert.False(Color.Transparent.IsDefault);
		}

		[Fact]
		public void GetHashCode_DiffersForDifferentAlpha()
		{
			var opaque = new Color(100, 150, 200, 255);
			var half = new Color(100, 150, 200, 128);
			Assert.NotEqual(opaque.GetHashCode(), half.GetHashCode());
		}

		[Fact]
		public void GetHashCode_Default_ReturnsMinusOne()
		{
			Assert.Equal(-1, Color.Default.GetHashCode());
		}

		[Fact]
		public void WithAlpha_ChangesAlphaPreservesRgb()
		{
			var c = new Color(10, 20, 30, 255);
			var result = c.WithAlpha(128);
			Assert.Equal(128, result.A);
			Assert.Equal(10, result.R);
			Assert.Equal(20, result.G);
			Assert.Equal(30, result.B);
		}

		[Fact]
		public void ToMarkup_AlphaPath_EmitsRgba()
		{
			var c = new Color(255, 0, 0, 128);
			var markup = c.ToMarkup();
			Assert.StartsWith("rgba(", markup);
			Assert.Contains("255,0,0", markup);
		}

		[Fact]
		public void ToMarkup_OpaqueColor_EmitsRgb()
		{
			var c = new Color(255, 0, 0, 255);
			Assert.Equal("rgb(255,0,0)", c.ToMarkup());
		}

		[Fact]
		public void TryFromHex_EightDigit_ZeroAlpha_ParsesTransparent()
		{
			Assert.True(Color.TryFromHex("#FF000000", out var color));
			Assert.Equal(255, color.R);
			Assert.Equal(0, color.A);
		}

		#endregion

		#region Xterm-256 Named Colors

		[Theory]
		[InlineData("red1", 255, 0, 0)]
		[InlineData("blue1", 0, 0, 255)]
		[InlineData("green1", 0, 255, 0)]
		[InlineData("yellow1", 255, 255, 0)]
		public void TryFromName_Xterm256_BasicBright_ReturnsCorrectRgb(string name, byte r, byte g, byte b)
		{
			Assert.True(Color.TryFromName(name, out var color));
			Assert.Equal(new Color(r, g, b), color);
		}

		[Theory]
		[InlineData("blue3", 0, 0, 175)]
		[InlineData("red3", 175, 0, 0)]
		[InlineData("gold31", 175, 175, 0)]
		[InlineData("green3", 0, 175, 0)]
		[InlineData("yellow3", 175, 215, 0)]
		public void TryFromName_Xterm256_NumberedVariants_ReturnsCorrectRgb(string name, byte r, byte g, byte b)
		{
			Assert.True(Color.TryFromName(name, out var color));
			Assert.Equal(new Color(r, g, b), color);
		}

		[Fact]
		public void TryFromName_Xterm256_UnderscoreTolerance_Blue3_1()
		{
			// "blue3_1" normalizes to "blue31", same as "blue31"
			Assert.True(Color.TryFromName("blue3_1", out var withUnderscore));
			Assert.True(Color.TryFromName("blue31", out var withoutUnderscore));
			Assert.Equal(withUnderscore, withoutUnderscore);
			Assert.Equal(new Color(0, 0, 215), withUnderscore);
		}

		[Theory]
		[InlineData("navyblue", 0, 0, 95)]
		[InlineData("orangered1", 255, 95, 0)]
		[InlineData("cornsilk1", 255, 255, 215)]
		[InlineData("springgreen4", 0, 135, 95)]
		[InlineData("turquoise4", 0, 135, 135)]
		[InlineData("magenta3", 175, 0, 175)]
		[InlineData("plum4", 135, 95, 135)]
		[InlineData("lightslateblue", 135, 135, 255)]
		public void TryFromName_Xterm256_NewColors_ReturnsCorrectRgb(string name, byte r, byte g, byte b)
		{
			Assert.True(Color.TryFromName(name, out var color));
			Assert.Equal(new Color(r, g, b), color);
		}

		[Theory]
		[InlineData("gold3_1", 175, 175, 0)]
		[InlineData("red3_1", 215, 0, 0)]
		[InlineData("deeppink3_1", 215, 0, 135)]
		public void TryFromName_Xterm256_UnderscoreVariants_ResolveCorrectly(string name, byte r, byte g, byte b)
		{
			Assert.True(Color.TryFromName(name, out var color));
			Assert.Equal(new Color(r, g, b), color);
		}

		#endregion
	}
}
