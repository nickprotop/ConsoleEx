// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------


namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A container whose children are a linear list of <see cref="IWindowControl"/> that can
	/// be added, removed, cleared, and enumerated. Implemented by containers with a flat
	/// child model (e.g. <see cref="ScrollablePanelControl"/>, <see cref="ColumnContainer"/>,
	/// <see cref="Window"/>) — not by <c>TabControl</c>, <c>MenuControl</c>,
	/// <c>ToolbarControl</c>, or <c>NavigationView</c>, whose child models differ.
	/// </summary>
	/// <remarks>
	/// This is a capability interface. It lets a consumer mutate children without binding to
	/// a concrete container type, and is intentionally separate from <see cref="IContainer"/>,
	/// which is a rendering abstraction only.
	/// </remarks>
	public interface IControlHost
	{
		/// <summary>Adds a child control to the host.</summary>
		void AddControl(IWindowControl control);

		/// <summary>Removes a child control from the host.</summary>
		void RemoveControl(IWindowControl control);

		/// <summary>Removes all child controls from the host.</summary>
		void ClearControls();

		/// <summary>Gets the current child controls in the order they were added.</summary>
		IReadOnlyList<IWindowControl> Children { get; }
	}
}
