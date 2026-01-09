// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;

namespace SharpConsoleUI.Events;

/// <summary>
/// Event arguments for tree node events
/// </summary>
public class TreeNodeEventArgs : EventArgs
{
	/// <summary>
	/// The tree node involved in the event
	/// </summary>
	public TreeNode? Node { get; }

	/// <summary>
	/// Creates new TreeNodeEventArgs
	/// </summary>
	/// <param name="node">The node involved in the event</param>
	public TreeNodeEventArgs(TreeNode? node)
	{
		Node = node;
	}
}
