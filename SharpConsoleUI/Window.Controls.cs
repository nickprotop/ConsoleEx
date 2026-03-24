// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Core;
using System.Drawing;

namespace SharpConsoleUI
{
	public partial class Window
	{
	/// <inheritdoc/>
	public void AddControl(IWindowControl content)
	{
		lock (_lock)
		{
			// Delegate to content manager for core collection management
			_contentManager.AddControl(_controls, _interactiveContents, content, this);

			// Trigger DOM rebuild so layout is ready for focus/scroll calculations
			EnsureContentReady();

			// Auto-focus the first interactive control added to the window.
			// Only triggers when no control is currently focused (prevents stealing focus).
			if (content is IInteractiveControl && FocusManager.FocusedControl == null)
				FocusManager.MoveFocus(false);

			// Auto-scroll to bottom for non-sticky controls if nothing is focused
			if (content.StickyPosition == StickyPosition.None && FocusManager.FocusedControl == null)
				GoToBottom();
		}
	}
	/// <summary>
	/// Inserts a control at the specified index.
	/// </summary>
	public void InsertControl(int index, IWindowControl content)
	{
		lock (_lock)
		{
			_contentManager.InsertControl(_controls, _interactiveContents, index, content, this);
			EnsureContentReady();
		}
	}

	/// <summary>
	/// Removes all controls from the window.
	/// </summary>
	public void ClearControls()
	{
		lock (_lock)
		{
			// Dispose all controls first
			foreach (var content in _controls.ToList())
			{
				content.Dispose();
			}

			// Clear focus tracking through coordinator
			FocusManager.SetFocus(null, Controls.FocusReason.Programmatic);

			// Delegate to content manager for core clearing
			_contentManager.ClearControls(_controls, _interactiveContents);
		}
	}

		/// <summary>
		/// Removes a control from this window and disposes it.
		/// </summary>
		/// <param name="content">The control to remove.</param>
		public void RemoveContent(IWindowControl content)
	{
		lock (_lock)
		{
			// Handle focus logic before removing
			if (content is IInteractiveControl interactiveControl)
			{
				bool wasFocused = FocusManager.IsFocused(interactiveControl as IFocusableControl);

				if (wasFocused)
				{
					// Clear focus on the removed control
					FocusManager.SetFocus(null, Controls.FocusReason.Programmatic);
				}

				// After clearing, auto-focus next control if one exists
				if (wasFocused && _interactiveContents.Count > 1)
				{
					var nextControl = _interactiveContents.FirstOrDefault(ic => ic != interactiveControl);
					if (nextControl != null)
					{
						FocusManager.SetFocus(nextControl as Controls.IFocusableControl, Controls.FocusReason.Programmatic);
					}
				}
			}

			// Delegate to content manager for core removal
			if (_contentManager.RemoveControl(_controls, _interactiveContents, content))
			{
				// Dispose the control
				content.Dispose();

				// Trigger DOM rebuild
				EnsureContentReady();

				// Auto-scroll to bottom
				GoToBottom();
			}
		}
	}

		/// <summary>
		/// Determines whether this window contains the specified control.
		/// </summary>
		/// <param name="content">The control to check for.</param>
		/// <returns>True if the control is in this window; otherwise false.</returns>
		public bool ContainsControl(IWindowControl content)
		{
			lock (_lock)
			{
				return _controls.Contains(content);
			}
		}

		/// <summary>
		/// Gets the control at the specified desktop coordinates.
		/// </summary>
		/// <param name="point">The desktop coordinates to check.</param>
		/// <returns>The control at the specified position, or null if none found.</returns>
		public IWindowControl? GetContentFromDesktopCoordinates(Point? point)
		{
			lock (_lock)
			{
				if (point == null) return null;
				if (_windowSystem == null) return null;

				// Translate the coordinates to the relative position within the window
				var relativePosition = GeometryHelpers.TranslateToRelative(this, point, _windowSystem.DesktopUpperLeft.Y);

				return _eventDispatcher?.GetControlAtPosition(relativePosition);
			}
		}

		/// <summary>
		/// Gets a control by its index in the controls collection.
		/// </summary>
		/// <param name="index">The zero-based index of the control.</param>
		/// <returns>The control at the specified index, or null if out of range.</returns>
		public IWindowControl? GetControlByIndex(int index)
		{
			lock (_lock)
			{
				if (index >= 0 && index < _controls.Count)
				{
					return _controls[index];
				}
				return null;
			}
		}

