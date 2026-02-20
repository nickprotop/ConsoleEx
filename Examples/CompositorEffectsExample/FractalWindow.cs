using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using Spectre.Console;

namespace CompositorEffectsExample;

/// <summary>
/// Renders an animated Mandelbrot fractal with cycling colors.
/// Uses Window's thread support with CancellationToken for clean shutdown.
/// </summary>
public class FractalWindow : Window
{
    private double _time = 0;
    private double _zoom = 1.0;
    private double _centerX = -0.5;
    private double _centerY = 0.0;
    private bool _isJulia = false;
    private double _juliaReal = -0.7;
    private double _juliaImag = 0.27015;

    // Animation modes
    private int _animMode = 0;
    private const int AnimModeCount = 4;

    // Color palette (pre-computed for speed)
    private readonly Color[] _palette;
    private const int PaletteSize = 256;
    private const int MaxIterations = 50;

    public FractalWindow(ConsoleWindowSystem windowSystem)
        : base(windowSystem, FractalAnimationLoop)
    {
        Title = "Fractal Explorer";
        Width = 80;
        Height = 40;
        IsResizable = true;

        // Pre-compute color palette
        _palette = GeneratePalette();

        // Add title at top
        var title = new MarkupControl(new List<string>
        {
            "[bold yellow] FRACTAL EXPLORER [/]"
        });
        title.Margin = new Margin(1, 1, 0, 0);
        title.StickyPosition = StickyPosition.Top;
        AddControl(title);

        // Add key instructions at bottom
        var help = new MarkupControl(new List<string>
        {
            "[cyan]Space[/]:[white]Mode[/] [cyan]M[/]:[white]Julia/Mandel[/] [cyan]+/-[/]:[white]Zoom[/] [cyan]Arrows[/]:[white]Pan[/] [cyan]R[/]:[white]Reset[/] [cyan]Esc[/]:[white]Close[/]"
        });
        help.Margin = new Margin(1, 0, 1, 0);
        help.StickyPosition = StickyPosition.Bottom;
        AddControl(help);

        // Hook into rendering - PreBufferPaint so controls render ON TOP of fractal
        PreBufferPaint += RenderFractal;

        // Keyboard controls
        KeyPressed += HandleKey;

        // Reset fractal on resize
        OnResize += (s, e) => Reset();

        OnClosing += (s, e) =>
        {
            PreBufferPaint -= RenderFractal;
        };
    }

    /// <summary>
    /// Animation loop running on window's background thread.
    /// Automatically cancelled when window closes.
    /// </summary>
    private static async Task FractalAnimationLoop(Window window, CancellationToken ct)
    {
        var fractalWindow = (FractalWindow)window;

        while (!ct.IsCancellationRequested)
        {
            fractalWindow._time += 0.05;
            fractalWindow.UpdateAnimation();
            fractalWindow.Invalidate(redrawAll: true);

            try
            {
                await Task.Delay(33, ct); // ~30 FPS
            }
            catch (OperationCanceledException)
            {
                break; // Clean exit
            }
        }
    }

    private Color[] GeneratePalette()
    {
        var palette = new Color[PaletteSize];
        for (int i = 0; i < PaletteSize; i++)
        {
            double t = (double)i / PaletteSize;
            // Create a vibrant cycling palette
            int r = (int)(Math.Sin(t * Math.PI * 2 + 0) * 127 + 128);
            int g = (int)(Math.Sin(t * Math.PI * 2 + 2.094) * 127 + 128); // +2π/3
            int b = (int)(Math.Sin(t * Math.PI * 2 + 4.189) * 127 + 128); // +4π/3
            palette[i] = new Color((byte)r, (byte)g, (byte)b);
        }
        return palette;
    }

    private void UpdateAnimation()
    {
        switch (_animMode)
        {
            case 0: // Color cycling only
                break;
            case 1: // Slow zoom into interesting region
                _zoom *= 1.005;
                _centerX = -0.743643887037151;
                _centerY = 0.131825904205330;
                if (_zoom > 1000) _zoom = 1.0;
                break;
            case 2: // Julia set morphing
                _isJulia = true;
                _juliaReal = -0.7 + Math.Sin(_time * 0.3) * 0.2;
                _juliaImag = 0.27015 + Math.Cos(_time * 0.4) * 0.15;
                break;
            case 3: // Panning exploration
                _isJulia = false;
                _centerX = -0.5 + Math.Sin(_time * 0.2) * 0.5;
                _centerY = Math.Cos(_time * 0.15) * 0.5;
                _zoom = 2.0 + Math.Sin(_time * 0.1) * 1.5;
                break;
        }
    }

