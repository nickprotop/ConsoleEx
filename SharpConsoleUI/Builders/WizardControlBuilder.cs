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
/// Fluent builder for <see cref="WizardControl"/>. Shares <see cref="FlowControlBuilderBase{TSelf}"/>'s
/// fluent surface (placeholder, name, margin, sizing, alignment, sticky position) — the same way
/// <see cref="WizardControl"/> shares <see cref="FlowControl"/> — and produces a <see cref="WizardControl"/>
/// from <see cref="Build"/>. Because the base is self-typed, the fluent chain keeps returning
/// <see cref="WizardControlBuilder"/>, so <c>Controls.Wizard().WithName(...).Build()</c> yields a
/// <see cref="WizardControl"/>. If no placeholder is set, the WizardControl's default placeholder is kept.
/// </summary>
public sealed class WizardControlBuilder : FlowControlBuilderBase<WizardControlBuilder>, IControlBuilder<WizardControl>
{
	/// <summary>Builds the configured <see cref="WizardControl"/>.</summary>
	public WizardControl Build() => Apply(new WizardControl());
}
