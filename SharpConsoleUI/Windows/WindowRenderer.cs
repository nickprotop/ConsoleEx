// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Logging;
using Color = Spectre.Console.Color;

namespace SharpConsoleUI.Windows
{
	/// <summary>
	/// Coordinates window rendering operations for the DOM-based layout system.
	/// Extracted from Window class as part of Phase 3.2 refactoring.
	/// Provides helper methods for building layout trees and performing layout passes.
	/// </summary>
	public class WindowRenderer
	{
		private readonly ILogService? _logService;
		private readonly Func<string> _getWindowTitle;

		/// <summary>
		/// Initializes a new instance of the WindowRenderer class.
		/// </summary>
		/// <param name="getWindowTitle">Function to get the window title (for logging)</param>
		/// <param name="logService">Optional log service for diagnostic logging</param>
		public WindowRenderer(
			Func<string> getWindowTitle,
			ILogService? logService)
		{
			_getWindowTitle = getWindowTitle;
			_logService = logService;
		}

		/// <summary>
		/// Builds a layout node tree from a control and its children.
		/// </summary>
		/// <param name="control">The root control</param>
		/// <param name="controlToNodeMap">Map to populate with control-to-node mappings</param>
		/// <param name="windowBackground">The window background color</param>
		/// <returns>The root layout node</returns>
		public LayoutNode BuildNodeTree(
			IWindowControl control,
			Dictionary<IWindowControl, LayoutNode> controlToNodeMap,
			Color windowBackground)
		{
			_logService?.LogTrace($"Building node tree for control {control.GetType().Name} in window '{_getWindowTitle()}'", "Renderer");

			var node = new LayoutNode(control);
			node.IsVisible = true;
			controlToNodeMap[control] = node;

			// Recursively build children if control has child controls
			// Note: Using reflection to check for GetChildren method to avoid interface dependency
			var getChildrenMethod = control.GetType().GetMethod("GetChildren");
			if (getChildrenMethod != null)
			{
				var children = getChildrenMethod.Invoke(control, null) as IEnumerable<IWindowControl>;
				if (children != null)
				{
					foreach (var child in children)
					{
						var childNode = BuildNodeTree(child, controlToNodeMap, windowBackground);
						node.AddChild(childNode);
					}
				}
			}

			return node;
		}

		/// <summary>
		/// Performs the Measure pass of the layout system.
		/// </summary>
		/// <param name="rootNode">The root layout node</param>
		/// <param name="contentWidth">Available content width</param>
		/// <param name="contentHeight">Available content height</param>
		public void PerformMeasurePass(LayoutNode rootNode, int contentWidth, int contentHeight)
		{
			_logService?.LogTrace($"Performing measure pass for window '{_getWindowTitle()}' ({contentWidth}x{contentHeight})", "Renderer");

			var constraints = LayoutConstraints.Loose(contentWidth, contentHeight);
			rootNode.Measure(constraints);
		}

		/// <summary>
		/// Performs the Arrange pass of the layout system.
		/// </summary>
		/// <param name="rootNode">The root layout node</param>
		/// <param name="contentWidth">Available content width</param>
		/// <param name="contentHeight">Available content height</param>
		public void PerformArrangePass(LayoutNode rootNode, int contentWidth, int contentHeight)
		{
			_logService?.LogTrace($"Performing arrange pass for window '{_getWindowTitle()}'", "Renderer");

			rootNode.Arrange(new LayoutRect(0, 0, contentWidth, contentHeight));
		}

		/// <summary>
		/// Converts a character buffer to a list of ANSI-formatted strings.
		/// </summary>
		/// <param name="buffer">The character buffer</param>
		/// <param name="defaultForeground">Default foreground color</param>
		/// <param name="defaultBackground">Default background color</param>
		/// <returns>List of ANSI-formatted strings</returns>
		public List<string> BufferToLines(CharacterBuffer buffer, Color defaultForeground, Color defaultBackground)
		{
			_logService?.LogTrace($"Converting buffer to lines for window '{_getWindowTitle()}'", "Renderer");

			return buffer.ToLines(defaultForeground, defaultBackground);
		}
	}
}
