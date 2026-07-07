// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Threading.Tasks;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;

namespace SharpConsoleUI.Flows
{
	/// <summary>
	/// Verdict returned by a flow button or step outcome, controlling navigation.
	/// </summary>
	public enum FlowVerdict
	{
		/// <summary>Advance to the next step.</summary>
		Next,

		/// <summary>Return to the previous step.</summary>
		Back,

		/// <summary>Abort the flow; result is discarded.</summary>
		Cancel,

		/// <summary>Complete the flow successfully (final step).</summary>
		Finish,

		/// <summary>Keep the current step open (e.g. after failed validation).</summary>
		Stay,

		// Dialog-answer verdicts (appended after the navigation verdicts so existing numeric
		// values are unchanged). Consumed by the dialog layer, not by flow navigation.

		/// <summary>Dialog answer: the user accepted (OK button).</summary>
		Ok,

		/// <summary>Dialog answer: the user answered affirmatively (Yes button).</summary>
		Yes,

		/// <summary>Dialog answer: the user answered negatively (No button).</summary>
		No,

		/// <summary>Dialog answer: the user chose to retry the operation (Retry button).</summary>
		Retry,

		/// <summary>Dialog answer: the user chose to abort the operation (Abort button).</summary>
		Abort,

		/// <summary>Dialog answer: the user chose to ignore the condition and continue (Ignore button).</summary>
		Ignore,

		/// <summary>Dismiss sentinel: no answer was chosen (e.g. the dialog was dismissed via Esc).</summary>
		None,
	}

	/// <summary>
	/// A single rendered button in a flow's button row.
	/// </summary>
	/// <param name="Label">The text displayed on the button.</param>
	/// <param name="Verdict">The <see cref="FlowVerdict"/> emitted when the button is clicked.</param>
	/// <param name="Enabled">Whether the button is currently interactive. Defaults to <c>true</c>.</param>
	public readonly record struct FlowButton(string Label, FlowVerdict Verdict, bool Enabled = true);

	/// <summary>
	/// Standardized button-set selector; determines which canonical button row the host renders.
	/// </summary>
	public enum FlowButtons
	{
		/// <summary>An OK button and a Cancel button.</summary>
		OkCancel,

		/// <summary>Back, Next, and Cancel buttons (typical middle wizard step).</summary>
		BackNextCancel,

		/// <summary>Back, Finish, and Cancel buttons (final wizard step).</summary>
		BackFinishCancel,

		/// <summary>Next and Cancel buttons (typical first wizard step).</summary>
		NextCancel,

		/// <summary>Only a Cancel button.</summary>
		CancelOnly,

		/// <summary>A single OK button (dialog preset). Affirmative verdict <see cref="FlowVerdict.Ok"/>.</summary>
		Ok,

		/// <summary>Yes and No buttons (dialog preset). Verdicts <see cref="FlowVerdict.Yes"/> / <see cref="FlowVerdict.No"/>.</summary>
		YesNo,

		/// <summary>Yes, No, and Cancel buttons (dialog preset).</summary>
		YesNoCancel,

		/// <summary>Retry and Cancel buttons (dialog preset).</summary>
		RetryCancel,

		/// <summary>No button row; the step body resolves itself via <see cref="IFlowStepContent{TResult}.Completion"/>.</summary>
		None,
	}

