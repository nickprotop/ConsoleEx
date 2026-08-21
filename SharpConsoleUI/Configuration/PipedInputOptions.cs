// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

// SharpConsoleUI/Configuration/PipedInputOptions.cs
namespace SharpConsoleUI.Configuration;

/// <summary>
/// Configuration for capturing text piped into the application via stdin
/// (<see cref="SharpConsoleUI.ConsoleWindowSystem.PipedInput"/>).
/// </summary>
/// <remarks>
/// Reading redirected stdin cannot be given a single correct deadline: <c>echo x | app</c> ends
/// immediately, while <c>tail -f log | app</c> is designed never to end. The capture therefore runs
/// off the startup path, and these options tune how long each side is prepared to wait for it.
/// </remarks>
/// <param name="Enabled">
/// Whether redirected stdin is captured at all. Set false to leave stdin entirely to the application,
/// in which case <see cref="SharpConsoleUI.ConsoleWindowSystem.PipedInput"/> is always null.
/// </param>
/// <param name="PreUiTimeoutMs">
/// How long reading <see cref="SharpConsoleUI.ConsoleWindowSystem.PipedInput"/> before the UI starts
/// waits for the capture to finish, in milliseconds. On timeout the property returns the text
/// received so far rather than continuing to block.
/// </param>
/// <param name="ShowDialog">
/// Whether the system shows a cancellable progress dialog when the capture is still running after the
/// UI is up. Set false to let the capture continue silently in the background.
/// </param>
/// <param name="DialogDelayMs">
/// How long the capture may remain pending once the UI is up before the dialog appears, in
/// milliseconds. Keeps the dialog from flashing for input that arrives promptly.
/// </param>
/// <param name="DialogTitle">Title of the progress dialog.</param>
/// <param name="DialogMessage">Status text shown in the progress dialog while the capture runs.</param>
public record PipedInputOptions(
	bool Enabled = true,
	int PreUiTimeoutMs = SystemDefaults.PipedInputPreUiTimeoutMs,
	bool ShowDialog = true,
	int DialogDelayMs = SystemDefaults.PipedInputDialogDelayMs,
	string DialogTitle = "Reading input",
	string DialogMessage = "Reading piped input from stdin..."
);
