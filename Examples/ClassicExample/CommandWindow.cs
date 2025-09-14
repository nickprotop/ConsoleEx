using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpConsoleUI.Example
{
	public class CommandWindow
	{
		private readonly MultilineEditControl _outputControl;
		private readonly PromptControl _promptControl;
		private Process? _cmdProcess;
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
				Prompt = "CMD> ",
				UnfocusOnEnter = false,
				StickyPosition = StickyPosition.Top
			};
			_promptControl.Entered += ExecuteCommand;

			_outputControl = new MultilineEditControl
			{
				ViewportHeight = _window.Height - 2 - 2,
				WrapMode = WrapMode.Wrap,
				ReadOnly = true
			};

			_window.AddControl(_promptControl);

			_window.AddControl(new RuleControl() { StickyPosition = StickyPosition.Top });

			_window.AddControl(_outputControl);

			_window.OnResize += (sender, args) =>
			{
				_outputControl.ViewportHeight = _window.Height - 2 - 2;
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

		private async void ExecuteCommand(object? sender, string command)
		{
			try
			{
				if (_cmdProcess == null || _cmdProcess.HasExited)
				{
					_outputControl.AppendContent("Command process not running. Restarting...\n");
					InitializeCommandProcess();
					if (_cmdProcess == null)
					{
						_outputControl.AppendContent("Failed to restart command process.\n");
						return;
					}
				}

				// Display the command in the output
				_outputControl.AppendContent($"\n> {command}\n");

				// Send the command to the process
				await _cmdProcess.StandardInput.WriteLineAsync(command);
				await _cmdProcess.StandardInput.FlushAsync();

				// Clear the input for the next command
				_promptControl.SetInput(string.Empty);

				// Special handling for exit command
				if (command.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase))
				{
					_outputControl.AppendContent("Command session terminated.\n");
					_outputControl.GoToEnd();

					// Don't dispose the process here as it will be handled by process.OutputDataReceived
				}
			}
			catch (Exception ex)
			{
				_outputControl.AppendContent($"Error executing command: {ex.Message}\n");
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
						_outputControl.AppendContent(args.Data + "\n");
						_outputControl.GoToEnd();
					}
				};
				_cmdProcess.ErrorDataReceived += (sender, args) =>
				{
					if (args.Data != null)
					{
						_outputControl.AppendContent($"Error: {args.Data}\n");
						_outputControl.GoToEnd();
					}
				};

				_cmdProcess.Start();
				_cmdProcess.BeginOutputReadLine();
				_cmdProcess.BeginErrorReadLine();

				// Add initial message
				_outputControl.AppendContent("Interactive command prompt started. Type 'exit' to close the session.\n");
			}
			catch (Exception ex)
			{
				_outputControl.AppendContent($"Error initializing command prompt: {ex.Message}\n");
			}
		}
	}
}