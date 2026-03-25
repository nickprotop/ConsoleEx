using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Dialogs;
using SharpConsoleUI.Video;

namespace DemoApp.DemoWindows;

public static class VideoDemoWindow
{
    private const string VideoFilter = "*.mp4;*.mkv;*.avi;*.webm;*.mov;*.flv;*.wmv";

    public static Window Create(ConsoleWindowSystem ws)
    {
        var videoControl = Controls.Video()
            .Fill()
            .WithLooping()
            .WithOverlay()
            .Build();

        var window = new WindowBuilder(ws)
            .WithTitle("Video Player")
            .WithSize(82, 30)
            .Centered()
            .WithColors(Color.White, Color.Black)
            .AddControl(videoControl)
            .WithAsyncWindowThread(async (win, ct) =>
            {
                // Open file picker asynchronously
                var filePath = await FileDialogs.ShowFilePickerAsync(ws,
                    startPath: AppContext.BaseDirectory,
                    filter: VideoFilter);

                // Fall back to bundled sample if user cancels file picker
                if (string.IsNullOrEmpty(filePath))
                {
                    string samplePath = Path.Combine(AppContext.BaseDirectory, "sample.mp4");
                    if (File.Exists(samplePath))
                        filePath = samplePath;
                    else
                    {
                        ws.EnqueueOnUIThread(() => win.TryClose(force: true));
                        return;
                    }
                }

                ws.EnqueueOnUIThread(() =>
                {
                    win.Title = $"Video — {Path.GetFileName(filePath)}";
                    videoControl.PlayFile(filePath);
                });

                // Keep thread alive until cancellation (playback runs internally)
                try { await Task.Delay(Timeout.Infinite, ct); }
                catch (OperationCanceledException) { }
            })
            .BuildAndShow();

        window.OnClosed += (_, _) =>
        {
            videoControl.Stop();
            videoControl.Dispose();
        };

        return window;
    }
}
