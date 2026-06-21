// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;

namespace SharpConsoleUI.Controls;

/// <summary>
/// Implemented by a transparent (<c>CanReceiveFocus == false</c>) <see cref="IFocusScope"/> whose
/// complete Tab order includes stops that are NOT exposed through <see cref="IContainerControl.GetChildren"/>.
/// </summary>
/// <remarks>
/// The window's global Tab list (<c>WindowRootScope</c>) normally builds itself by recursing into a
/// transparent scope's DOM children. That works when every Tab stop is a real child control (as with
/// <c>HorizontalGridControl</c>, whose splitters are <c>SplitterControl</c> DOM nodes). It does NOT work
/// for a scope with grid-native, non-DOM stops — e.g. <see cref="GridControl"/>, whose splitters live in
/// the grid's own splitter list and never appear in <c>GetChildren()</c>. Such a scope returns its full
/// ordered Tab-stop list here so the root scope can enumerate it verbatim, keeping splitters reachable as
/// first-class Tab stops (including the case where the very first stop is a splitter at index 0).
/// </remarks>
internal interface IFlattenableFocusScope
{
	/// <summary>
	/// Returns this scope's complete ordered list of focusable Tab stops, in Tab order, including any
	/// non-DOM stops (e.g. grid-native splitters). The root scope inserts this list in place of recursing
	/// into the scope's DOM children.
	/// </summary>
	List<IFocusableControl> GetOrderedTabStops();
}
