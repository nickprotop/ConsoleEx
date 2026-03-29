using SharpConsoleUI;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Helpers;
using System.Diagnostics;
using BenchmarkApp;

// --- Key handling state ---
bool escPressed = false;

// --- Setup ---
var driver = new NetConsoleDriver(RenderMode.Buffer);
var options = new ConsoleWindowSystemOptions(
    EnableFrameRateLimiting: false,
    EnablePerformanceMetrics: false);
var windowSystem = new ConsoleWindowSystem(driver, options: options);

// --- System info ---
string system = $"{Environment.MachineName} / {Environment.OSVersion}";
var screenSize = driver.ScreenSize;
string terminal = $"{screenSize.Width}x{screenSize.Height}";
string version = typeof(ConsoleWindowSystem).Assembly.GetName().Version?.ToString() ?? "unknown";

// --- Constants ---
const int BenchmarkWindowWidth = 76;
const int ResultsWindowWidth = 76;
const int WarmupFrames = 10;
const int BenchmarkFrames = 500;
const int ProgressUpdateInterval = 50;
const int UiYieldMs = 10;

// --- Build benchmark window ---
int windowHeight = Math.Min(screenSize.Height - 4, 35);
int windowLeft = Math.Max(1, (screenSize.Width - BenchmarkWindowWidth) / 2);
int windowTop = Math.Max(1, (screenSize.Height - windowHeight) / 2);

