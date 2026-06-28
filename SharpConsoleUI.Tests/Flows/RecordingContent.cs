// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Flows;

namespace SharpConsoleUI.Tests.Flows;

/// <summary>
/// A minimal, app-style flow step body for tests. It builds a trivial markup body and does NOT
/// build its own buttons or self-resolve its <see cref="IFlowStepContent{TResult}.Completion"/>,
/// so the only resolution path is a host-rendered button click (or cancellation). Records whether
/// its content was built so a test can assert it was actually presented.
/// </summary>
public sealed class RecordingContent : IFlowStepContent<bool>, IRaiseStateChangedForTest
{
	private readonly TaskCompletionSource<bool> _tcs = new();

	/// <summary>Whether <see cref="BuildContent"/> has been invoked by a host.</summary>
	public bool ContentBuilt { get; private set; }

	/// <inheritdoc/>
	public Task<bool> Completion => _tcs.Task;

	/// <inheritdoc/>
	public event Action? StateChanged;

	/// <summary>Raises <see cref="StateChanged"/> (test hook).</summary>
	public void RaiseStateChanged() => StateChanged?.Invoke();

	/// <summary>Resolves the body's own completion with the given value (body self-resolve path).</summary>
	public void Resolve(bool value) => _tcs.TrySetResult(value);

	/// <inheritdoc/>
	public IWindowControl BuildContent(FlowChrome chrome)
	{
		ContentBuilt = true;
		return Builders.Controls.Markup().AddLine("recording-body").Build();
	}
}
