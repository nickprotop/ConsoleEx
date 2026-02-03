// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Dialogs;
using SharpConsoleUI.Models;

namespace SharpConsoleUI.StartMenu
{
	/// <summary>
	/// Coordinates start menu actions and display.
	/// Manages the collection of registered actions and shows the start menu dialog.
	/// Extracted from ConsoleWindowSystem as part of Phase 1.4 refactoring.
	/// </summary>
	public class StartMenuCoordinator
	{
		private readonly List<StartMenuAction> _actions = new();
		private readonly Func<ConsoleWindowSystem> _getWindowSystem;

		/// <summary>
		/// Initializes a new instance of the StartMenuCoordinator class.
		/// </summary>
		/// <param name="getWindowSystem">Function to get the window system (lazy to avoid circular dependency).</param>
		public StartMenuCoordinator(Func<ConsoleWindowSystem> getWindowSystem)
		{
			_getWindowSystem = getWindowSystem ?? throw new ArgumentNullException(nameof(getWindowSystem));
		}

		/// <summary>
		/// Registers a new action in the Start menu.
		/// </summary>
		/// <param name="name">Display name of the action.</param>
		/// <param name="callback">Callback to execute when action is selected.</param>
		/// <param name="category">Optional category for grouping actions.</param>
		/// <param name="order">Display order (lower values appear first).</param>
		public void RegisterAction(string name, Action callback, string? category = null, int order = 0)
		{
			var action = new StartMenuAction(name, callback, category, order);
			_actions.Add(action);
		}

		/// <summary>
		/// Removes an action from the Start menu by name.
		/// </summary>
		/// <param name="name">Name of the action to remove.</param>
		public void UnregisterAction(string name)
		{
			_actions.RemoveAll(a => a.Name == name);
		}

		/// <summary>
		/// Gets all registered Start menu actions.
		/// </summary>
		/// <returns>Read-only list of actions.</returns>
		public IReadOnlyList<StartMenuAction> GetActions() => _actions.AsReadOnly();

		/// <summary>
		/// Shows the Start menu dialog.
		/// </summary>
		public void ShowMenu()
		{
			var windowSystem = _getWindowSystem();
			StartMenuDialog.Show(windowSystem);
		}
	}
}
