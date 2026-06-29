// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Flows;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A discoverable, wizard-shaped <see cref="FlowControl"/>: an embeddable control that runs a
	/// multi-step <see cref="Flow.Wizard{TState}"/> <em>inline</em> inside an existing window's layout
	/// (banner + scrollable body + button toolbar), rather than opening a modal window per step.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>It IS a <see cref="FlowControl"/>.</b> <see cref="WizardControl"/> subclasses
	/// <see cref="FlowControl"/> the same way <see cref="PanelControl"/> subclasses
	/// <see cref="CollapsiblePanel"/> — a presetted, honestly-named specialization, not a wrapper. It
	/// inherits the entire inline-flow surface (rendering, focus/child hosting, <see cref="FlowControl.AsHost"/>,
	/// the <see cref="FlowControl.Run{TState}(FlowWizardBuilder{TState})"/> path, the idle
	/// <see cref="FlowControl.Placeholder"/>). It adds only a wizard-flavoured identity: a discoverable
	/// type name (reach for <c>new WizardControl()</c> when you want a wizard) and a wizard-friendly
	/// default placeholder. It does <b>not</b> re-implement the wizard loop or re-declare
	/// <c>Run</c> — the inherited <see cref="FlowControl.Run{TState}(FlowWizardBuilder{TState})"/> is
	/// the entry point, unchanged.
	/// </para>
	/// <para>
	/// <b>Generics live on the builder, not the control.</b> The control is non-generic (so it drops
	/// cleanly into the control tree and collections like every other control); the wizard's state type
	/// stays on <see cref="Flow.Wizard{TState}"/> (the builder), where it is used to author strongly
	/// typed steps and surfaces as <see cref="FlowResult{T}"/>.
	/// </para>
	/// <para>
	/// <b>Scope.</b> <see cref="WizardControl"/> runs a wizard <em>inline</em> (it is a
	/// <see cref="FlowControl"/>). To run a wizard as a <em>modal</em> instead, use
	/// <c>Flow.Wizard&lt;TState&gt;()...Run(ws, parent)</c> directly.
	/// </para>
	/// <example>
	/// <code>
	/// var wiz = new WizardControl();
	/// panel.AddControl(wiz);
	/// var result = await wiz.Run(Flow.Wizard&lt;InstallState&gt;()
	///     .WithStepIndicator()
	///     .Step((ctx, s) =&gt; ...)
	///     .Step((ctx, s) =&gt; ...));
	/// </code>
	/// </example>
	/// </remarks>
	public class WizardControl : FlowControl
	{
		/// <summary>
		/// Initializes a new <see cref="WizardControl"/> with a wizard-friendly idle placeholder.
		/// The placeholder is replaced by the wizard's steps while a wizard is running and restored
		/// when it ends; assign <see cref="FlowControl.Placeholder"/> to override it.
		/// </summary>
		public WizardControl()
		{
			Placeholder = Builders.Controls.Markup("[dim]No wizard running.[/]")
				.WithMargin(1, 1, 1, 1)
				.Build();
		}

		// The wizard entry point — Run&lt;TState&gt;(FlowWizardBuilder&lt;TState&gt;) — is inherited from
		// FlowControl unchanged; WizardControl is that surface, named and presetted for wizards. It
		// deliberately does NOT re-declare Run (that would be a redundant shadow); inheritance is the
		// honest IS-A, the same way PanelControl inherits CollapsiblePanel's container surface.
	}
}