var benchmarkWindow = new SharpConsoleUI.Builders.WindowBuilder(windowSystem)
    .WithTitle("SharpConsoleUI Benchmark")
    .WithSize(BenchmarkWindowWidth, windowHeight)
    .AtPosition(windowLeft, windowTop)
    .WithBackgroundGradient(
        SharpConsoleUI.Helpers.ColorGradient.FromColors(
            new Color(20, 10, 40), new Color(40, 20, 60), new Color(20, 30, 50)),
        SharpConsoleUI.Rendering.GradientDirection.DiagonalDown)
    .WithAsyncWindowThread(async (win, ct) =>
    {
        while (!ct.IsCancellationRequested)
        {
            // === WELCOME STATE ===
            var actionTcs = new TaskCompletionSource<string>();
            windowSystem.EnqueueOnUIThread(() =>
            {
                ScreenBuilder.BuildWelcomeScreen(win, version, system, terminal,
                    onStart: () => actionTcs.TrySetResult("start"),
                    onExit: () => actionTcs.TrySetResult("exit"));
            });
            Thread.Sleep(UiYieldMs);

            string action;
            try
            {
                using var reg = ct.Register(() => actionTcs.TrySetCanceled());
                action = await actionTcs.Task;
            }
            catch (TaskCanceledException)
            {
                return;
            }

            if (action == "exit")
            {
                windowSystem.Shutdown(0);
                return;
            }

            // === RUNNING STATE ===
            var tests = BenchmarkScenes.GetAllTests();
            var results = new List<TestResult>();
            bool stopped = false;
            ProgressBarControl? currentProgressBar = null;

            for (int t = 0; t < tests.Count && !stopped; t++)
            {
                var test = tests[t];

                // Build running screen with real progress bar
                var stopTcs = new TaskCompletionSource<bool>();
                var buildTcs = new TaskCompletionSource();
                windowSystem.EnqueueOnUIThread(() =>
                {
                    currentProgressBar = ScreenBuilder.BuildRunningScreen(
                        win, t + 1, tests.Count, test.Name, results,
                        onStop: () => stopTcs.TrySetResult(true));
                    buildTcs.SetResult();
                });
                await buildTcs.Task;

                // Create test window on UI thread
                Window? testWindow = null;
                var createTcs = new TaskCompletionSource();
                windowSystem.EnqueueOnUIThread(() =>
                {
                    // Position benchmark window left, test window right
                    int benchRunWidth = 40;
                    win.Width = benchRunWidth;
                    win.Left = 1;
                    win.IsDirty = true;
                    int testLeft = benchRunWidth + 3;
                    testWindow = test.CreateWindow(windowSystem, testLeft, windowTop);
                    windowSystem.AddWindow(testWindow);
                    createTcs.SetResult();
                });
                await createTcs.Task;

                // Warmup
                for (int i = 0; i < WarmupFrames && !stopped; i++)
                {
                    var tcs = new TaskCompletionSource();
                    windowSystem.EnqueueOnUIThread(() =>
                    {
                        test.Mutate(testWindow!, i);
                        windowSystem.ForceRender();
                        tcs.SetResult();
                    });
                    await tcs.Task;

                    if (escPressed || stopTcs.Task.IsCompleted)
                    {
                        stopped = true;
                        escPressed = false;
                    }
                }

                // Benchmark
                var sw = Stopwatch.StartNew();
                for (int i = 0; i < BenchmarkFrames && !stopped; i++)
                {
                    var tcs = new TaskCompletionSource();
                    windowSystem.EnqueueOnUIThread(() =>
                    {
                        test.Mutate(testWindow!, WarmupFrames + i);
                        windowSystem.ForceRender();
                        tcs.SetResult();
                    });
                    await tcs.Task;

                    if (i % ProgressUpdateInterval == 0)
                    {
                        double progressPercent = (double)i / BenchmarkFrames * 100;
                        windowSystem.EnqueueOnUIThread(() =>
                        {
                            if (currentProgressBar != null)
                                currentProgressBar.Value = progressPercent;
                        });
                    }

                    if (escPressed || stopTcs.Task.IsCompleted)
                    {
                        stopped = true;
                        escPressed = false;
                    }
                }
                sw.Stop();

                // Close test window(s)
                var closeTcs = new TaskCompletionSource();
                windowSystem.EnqueueOnUIThread(() =>
                {
                    if (test.Cleanup != null)
                    {
                        test.Cleanup(windowSystem);
                    }
                    else if (testWindow != null)
                    {
                        windowSystem.CloseWindow(testWindow);
                    }
                    closeTcs.SetResult();
                });
                await closeTcs.Task;

                if (!stopped)
                {
                    double fps = BenchmarkFrames / sw.Elapsed.TotalSeconds;
                    results.Add(new TestResult(test.Name, fps, test.Weight));
                }
            }

            // === RESULTS STATE ===
            var resultActionTcs = new TaskCompletionSource<string>();
            windowSystem.EnqueueOnUIThread(() =>
            {
                win.Width = ResultsWindowWidth;
                win.Left = Math.Max(1, (driver.ScreenSize.Width - ResultsWindowWidth) / 2);
                win.IsDirty = true;

                ScreenBuilder.BuildResultsScreen(win, results, system, terminal, version,
                    onRunAgain: () => resultActionTcs.TrySetResult("again"),
                    onExit: () => resultActionTcs.TrySetResult("exit"));
            });
            Thread.Sleep(UiYieldMs);

            string resultAction;
            try
            {
                using var reg = ct.Register(() => resultActionTcs.TrySetCanceled());
                resultAction = await resultActionTcs.Task;
            }
            catch (TaskCanceledException)
            {
                return;
            }

            if (resultAction == "exit")
            {
                windowSystem.Shutdown(0);
                return;
            }

            // Reset window size for next run (centered)
            windowSystem.EnqueueOnUIThread(() =>
            {
                win.Width = BenchmarkWindowWidth;
                win.Left = Math.Max(1, (driver.ScreenSize.Width - BenchmarkWindowWidth) / 2);
                win.IsDirty = true;
            });
        }
    })
    .BuildAndShow();

// Wire PreviewKeyPressed for Esc handling and Enter as fallback
benchmarkWindow.PreviewKeyPressed += (s, e) =>
{
    if (e.KeyInfo.Key == ConsoleKey.Escape)
    {
        escPressed = true;
        e.Handled = true;
    }
};

windowSystem.Run();
