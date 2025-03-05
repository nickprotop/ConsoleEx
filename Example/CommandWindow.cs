using ConsoleEx;
using ConsoleEx.Controls;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleEx.Example
{
	public class CommandWindow
	{
		private readonly MultilineEditControl _outputControl;
		private readonly PromptControl _promptControl;
		private Process? _cmdProcess;
		private ConsoleWindowSystem _consoleWindowSystem;
		private StringBuilder _outputBuffer = new StringBuilder();
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
				Prompt = "CMD> ",
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

			// Initialize the interactive command process
			InitializeCommandProcess();
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
			try
			{
				if (_cmdProcess == null || _cmdProcess.HasExited)
				{
					_outputBuffer.AppendLine("Command process not running. Restarting...");
					InitializeCommandProcess();
					if (_cmdProcess == null)
					{
						_outputBuffer.AppendLine("Failed to restart command process.");
						_outputControl.SetContent(_outputBuffer.ToString());
						return;
					}
				}

				// Display the command in the output
				_outputBuffer.AppendLine($"\n> {command}");
				_outputControl.SetContent(_outputBuffer.ToString());

				// Send the command to the process
				await _cmdProcess.StandardInput.WriteLineAsync(command);
				await _cmdProcess.StandardInput.FlushAsync();

				// Clear the input for the next command
				sender.SetInput(string.Empty);

				// Special handling for exit command
				if (command.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase))
				{
					_outputBuffer.AppendLine("Command session terminated.");
					_outputControl.SetContent(_outputBuffer.ToString());
					_outputControl.GoToEnd();

					// Don't dispose the process here as it will be handled by process.OutputDataReceived
				}
			}
			catch (Exception ex)
			{
				_outputBuffer.AppendLine($"Error executing command: {ex.Message}");
				_outputControl.SetContent(_outputBuffer.ToString());
				_outputControl.GoToEnd();
			}
		}

		private void InitializeCommandProcess()
		{
			try
			{
				// Set up a persistent cmd process
				var processStartInfo = new ProcessStartInfo
				{
					FileName = "cmd.exe",
					Arguments = "/q /k chcp 65001", // /q = quiet, /k = keep running, set UTF-8 codepage
					RedirectStandardInput = true,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					UseShellExecute = false,
					CreateNoWindow = true,
					StandardInputEncoding = Encoding.UTF8,
					StandardOutputEncoding = Encoding.UTF8,
					StandardErrorEncoding = Encoding.UTF8
				};

				_cmdProcess = new Process { StartInfo = processStartInfo };
				_cmdProcess.OutputDataReceived += (sender, args) =>
				{
					if (args.Data != null)
					{
						_outputBuffer.AppendLine(args.Data);
						_outputControl.SetContent(_outputBuffer.ToString());
						_outputControl.GoToEnd();
					}
				};
				_cmdProcess.ErrorDataReceived += (sender, args) =>
				{
					if (args.Data != null)
					{
						_outputBuffer.AppendLine($"Error: {args.Data}");
						_outputControl.SetContent(_outputBuffer.ToString());
						_outputControl.GoToEnd();
					}
				};

				_cmdProcess.Start();
				_cmdProcess.BeginOutputReadLine();
				_cmdProcess.BeginErrorReadLine();

				// Add initial message
				_outputBuffer.AppendLine("Interactive command prompt started. Type 'exit' to close the session.");
				_outputControl.SetContent(_outputBuffer.ToString());
			}
			catch (Exception ex)
			{
				_outputControl.SetContent($"Error initializing command prompt: {ex.Message}");
			}
		}
	}
}