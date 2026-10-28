// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Threading;
using System.Threading.Tasks;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;
using Xunit;

namespace SharpConsoleUI.Tests.Core;

/// <summary>
/// Knowing when the system has finished reacting, without polling or sleeping.
/// </summary>
/// <remarks>
/// <para>
/// The existing <c>IsIdle</c> is a wall-clock timeout - "nobody typed for a
/// while" - so it can report idle while work is still queued, and it needs real
/// time to pass. Tests were polling for a window to have a height, then polling
/// again for the effect of an input. <c>IsSettled</c> answers the question they
/// were actually asking.
/// </para>
/// </remarks>
public class SettledTests
{
	private static ConsoleWindowSystem Headless(out HeadlessConsoleDriver driver)
	{
		driver = new HeadlessConsoleDriver(60, 20);
		return new ConsoleWindowSystem(driver);
	}

	[Fact]
	public void A_fresh_system_with_nothing_to_do_is_settled()
	{
		var system = Headless(out _);

		Assert.True(system.IsSettled);
	}

	[Fact]
	public void Queued_work_is_not_settled()
	{
		var system = Headless(out _);

		system.EnqueueOnUIThread(() => { });

		Assert.False(system.IsSettled);
	}

	[Fact]
	public void A_window_waiting_to_paint_is_not_settled()
	{
		var system = Headless(out _);

		new WindowBuilder(system)
			.WithTitle("W")
			.WithSize(30, 8)
			.AddControl(SharpConsoleUI.Builders.Controls.Markup().AddLine("hello").Build())
			.BuildAndShow();

		// Added but never rendered: the window has work pending.
		Assert.False(system.IsSettled);
	}

	[Fact]
	public async Task WaitForFrameAsync_returns_the_next_frame_number()
	{
		var system = Headless(out var driver);

		long before = driver.FrameNumber;
		var waiting = system.WaitForFrameAsync(CancellationToken.None);

		system.Render.UpdateDisplay();

		long got = await waiting.WaitAsync(System.TimeSpan.FromSeconds(5));

		Assert.True(got > before, $"frame did not advance: {before} -> {got}");
	}

	[Fact]
	public void SettleBlockers_names_what_is_holding_it()
	{
		var system = Headless(out _);

		Assert.Equal(string.Empty, system.SettleBlockers());

		system.EnqueueOnUIThread(() => { });

		// A bare false tells a timing-out caller nothing; the reason is the point.
		Assert.Contains("queued action", system.SettleBlockers());
	}

	/// <summary>
	/// The real thing: a window with a control, driven through the real input
	/// path, settling after the effect has actually been painted.
	/// </summary>
	[Fact]
	public async Task A_system_settles_after_the_work_it_was_given_is_painted()
	{
		var system = Headless(out var driver);

		var label = SharpConsoleUI.Builders.Controls.Markup().AddLine("before").Build();

		new WindowBuilder(system)
			.WithTitle("W")
			.WithSize(30, 8)
			.AddControl(label)
			.BuildAndShow();

		// Settling must include the first paint, not just the queue draining.
		await system.WaitUntilSettledAsync(CancellationToken.None)
			.WaitAsync(System.TimeSpan.FromSeconds(5));

		Assert.True(system.IsSettled);

		var text = string.Join("\n", driver.CaptureComposed()!.ToLines());
		Assert.Contains("before", text);

		// A change goes through the queue, and settling waits for its paint too.
		system.EnqueueOnUIThread(() => label.SetContent(["after"]));

		await system.WaitUntilSettledAsync(CancellationToken.None)
			.WaitAsync(System.TimeSpan.FromSeconds(5));

		text = string.Join("\n", driver.CaptureComposed()!.ToLines());
		Assert.Contains("after", text);

		// And it SURVIVES a re-render rather than being a one-frame accident.
		system.Render.UpdateDisplay();
		Assert.True(system.IsSettled);
	}
}
