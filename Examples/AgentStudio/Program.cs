// -----------------------------------------------------------------------
// OpenCodeShowcase - AgentStudio TUI Showcase
//
// An OpenCode-inspired demo showcasing SharpConsoleUI capabilities
// NOT a real AI coding agent - purely a visual showcase
//
// Author: Nikolaos Protopapas
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Drivers;
using Spectre.Console;

namespace AgentStudio;

internal class Program
{
    static async Task<int> Main(string[] args)
    {
        try
        {
            // Initialize window system
            var windowSystem = new ConsoleWindowSystem(RenderMode.Buffer)
            {
                TopStatus = "AgentStudio - TUI Showcase",
                ShowTaskBar = false,
                ShowBottomStatus = false
            };

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
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }
}
