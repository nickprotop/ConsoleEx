using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drawing;
using SharpConsoleUI.Layout;

namespace DemoApp.DemoWindows;

public static class CanvasDemoWindow
{
    #region Constants

    private const int WindowWidth = 52;
    private const int WindowHeight = 26;
    private const int AnimationDelayMs = 40; // ~25 fps

    // Starfield
    private const int StarCount = 120;
    private const int StarLayers = 3;
    private const int BurstParticleCount = 12;
    private const int BurstMinLife = 20;
    private const int BurstLifeRange = 15;
    private const double BurstMaxFade = 35.0;
    private const double BurstDamping = 0.95;

    // Plasma
    private const double PlasmaScaleX = 0.08;
    private const double PlasmaScaleY = 0.15;
    private const double RippleAgeStep = 0.15;
    private const double RippleMaxAge = 8.0;
    private const double PlasmaTimeStep = 0.08;

    // Geometry
    private const double GeometryTimeStep = 0.06;
    private const int GridSpacingX = 6;
    private const int GridSpacingY = 4;
    private const int RadiatingLineCount = 6;
    private const int BoxWidth = 8;
    private const int BoxHeight = 4;
    private const double ExpandingRingMaxRadius = 20.0;
    private const double ExpandingRingGrowth = 0.6;

    #endregion

    #region Public Factory Methods

    public static Window CreateStarfieldWindow(ConsoleWindowSystem ws)
    {
        var canvas = MakeCanvas();

        var stars = new (double X, int Y, int Layer)[StarCount];
        var rng = new Random(42);
        for (int i = 0; i < StarCount; i++)
        {
            stars[i] = (
                rng.Next(200),
                rng.Next(100),
                rng.Next(StarLayers)
            );
        }

        var bursts = new List<(double X, double Y, double Vx, double Vy, int Life, Color C)>();
        var burstLock = new object();

        canvas.CanvasMouseClick += (_, e) =>
        {
            lock (burstLock)
            {
                var burstRng = new Random();
                for (int i = 0; i < BurstParticleCount; i++)
                {
                    double angle = burstRng.NextDouble() * 2 * Math.PI;
                    double speed = 0.5 + burstRng.NextDouble() * 1.5;
                    bursts.Add((e.CanvasX, e.CanvasY,
                        Math.Cos(angle) * speed, Math.Sin(angle) * speed,
                        BurstMinLife + burstRng.Next(BurstLifeRange),
                        new Color(
                            (byte)(200 + burstRng.Next(55)),
                            (byte)(100 + burstRng.Next(155)),
                            (byte)burstRng.Next(80))));
                }
            }
        };

        char[] starChars = ['·', '∗', '★'];
        Color[] starColors =
        [
            new Color(80, 80, 120),
            new Color(160, 160, 200),
            new Color(255, 255, 255)
        ];
        double[] speeds = [0.3, 0.7, 1.4];
        var spaceBg = new Color(5, 5, 15);

        return new WindowBuilder(ws)
            .WithTitle("Starfield")
            .WithSize(WindowWidth, WindowHeight)
            .Centered()
            .WithColors(Color.White, spaceBg)
            .AddControl(canvas)
            .WithAsyncWindowThread(async (window, ct) =>
            {
                while (!ct.IsCancellationRequested)
                {
                    int w = canvas.CanvasWidth;
                    int h = canvas.CanvasHeight;

                    var g = canvas.BeginPaint();
                    try
                    {
                        g.Clear(spaceBg);

                        for (int i = 0; i < stars.Length; i++)
                        {
                            ref var s = ref stars[i];
                            s.X -= speeds[s.Layer];
                            if (s.X < 0) s.X += w;
                            if (s.X >= w) s.X %= w;

                            int sy = s.Y % h;
                            g.SetNarrowCell((int)s.X, sy, starChars[s.Layer],
                                starColors[s.Layer], spaceBg);
                        }

                        lock (burstLock)
                        {
                            for (int i = bursts.Count - 1; i >= 0; i--)
                            {
                                var b = bursts[i];
                                int bx = (int)b.X, by = (int)b.Y;
                                if (bx >= 0 && bx < w && by >= 0 && by < h)
                                {
                                    double fade = (double)b.Life / BurstMaxFade;
                                    var c = new Color(
                                        (byte)(b.C.R * fade),
                                        (byte)(b.C.G * fade),
                                        (byte)(b.C.B * fade));
                                    g.SetNarrowCell(bx, by, '●', c, spaceBg);
                                }

                                bursts[i] = (b.X + b.Vx, b.Y + b.Vy,
                                    b.Vx * BurstDamping, b.Vy * BurstDamping, b.Life - 1, b.C);
                                if (bursts[i].Life <= 0)
                                    bursts.RemoveAt(i);
                            }
                        }
                    }
                    finally
                    {
                        canvas.EndPaint();
                    }

                    await Task.Delay(AnimationDelayMs, ct);
                }
            })
            .BuildAndShow();
    }

