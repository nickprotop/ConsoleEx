using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Rendering;

namespace DemoApp.DemoWindows;

internal static class InternationalWindow
{
    public static Window Create(ConsoleWindowSystem ws)
    {
        var content = Controls.ScrollablePanel()
            // Header
            .AddControl(Controls.Markup(
                "[bold underline gradient=spectrum]International Characters & Emoji Showcase[/]")
                .Centered().Build())
            .AddControl(Controls.Markup(
                "[dim]Demonstrating surrogate pairs, wide characters, and rich markup[/]")
                .Centered().Build())

            // === CJK Ideographs ===
            .AddControl(Controls.Rule("CJK Ideographs"))
            .AddControl(Controls.Markup(
                "  [bold yellow]Chinese:[/] " +
                "\u4f60\u597d\u4e16\u754c " +
                "[red]\u7f8e\u4e3d[/] [green]\u5fae\u7b11[/] [cyan]\u6c38\u8fdc[/] " +
                "[bold #FFD700]\u9f99\u51e4\u5448\u7965[/]").Build())
            .AddControl(Controls.Markup(
                "  [bold yellow]Japanese:[/] " +
                "[magenta]\u3053\u3093\u306b\u3061\u306f[/] " +
                "[#FF69B4]\u30b5\u30af\u30e9[/] " +
                "[bold cyan]\u6771\u4eac[/] [italic]\u5bcc\u58eb\u5c71[/] " +
                "\u6f22\u5b57\u3068\u304b\u306a\u3068\u30ab\u30bf\u30ab\u30ca").Build())
            .AddControl(Controls.Markup(
                "  [bold yellow]Korean:[/] " +
                "[green]\uc548\ub155\ud558\uc138\uc694[/] " +
                "[bold blue]\ub300\ud55c\ubbfc\uad6d[/] " +
                "\ud55c\uae00 [underline]\uc11c\uc6b8[/] " +
                "[italic #87CEEB]\ubd04\uc5ec\ub984\uac00\uc744\uaca8\uc6b8[/]").Build())
            .AddControl(Controls.Markup(
                "  [dim]CJK Extension B (surrogate pairs):[/] " +
                "\U00020000 \U00020001 \U00020002 \U00020003 \U00020005 " +
                "[bold red]\U00020006[/] [green]\U00020008[/] [cyan]\U0002000A[/]").Build())

            // === Scripts of the World ===
            .AddControl(Controls.Rule("Scripts of the World"))
            .AddControl(Controls.Markup(
                "  [bold #CD853F]Arabic:[/] " +
                "[#DEB887]\u0645\u0631\u062d\u0628\u0627 \u0628\u0627\u0644\u0639\u0627\u0644\u0645[/] " +
                "  [bold #CD853F]Hebrew:[/] " +
                "[#F0E68C]\u05e9\u05dc\u05d5\u05dd \u05e2\u05d5\u05dc\u05dd[/]").Build())
            .AddControl(Controls.Markup(
                "  [bold #FF6347]Devanagari:[/] " +
                "[#FFA07A]\u0928\u092e\u0938\u094d\u0924\u0947 \u0926\u0941\u0928\u093f\u092f\u093e[/] " +
                "  [bold #FF6347]Thai:[/] " +
                "[#FFB6C1]\u0e2a\u0e27\u0e31\u0e2a\u0e14\u0e35\u0e04\u0e23\u0e31\u0e1a[/]").Build())
            .AddControl(Controls.Markup(
                "  [bold #9370DB]Georgian:[/] " +
                "[#DDA0DD]\u10d2\u10d0\u10db\u10d0\u10e0\u10ef\u10dd\u10d1\u10d0[/] " +
                "  [bold #9370DB]Armenian:[/] " +
                "[#E6E6FA]\u0532\u0561\u0580\u0565\u0582 \u0561\u0577\u056d\u0561\u0580\u0570[/]").Build())
            .AddControl(Controls.Markup(
                "  [bold #4682B4]Greek:[/] " +
                "[underline #87CEEB]\u0393\u03b5\u03b9\u03ac \u03c3\u03bf\u03c5 \u039a\u03cc\u03c3\u03bc\u03b5[/] " +
                "  [bold #4682B4]Cyrillic:[/] " +
                "[italic #B0C4DE]\u041f\u0440\u0438\u0432\u0435\u0442 \u043c\u0438\u0440[/]").Build())

            // === Emoji: Wide (Surrogate Pairs) ===
            .AddControl(Controls.Rule("Emoji \u2014 Wide (Surrogate Pairs)"))
            .AddControl(Controls.Markup(
                "  [bold]Faces:[/]  " +
                "\U0001F600 \U0001F602 \U0001F60D \U0001F914 \U0001F92F \U0001F973 \U0001F976 \U0001F60E " +
                "\U0001F644 \U0001F62D \U0001F621 \U0001F634").Build())
            .AddControl(Controls.Markup(
                "  [bold]Hands:[/]  " +
                "\U0001F44D \U0001F44E \U0001F44B \U0001F91D \U0001F64F \U0001F4AA " +
                "\u270C\uFE0F \U0001F448 \U0001F449 \U0001F446").Build())
            .AddControl(Controls.Markup(
                "  [bold]Animals:[/] " +
                "\U0001F436 \U0001F431 \U0001F98A \U0001F43B \U0001F427 \U0001F40D " +
                "\U0001F985 \U0001F994 \U0001F41D \U0001F422").Build())
            .AddControl(Controls.Markup(
                "  [bold]Food:[/]   " +
                "\U0001F355 \U0001F363 \U0001F370 \U0001F354 \U0001F32E \U0001F96A " +
                "\U0001F377 \U0001F375 \U0001F382 \U0001F36D").Build())
            .AddControl(Controls.Markup(
                "  [bold]Travel:[/] " +
                "\U0001F680 \U0001F6F8 \u2708\uFE0F \U0001F697 \U0001F6B2 \U0001F3D4\uFE0F " +
                "\U0001F30D \U0001F3D6\uFE0F \U0001F3E0 \U0001F5FC").Build())
            .AddControl(Controls.Markup(
                "  [bold]Symbols:[/] " +
                "\U0001F4A1 \U0001F525 \u2764\uFE0F \u2B50 \U0001F308 \u26A1 " +
                "\U0001F4A5 \U0001F4AB \U0001F3B5 \U0001F3B6").Build())
            .AddControl(Controls.Markup(
                "  [bold]Flags:[/]  " +
                "\U0001F1FA\U0001F1F8 \U0001F1EC\U0001F1E7 \U0001F1EF\U0001F1F5 " +
                "\U0001F1E9\U0001F1EA \U0001F1EB\U0001F1F7 \U0001F1E8\U0001F1F3 " +
                "\U0001F1E7\U0001F1F7 \U0001F1F0\U0001F1F7 " +
                "[dim](regional indicator pairs)[/]").Build())

            // === Styled Emoji ===
            .AddControl(Controls.Rule("Emoji with Markup Styling"))
            .AddControl(Controls.Markup(
                "  [bold red]\U0001F525 Fire[/]  " +
                "[bold cyan]\U0001F4A7 Water[/]  " +
                "[bold green]\U0001F33F Herb[/]  " +
                "[bold yellow]\u26A1 Zap[/]  " +
                "[bold magenta]\U0001F4AB Dizzy[/]").Build())
            .AddControl(Controls.Markup(
                "  [white on red] \U0001F6A8 Alert [/]  " +
                "[black on yellow] \u26A0\uFE0F Warning [/]  " +
                "[white on green] \u2705 Success [/]  " +
                "[white on blue] \u2139\uFE0F Info [/]").Build())
            .AddControl(Controls.Markup(
                "  [gradient=warm]\U0001F30D International text with gradient styling \U0001F30E[/]").Build())

            // === Emoji: Narrow ===
            .AddControl(Controls.Rule("Emoji \u2014 Narrow (EAW=N)"))
            .AddControl(Controls.Markup(
                "  These emoji are 1 column wide, not 2:").Build())
            .AddControl(Controls.Markup(
                "  \U0001F336\uFE0F\u2190pepper " +
                "\U0001F37D\uFE0F\u2190plate " +
                "\U0001F43F\uFE0F\u2190chipmunk " +
                "\U0001F54A\uFE0F\u2190dove " +
                "\U0001F56F\uFE0F\u2190candle " +
                "\U0001F5A5\uFE0F\u2190desktop").Build())
            .AddControl(Controls.Markup(
                "  [dim]Adjacent text should not be consumed by narrow emoji[/]").Build())

            // === Accented & Extended Latin ===
            .AddControl(Controls.Rule("Accented & Extended Latin"))
            .AddControl(Controls.Markup(
                "  [bold #4169E1]French:[/] " +
                "caf\u00e9 cr\u00e8me br\u00fbl\u00e9e " +
                "[italic]l'\u00e9l\u00e8ve fran\u00e7aise[/]").Build())
            .AddControl(Controls.Markup(
                "  [bold #DAA520]German:[/] " +
                "\u00dc\u00f6\u00e4 Stra\u00dfe Gr\u00fc\u00dfe " +
                "[underline]Gem\u00fctlichkeit[/] " +
                "Fr\u00fchst\u00fcck").Build())
            .AddControl(Controls.Markup(
                "  [bold #FF4500]Spanish:[/] " +
                "\u00a1Buenos d\u00edas! " +
                "Espa\u00f1a [italic]ni\u00f1o[/] " +
                "se\u00f1or cig\u00fce\u00f1a").Build())
            .AddControl(Controls.Markup(
                "  [bold #DC143C]Turkish:[/] " +
                "\u00c7ay \u015feker g\u00f6t\u00fcr\u00fc\u015f " +
                "  [bold #228B22]Vietnamese:[/] " +
                "Vi\u1ec7t Nam c\u1ea3m \u01a1n").Build())
            .AddControl(Controls.Markup(
                "  [bold #8B0000]Polish:[/] " +
                "Cze\u015b\u0107 \u017c\u00f3\u0142w \u0119 " +
                "  [bold #FF8C00]Icelandic:[/] " +
                "\u00de\u00f3rsmerkurinn \u00e6\u00f0 a\u00f0").Build())

            // === Decorative Showcase ===
            .AddControl(Controls.Rule("Decorative Showcase"))
            .AddControl(Controls.Markup(
                "  [gradient=spectrum]Stars \u2605 and moons \u263D across the spectrum[/]").Build())
            .AddControl(Controls.Markup(
                "  [bold][red]\u2764[/] [orange1]\u2764[/] [yellow]\u2764[/] " +
                "[green]\u2764[/] [cyan]\u2764[/] [blue]\u2764[/] [purple]\u2764[/][/] " +
                " Rainbow hearts").Build())
            .AddControl(Controls.Markup(
                "  [bold underline #FFD700]\u2606 \u2605 \u2606[/] " +
                "[italic dim] golden stars [/]" +
                "[bold underline #C0C0C0]\u2606 \u2605 \u2606[/]").Build())
            .AddControl(Controls.Markup(
                "  [strikethrough red]deleted text[/] \u2192 " +
                "[bold green]replacement text[/] " +
                "[dim italic](tracked change)[/]").Build())
            .AddControl(Controls.Markup(
                "  [invert] Inverted \u4e16\u754c [/] " +
                "[bold underline cyan]\u039a\u03cc\u03c3\u03bc\u03b5[/] " +
                "[italic #FF69B4]\u0441\u043c\u043e\u0442\u0440\u0438[/] " +
                "[gradient=cool]\U0001F30D Earth \U0001F30F[/]").Build())
            .AddControl(Controls.Markup(
                "  [bold][gradient=red->yellow->green]Status: \u2588\u2588\u2588\u2588\u2588\u2588\u2588\u2588\u2588\u2588\u2588\u2588\u2588\u2588\u2588\u2588\u2588\u2588\u2588\u2588 100% Complete[/][/]").Build())

            // === Surrogate Pairs Beyond Emoji ===
            .AddControl(Controls.Rule("Surrogate Pairs Beyond Emoji"))
            .AddControl(Controls.Markup(
                "  [bold]Mathematical:[/] " +
                "[cyan]\U0001D538 \U0001D539 \U0001D53B[/] " +
                "[yellow]\U0001D49C \U0001D49E \U0001D49F[/] " +
                "[green]\U0001D504 \U0001D505 \U0001D507[/] " +
                "[dim](double-struck, script, fraktur)[/]").Build())
            .AddControl(Controls.Markup(
                "  [bold]Musical:[/] " +
                "[#FFD700]\U0001D11E[/] " +
                "[#C0C0C0]\U0001D122[/] " +
                "[magenta]\U0001D160 \U0001D161 \U0001D162[/] " +
                "[dim](clef, rest, notes)[/]").Build())
            .AddControl(Controls.Markup(
                "  [bold]Historic:[/] " +
                "\U00010000 \U00010001 \U00010002 \U00010003 " +
                "[dim](Linear B syllables \u2014 U+10000..U+10003)[/]").Build())
            .AddControl(Controls.Markup(
                "  [bold]Tags & Specials:[/] " +
                "\U0001F3F4\U000E0067\U000E0062\U000E0073\U000E0063\U000E0074\U000E007F " +
                "[dim](Scotland flag \u2014 7-codepoint sequence)[/]").Build())

            // === Box Drawing & Symbols ===
            .AddControl(Controls.Rule("Box Drawing & Symbols"))
            .AddControl(Controls.Markup(
                "  [bold]Box:[/] " +
                "[cyan]\u250C\u2500\u2500\u2500\u2510[/]  " +
                "[bold]Arrows:[/] " +
                "[yellow]\u2190 \u2191 \u2192 \u2193 \u21D0 \u21D2 \u21B5[/]  " +
                "[bold]Math:[/] " +
                "[green]\u221A \u222B \u2211 \u221E \u2202 \u2248 \u2260[/]").Build())
            .AddControl(Controls.Markup(
                "       [cyan]\u2502   \u2502[/]  " +
                "[bold]Braille:[/] " +
                "[magenta]\u2801\u2802\u2804\u2808\u2810\u2820\u2840\u2880[/]  " +
                "[bold]Music:[/] " +
                "[#FFD700]\u266A \u266B \u266C \u266D \u266E \u266F[/]").Build())
            .AddControl(Controls.Markup(
                "       [cyan]\u2514\u2500\u2500\u2500\u2518[/]  " +
                "[bold]Currency:[/] " +
                "\u00A3 \u20AC \u00A5 \u20A3 \u20BD \u20B9 \u20BF  " +
                "[bold]Misc:[/] " +
                "\u2622 \u2623 \u262E \u262F \u2618").Build())

            .WithVerticalAlignment(SharpConsoleUI.Layout.VerticalAlignment.Fill)
            .Build();

        var gradient = ColorGradient.FromColors(
            new Color(25, 10, 60),
            new Color(10, 45, 55));

        return new WindowBuilder(ws)
            .WithTitle("International & Emoji")
            .WithSize(90, 38)
            .Centered()
            .WithBackgroundGradient(gradient, GradientDirection.Vertical)
            .AddControl(content)
            .OnKeyPressed((s, e) =>
            {
                if (e.KeyInfo.Key == ConsoleKey.Escape)
                {
                    ws.CloseWindow((Window)s!);
                    e.Handled = true;
                }
            })
            .BuildAndShow();
    }
}
