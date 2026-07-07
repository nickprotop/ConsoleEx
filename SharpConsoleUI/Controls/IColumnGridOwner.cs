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
	/// The contract a control must satisfy to OWN <see cref="ColumnContainer"/> children (a horizontal grid
	/// strip). ColumnContainer uses only the owner's foreground color, its container back-link, its invalidation
	/// entry, and its identity — never any HGC-specific member. Implemented by both
	/// <see cref="HorizontalGridControl"/> and (later) HorizontalGridControl so a column can be hosted by either.
	/// </summary>
	public interface IColumnGridOwner : IWindowControl
	{
		/// <summary>
		/// Gets the foreground color the owning grid contributes to its columns' color-resolution chain.
		/// Nullable so an owner may defer to its own container/theme (matches
		/// <see cref="HorizontalGridControl.ForegroundColor"/>).
		/// </summary>
		Color? ForegroundColor { get; }

		/// <summary>
		/// Gets the owning grid's columns. Lets framework walks that need to descend into a column-grid's
		/// columns (nested-control position/containment) treat any column-grid owner uniformly, instead of
		/// type-checking the concrete <see cref="HorizontalGridControl"/>.
		/// </summary>
		List<ColumnContainer> Columns { get; }
	}
}