    private void HandleKey(object? sender, KeyPressedEventArgs e)
    {
        switch (e.KeyInfo.Key)
        {
            case ConsoleKey.Escape:
                Close();
                e.Handled = true;
                break;

            case ConsoleKey.Spacebar:
                _animMode = (_animMode + 1) % AnimModeCount;
                if (_animMode == 0) Reset();
                e.Handled = true;
                break;

            case ConsoleKey.M:
                _isJulia = !_isJulia;
                e.Handled = true;
                break;

            case ConsoleKey.R:
                Reset();
                e.Handled = true;
                break;

            case ConsoleKey.Add:
            case ConsoleKey.OemPlus:
                _zoom *= 1.5;
                e.Handled = true;
                break;

            case ConsoleKey.Subtract:
            case ConsoleKey.OemMinus:
                _zoom /= 1.5;
                if (_zoom < 0.5) _zoom = 0.5;
                e.Handled = true;
                break;

            case ConsoleKey.UpArrow:
                _centerY -= 0.1 / _zoom;
                e.Handled = true;
                break;

            case ConsoleKey.DownArrow:
                _centerY += 0.1 / _zoom;
                e.Handled = true;
                break;

            case ConsoleKey.LeftArrow:
                _centerX -= 0.1 / _zoom;
                e.Handled = true;
                break;

            case ConsoleKey.RightArrow:
                _centerX += 0.1 / _zoom;
                e.Handled = true;
                break;
        }
    }

    private void Reset()
    {
        _zoom = 1.0;
        _centerX = -0.5;
        _centerY = 0.0;
        _isJulia = false;
        _juliaReal = -0.7;
        _juliaImag = 0.27015;
    }

    private void RenderFractal(CharacterBuffer buffer, LayoutRect dirtyRegion, LayoutRect clipRect)
    {
        // PreBufferPaint: Paint full buffer, controls will render ON TOP
        int width = buffer.Width;
        int height = buffer.Height;

        // Aspect ratio correction (console chars are ~2:1)
        double aspectRatio = (double)width / height * 0.5;

        // Calculate view bounds
        double scale = 2.0 / _zoom;
        double xMin = _centerX - scale * aspectRatio;
        double xMax = _centerX + scale * aspectRatio;
        double yMin = _centerY - scale;
        double yMax = _centerY + scale;

        double xStep = (xMax - xMin) / width;
        double yStep = (yMax - yMin) / height;

        // Color offset for animation
        int colorOffset = (int)(_time * 20) % PaletteSize;

        // Render fractal to entire buffer
        for (int py = 0; py < height; py++)
        {
            double y0 = yMin + py * yStep;

            for (int px = 0; px < width; px++)
            {
                double x0 = xMin + px * xStep;

                int iterations;
                if (_isJulia)
                {
                    iterations = ComputeJulia(x0, y0, _juliaReal, _juliaImag);
                }
                else
                {
                    iterations = ComputeMandelbrot(x0, y0);
                }

                // Map iterations to color
                Color color;
                char ch;

                if (iterations == MaxIterations)
                {
                    // Inside the set - deep color
                    color = Color.Black;
                    ch = ' ';
                }
                else
                {
                    // Outside - colorful gradient with animation
                    int colorIndex = (iterations * 8 + colorOffset) % PaletteSize;
                    color = _palette[colorIndex];

                    // Use different chars based on iteration density
                    ch = iterations switch
                    {
                        < 5 => '░',
                        < 10 => '▒',
                        < 20 => '▓',
                        _ => '█'
                    };
                }

                buffer.SetCell(px, py, ch, color, Color.Black);
            }
        }
    }

    private int ComputeMandelbrot(double x0, double y0)
    {
        double x = 0, y = 0;
        int iteration = 0;

        while (x * x + y * y <= 4 && iteration < MaxIterations)
        {
            double xTemp = x * x - y * y + x0;
            y = 2 * x * y + y0;
            x = xTemp;
            iteration++;
        }

        return iteration;
    }

    private int ComputeJulia(double x0, double y0, double cReal, double cImag)
    {
        double x = x0, y = y0;
        int iteration = 0;

        while (x * x + y * y <= 4 && iteration < MaxIterations)
        {
            double xTemp = x * x - y * y + cReal;
            y = 2 * x * y + cImag;
            x = xTemp;
            iteration++;
        }

        return iteration;
    }
}
