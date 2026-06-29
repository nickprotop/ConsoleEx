// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Drawing;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Logging;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

/// <summary>
/// Regression tests for <see cref="LogViewerControl"/> rendering through the real window pipeline.
/// </summary>
public class LogViewerRenderTests
{
    /// <summary>
    /// A LogViewerControl processes its queued log entries during the Measure pass
    /// (<c>MeasureDOM</c> -> <c>ProcessPendingEntries</c> -> inner panel <c>AddControl</c>). The panel's
    /// AddControl calls <c>ForceRebuildLayout()</c> on the parent window, which nulls the renderer's
    /// root layout node MID-PASS. The arrange pass that immediately follows must not dereference the
    /// now-null root and NRE. This reproduces the MultiDashboard "Log Stream" window crash on first render.
    /// </summary>
    [Fact]
    public void LogViewer_WithPendingEntries_RendersWithoutCrashing()
    {
        var driver = new HeadlessConsoleDriver(120, 30);
        var system = new ConsoleWindowSystem(driver);

        // Boundary-stressing small window (mirrors the real LogStream window: 100x12).
        var window = new Window(system) { Title = "Log Stream", Width = 100, Height = 12 };
        system.AddWindow(window);

        // Queue real log entries so the control has pending work to flush during measure.
        system.LogService.MinimumLevel = LogLevel.Trace;
        for (int i = 0; i < 20; i++)
            system.LogService.Log(LogLevel.Information, $"log line {i}", "MultiDashboard");

        var logViewer = new LogViewerControl(system.LogService)
        {
            VerticalAlignment = VerticalAlignment.Fill,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            FilterLevel = LogLevel.Trace,
            AutoScroll = true
        };
        window.AddControl(logViewer);

        var region = new List<Rectangle> { new Rectangle(0, 0, window.Width, window.Height) };

        // First render: previously threw NullReferenceException inside the arrange pass.
        window.RenderAndGetVisibleContent(region);

        // Re-render: the rebuilt tree (the in-measure invalidation requested a relayout) must
        // survive and stay renderable.
        window.RenderAndGetVisibleContent(region);
    }
}
