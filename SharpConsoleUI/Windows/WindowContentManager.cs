// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Core;
using SharpConsoleUI.Logging;
using SharpConsoleUI.Controls;

namespace SharpConsoleUI.Windows
{
	/// <summary>
	/// Coordinates window control management operations.
	/// Extracted from Window class as part of Phase 3.1 refactoring.
	/// Operates on Window's control lists to maintain backward compatibility.
	/// </summary>
	public class WindowContentManager
	{
		private readonly ILogService? _logService;
		private readonly Func<string> _getWindowTitle;
		private readonly Action _invalidateCallback;
		private readonly Action _invalidateLayoutCallback;

		/// <summary>
		/// Initializes a new instance of the WindowContentManager class.
		/// </summary>
		/// <param name="getWindowTitle">Function to get the window title (for logging purposes)</param>
		/// <param name="logService">Optional log service for diagnostic logging</param>
		/// <param name="invalidateCallback">Callback to invalidate the window when controls change</param>
		/// <param name="invalidateLayoutCallback">Callback to invalidate the layout tree when controls change</param>
		public WindowContentManager(
			Func<string> getWindowTitle,
			ILogService? logService,
			Action invalidateCallback,
			Action invalidateLayoutCallback)
		{
			_getWindowTitle = getWindowTitle;
			_logService = logService;
			_invalidateCallback = invalidateCallback;
			_invalidateLayoutCallback = invalidateLayoutCallback;
		}

		/// <summary>
		/// Adds a control to the control list.
		/// </summary>
		/// <param name="controls">The control list to add to</param>
		/// <param name="interactiveControls">The interactive control list to add to</param>
		/// <param name="control">The control to add</param>
		/// <param name="container">The container to set on the control</param>
		public void AddControl(
			List<IWindowControl> controls,
			List<IInteractiveControl> interactiveControls,
			IWindowControl control,
			IContainer container)
		{
			if (control == null)
				throw new ArgumentNullException(nameof(control));

			_logService?.LogDebug($"Control added to window '{_getWindowTitle()}': {control.GetType().Name}", "Window");

			control.Container = container;
			controls.Add(control);

			// Track interactive controls separately for event routing
			if (control is IInteractiveControl interactiveControl)
			{
				interactiveControls.Add(interactiveControl);
			}

			// Register the control with the InvalidationManager for proper coordination
			InvalidationManager.Instance.RegisterControl(control);

			// Notify that layout needs to be rebuilt
			_invalidateLayoutCallback();

			// Invalidate the window
			_invalidateCallback();
		}

		/// <summary>
		/// Inserts a control at the specified index.
		/// </summary>
		public void InsertControl(
			List<IWindowControl> controls,
			List<IInteractiveControl> interactiveControls,
			int index,
			IWindowControl control,
			IContainer container)
		{
			if (control == null)
				throw new ArgumentNullException(nameof(control));

			_logService?.LogDebug($"Control inserted into window '{_getWindowTitle()}' at index {index}: {control.GetType().Name}", "Window");

			control.Container = container;
			controls.Insert(index, control);

			if (control is IInteractiveControl interactiveControl)
			{
				interactiveControls.Add(interactiveControl);
			}

			InvalidationManager.Instance.RegisterControl(control);
			_invalidateLayoutCallback();
			_invalidateCallback();
		}

		/// <summary>
		/// Removes a control from the control list.
		/// </summary>
		/// <param name="controls">The control list to remove from</param>
		/// <param name="interactiveControls">The interactive control list to remove from</param>
		/// <param name="control">The control to remove</param>
		/// <returns>True if the control was removed; false if it wasn't found</returns>
		public bool RemoveControl(
			List<IWindowControl> controls,
			List<IInteractiveControl> interactiveControls,
			IWindowControl control)
		{
			if (control == null)
				return false;

			if (controls.Remove(control))
			{
				_logService?.LogDebug($"Control removed from window '{_getWindowTitle()}': {control.GetType().Name}", "Window");

				if (control is IInteractiveControl interactiveControl)
				{
					interactiveControls.Remove(interactiveControl);
				}

				// Unregister from InvalidationManager
				InvalidationManager.Instance.UnregisterControl(control);

				// Notify that layout needs to be rebuilt
				_invalidateLayoutCallback();

				// Invalidate the window
				_invalidateCallback();

				return true;
			}

			return false;
		}

		/// <summary>
		/// Removes all controls from the control lists.
		/// </summary>
		/// <param name="controls">The control list to clear</param>
		/// <param name="interactiveControls">The interactive control list to clear</param>
		public void ClearControls(
			List<IWindowControl> controls,
			List<IInteractiveControl> interactiveControls)
		{
			var controlsToRemove = controls.ToList();

			foreach (var control in controlsToRemove)
			{
				controls.Remove(control);

				if (control is IInteractiveControl interactiveControl)
				{
					interactiveControls.Remove(interactiveControl);
				}

				// Unregister from InvalidationManager
				InvalidationManager.Instance.UnregisterControl(control);
			}

			// Notify that layout needs to be rebuilt
			_invalidateLayoutCallback();

			// Invalidate the window
			_invalidateCallback();
		}
	}
}
