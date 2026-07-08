// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using SharpConsoleUI;
using SharpConsoleUI.Dialogs;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;
using DialogsApi = SharpConsoleUI.Dialogs.Dialogs;

namespace SharpConsoleUI.Tests.Dialogs;

public class RunWithProgressAsyncTests
{
	[Fact]
	public async Task NewOverload_ReturnsWorkResult()
	{
		var sys = TestWindowSystemBuilder.CreateTestSystem(80, 24);
		var result = await DialogsApi.RunWithProgressAsync<int>(
			sys, "T", "start",
			async (ct, progress) =>
			{
				progress.Report(new ProgressUpdate(fraction: 0.5, message: "half"));
				await Task.Yield();
				return 42;
			});

		Assert.Equal(42, result);
	}

	[Fact]
	public async Task OldStringOverload_StillCompilesAndReturnsResult()
	{
		var sys = TestWindowSystemBuilder.CreateTestSystem(80, 24);
		// This call MUST compile unchanged (backward-compat guard) and behave as before.
		var result = await DialogsApi.RunWithProgressAsync<string>(
			sys, "T", "start",
			async (ct, progress) =>
			{
				progress.Report("working");
				await Task.Yield();
				return "ok";
			});

		Assert.Equal("ok", result);
	}
}
