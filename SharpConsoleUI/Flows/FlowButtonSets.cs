// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace SharpConsoleUI.Flows
{
	/// <summary>
	/// Maps a canonical <see cref="FlowButtons"/> selector to the concrete ordered list of
	/// <see cref="FlowButton"/> rows (label + navigation verdict + enabled state) the host renders.
	/// Shared by <see cref="FlowContext"/> (Tier A) and the wizard loop (Tier B) so the button-row
	/// vocabulary lives in one place.
	/// </summary>
	public static class FlowButtonSets
	{
		/// <summary>
		/// Returns the concrete button list for <paramref name="buttons"/>. The first button in each
		/// non-empty set is the affirmative/default action (the host tints it as the primary action).
		/// </summary>
		/// <param name="buttons">The canonical button-set selector.</param>
		/// <returns>
		/// An ordered, read-only list of <see cref="FlowButton"/> values. For
		/// <see cref="FlowButtons.None"/> an empty list is returned so the step body self-resolves via
		/// its own <see cref="IFlowStepContent{TResult}.Completion"/>.
		/// </returns>
		public static IReadOnlyList<FlowButton> For(FlowButtons buttons) => buttons switch
		{
			FlowButtons.OkCancel => new[]
			{
				new FlowButton("OK", FlowVerdict.Next),
				new FlowButton("Cancel", FlowVerdict.Cancel),
			},
			FlowButtons.BackNextCancel => new[]
			{
				new FlowButton("Next", FlowVerdict.Next),
				new FlowButton("Back", FlowVerdict.Back),
				new FlowButton("Cancel", FlowVerdict.Cancel),
			},
			FlowButtons.BackFinishCancel => new[]
			{
				new FlowButton("Finish", FlowVerdict.Finish),
				new FlowButton("Back", FlowVerdict.Back),
				new FlowButton("Cancel", FlowVerdict.Cancel),
			},
			FlowButtons.NextCancel => new[]
			{
				new FlowButton("Next", FlowVerdict.Next),
				new FlowButton("Cancel", FlowVerdict.Cancel),
			},
			FlowButtons.CancelOnly => new[]
			{
				new FlowButton("Cancel", FlowVerdict.Cancel),
			},
			FlowButtons.None => Array.Empty<FlowButton>(),
			_ => Array.Empty<FlowButton>(),
		};
	}
}