    public static Window CreatePlasmaWindow(ConsoleWindowSystem ws)
    {
        var canvas = MakeCanvas();

        var ripples = new List<(int Cx, int Cy, double StartTime)>();
        var rippleLock = new object();

        canvas.CanvasMouseClick += (_, e) =>
        {
            lock (rippleLock)
            {
                ripples.Add((e.CanvasX, e.CanvasY, 0));
            }
        };

        canvas.Paint += (_, e) =>
        {
            e.Graphics.WriteStringCentered(0,
                "[ Click to add ripples ]", Color.White, Color.Black);
        };

        var bg = new Color(5, 5, 15);

        return new WindowBuilder(ws)
            .WithTitle("Plasma")
            .WithSize(WindowWidth, WindowHeight)
            .Centered()
            .WithColors(Color.White, bg)
            .AddControl(canvas)
            .WithAsyncWindowThread(async (window, ct) =>
            {
                double time = 0;
                while (!ct.IsCancellationRequested)
                {
                    int w = canvas.CanvasWidth;
                    int h = canvas.CanvasHeight;

                    var g = canvas.BeginPaint();
                    try
                    {
                        for (int y = 0; y < h; y++)
                        {
                            for (int x = 0; x < w; x++)
                            {
                                double nx = x * PlasmaScaleX;
                                double ny = y * PlasmaScaleY;

                                double v = Math.Sin(nx + time)
                                    + Math.Sin(ny + time * 0.7)
                                    + Math.Sin((nx + ny + time) * 0.5)
                                    + Math.Sin(Math.Sqrt(nx * nx + ny * ny + 1) + time * 0.8);

                                lock (rippleLock)
                                {
                                    foreach (var r in ripples)
                                    {
                                        double dx = x - r.Cx;
                                        double dy = (y - r.Cy) * 2;
                                        double dist = Math.Sqrt(dx * dx + dy * dy);
                                        double rippleAge = r.StartTime;
                                        double rippleWave = Math.Sin(dist * 0.5 - rippleAge * 3);
                                        double rippleFade = Math.Max(0, 1 - rippleAge / RippleMaxAge);
                                        v += rippleWave * rippleFade * 2;
                                    }
                                }

                                double hue = (v / 8.0 + 0.5) % 1.0;
                                if (hue < 0) hue += 1.0;

                                var (cr, cg, cb) = HsvToRgb(hue, 0.85, 0.9);
                                g.SetNarrowCell(x, y, '▓', new Color(cr, cg, cb), Color.Black);
                            }
                        }

                        lock (rippleLock)
                        {
                            for (int i = ripples.Count - 1; i >= 0; i--)
                            {
                                var r = ripples[i];
                                ripples[i] = (r.Cx, r.Cy, r.StartTime + RippleAgeStep);
                                if (ripples[i].StartTime > RippleMaxAge)
                                    ripples.RemoveAt(i);
                            }
                        }
                    }
                    finally
                    {
                        canvas.EndPaint();
                    }

                    time += PlasmaTimeStep;
                    await Task.Delay(AnimationDelayMs, ct);
                }
            })
            .BuildAndShow();
    }