	/// <summary>
	/// Chrome hints supplied to a flow step when building its content. The host uses these
	/// to size and decorate the host window; the content may read them to size its own body.
	/// </summary>
	public readonly struct FlowChrome
	{
		/// <summary>
		/// Initialises a <see cref="FlowChrome"/> value.
		/// </summary>
		/// <param name="title">The window or dialog title.</param>
		/// <param name="stepIndicator">Optional step-position indicator, e.g. <c>(2, 4)</c> for "step 2 of 4".</param>
		/// <param name="widthHint">Optional preferred width of the host window in columns.</param>
		/// <param name="heightHint">Optional preferred height of the host window in rows.</param>
		/// <param name="buttons">
		/// The concrete button list to render. When <c>null</c> or omitted an empty list is used;
		/// the host typically populates this from the chosen <see cref="FlowButtons"/> set.
		/// </param>
		/// <param name="refreshButtons">
		/// Optional delegate the host calls on each <see cref="IFlowStepContent{TResult}.StateChanged"/>
		/// to re-evaluate the button row's enabled state in place (dynamic buttons). When <c>null</c> the
		/// host leaves the button enabled state unchanged on state changes. Supplied by the wizard;
		/// primitives and Tier-A leave it <c>null</c>.
		/// </param>
		/// <param name="severity">
		/// Optional severity that controls the host-built top band's glyph and accent-rule colour role.
		/// The host always builds the top band; <see cref="NotificationSeverityEnum.None"/> (the default)
		/// renders a glyph-less bold title on a Primary rule, while Info/Success/Warning/Danger render the
		/// matching severity glyph and rule role. Additive trailing parameter — existing
		/// <see cref="FlowChrome"/> construction is unaffected.
		/// </param>
		/// <param name="autoSizeHeight">When <c>true</c> and <see cref="HeightHint"/> is null, the host auto-sizes the window
		/// height to the content (clamped to a min floor and a terminal-derived cap; it scrolls beyond the cap).
		/// An explicit <see cref="HeightHint"/> always overrides this. Default <c>false</c>.</param>
		/// <param name="resizable">When <c>true</c>, the host lets the user drag-resize the window (minimize/maximize
		/// buttons stay disabled). Independent of <see cref="AutoSizeHeight"/>: the initial height is chosen by the normal
		/// rule, and this only allows manual resize afterward. Default <c>false</c>.</param>
		public FlowChrome(
			string title,
			(int Index, int? Count)? stepIndicator = null,
			int? widthHint = null,
			int? heightHint = null,
			IReadOnlyList<FlowButton>? buttons = null,
			System.Func<IReadOnlyList<FlowButton>>? refreshButtons = null,
			NotificationSeverityEnum severity = NotificationSeverityEnum.None,
			bool autoSizeHeight = false,
			bool resizable = false)
		{
			Title = title;
			StepIndicator = stepIndicator;
			WidthHint = widthHint;
			HeightHint = heightHint;
			Buttons = buttons ?? System.Array.Empty<FlowButton>();
			RefreshButtons = refreshButtons;
			Severity = severity;
			UseProgressGlyph = false;
			AutoSizeHeight = autoSizeHeight;
			Resizable = resizable;
		}

		/// <summary>
		/// Internal constructor used by the progress dialog path to request the spinner (⟳) glyph in the
		/// host-built top band on a Primary rule, which the <see cref="NotificationSeverityEnum"/> set does
		/// not model. All other behaviour matches the public constructor.
		/// </summary>
		internal FlowChrome(
			string title,
			(int Index, int? Count)? stepIndicator,
			int? widthHint,
			int? heightHint,
			IReadOnlyList<FlowButton>? buttons,
			System.Func<IReadOnlyList<FlowButton>>? refreshButtons,
			NotificationSeverityEnum severity,
			bool useProgressGlyph,
			bool autoSizeHeight = false,
			bool resizable = false)
		{
			Title = title;
			StepIndicator = stepIndicator;
			WidthHint = widthHint;
			HeightHint = heightHint;
			Buttons = buttons ?? System.Array.Empty<FlowButton>();
			RefreshButtons = refreshButtons;
			Severity = severity;
			UseProgressGlyph = useProgressGlyph;
			AutoSizeHeight = autoSizeHeight;
			Resizable = resizable;
		}

		/// <summary>The window or dialog title text.</summary>
		public string Title { get; }

		/// <summary>
		/// Optional step position, e.g. <c>(Index: 2, Count: 4)</c>.
		/// <c>Count</c> may be <c>null</c> for flows with a dynamic or unknown number of steps.
		/// </summary>
		public (int Index, int? Count)? StepIndicator { get; }

		/// <summary>Optional preferred width of the host window in terminal columns.</summary>
		public int? WidthHint { get; }

		/// <summary>Optional preferred height of the host window in terminal rows.</summary>
		public int? HeightHint { get; }

		/// <summary>
		/// The standardized button row to render. Built by the host from the chosen
		/// <see cref="FlowButtons"/> set plus any per-step label or enable overrides.
		/// Defaults to an empty list when not provided.
		/// </summary>
		public IReadOnlyList<FlowButton> Buttons { get; }

		/// <summary>
		/// Optional delegate invoked by the host on each <see cref="IFlowStepContent{TResult}.StateChanged"/>
		/// to re-compute the button row's enabled state in place (dynamic buttons). Returns the refreshed
		/// button list (same labels/verdicts, updated <see cref="FlowButton.Enabled"/>); the host applies
		/// each enabled flag to the live button without rebuilding the window. <c>null</c> means no
		/// dynamic re-evaluation. Supplied by the wizard (Tier&#160;B); <c>null</c> for primitives/Tier&#160;A.
		/// </summary>
		public System.Func<IReadOnlyList<FlowButton>>? RefreshButtons { get; }

		/// <summary>
		/// The severity that drives the host-built top band's glyph and accent-rule colour role.
		/// <see cref="NotificationSeverityEnum.None"/> (the default) renders the bold title with no glyph
		/// on a Primary rule; Info/Success/Warning/Danger render the matching glyph and rule role. The host
		/// always builds the top band from this value, so primitives, plain custom content, and wizard steps
		/// all show a consistent title band.
		/// </summary>
		public NotificationSeverityEnum Severity { get; }

		/// <summary>
		/// When <c>true</c>, the host top-band builder uses the progress spinner glyph (⟳) on a Primary
		/// rule instead of a severity glyph. Set only by the progress dialog path, which has no matching
		/// <see cref="NotificationSeverityEnum"/> member.
		/// </summary>
		internal bool UseProgressGlyph { get; }

		/// <summary>When <c>true</c> and <see cref="HeightHint"/> is null, the host auto-sizes the window
		/// height to the content (clamped to a min floor and a terminal-derived cap; it scrolls beyond the
		/// cap). An explicit <see cref="HeightHint"/> always overrides this. Default <c>false</c>.</summary>
		public bool AutoSizeHeight { get; }

		/// <summary>When <c>true</c>, the host lets the user drag-resize the window (minimize/maximize
		/// buttons stay disabled). Independent of <see cref="AutoSizeHeight"/>: the initial height is chosen
		/// by the normal rule, and this only allows manual resize afterward. Default <c>false</c>.</summary>
		public bool Resizable { get; }
	}

