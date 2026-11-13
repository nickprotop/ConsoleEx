// -----------------------------------------------------------------------
// AgentStudio - an agent-workbench UI, built as a showcase
//
// The layout and controls of a multi-agent terminal workbench. The agents
// are not real: nothing here talks to a model, and it is a visual showcase.
//
// cxagent grew out of this showcase and is built entirely on SharpConsoleUI:
// https://github.com/nickprotop/cxagent
//
// Author: Nikolaos Protopapas
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Helpers;

namespace AgentStudio;

internal class Program
{
	static async Task<int> Main(string[] args)
	{
		try
		{
			// Initialize window system with driver options (demonstrates advanced configuration)
			var driverOptions = new NetConsoleDriverOptions
			{
				RenderMode = RenderMode.Buffer,
				BufferSize = 8192,  // Future use
				CursorBlinkRate = 500  // Future use
			};
			var driver = new NetConsoleDriver(driverOptions);
			// The app draws its own top and bottom StatusBar rows (inside AgentStudioWindow's grid),
			// so the window-system's built-in top/bottom panels are turned off to avoid doubled bars.
			var windowSystem = new ConsoleWindowSystem(
				driver,
				options: new ConsoleWindowSystemOptions(
					ShowTopPanel: false,
					ShowBottomPanel: false
				));

			// Setup graceful shutdown
			Console.CancelKeyPress += (sender, e) =>
			{
				e.Cancel = true;
				windowSystem?.Shutdown(0);
			};

			// Create and show the main window
			using var studioWindow = new AgentStudioWindow(windowSystem);
			studioWindow.Show();

			// Run the application
			await Task.Run(() => windowSystem.Run());

			return 0;
		}
		catch (Exception ex)
		{
			Console.Clear();
			ExceptionFormatter.WriteException(ex);
			return 1;
		}
	}
}
