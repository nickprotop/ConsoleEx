// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Flows;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Flows;

public class SwapContentHostTests
{
	/// <summary>
	/// A minimal app-style step body that relies entirely on the host-rendered button row.
	/// Its Completion never self-resolves, so the only resolution path is a host button or cancel.
	/// </summary>
	private sealed class StaticBodyContent : IFlowStepContent<string>
	{
		private readonly TaskCompletionSource<string?> _tcs = new();

		public Task<string?> Completion => _tcs.Task;

		public event Action? StateChanged;

		public void RaiseStateChanged() => StateChanged?.Invoke();

		public IWindowControl BuildContent(FlowChrome chrome)
			=> Builders.Controls.Markup().AddLine("body").Build();
	}

	private static FlowChrome ChromeWith(string title)
		=> new FlowChrome(
			title,
			widthHint: 50,
			heightHint: 12,
			buttons: new[]
			{
				new FlowButton("Next", FlowVerdict.Next),
				new FlowButton("Cancel", FlowVerdict.Cancel),
			});

	[Fact]
	public async Task PresentAsync_TwoSteps_ReusesSingleWindow()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(120, 40);
		using var host = new SwapContentHost(system, null);

		// Step 1
		var present1 = host.PresentAsync(new StaticBodyContent(), ChromeWith("Step 1"), CancellationToken.None);
		system.Render.UpdateDisplay();

		Assert.Single(system.Windows.Values);
		var window1 = system.Windows.Values.First();

		Assert.True(FlowTestHelpers.ClickButtonByName(system, "flow-host-btn-Next"), "step 1: click Next");
		var outcome1 = await present1.WaitAsync(TimeSpan.FromSeconds(5));
		Assert.Equal(FlowVerdict.Next, outcome1.Verdict);

		// Still exactly one window after the first step resolves (content swapped, not closed).
		Assert.Single(system.Windows.Values);

		// Step 2 — must reuse the SAME window instance.
		var present2 = host.PresentAsync(new StaticBodyContent(), ChromeWith("Step 2"), CancellationToken.None);
		system.Render.UpdateDisplay();

		Assert.Single(system.Windows.Values);
		var window2 = system.Windows.Values.First();
		Assert.Same(window1, window2);

		Assert.True(FlowTestHelpers.ClickButtonByName(system, "flow-host-btn-Next"), "step 2: click Next");
		var outcome2 = await present2.WaitAsync(TimeSpan.FromSeconds(5));
		Assert.Equal(FlowVerdict.Next, outcome2.Verdict);

		Assert.Single(system.Windows.Values);
	}

	[Fact]
	public async Task PresentAsync_HostButton_ResolvesVerdict_StillOneWindow()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(120, 40);
		using var host = new SwapContentHost(system, null);

		var present = host.PresentAsync(new StaticBodyContent(), ChromeWith("Pick"), CancellationToken.None);
		system.Render.UpdateDisplay();

		Assert.True(FlowTestHelpers.ClickButtonByName(system, "flow-host-btn-Next"), "click Next");
		var outcome = await present.WaitAsync(TimeSpan.FromSeconds(5));

		Assert.Equal(FlowVerdict.Next, outcome.Verdict);
		Assert.Single(system.Windows.Values);
	}

	[Fact]
	public async Task PresentAsync_CancelViaToken_ResolvesCancel_AndClosesWindow()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(120, 40);
		using var host = new SwapContentHost(system, null);

		using var cts = new CancellationTokenSource();
		var present = host.PresentAsync(new StaticBodyContent(), ChromeWith("Pick"), cts.Token);
		system.Render.UpdateDisplay();

		cts.Cancel();

		var outcome = await present.WaitAsync(TimeSpan.FromSeconds(5));
		Assert.Equal(FlowVerdict.Cancel, outcome.Verdict);

		// Disposing the host closes the single window — no leak.
		host.Dispose();
		await WaitForNoWindowsAsync(system);
		Assert.Empty(system.Windows.Values);
	}

	[Fact]
	public async Task PresentAsync_WindowDismissed_ResolvesCancel()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(120, 40);
		using var host = new SwapContentHost(system, null);

		var present = host.PresentAsync(new StaticBodyContent(), ChromeWith("Pick"), CancellationToken.None);
		system.Render.UpdateDisplay();

		// User dismisses the window (Esc / title-bar close).
		var window = system.Windows.Values.First();
		window.Close(force: true);

		var outcome = await present.WaitAsync(TimeSpan.FromSeconds(5));
		Assert.Equal(FlowVerdict.Cancel, outcome.Verdict);
	}

	private static async Task WaitForNoWindowsAsync(ConsoleWindowSystem system)
	{
		for (int i = 0; i < 50 && system.Windows.Values.Any(); i++)
		{
			system.DrainPendingUIActionsForTest();
			await Task.Delay(10);
		}
	}
}
