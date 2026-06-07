using System.Text;
using SharpConsoleUI.Drivers.Input;
using Xunit;

namespace SharpConsoleUI.Tests.Drivers;

public class BracketedPasteParserTests
{
    private static List<InputEvent> Parse(string ascii)
    {
        var parser = new AnsiInputParser();
        var bytes = Encoding.UTF8.GetBytes(ascii);
        return parser.Parse(bytes.AsSpan(), bytes.Length);
    }

    [Fact]
    public void Paste_DeliversSingleEvent_NoPerCharKeys()
    {
        var events = Parse("\x1b[200~hello\nworld\x1b[201~");
        var pastes = events.OfType<PasteInputEvent>().ToList();
        Assert.Single(pastes);
        Assert.Equal("hello\nworld", pastes[0].Text);
        Assert.Empty(events.OfType<KeyInputEvent>());
    }

    [Fact]
    public void Paste_ContentWithCsiLikeBytes_IsLiteral()
    {
        var events = Parse("\x1b[200~a[1;5B~c\x1b[201~");
        var pastes = events.OfType<PasteInputEvent>().ToList();
        Assert.Single(pastes);
        Assert.Equal("a[1;5B~c", pastes[0].Text);
    }

    [Fact]
    public void Paste_SplitAcrossChunks_Reassembles()
    {
        var parser = new AnsiInputParser();
        var all = new List<InputEvent>();
        foreach (var chunk in new[] { "\x1b[2", "00~hel", "lo\x1b[20", "1~" })
        {
            var b = Encoding.UTF8.GetBytes(chunk);
            all.AddRange(parser.Parse(b.AsSpan(), b.Length));
        }
        var pastes = all.OfType<PasteInputEvent>().ToList();
        Assert.Single(pastes);
        Assert.Equal("hello", pastes[0].Text);
    }

    [Fact]
    public void Paste_Empty_DeliversEmptyText()
    {
        var events = Parse("\x1b[200~\x1b[201~");
        Assert.Single(events.OfType<PasteInputEvent>());
        Assert.Equal("", events.OfType<PasteInputEvent>().Single().Text);
    }

    [Fact]
    public void Bare_EndMarker_NoOpenPaste_IsIgnored()
    {
        var events = Parse("\x1b[201~");
        Assert.Empty(events.OfType<PasteInputEvent>());
    }

    [Fact]
    public void NormalKeysAfterPaste_StillParse()
    {
        var events = Parse("\x1b[200~x\x1b[201~A");
        Assert.Single(events.OfType<PasteInputEvent>());
        Assert.Contains(events.OfType<KeyInputEvent>(), k => k.KeyInfo.KeyChar == 'A');
    }

    [Fact]
    public void UnterminatedPaste_FlushesAccumulatedAndResets()
    {
        var parser = new AnsiInputParser();
        var open = Encoding.UTF8.GetBytes("\x1b[200~partial");
        parser.Parse(open.AsSpan(), open.Length); // no end marker

        var flushed = parser.Flush();
        var paste = Assert.Single(flushed.OfType<PasteInputEvent>());
        Assert.Equal("partial", paste.Text);

        // Parser recovered to Ground: a normal key after flush parses correctly.
        var after = Encoding.UTF8.GetBytes("A");
        var ev = parser.Parse(after.AsSpan(), after.Length);
        Assert.Contains(ev.OfType<KeyInputEvent>(), k => k.KeyInfo.KeyChar == 'A');
    }

    [Fact]
    public void Paste_Utf8Content_Decodes()
    {
        var parser = new AnsiInputParser();
        var bytes = Encoding.UTF8.GetBytes("\x1b[200~café 漢字\x1b[201~");
        var events = parser.Parse(bytes.AsSpan(), bytes.Length);
        Assert.Equal("café 漢字", events.OfType<PasteInputEvent>().Single().Text);
    }
}