    public static Window CreateGeometryWindow(ConsoleWindowSystem ws)
    {
        var canvas = MakeCanvas();

        var bg = new Color(10, 10, 25);
        var dimGrid = new Color(25, 25, 50);

        var rings = new List<(int Cx, int Cy, double Radius, Color C)>();
        var ringLock = new object();

        canvas.CanvasMouseClick += (_, e) =>
        {
            lock (ringLock)
            {
                var rng = new Random();
                var (r, g, b) = HsvToRgb(rng.NextDouble(), 0.9, 1.0);
                rings.Add((e.CanvasX, e.CanvasY, 1.0, new Color(r, g, b)));
            }
        };

        return new WindowBuilder(ws)
            .WithTitle("Geometry")
            .WithSize(WindowWidth, WindowHeight)
            .Centered()
            .WithColors(Color.White, bg)
            .AddControl(canvas)
            .WithAsyncWindowThread(async (window, ct) =>
            {
                double t = 0;
                while (!ct.IsCancellationRequested)
                {
                    int w = canvas.CanvasWidth;
                    int h = canvas.CanvasHeight;
                    int cx = w / 2;
                    int cy = h / 2;

                    var g = canvas.BeginPaint();
                    try
                    {
                        g.Clear(bg);

                        // Background: dotted grid
                        for (int y = 0; y < h; y += GridSpacingY)
                            for (int x = 0; x < w; x += GridSpacingX)
                                g.SetNarrowCell(x, y, '·', dimGrid, bg);

                        // Rotating polygon ring (morphs 3-8 sides)
                        int sides = 3 + (int)(t * 0.3) % 6;
                        double polyRadius = Math.Min(cx, cy) * 0.7;
                        double rotAngle = t * 0.6;
                        var polyPoints = MakeRegularPolygon(cx, cy, polyRadius, sides, rotAngle);
                        var polyColor = CycleColor(t, 0.0);
                        g.DrawPolygon(polyPoints, '*', polyColor, bg);

                        // Filled triangle orbiting
                        double orbitRx = cx * 0.7;
                        double orbitRy = cy * 0.65;
                        double triAngle = t * 0.8;
                        int triCx = cx + (int)(orbitRx * Math.Cos(triAngle));
                        int triCy = cy + (int)(orbitRy * Math.Sin(triAngle));
                        double triRot = t * 1.5;
                        var triColor = CycleColor(t, 0.33);
                        g.FillTriangle(
                            triCx + (int)(3 * Math.Cos(triRot)),
                            triCy + (int)(2 * Math.Sin(triRot)),
                            triCx + (int)(3 * Math.Cos(triRot + 2.094)),
                            triCy + (int)(2 * Math.Sin(triRot + 2.094)),
                            triCx + (int)(3 * Math.Cos(triRot + 4.189)),
                            triCy + (int)(2 * Math.Sin(triRot + 4.189)),
                            '▲', triColor, bg);

                        // Pulsing circles
                        double pulse1 = 2 + 2 * Math.Sin(t * 1.2);
                        double pulse2 = 3 + 2 * Math.Sin(t * 0.9 + 1);
                        int circleSpread = Math.Max(8, cx / 2);
                        g.DrawCircle(cx - circleSpread, cy, (int)pulse1, '○', CycleColor(t, 0.15), bg);
                        g.DrawCircle(cx + circleSpread, cy, (int)pulse2, '◎', CycleColor(t, 0.55), bg);

                        // Filled ellipse breathing
                        int erx = Math.Max(3, cx / 4) + (int)(2 * Math.Sin(t * 0.7));
                        int ery = Math.Max(2, cy / 4) + (int)(1 * Math.Cos(t * 0.9));
                        g.FillEllipse(cx, cy, erx, ery, '░',
                            new Color(60, 40, 120), new Color(20, 15, 50));

                        // Sweeping arc (radar)
                        double arcStart = t * 1.5;
                        double arcSpan = Math.PI * 0.6;
                        int arcRadius = Math.Min(cx, cy) - 2;
                        g.DrawArc(cx, cy, arcRadius, arcStart, arcStart + arcSpan,
                            '▪', new Color(0, 255, 120), bg);

                        // Radiating lines from center
                        for (int i = 0; i < RadiatingLineCount; i++)
                        {
                            double la = t * 0.4 + i * (Math.PI * 2.0 / RadiatingLineCount);
                            int lx = cx + (int)((cx - 4) * Math.Cos(la));
                            int ly = cy + (int)((cy - 2) * Math.Sin(la));
                            double fade = 0.3 + 0.7 * ((Math.Sin(t * 2 + i) + 1) / 2);
                            var lc = new Color(
                                (byte)(100 * fade), (byte)(180 * fade), (byte)(255 * fade));
                            g.DrawLine(cx, cy, lx, ly, '─', lc, bg);
                        }

                        // Bouncing box
                        int boxMaxX = Math.Max(1, w - BoxWidth - 2);
                        int boxMaxY = Math.Max(1, h - BoxHeight - 2);
                        int boxX = (int)(boxMaxX * 0.5 * (Math.Sin(t * 0.5) + 1));
                        int boxY = (int)(boxMaxY * 0.5 * (Math.Sin(t * 0.7 + 2) + 1));
                        g.DrawBox(boxX, boxY, BoxWidth, BoxHeight, BoxChars.Single,
                            CycleColor(t, 0.75), bg);

                        // Horizontal gradient bar at bottom
                        if (h > 3)
                        {
                            g.GradientFillRect(2, h - 2, Math.Max(1, w - 4), 1,
                                CycleColor(t, 0.0), CycleColor(t, 0.5), true);
                        }

                        // Expanding click rings
                        lock (ringLock)
                        {
                            for (int i = rings.Count - 1; i >= 0; i--)
                            {
                                var ring = rings[i];
                                double fade = Math.Max(0, 1 - ring.Radius / ExpandingRingMaxRadius);
                                var rc = new Color(
                                    (byte)(ring.C.R * fade),
                                    (byte)(ring.C.G * fade),
                                    (byte)(ring.C.B * fade));
                                g.DrawCircle(ring.Cx, ring.Cy, (int)ring.Radius, '·', rc, bg);
                                rings[i] = (ring.Cx, ring.Cy, ring.Radius + ExpandingRingGrowth, ring.C);
                                if (ring.Radius > ExpandingRingMaxRadius)
                                    rings.RemoveAt(i);
                            }
                        }
                    }
                    finally
                    {
                        canvas.EndPaint();
                    }

                    t += GeometryTimeStep;
                    await Task.Delay(AnimationDelayMs, ct);
                }
            })
            .BuildAndShow();
    }

