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

public class RunWithProgressEndToEndTests
{
	[Fact]
	public async Task ScriptedSequence_DrivesBarAndMarkupStatus_ToCompletion()
	{
		var sys = TestWindowSystemBuilder.CreateTestSystem(80, 24);

		var result = await DialogsApi.RunWithProgressAsync<int>(
			sys, "Pull", "starting",
			async (ct, progress) =>
			{
				progress.Report(new ProgressUpdate(fraction: 0.25));
				progress.Report(new ProgressUpdate(message: "[cyan]a[/]"));
				progress.Report(new ProgressUpdate(indeterminate: true));
				progress.Report(new ProgressUpdate(fraction: 0.9));
				await Task.Yield();
				return 7;
			},
			allowMarkup: true);

		Assert.Equal(7, result);
	}
}
