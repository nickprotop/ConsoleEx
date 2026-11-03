# cxdemo

The SharpConsoleUI showcase, as a command you can run.

```bash
dotnet tool install -g cxdemo
cxdemo
```

It opens a launcher. Everything past that point is the framework running, not a
video of it: overlapping translucent windows compositing over a gradient, a data
grid with in-cell editing and a filter grammar, an HTML control rendering a real
page with images, a terminal emulator with a shell inside it, video playback, and
the arcade games driven by the animation manager.

## What it is for

Seeing whether the framework does what the README claims, without cloning
anything. A screenshot cannot show you a compositor, and a list of controls
cannot show you how one feels under the cursor.

If it does what you want, the library is
[SharpConsoleUI](https://www.nuget.org/packages/SharpConsoleUI/).

## Requires

.NET 10 or newer, and a terminal. Video playback additionally needs FFmpeg on
the path; the rest runs without it.

## Links

- [Documentation](https://nickprotop.github.io/ConsoleEx/)
- [Source](https://github.com/nickprotop/ConsoleEx)
- [Why SharpConsoleUI](https://nickprotop.github.io/ConsoleEx/docs/WHY.html), which is
  candid about what the framework is not

MIT.
