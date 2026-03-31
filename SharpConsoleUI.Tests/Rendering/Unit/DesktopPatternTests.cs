using SharpConsoleUI.Rendering;
using Spectre.Console;
using System.Reflection;
using Xunit;

namespace SharpConsoleUI.Tests.Rendering.Unit;

public class DesktopPatternTests
{
    #region Preset Dimensions

    public static IEnumerable<object[]> PresetDimensionData =>
    [
        ["Checkerboard",    2, 2],
        ["Dots",            3, 3],
        ["HatchDown",       3, 3],
        ["HatchUp",         3, 3],
        ["Crosshatch",      3, 3],
        ["LightShade",      1, 1],
        ["MediumShade",     1, 1],
        ["DenseShade",      1, 1],
        ["HorizontalLines", 1, 2],
        ["VerticalLines",   3, 1],
        ["Grid",            3, 3],
    ];

    [Theory]
    [MemberData(nameof(PresetDimensionData))]
    public void Preset_HasExpectedDimensions(string presetName, int expectedWidth, int expectedHeight)
    {
        var prop = typeof(DesktopPatterns).GetProperty(presetName, BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(prop);

        var pattern = (DesktopPattern)prop.GetValue(null)!;

        Assert.Equal(expectedWidth, pattern.Width);
        Assert.Equal(expectedHeight, pattern.Height);
    }

    #endregion

    #region Checkerboard Character Positions

    [Fact]
    public void Checkerboard_AlternatesCharacters()
    {
        var pattern = DesktopPatterns.Checkerboard;

        // Row 0: '░', ' '
        Assert.Equal('░', pattern.Characters[0, 0]);
        Assert.Equal(' ', pattern.Characters[0, 1]);
        // Row 1: ' ', '░'
        Assert.Equal(' ', pattern.Characters[1, 0]);
        Assert.Equal('░', pattern.Characters[1, 1]);
    }

    #endregion

    #region All Presets Non-Empty

    [Fact]
    public void AllPresets_HaveNonEmptyCharacters()
    {
        var props = typeof(DesktopPatterns)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(DesktopPattern));

        foreach (var prop in props)
        {
            var pattern = (DesktopPattern)prop.GetValue(null)!;

            Assert.True(pattern.Width > 0, $"{prop.Name} should have Width > 0");
            Assert.True(pattern.Height > 0, $"{prop.Name} should have Height > 0");

            // Verify at least one non-null character exists
            bool hasChar = false;
            for (int row = 0; row < pattern.Height; row++)
                for (int col = 0; col < pattern.Width; col++)
                    if (pattern.Characters[row, col] != '\0')
                        hasChar = true;

            Assert.True(hasChar, $"{prop.Name} should have at least one non-null character");
        }
    }

    #endregion

    #region Custom Pattern with Colors

    [Fact]
    public void CustomPattern_WithColors()
    {
        var chars = new char[2, 2]
        {
            { 'X', 'O' },
            { 'O', 'X' }
        };
        var fg = new Color?[2, 2]
        {
            { Color.Red,  null       },
            { null,       Color.Blue }
        };
        var bg = new Color?[2, 2]
        {
            { Color.Black, Color.White },
            { Color.White, Color.Black }
        };

        var pattern = new DesktopPattern(chars)
        {
            ForegroundColors = fg,
            BackgroundColors = bg
        };

        Assert.Equal(2, pattern.Width);
        Assert.Equal(2, pattern.Height);

        Assert.NotNull(pattern.ForegroundColors);
        Assert.NotNull(pattern.BackgroundColors);

        Assert.Equal(Color.Red,   pattern.ForegroundColors![0, 0]);
        Assert.Null(pattern.ForegroundColors[0, 1]);
        Assert.Equal(Color.Blue,  pattern.ForegroundColors[1, 1]);

        Assert.Equal(Color.Black, pattern.BackgroundColors![0, 0]);
        Assert.Equal(Color.White, pattern.BackgroundColors[0, 1]);
    }

    [Fact]
    public void CustomPattern_NullColorArraysByDefault()
    {
        var pattern = new DesktopPattern(new char[1, 1] { { '.' } });

        Assert.Null(pattern.ForegroundColors);
        Assert.Null(pattern.BackgroundColors);
    }

    #endregion

    #region Grid Preset Character Verification

    [Fact]
    public void Grid_HasCorrectCornerAndEdgeChars()
    {
        var pattern = DesktopPatterns.Grid;

        // Top-left corner is intersection character
        Assert.Equal('┼', pattern.Characters[0, 0]);
        // Top row has horizontal lines
        Assert.Equal('─', pattern.Characters[0, 1]);
        Assert.Equal('─', pattern.Characters[0, 2]);
        // Left column has vertical lines
        Assert.Equal('│', pattern.Characters[1, 0]);
        Assert.Equal('│', pattern.Characters[2, 0]);
    }

    #endregion
}
