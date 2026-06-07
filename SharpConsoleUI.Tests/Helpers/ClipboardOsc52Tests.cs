using SharpConsoleUI.Helpers;
using Xunit;

namespace SharpConsoleUI.Tests.Helpers;

[Collection("EnvSerial")]
public class ClipboardOsc52Tests : IDisposable
{
    private readonly List<string> _emitted = new();

    public ClipboardOsc52Tests()
    {
        ClipboardHelper.ForceBackendForTests(ClipboardBackend.InternalFallback);
        ClipboardHelper.RegisterOsc52Emitter(s => _emitted.Add(s));
        ClipboardHelper.Osc52Mode = Osc52Mode.Auto;
        TerminalCapabilities.SetOsc52Override(null);
    }

    public void Dispose()
    {
        ClipboardHelper.RegisterOsc52Emitter(null);
        ClipboardHelper.Osc52Mode = Osc52Mode.Auto;
        TerminalCapabilities.SetOsc52Override(null);
        ClipboardHelper.MaxOsc52Bytes = Osc52.DefaultMaxBytes;
    }

    [Fact]
    public void Auto_WithOsc52Supported_EmitsAndSetsLocalBuffer()
    {
        TerminalCapabilities.SetOsc52Override(true);
        ClipboardHelper.SetText("hello");
        Assert.Single(_emitted);
        Assert.Contains("\x1b]52;c;", _emitted[0]);
        Assert.Equal("hello", ClipboardHelper.GetText());
    }

    [Fact]
    public void Disabled_NeverEmits_ButLocalStillSet()
    {
        ClipboardHelper.Osc52Mode = Osc52Mode.Disabled;
        ClipboardHelper.SetText("hello");
        Assert.Empty(_emitted);
        Assert.Equal("hello", ClipboardHelper.GetText());
    }

    [Fact]
    public void Enabled_OverridesUnsupported()
    {
        ClipboardHelper.Osc52Mode = Osc52Mode.Enabled;
        TerminalCapabilities.SetOsc52Override(false);
        ClipboardHelper.SetText("hi");
        Assert.Single(_emitted);
    }

    [Fact]
    public void Auto_WhenOsc52Unsupported_DoesNotEmit_ButLocalStillSet()
    {
        TerminalCapabilities.SetOsc52Override(false);
        ClipboardHelper.SetText("hi");
        Assert.Empty(_emitted);
        Assert.Equal("hi", ClipboardHelper.GetText());
    }

    [Fact]
    public void OverCap_DoesNotEmit_ButLocalStillSet()
    {
        TerminalCapabilities.SetOsc52Override(true);
        ClipboardHelper.MaxOsc52Bytes = 8;
        ClipboardHelper.SetText(new string('x', 1000));
        Assert.Empty(_emitted);
        Assert.Equal(new string('x', 1000), ClipboardHelper.GetText());
    }

    [Fact]
    public void EmitterThrows_SetTextStillSucceeds()
    {
        TerminalCapabilities.SetOsc52Override(true);
        ClipboardHelper.RegisterOsc52Emitter(_ => throw new InvalidOperationException("boom"));
        ClipboardHelper.SetText("safe");
        Assert.Equal("safe", ClipboardHelper.GetText());
    }
}
