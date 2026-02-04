// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using Spectre.Console;

namespace SharpConsoleUI.Windows
{
	/// <summary>
	/// Helper class for window lifecycle operations: async cleanup, grace periods, and error states.
	/// Extracted from Window.cs to reduce complexity and improve maintainability.
	/// </summary>
	internal static class WindowLifecycleHelper
	{
		/// <summary>
		/// Begins the grace period for window thread cleanup with visual feedback.
		/// Extracted from Window.BeginGracePeriodClose (lines 956-1058, 102 lines).
		/// </summary>
		/// <param name="window">The window being closed</param>
		/// <param name="windowTask">The async window task to wait for</param>
		/// <param name="cts">Cancellation token source for the window thread</param>
		public static void BeginGracePeriodClose(
			Window window,
			Task windowTask,
			CancellationTokenSource cts)
		{
			var originalTitle = window.Title;
			window.Title = $"{originalTitle} [Closing...]";

			// Add status indicator
			var statusControl = new MarkupControl(new List<string>
			{
				"[yellow on grey11] ⏳ Waiting for window thread to stop... [/]"
			});
			window.AddControl(statusControl);

			// Lock down window during grace period
			window.IsResizable = false;
			window.IsMovable = false;
			var wasClosable = window.IsClosable;
			window.IsClosable = false;
			window.Invalidate(true);

			// Start countdown timer
			var remainingSeconds = (int)window.AsyncThreadCleanupTimeout.TotalSeconds;
			var countdownTimer = new System.Timers.Timer(1000);
			countdownTimer.Elapsed += (s, e) =>
			{
				remainingSeconds--;

				// After threshold, show countdown
				if (remainingSeconds <= ControlDefaults.GracePeriodWarningThresholdSeconds)
				{
					statusControl.SetContent(new List<string>
					{
						$"[yellow on grey11] ⏳ Waiting for thread to stop... ({remainingSeconds}s remaining) [/]"
					});
					window.Invalidate(true);
				}
			};
			countdownTimer.Start();

			// Wait for completion or timeout
			_ = Task.Run(async () =>
			{
				try
				{
					var completedTask = await Task.WhenAny(
						windowTask,
						Task.Delay(window.AsyncThreadCleanupTimeout)
					);

					countdownTimer.Stop();
					countdownTimer.Dispose();

					if (completedTask == windowTask)
					{
						// SUCCESS: Thread stopped gracefully
						await windowTask; // Propagate exceptions

						// Remove status control and restore window
						window.RemoveContent(statusControl);
						window.Title = originalTitle;

						// Close via system (handles removal and CompleteClose)
						// If not in system (orphan or already removed), call CompleteClose directly
						if (window._windowSystem == null || !window._windowSystem.CloseWindow(window, force: true))
						{
							window.CompleteClose();
						}

						window._windowSystem?.LogService?.LogDebug(
							$"Window thread stopped gracefully within timeout",
							"Window");
					}
					else
					{
						// TIMEOUT: Thread hung - transform to error window
						statusControl.SetContent(new List<string>
						{
							"[red on yellow] ⚠ Thread did not respond - transforming to error state... [/]"
						});
						window.Invalidate(true);

						await Task.Delay(ControlDefaults.ErrorTransformDelayMs); // Brief pause so user sees message

						TransformToErrorWindow(window, statusControl);
					}
				}
				catch (Exception ex)
				{
					window._windowSystem?.LogService?.LogError(
						$"Error during grace period: {ex.Message}",
						ex, "Window");

					// Fallback: force close via system or directly if not in system
					if (window._windowSystem == null || !window._windowSystem.CloseWindow(window, force: true))
					{
						window.CompleteClose();
					}
				}
				finally
				{
					cts?.Dispose();
				}
			});
		}

		/// <summary>
		/// Transforms a window into an error boundary showing hung thread information.
		/// Extracted from Window.TransformToErrorWindow (lines 1078-1178, 100 lines).
		/// </summary>
		/// <param name="window">The window to transform</param>
		/// <param name="statusControl">The status control to remove (if any)</param>
		public static void TransformToErrorWindow(
			Window window,
			IWindowControl? statusControl)
		{
			try
			{
				var originalTitle = window.Title.Replace("[Closing...]", "").Trim();

				// Build error message
				var errorLines = BuildErrorContent(window, originalTitle);

				// Calculate required size (strip markup for length calculation)
				var (width, height) = CalculateRequiredSize(errorLines);

				// Apply error state
				ApplyErrorState(window, errorLines, width, height);

				window._windowSystem?.LogService?.LogCritical(
					$"Window thread hung and did not respond to cancellation. " +
					$"Original window '{originalTitle}' transformed to error boundary (AlwaysOnTop, movable).",
					null, "Window");
			}
			catch (Exception ex)
			{
				// If transformation fails, fallback to logging
				window._windowSystem?.LogService?.LogCritical(
					$"Failed to transform hung window to error state: {ex.Message}",
					ex, "Window");
			}
		}

