# Gallery

Photographs every control that is worth a picture, and writes one PNG each.

```bash
dotnet run --project Examples/Gallery
```

Images land in `docs/images/controls/`, where the control pages and the site's
controls page both read them.

## Why not DemoApp

DemoApp is a launcher: every capture of it carries a navigation tree, a toolbar
and a status bar, so a control's photograph would be mostly chrome. Reorganising
that launcher would also silently change every screenshot.

This app frames each control in a window of its own, sized to it, with the
desktop panels turned off.

## How it works

Headless, so it runs in CI with no terminal. Each shot starts a system, opens one
window, and asks over the diagnostics socket for a PNG — the same public path
`cxdbg` uses, so nothing is photographed through a door a reader cannot open.

`fontSource: "embedded"` is what makes a rebuild produce the same bytes rather
than a diff. Seeded data for the same reason.

The theme is **Slate**, set per run. The library's default stays `ModernGray`:
changing it would repaint every application that never chose a theme.

## Adding a shot

One entry in `Shots.cs`:

```csharp
new("MyControl", 60, 12, () => Controls.MyControl().Build()),
```

A control whose point is a state change takes two pictures:

```csharp
new("MyControl", 60, 12, () => Controls.MyControl().Build(),
    Then: ["Tab", "Enter"], Suffix: "open"),
```

The keys are sent, the frame counter is waited on, and a second image is written
as `MyControl-open.png`. A closed dropdown is a text field with an arrow on it;
the open one is the control.

Not every control earns a picture. A button is better explained by the three
lines that build it.

## Supplying a screenshot by hand

Some controls cannot photograph themselves here. A `TerminalControl` needs a
pty with something running in it; an `HtmlControl` only downloads images on the
`LoadUrlAsync` path, and pointing a gallery at a live website would capture
whatever that site looked like this morning.

For those, take the picture yourself and claim it:

```bash
cp my-shot.png docs/images/controls/TerminalControl.png
touch docs/images/controls/TerminalControl.png.manual
```

The `.manual` file is the claim. The gallery sees it, prints `kept (manual)`,
and never overwrites that image — so a regeneration leaves your capture alone.
