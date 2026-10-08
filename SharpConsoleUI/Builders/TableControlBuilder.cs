// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.DataBinding;

namespace SharpConsoleUI.Builders;

/// <summary>
/// Fluent builder for creating TableControl instances with comprehensive configuration.
/// </summary>
/// <remarks>
/// Every configuration method lives on <see cref="TableControlBuilderBase{TSelf}"/>, shared with the
/// builders of tables derived from <see cref="TableControl"/>.
/// </remarks>
public sealed class TableControlBuilder : TableControlBuilderBase<TableControlBuilder>, IControlBuilder<TableControl>
{
	/// <summary>
	/// Builds the TableControl with all configured options.
	/// </summary>
	public TableControl Build() => Apply(new TableControl());

	/// <summary>
	/// Implicit conversion to TableControl.
	/// </summary>
	public static implicit operator TableControl(TableControlBuilder builder) => builder.Build();
}
