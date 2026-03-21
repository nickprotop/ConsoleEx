using Xunit;
using SharpConsoleUI;

namespace SharpConsoleUI.Tests.Rendering.Unit.TopLayer;

public class ColorAlphaTests
{
    [Fact]
    public void Blend_FullyOpaqueSrc_ReturnsSrc()
    {
        var src = new Color(255, 0, 0, 255);
        var dst = new Color(0, 0, 255, 255);
        Assert.Equal(src, Color.Blend(src, dst));
    }

    [Fact]
    public void Blend_FullyTransparentSrc_ReturnsDst()
    {
        var dst = new Color(0, 0, 255, 255);
        Assert.Equal(dst, Color.Blend(Color.Transparent, dst));
    }

    [Fact]
    public void Blend_DefaultDst_ReturnsSrc()
    {
        var src = new Color(255, 0, 0, 128);
        Assert.Equal(src, Color.Blend(src, Color.Default));
    }

    [Fact]
    public void Blend_50Percent_MidpointColor()
    {
        var src = new Color(200, 100, 0, 128);
        var dst = new Color(0, 100, 200, 255);
        var result = Color.Blend(src, dst);

        Assert.InRange(result.R, 98, 101);
        Assert.InRange(result.G, 99, 101);
        Assert.InRange(result.B, 98, 101);
        Assert.Equal(255, result.A);
    }

    [Fact]
    public void Blend_AlphaOne_AlmostTransparent()
    {
        var src = new Color(255, 0, 0, 1);
        var dst = new Color(0, 0, 255, 255);
        var result = Color.Blend(src, dst);
        Assert.True(result.B > 250);
        Assert.Equal(255, result.A);
    }

    [Fact]
    public void Blend_Alpha254_AlmostOpaque()
    {
        var src = new Color(255, 0, 0, 254);
        var dst = new Color(0, 0, 255, 255);
        var result = Color.Blend(src, dst);
        Assert.True(result.R > 250);
        Assert.Equal(255, result.A);
    }

    [Fact]
    public void Blend_ResultIsAlwaysFullyOpaque()
    {
        var src = new Color(100, 100, 100, 128);
        var dst = new Color(200, 200, 200, 255);
        var result = Color.Blend(src, dst);
        Assert.Equal(255, result.A);
    }
}