		/// <summary>
		/// Builds the error message content for a hung window thread.
		/// </summary>
		private static List<string> BuildErrorContent(Window window, string originalTitle)
		{
			return new List<string>
			{
				"",
				"[bold red]⚠ WINDOW THREAD HUNG ⚠[/]",
				"",
				$"[yellow]Window:[/] {originalTitle}",
				$"[yellow]Timeout:[/] {(int)window.AsyncThreadCleanupTimeout.TotalSeconds} seconds",
				"",
				"[white]The window's background thread did not respond to[/]",
				"[white]cancellation within the timeout period.[/]",
				"",
				"[bold cyan]Cause:[/]",
				"[white]• Infinite loop without checking CancellationToken[/]",
				"[white]• Blocking operation ignoring cancellation[/]",
				"",
				"[bold cyan]How to fix:[/]",
				"[white]1. Check [cyan]ct.IsCancellationRequested[/] in loops[/]",
				"[white]2. Pass token: [cyan]await Task.Delay(ms, ct)[/][/]",
				"[white]3. Use [cyan]ct.ThrowIfCancellationRequested()[/][/]",
				"",
				"[grey]Move this window aside to continue debugging.[/]",
				""
			};
		}

		/// <summary>
		/// Calculates the required window size based on error content.
		/// </summary>
		private static (int width, int height) CalculateRequiredSize(List<string> errorLines)
		{
			var maxLineLength = errorLines
				.Select(line => Helpers.AnsiConsoleHelper.StripSpectreLength(line))
				.Max();

			var requiredWidth = Math.Max(
				ControlDefaults.MinimumErrorWindowWidth,
				maxLineLength + ControlDefaults.ErrorWindowBorderOffset);
			var requiredHeight = errorLines.Count + ControlDefaults.ErrorWindowSpacingOffset;

			return (requiredWidth, requiredHeight);
		}

		/// <summary>
		/// Applies the error state to the window: clears controls, adds error UI, and configures behavior.
		/// </summary>
		private static void ApplyErrorState(Window window, List<string> errorLines, int width, int height)
		{
			// Remove ALL existing controls
			foreach (var control in window._controls.ToList())
			{
				window.RemoveContent(control);
				try { (control as IDisposable)?.Dispose(); }
				catch { /* Ignore disposal errors */ }
			}

			// Resize and center window
			window.Width = width;
			window.Height = height;
			if (window._windowSystem != null)
			{
				window.Left = Math.Max(0, (window._windowSystem.DesktopDimensions.Width - width) / 2);
				window.Top = Math.Max(0, (window._windowSystem.DesktopDimensions.Height - height) / 2);
			}

			// Add error content
			var errorControl = new MarkupControl(errorLines);
			window.AddControl(errorControl);

			// Add quit button
			var quitButton = new ButtonControl
			{
				Text = "[white on red] Force Quit Application [/]"
			};
			quitButton.Click += (sender, e) =>
			{
				window._windowSystem?.LogService?.LogCritical(
					"User force quit application due to hung window thread",
					null, "Window");
				window._windowSystem?.Shutdown(1);
			};
			window.AddControl(quitButton);

			// Configure window behavior
			window._isClosing = false;         // Allow window to live
			window.IsResizable = false;        // Can't resize (content is fixed)
			window.IsClosable = false;         // Can't close (would hide error!)
			window.IsMovable = true;           // ✅ CAN MOVE - user can move it aside
			window.AlwaysOnTop = true;         // ✅ ALWAYS VISIBLE - can't hide the error
			window.Title = "⚠ HUNG THREAD ERROR";
			window.BackgroundColor = Color.Red;
			window.ForegroundColor = Color.White;

			// Bring to front
			window._windowSystem?.WindowStateService.BringToFront(window);
			window.Invalidate(true);
		}
	}
}
