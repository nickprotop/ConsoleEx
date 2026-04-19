#!/usr/bin/env dotnet
#:package SharpConsoleUI@2.4.61

// progress.cs — Run a subprocess while showing an indeterminate progress bar.
//
// Usage:      dotnet run progress.cs -- <command> [args...]
// stdin:      ignored (not forwarded to the subprocess)
// stdout:     the subprocess's stdout (captured and written after it exits)
// stderr:     the subprocess's stderr
// exit:       the subprocess's exit code (or >2 on launch failure)
//
// The progress bar is indeterminate — it pulses while the command runs.
// When the command exits, the bar closes automatically and the captured
// stdout is written to this script's stdout so it composes in pipelines.
//
// Example:
//   dotnet run progress.cs -- npm install
//   dotnet run progress.cs -- git clone https://github.com/foo/bar.git

using System.Diagnostics;
using System.Text;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;

if (args.Length == 0)
{
    Console.Error.WriteLine("progress: usage: progress.cs -- <command> [args...]");
    return 2;
}

var cmdStart = Array.IndexOf(args, "--");
var cmdArgs = cmdStart >= 0 ? args.Skip(cmdStart + 1).ToArray() : args;
if (cmdArgs.Length == 0)
{
    Console.Error.WriteLine("progress: no command specified.");
    return 2;
}

var psi = new ProcessStartInfo
{
    FileName = cmdArgs[0],
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    UseShellExecute = false,
};
for (int i = 1; i < cmdArgs.Length; i++)
    psi.ArgumentList.Add(cmdArgs[i]);

Process? proc;
try
{
    proc = Process.Start(psi);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"progress: failed to start '{cmdArgs[0]}': {ex.Message}");
    return 3;
}
if (proc is null)
{
    Console.Error.WriteLine($"progress: failed to start '{cmdArgs[0]}'.");
    return 3;
}

var stdoutBuffer = new StringBuilder();
var stderrBuffer = new StringBuilder();
proc.OutputDataReceived += (_, e) => { if (e.Data is not null) stdoutBuffer.AppendLine(e.Data); };
proc.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderrBuffer.AppendLine(e.Data); };
proc.BeginOutputReadLine();
proc.BeginErrorReadLine();

var system = new ConsoleWindowSystem(new NetConsoleDriver(RenderMode.Buffer));

var progressBar = Controls.ProgressBar()
    .WithHeader($"Running: {string.Join(' ', cmdArgs)}")
    .Indeterminate(true)
    .Stretch()
    .Build();

var window = new WindowBuilder(system)
    .WithTitle("progress")
    .WithSize(70, 7)
    .AddControl(progressBar)
    .Build();

system.AddWindow(window);

_ = Task.Run(() =>
{
    proc.WaitForExit();
    system.Shutdown();
});

system.Run();

if (!proc.HasExited)
    proc.WaitForExit();

if (stdoutBuffer.Length > 0)
    Console.Out.Write(stdoutBuffer.ToString());
if (stderrBuffer.Length > 0)
    Console.Error.Write(stderrBuffer.ToString());

return proc.ExitCode;
