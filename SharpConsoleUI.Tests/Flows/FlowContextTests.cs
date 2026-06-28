// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using SharpConsoleUI;
using SharpConsoleUI.Drivers;
using SharpConsoleUI.Flows;
using Xunit;

namespace SharpConsoleUI.Tests.Flows;

public class FlowContextTests
{
	private static ConsoleWindowSystem Sys() => new(new HeadlessConsoleDriver(120, 40));

	[Fact]
	public async Task Run_Completes_WithBodyValue()
	{
		var host = new HeadlessFlowHost(HeadlessFlowHost.Answer(true, FlowVerdict.Next));
		var r = await Flow.Run(Sys(), null, async ctx =>
		{
			var ok = await ctx.Confirm("T", "go?");
			return ok ? 99 : -1;
		}, host);
		Assert.True(r.Completed);
		Assert.Equal(99, r.Value);
	}

	[Fact]
	public async Task Run_ReturnsCancelled_OnOperationCanceled()
	{
		var host = new HeadlessFlowHost();
		var r = await Flow.Run<int>(Sys(), null,
			_ => throw new OperationCanceledException(), host);
		Assert.True(r.Cancelled);
		Assert.False(r.Completed);
		Assert.False(r.Faulted);
	}

	[Fact]
	public async Task Run_ReturnsFaulted_OnException()
	{
		var host = new HeadlessFlowHost();
		var r = await Flow.Run<int>(Sys(), null,
			_ => throw new InvalidOperationException("boom"), host);
		Assert.True(r.Faulted);
		Assert.Equal("boom", r.Error!.Message);
	}

	[Fact]
	public async Task Confirm_RoutesThroughHost_AndReturnsScriptedValue()
	{
		var host = new HeadlessFlowHost(HeadlessFlowHost.Answer(true, FlowVerdict.Next));
		bool seen = false;
		var r = await Flow.Run(Sys(), null, async ctx =>
		{
			seen = await ctx.Confirm("ConfirmTitle", "are you sure?");
			return seen;
		}, host);

		Assert.True(r.Completed);
		Assert.True(seen);
		Assert.Contains("ConfirmTitle", host.PresentedTitles);
	}

	[Fact]
	public async Task Confirm_ReturnsFalse_OnCancelVerdict()
	{
		var host = new HeadlessFlowHost(HeadlessFlowHost.Answer(false, FlowVerdict.Cancel));
		var r = await Flow.Run(Sys(), null, ctx => ctx.Confirm("T", "go?"), host);
		Assert.True(r.Completed);
		Assert.False(r.Value);
	}

	[Fact]
	public async Task Show_RoutesThroughHost_AndReturnsValue()
	{
		var host = new HeadlessFlowHost(HeadlessFlowHost.Answer("hello", FlowVerdict.Next));
		var r = await Flow.Run(Sys(), null, async ctx =>
		{
			var content = new PromptContent("name?");
			return await ctx.Show(content, "ShowTitle");
		}, host);

		Assert.True(r.Completed);
		Assert.Equal("hello", r.Value);
		Assert.Contains("ShowTitle", host.PresentedTitles);
	}

	[Fact]
	public async Task Show_ReturnsDefault_OnCancelVerdict_DoesNotThrow()
	{
		// A Cancel verdict from a plain button-Cancel must make ctx.Show RETURN default — NOT throw
		// OperationCanceledException and NOT force-cancel the whole flow. The body decides what to do.
		var host = new HeadlessFlowHost(HeadlessFlowHost.Answer("ignored", FlowVerdict.Cancel));
		string? observed = "sentinel";
		bool reachedAfterShow = false;
		var r = await Flow.Run(Sys(), null, async ctx =>
		{
			var content = new PromptContent("name?");
			observed = await ctx.Show(content, "ShowTitle");
			reachedAfterShow = true; // proves Show did not throw
			return observed ?? "completed-after-cancel";
		}, host);

		Assert.True(reachedAfterShow, "Show must return on a Cancel verdict, not throw.");
		Assert.Null(observed); // default(string?) on cancel
							   // The body swallowed the cancel and returned a value → the flow COMPLETES (not Cancelled).
		Assert.True(r.Completed, $"Expected Completed; Cancelled={r.Cancelled} Faulted={r.Faulted}");
		Assert.Equal("completed-after-cancel", r.Value);
	}

	[Fact]
	public async Task Prompt_ReturnsNull_OnCancel()
	{
		var host = new HeadlessFlowHost(HeadlessFlowHost.Answer(null, FlowVerdict.Cancel));
		var r = await Flow.Run(Sys(), null, ctx => ctx.Prompt("T", "name?"), host);
		Assert.True(r.Completed);
		Assert.Null(r.Value);
	}

	[Fact]
	public async Task VoidRun_ReturnsTrue_OnCompletion()
	{
		var host = new HeadlessFlowHost();
		var r = await Flow.Run(Sys(), null, _ => Task.CompletedTask, host);
		Assert.True(r.Completed);
		Assert.True(r.Value);
	}

	[Fact]
	public async Task Commit_SetsCommittedFlag()
	{
		var host = new HeadlessFlowHost();
		bool committed = false;
		await Flow.Run(Sys(), null, ctx =>
		{
			Assert.False(ctx.Committed);
			ctx.Commit();
			committed = ctx.Committed;
			return Task.CompletedTask;
		}, host);
		Assert.True(committed);
	}
}
