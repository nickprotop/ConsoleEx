// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;

namespace SharpConsoleUI.Flows
{
	/// <summary>
	/// Entry points for running composable flows. <see cref="Run{T}"/> hosts an imperative flow body
	/// (handed a <see cref="FlowContext"/>) and surfaces its terminal state as a <see cref="FlowResult{T}"/>:
	/// completed with a value, cancelled, or faulted. The declarative wizard loop is added to this same
	/// class (Tier B) in a separate partial.
	/// </summary>
	public static partial class Flow
	{
		/// <summary>
		/// Runs an imperative flow that produces a typed value.
		/// </summary>
		/// <typeparam name="T">The value type produced by a successful flow.</typeparam>
		/// <param name="ws">The window system the flow presents into.</param>
		/// <param name="parent">Optional parent window for the default modal host; ignored when <paramref name="host"/> is supplied.</param>
		/// <param name="body">The flow body; receives a <see cref="FlowContext"/> and returns the flow's value.</param>
		/// <param name="host">Optional presentation host. When <c>null</c> a <see cref="ModalWindowHost"/> is used.</param>
		/// <param name="cancellationToken">
		/// Optional external cancellation. When it is cancelled the flow's own token trips (via a linked
		/// source), so the in-flight step resolves Cancel and any subsequent <c>await</c> in the body
		/// observes <see cref="OperationCanceledException"/> → <see cref="FlowResult{T}.Cancelled"/>.
		/// Used, for example, by <see cref="Controls.FlowControl"/> to cancel a running inline flow when
		/// its control is removed from the visual tree mid-flow.
		/// </param>
		/// <returns>
		/// A <see cref="FlowResult{T}"/>: completed with the body's value, cancelled when the body throws
		/// <see cref="OperationCanceledException"/>, or faulted for any other exception.
		/// </returns>
		public static async Task<FlowResult<T>> Run<T>(
			ConsoleWindowSystem ws,
			Window? parent,
			Func<FlowContext, Task<T>> body,
			IFlowHost? host = null,
			CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(ws);
			ArgumentNullException.ThrowIfNull(body);

			using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			var activeHost = host ?? new ModalWindowHost(ws, parent);
			var ctx = new FlowContext(ws, activeHost, cts);
			try
			{
				var value = await body(ctx).ConfigureAwait(false);

				// The flow token is the authoritative whole-flow cancel signal (e.g. an external
				// cancellationToken, or control-removal cancelling the linked source). Since ctx.Show now
				// returns default on a Cancel verdict rather than throwing, a body that ignores the return
				// value would otherwise "complete" after the token was cancelled. Honour the token: if it
				// tripped during the run, the flow is Cancelled regardless of the body's return.
				if (cts.IsCancellationRequested)
					return FlowResult<T>.Cancel();

				return FlowResult<T>.Complete(value);
			}
			catch (OperationCanceledException)
			{
				return FlowResult<T>.Cancel();
			}
			catch (Exception ex)
			{
				ws.LogService?.LogError($"Flow faulted: {ex.Message}", ex, "Flows");
				return FlowResult<T>.Fault(ex);
			}
			finally
			{
				// Hosts that own a long-lived resource (e.g. the seamless SwapContentHost's reused
				// window) tear it down here on normal completion. The default ModalWindowHost is not
				// IDisposable, so this is a no-op for it (additive, non-breaking).
				if (activeHost is IDisposable disposable)
					disposable.Dispose();
			}
		}

		/// <summary>
		/// Runs an imperative flow that produces no payload. The returned <see cref="FlowResult{T}"/> carries
		/// <c>bool</c> with <see cref="FlowResult{T}.Value"/> set to <c>true</c> on completion (callers
		/// typically inspect only <see cref="FlowResult{T}.Completed"/> / <see cref="FlowResult{T}.Cancelled"/>).
		/// </summary>
		/// <param name="ws">The window system the flow presents into.</param>
		/// <param name="parent">Optional parent window for the default modal host; ignored when <paramref name="host"/> is supplied.</param>
		/// <param name="body">The flow body; receives a <see cref="FlowContext"/>.</param>
		/// <param name="host">Optional presentation host. When <c>null</c> a <see cref="ModalWindowHost"/> is used.</param>
		/// <param name="cancellationToken">
		/// Optional external cancellation; when cancelled the flow's own token trips (see the typed
		/// <see cref="Run{T}(ConsoleWindowSystem, Window?, Func{FlowContext, Task{T}}, IFlowHost?, CancellationToken)"/>).
		/// </param>
		/// <returns>A <see cref="FlowResult{T}"/> of <c>bool</c>, with <c>Value == true</c> on completion.</returns>
		public static Task<FlowResult<bool>> Run(
			ConsoleWindowSystem ws,
			Window? parent,
			Func<FlowContext, Task> body,
			IFlowHost? host = null,
			CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(body);

			return Run(ws, parent, async ctx =>
			{
				await body(ctx).ConfigureAwait(false);
				return true;
			}, host, cancellationToken);
		}
	}
}