    #endregion

    #region Private Helpers

    private static CanvasControl MakeCanvas() => new()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Fill,
        AutoSize = true,
    };

    private static (byte R, byte G, byte B) HsvToRgb(double h, double s, double v)
    {
        double c = v * s;
        double x = c * (1 - Math.Abs((h * 6) % 2 - 1));
        double m = v - c;

        double r, g, b;
        int sector = (int)(h * 6) % 6;
        (r, g, b) = sector switch
        {
            0 => (c, x, 0.0),
            1 => (x, c, 0.0),
            2 => (0.0, c, x),
            3 => (0.0, x, c),
            4 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };

        return (
            (byte)((r + m) * 255),
            (byte)((g + m) * 255),
            (byte)((b + m) * 255));
    }

    private static (int X, int Y)[] MakeRegularPolygon(
        int cx, int cy, double radius, int sides, double startAngle)
    {
        var pts = new (int X, int Y)[sides];
        double step = 2 * Math.PI / sides;
        for (int i = 0; i < sides; i++)
        {
            double a = startAngle + i * step;
            pts[i] = (cx + (int)(radius * Math.Cos(a)),
                cy + (int)(radius * 0.5 * Math.Sin(a)));
        }
        return pts;
    }

    private static Color CycleColor(double time, double offset)
    {
        double h = (time * 0.08 + offset) % 1.0;
        if (h < 0) h += 1.0;
        var (r, g, b) = HsvToRgb(h, 0.85, 0.95);
        return new Color(r, g, b);
    }

    #endregion
}
