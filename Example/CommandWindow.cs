﻿using ConsoleEx;
using ConsoleEx.Controls;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleEx.Example
{
	public class CommandWindow
	{
		private readonly MultilineEditControl _outputControl;
		private readonly PromptControl _promptControl;
		private ConsoleWindowSystem _consoleWindowSystem;
		private Window _window;

		public CommandWindow(ConsoleWindowSystem consoleWindowSystem)
		{
			_consoleWindowSystem = consoleWindowSystem;

			_window = new Window(consoleWindowSystem, WindowThread)
			{
				Top = 0,
				Left = 0,
				Width = 80,
				Height = 25,
				Title = "Command Window"
			};

			_promptControl = new PromptControl
			{
				Prompt = "Enter command: ",
				OnEnter = ExecuteCommand,
				UnfocusOnEnter = false,
				StickyPosition = StickyPosition.Top
			};

			_outputControl = new MultilineEditControl
			{
				Width = 80,
				ViewportHeight = _window.Height - 2 - 2,
				WrapMode = WrapMode.Wrap,
				ReadOnly = true
			};

			_window.AddContent(_promptControl);

			_window.AddContent(new RuleControl() { StickyPosition = StickyPosition.Top });

			_window.AddContent(_outputControl);

			_window.OnResize += (sender, args) =>
			{
				_outputControl.ViewportHeight = _window.Height - 2 - 2;
				_outputControl.Width = _window.Width - 2;
			};
		}

		public Window Window
		{
			get { return _window; }
		}

		public void WindowThread(Window window)
		{
		}

		private async void ExecuteCommand(PromptControl sender, string command)
		{
			sender.SetInput(string.Empty);

			try
			{
				// Set up process with UTF-8 encoding for proper Unicode support
				var processStartInfo = new ProcessStartInfo("cmd", $"/c chcp 65001 >nul && {command}")
				{
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					UseShellExecute = false,
					CreateNoWindow = true,
					StandardOutputEncoding = Encoding.UTF8,
					StandardErrorEncoding = Encoding.UTF8
				};

				using var process = new Process { StartInfo = processStartInfo };
				process.Start();

				var outputBuilder = new StringBuilder();

				// Read the output asynchronously with UTF-8 encoding
				while (!process.StandardOutput.EndOfStream)
				{
					var line = await process.StandardOutput.ReadLineAsync();

					if (line != null)
					{
						outputBuilder.AppendLine(line);
						_outputControl.SetContent(outputBuilder.ToString());
					}
				}

				// Ensure any remaining output is captured
				var remainingOutput = await process.StandardOutput.ReadToEndAsync();
				if (!string.IsNullOrEmpty(remainingOutput))
				{
					outputBuilder.Append(remainingOutput);
					_outputControl.SetContent(outputBuilder.ToString());
				}

				// Check for errors with UTF-8 encoding
				var errorOutput = await process.StandardError.ReadToEndAsync();
				if (!string.IsNullOrEmpty(errorOutput))
				{
					outputBuilder.AppendLine("\nErrors:");
					outputBuilder.AppendLine(errorOutput);
					_outputControl.SetContent(outputBuilder.ToString());
				}

				process.WaitForExit();
			}
			catch (Exception ex)
			{
				_outputControl.SetContent($"Error: {ex.Message}");
			}
		}
	}
}