// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Threading.Tasks;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Flows;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class WizardControlTests
{
	private sealed class WState { public int A; public int B; }

	[Fact]
	public void WizardControl_IsAFlowControl_WithWizardPlaceholder()
	{
		var wiz = new WizardControl();
		Assert.IsAssignableFrom<FlowControl>(wiz);     // honest IS-A, not a wrapper
		Assert.NotNull(wiz.Placeholder);               // wizard-friendly default placeholder
		// Idle: the placeholder is hosted as a child.
		Assert.Contains(wiz.GetChildren(), c => ReferenceEquals(c, wiz.Placeholder));
	}

	[Fact]
	public void ControlsFactory_Wizard_ReturnsWizardControl()
	{
		Assert.IsType<WizardControl>(Builders.Controls.Wizard());
	}

	[Fact]
	public async Task WizardControl_RunsTwoStepWizardInline_Completes()
	{
		var system = TestWindowSystemBuilder.CreateTestSystem(120, 40);
		var wiz = new WizardControl();
		var win = new WindowBuilder(system).WithTitle("Host").WithSize(80, 30).AddControl(wiz).Build();
		system.AddWindow(win);

		var result = await wiz.Run(Flow.Wizard<WState>()
				.Step((ctx, s) => { s.A = 10; return Task.FromResult(FlowVerdict.Next); })
				.Step((ctx, s) => { s.B = 20; return Task.FromResult(FlowVerdict.Finish); }))
			.WaitAsync(TimeSpan.FromSeconds(10));

		Assert.True(result.Completed, $"Expected Completed; Cancelled={result.Cancelled} Faulted={result.Faulted}");
		Assert.Equal(10, result.Value!.A);
		Assert.Equal(20, result.Value!.B);
	}
}
