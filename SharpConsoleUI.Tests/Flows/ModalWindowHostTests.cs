// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpConsoleUI;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Flows;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Flows;

public class ModalWindowHostTests
{
	/// <summary>
	/// A minimal app-style step body that does NOT build its own buttons — it relies entirely
	/// on the host-rendered button row from <see cref="FlowChrome.Buttons"/>. Its Completion
	/// never self-resolves, so the only resolution path is a host button click or cancel.
	/// </summary>
	private sealed class StaticBodyContent : IFlowStepContent<string>
	{
		private readonly TaskCompletionSource<string?> _tcs = new();

		public string? CurrentValue { get; set; } = "picked";

		public Task<string?> Completion => _tcs.Task;

		public event Action? StateChanged;

		public void RaiseStateChanged() => StateChanged?.Invoke();

		public IWindowControl BuildContent(FlowChrome chrome)
			=> Builders.Controls.Markup().AddLine("body").Build();
	}

	[Fact]
	public async Task PresentAsync_HostButtonRow_ResolvesWithVerdict_AndNoLeak()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(120, 40);
		var host = new ModalWindowHost(system, null);
		var content = new StaticBodyContent();

		var chrome = new FlowChrome(
			"Pick",
			widthHint: 50,
			heightHint: 12,
			buttons: new[]
			{
				new FlowButton("Next", FlowVerdict.Next),
				new FlowButton("Cancel", FlowVerdict.Cancel),
			});

		var present = host.PresentAsync(content, chrome, CancellationToken.None);
		system.Render.UpdateDisplay();

		// Click the host-rendered Next button by its standardized name.
		var clicked = FlowTestHelpers.ClickButtonByName(system, "flow-host-btn-Next");
		Assert.True(clicked, "Expected to find and click the host-rendered Next button.");

		var outcome = await present.WaitAsync(TimeSpan.FromSeconds(5));

		Assert.Equal(FlowVerdict.Next, outcome.Verdict);

		// No leaked modal window after the host resolves and disposes.
		await WaitForNoWindowsAsync(system);
		Assert.Empty(system.Windows.Values);
	}

	[Fact]
	public async Task PresentAsync_PlainContent_ShowsHostBuiltTitleBand()
	{
		// A PLAIN custom-content step (NOT IFlowChromeBands) must still get a host-built top band
		// (title + accent rule) — previously it showed none. The banner is a window child the host
		// always adds, regardless of content type.
		var system = TestWindowSystemBuilder.CreateTestSystem(120, 40);
		var host = new ModalWindowHost(system, null);
		var content = new StaticBodyContent();

		using var cts = new CancellationTokenSource();
		var chrome = new FlowChrome("My Step Title", widthHint: 50, heightHint: 12);

		var present = host.PresentAsync(content, chrome, cts.Token);
		system.Render.UpdateDisplay();

		// The host-built banner markup must be present in the window tree and carry the title text,
		// proving the title band renders above the body for plain content (consistency).
		var window = system.Windows.Values.Single();
		var banner = window.FindControl<MarkupControl>(FlowContentHelpers.TopBandTitleName);
		Assert.NotNull(banner);
		Assert.Contains("My Step Title", banner!.Text);

		// A sticky-top accent rule accompanies the banner.
		Assert.Contains(window.GetControlsByType<RuleControl>(), r => r.StickyPosition == StickyPosition.Top);

		// Tear down so no window leaks.
		cts.Cancel();
		await present.WaitAsync(TimeSpan.FromSeconds(5));
		await WaitForNoWindowsAsync(system);
	}

	[Fact]
	public async Task PresentAsync_CancelViaToken_ResolvesCancel_AndNoLeak()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(120, 40);
		var host = new ModalWindowHost(system, null);
		var content = new StaticBodyContent();

		using var cts = new CancellationTokenSource();
		var chrome = new FlowChrome(
			"Pick",
			widthHint: 50,
			heightHint: 12,
			buttons: new[] { new FlowButton("Next", FlowVerdict.Next), new FlowButton("Cancel", FlowVerdict.Cancel) });

		var present = host.PresentAsync(content, chrome, cts.Token);
		system.Render.UpdateDisplay();

		cts.Cancel();

		var outcome = await present.WaitAsync(TimeSpan.FromSeconds(5));

		Assert.Equal(FlowVerdict.Cancel, outcome.Verdict);

		await WaitForNoWindowsAsync(system);
		Assert.Empty(system.Windows.Values);
	}

	[Fact]
	public async Task PresentAsync_BodySelfResolve_ResolvesNext_WithValue()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(120, 40);
		var host = new ModalWindowHost(system, null);
		var content = new SelfResolvingContent("the-value");

		var chrome = new FlowChrome("Pick", widthHint: 50, heightHint: 12);

		var present = host.PresentAsync(content, chrome, CancellationToken.None);
		system.Render.UpdateDisplay();

		content.Resolve();

		var outcome = await present.WaitAsync(TimeSpan.FromSeconds(5));

		Assert.Equal(FlowVerdict.Next, outcome.Verdict);
		Assert.Equal("the-value", outcome.Value);

		await WaitForNoWindowsAsync(system);
		Assert.Empty(system.Windows.Values);
	}

	[Fact]
	public async Task PresentAsync_BodyFaults_PropagatesException_AndNoLeak()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(120, 40);
		var host = new ModalWindowHost(system, null);
		var content = new FaultingContent();

		// Empty button set — only resolution path is body self-resolve.
		var chrome = new FlowChrome("Pick", widthHint: 50, heightHint: 12);

		var present = host.PresentAsync(content, chrome, CancellationToken.None);
		system.Render.UpdateDisplay();

		content.Fault(new InvalidOperationException("boom"));

		await Assert.ThrowsAsync<InvalidOperationException>(() =>
			present.WaitAsync(TimeSpan.FromSeconds(5)));

		// finally must still close the modal window.
		await WaitForNoWindowsAsync(system);
		Assert.Empty(system.Windows.Values);
	}

	private sealed class FaultingContent : IFlowStepContent<string>
	{
		private readonly TaskCompletionSource<string?> _tcs = new();

		public Task<string?> Completion => _tcs.Task;

		public event Action? StateChanged;

		public void Fault(Exception ex) => _tcs.TrySetException(ex);

		public IWindowControl BuildContent(FlowChrome chrome)
			=> Builders.Controls.Markup().AddLine("body").Build();
	}

	private sealed class SelfResolvingContent : IFlowStepContent<string>
	{
		private readonly TaskCompletionSource<string?> _tcs = new();
		private readonly string _value;

		public SelfResolvingContent(string value) => _value = value;

		public Task<string?> Completion => _tcs.Task;

		public event Action? StateChanged;

		public void Resolve() => _tcs.TrySetResult(_value);

		public IWindowControl BuildContent(FlowChrome chrome)
			=> Builders.Controls.Markup().AddLine("body").Build();
	}

	private static async Task WaitForNoWindowsAsync(ConsoleWindowSystem system)
	{
		// The host marshals modal.Close via EnqueueOnUIThread; drain the queue until empty or timeout.
		for (int i = 0; i < 50 && system.Windows.Values.Any(); i++)
		{
			system.DrainPendingUIActionsForTest();
			await Task.Delay(10);
		}
	}
}
