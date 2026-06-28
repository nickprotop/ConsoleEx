// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Threading;
using System.Threading.Tasks;

namespace SharpConsoleUI.Flows
{
	/// <summary>
	/// The pluggable presentation seam for flow steps. A host takes an <see cref="IFlowStepContent{TResult}"/>
	/// plus its <see cref="FlowChrome"/>, renders it (with the standardized button row), and resolves to a
	/// <see cref="FlowStepOutcome{TResult}"/> carrying BOTH the content's typed value AND the chosen
	/// navigation verdict — so a wizard can navigate Next/Back/Cancel/Finish.
	/// </summary>
	/// <remarks>
	/// The framework default is <see cref="ModalWindowHost"/> (one modal window per step). Tests use a
	/// scripted headless host. The host renders the chrome's button row; content that builds its own
	/// buttons (the framework primitives) is presented with an empty <see cref="FlowChrome.Buttons"/> set
	/// and resolves via its own <see cref="IFlowStepContent{TResult}.Completion"/> instead.
	/// </remarks>
	public interface IFlowHost
	{
		/// <summary>
		/// Presents <paramref name="content"/> using <paramref name="chrome"/> and resolves to its outcome.
		/// </summary>
		/// <typeparam name="TResult">The content's result type.</typeparam>
		/// <param name="content">The step body to present.</param>
		/// <param name="chrome">Chrome hints (title, size, step indicator, button row).</param>
		/// <param name="ct">Cancellation token; when tripped the step resolves with <see cref="FlowVerdict.Cancel"/>.</param>
		/// <returns>
		/// A task completing with the step's <see cref="FlowStepOutcome{TResult}"/>: the content value plus the
		/// navigation verdict (or <c>(default, Cancel)</c> on dismiss/cancellation).
		/// </returns>
		Task<FlowStepOutcome<TResult>> PresentAsync<TResult>(
			IFlowStepContent<TResult> content, FlowChrome chrome, CancellationToken ct);
	}
}