	/// <summary>
	/// A self-contained flow step body that produces a typed result.
	/// The host places the control returned by <see cref="BuildContent"/> inside the step window
	/// and awaits <see cref="Completion"/> (or a button click) to resolve the step.
	/// </summary>
	/// <typeparam name="TResult">The type of value produced when the step completes.</typeparam>
	public interface IFlowStepContent<TResult>
	{
		/// <summary>
		/// Builds and returns the control to display inside the host window's content region.
		/// Called once per presentation. The returned control is owned by the host for its lifetime.
		/// </summary>
		/// <param name="chrome">Chrome hints for title, size, and button row.</param>
		/// <returns>The content control to display.</returns>
		IWindowControl BuildContent(FlowChrome chrome);

		/// <summary>
		/// Completes when the step body resolves itself — for example, when the user presses Enter
		/// on a list (body self-resolve). Returns <c>null</c> when the step is dismissed or cancelled.
		/// Static or button-driven content may leave this task permanently incomplete.
		/// </summary>
		Task<TResult?> Completion { get; }

		/// <summary>
		/// Raised AFTER the content has written its live value into the step's internal state,
		/// so the host can re-evaluate dynamic button enable-predicates in place.
		/// Contract: <em>mutate state, THEN raise</em>.
		/// Built-in Prompt/Confirm raise this on each edit; static content may never raise it.
		/// </summary>
		event System.Action? StateChanged;
	}

	/// <summary>
	/// Optional contract for step content that supplies its OWN bottom band (StickyBottom ruler +
	/// right-aligned button toolbar). A host that recognises this adds the band as WINDOW children —
	/// only the window's content layout honours <c>StickyPosition</c>; a ScrollablePanel does not.
	/// The TOP band (title banner + accent rule) is ALWAYS built by the host from
	/// <see cref="FlowChrome"/> (uniform across primitives, plain content, and wizard steps); content
	/// never builds its own top band. The framework primitives (Confirm/Prompt/Progress) implement this
	/// to own their buttons, so the host must NOT also render a button row from <see cref="FlowChrome.Buttons"/>.
	/// </summary>
	internal interface IFlowChromeBands
	{
		/// <summary>StickyBottom band controls (ruler + right-aligned button toolbar).</summary>
		IReadOnlyList<IWindowControl> BuildBottomBand(FlowChrome chrome);
	}

	/// <summary>
	/// Combines the typed result value produced by a step's content with the navigation verdict
	/// chosen by the button that resolved the step.
	/// </summary>
	/// <typeparam name="TResult">The content's result type.</typeparam>
	public readonly struct FlowStepOutcome<TResult>
	{
		/// <summary>
		/// Initialises a <see cref="FlowStepOutcome{TResult}"/>.
		/// </summary>
		/// <param name="value">The value produced by the step content, or <c>default</c> on cancel.</param>
		/// <param name="verdict">The navigation verdict (which button fired, or Cancel on dismiss).</param>
		public FlowStepOutcome(TResult? value, FlowVerdict verdict)
		{
			Value = value;
			Verdict = verdict;
		}

		/// <summary>The typed value produced by the step (e.g. the selected version string).</summary>
		public TResult? Value { get; }

		/// <summary>
		/// The verdict that resolved the step: <see cref="FlowVerdict.Next"/>,
		/// <see cref="FlowVerdict.Back"/>, <see cref="FlowVerdict.Cancel"/>,
		/// <see cref="FlowVerdict.Finish"/>, or <see cref="FlowVerdict.Stay"/>.
		/// </summary>
		public FlowVerdict Verdict { get; }
	}
}