		/// <summary>
		/// Gets a control of type T by its tag value.
		/// </summary>
		/// <typeparam name="T">The type of control to search for.</typeparam>
		/// <param name="tag">The tag value to match.</param>
		/// <returns>The first matching control, or null if not found.</returns>
		public IWindowControl? GetControlByTag<T>(string tag) where T : IWindowControl
		{
			lock (_lock)
			{
				return _controls.FirstOrDefault(c => c is T && c.Tag?.ToString() == tag);
			}
		}

		/// <summary>
		/// Finds a control by name, searching recursively through all containers.
		/// </summary>
		/// <typeparam name="T">The type of control to find.</typeparam>
		/// <param name="name">The name of the control to find.</param>
		/// <returns>The control if found, otherwise null.</returns>
		public T? FindControl<T>(string name) where T : class, IWindowControl
		{
			lock (_lock)
			{
				return FindControlRecursive(_controls, name) as T;
			}
		}

		/// <summary>
		/// Finds a control by name, searching recursively through all containers.
		/// </summary>
		/// <param name="name">The name of the control to find.</param>
		/// <returns>The control if found, otherwise null.</returns>
		public IWindowControl? FindControl(string name)
		{
			lock (_lock)
			{
				return FindControlRecursive(_controls, name);
			}
		}

		/// <summary>
		/// Gets all named controls as a dictionary for batch access.
		/// </summary>
		/// <returns>A dictionary mapping control names to controls.</returns>
		public IReadOnlyDictionary<string, IWindowControl> GetNamedControls()
		{
			var result = new Dictionary<string, IWindowControl>();
			lock (_lock)
			{
				CollectNamedControls(_controls, result);
			}
			return result;
		}

		private static IWindowControl? FindControlRecursive(IEnumerable<IWindowControl> controls, string name)
		{
			foreach (var control in controls)
			{
				if (control.Name == name)
					return control;

				// Search nested containers
				var nested = GetNestedControls(control);
				if (nested != null)
				{
					var found = FindControlRecursive(nested, name);
					if (found != null)
						return found;
				}
			}
			return null;
		}

		private static void CollectNamedControls(IEnumerable<IWindowControl> controls, Dictionary<string, IWindowControl> result)
		{
			foreach (var control in controls)
			{
				if (!string.IsNullOrEmpty(control.Name) && !result.ContainsKey(control.Name))
				{
					result[control.Name] = control;
				}

				// Collect from nested containers
				var nested = GetNestedControls(control);
				if (nested != null)
				{
					CollectNamedControls(nested, result);
				}
			}
		}

		private static IEnumerable<IWindowControl>? GetNestedControls(IWindowControl control)
		{
			return control switch
			{
				ToolbarControl toolbar => toolbar.Items,
				HorizontalGridControl grid => grid.Columns.SelectMany(c => c.Contents),
				ColumnContainer column => column.Contents,
			ScrollablePanelControl panel => panel.Children,
				TabControl tabControl => tabControl.TabPages.Select(tp => tp.Content),
				_ => null
			};
		}

		/// <summary>
		/// Gets a copy of all controls in this window.
		/// </summary>
		/// <returns>A list containing all controls.</returns>
		public List<IWindowControl> GetControls()
		{
			lock (_lock)
			{
				return _controls.ToList(); // Return a copy to avoid external modification
			}
		}

		/// <summary>
		/// Gets all controls of the specified type.
		/// </summary>
		/// <typeparam name="T">The type of controls to retrieve.</typeparam>
		/// <returns>A list of controls of type T.</returns>
		public List<T> GetControlsByType<T>() where T : IWindowControl
		{
			lock (_lock)
			{
				return _controls.OfType<T>().ToList();
			}
		}

		/// <summary>
		/// Gets the number of controls in this window.
		/// </summary>
		/// <returns>The control count.</returns>
		public int GetControlsCount()
		{
			lock (_lock)
			{
				return _controls.Count;
			}
		}

		/// <summary>
		/// Changes the order of a control in the rendering sequence.
		/// </summary>
		/// <param name="content">The control to reorder.</param>
		/// <param name="newIndex">The new index position for the control.</param>
		public void UpdateContentOrder(IWindowControl content, int newIndex)
		{
			lock (_lock)
			{
				if (_controls.Contains(content) && newIndex >= 0 && newIndex < _controls.Count)
				{
					_controls.Remove(content);
					_controls.Insert(newIndex, content);
					_invalidated = true;
					Invalidate(true);
				}
			}
		}
	}
}
