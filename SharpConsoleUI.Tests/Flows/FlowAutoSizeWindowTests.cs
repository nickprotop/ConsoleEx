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
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Flows;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Flows
{
	public class FlowAutoSizeWindowTests
	{
		[Fact]
		public async Task ModalWindowHost_AutoSize_ShortBody_TightWindow()
		{
			var sys = TestWindowSystemBuilder.CreateTestSystem(80, 40);
			var host = new ModalWindowHost(sys, parent: null);
			var content = new TallTextStepContent(lineCount: 1);
			var chrome = new FlowChrome("Short", widthHint: 50, autoSizeHeight: true,
				buttons: new[] { new FlowButton("OK", FlowVerdict.Next) });

			var task = host.PresentAsync(content, chrome, CancellationToken.None);
			sys.Render.UpdateDisplay();

			var win = sys.Windows.Values.Single();
			Assert.True(win.Height >= ControlDefaults.FlowAutoSizeMinHeight);
			Assert.True(win.Height < 12, $"auto-sized short window should be tighter than the old fixed 12, got {win.Height}");

			FlowTestHelpers.ClickButtonByName(sys, "flow-host-btn-OK");
			await task.WaitAsync(TimeSpan.FromSeconds(5));
		}

		[Fact]
		public async Task ModalWindowHost_AutoSize_TallBody_CapsAtTerminal()
		{
			var sys = TestWindowSystemBuilder.CreateTestSystem(80, 16);
			var host = new ModalWindowHost(sys, parent: null);
			var content = new TallTextStepContent(lineCount: 100);
			var chrome = new FlowChrome("Tall", widthHint: 50, autoSizeHeight: true,
				buttons: new[] { new FlowButton("OK", FlowVerdict.Next) });

			var task = host.PresentAsync(content, chrome, CancellationToken.None);
			sys.Render.UpdateDisplay();

			var win = sys.Windows.Values.Single();
			Assert.Equal(16 - ControlDefaults.FlowAutoSizeCapMargin, win.Height);

			FlowTestHelpers.ClickButtonByName(sys, "flow-host-btn-OK");
			await task.WaitAsync(TimeSpan.FromSeconds(5));
		}

		[Fact]
		public async Task ModalWindowHost_ExplicitHint_OverridesAutoSize()
		{
			var sys = TestWindowSystemBuilder.CreateTestSystem(80, 40);
			var host = new ModalWindowHost(sys, parent: null);
			var content = new TallTextStepContent(lineCount: 1);
			var chrome = new FlowChrome("Pinned", widthHint: 50, heightHint: 18, autoSizeHeight: true,
				buttons: new[] { new FlowButton("OK", FlowVerdict.Next) });

			var task = host.PresentAsync(content, chrome, CancellationToken.None);
			sys.Render.UpdateDisplay();

			Assert.Equal(18, sys.Windows.Values.Single().Height);

			FlowTestHelpers.ClickButtonByName(sys, "flow-host-btn-OK");
			await task.WaitAsync(TimeSpan.FromSeconds(5));
		}

		[Fact]
		public async Task ModalWindowHost_Resizable_FlagEnablesResize_NoMinMax()
		{
			var sys = TestWindowSystemBuilder.CreateTestSystem(80, 40);
			var host = new ModalWindowHost(sys, parent: null);
			var content = new TallTextStepContent(lineCount: 1);
			var chrome = new FlowChrome("Resz", widthHint: 50, resizable: true,
				buttons: new[] { new FlowButton("OK", FlowVerdict.Next) });

			var task = host.PresentAsync(content, chrome, CancellationToken.None);
			sys.Render.UpdateDisplay();

			var win = sys.Windows.Values.Single();
			Assert.True(win.IsResizable);
			Assert.False(win.IsMinimizable);
			Assert.False(win.IsMaximizable);

			FlowTestHelpers.ClickButtonByName(sys, "flow-host-btn-OK");
			await task.WaitAsync(TimeSpan.FromSeconds(5));
		}

		[Fact]
		public async Task SwapContentHost_AutoSize_ResizesAndRecentersPerStep()
		{
			var sys = TestWindowSystemBuilder.CreateTestSystem(80, 40);
			using var host = new SwapContentHost(sys, parent: null);

			var step1 = new TallTextStepContent(lineCount: 1);
			var chrome1 = new FlowChrome("One", widthHint: 50, autoSizeHeight: true,
				buttons: new[] { new FlowButton("OK", FlowVerdict.Next) });
			var t1 = host.PresentAsync(step1, chrome1, CancellationToken.None);
			sys.DrainPendingUIActionsForTest(); sys.Render.UpdateDisplay();
			sys.DrainPendingUIActionsForTest(); sys.Render.UpdateDisplay();

			var win = sys.Windows.Values.Single();
			int h1 = win.Height;
			int expTop1 = (sys.DesktopDimensions.Height - h1) / 2;
			Assert.Equal(expTop1, win.Top); // centered for its height

			FlowTestHelpers.ClickButtonByName(sys, "flow-host-btn-OK");
			await t1.WaitAsync(TimeSpan.FromSeconds(5));

			var step2 = new TallTextStepContent(lineCount: 30);
			var chrome2 = new FlowChrome("Two", widthHint: 50, autoSizeHeight: true,
				buttons: new[] { new FlowButton("OK", FlowVerdict.Next) });
			var t2 = host.PresentAsync(step2, chrome2, CancellationToken.None);
			sys.DrainPendingUIActionsForTest(); sys.Render.UpdateDisplay();
			sys.DrainPendingUIActionsForTest(); sys.Render.UpdateDisplay();

			int h2 = win.Height;
			Assert.True(h2 > h1, $"taller step should grow the reused window ({h1} -> {h2})");
			int expTop2 = (sys.DesktopDimensions.Height - h2) / 2;
			Assert.Equal(expTop2, win.Top); // re-centered for the new height
			sys.Render.UpdateDisplay();
			Assert.Equal(h2, win.Height); // survives re-render

			FlowTestHelpers.ClickButtonByName(sys, "flow-host-btn-OK");
			await t2.WaitAsync(TimeSpan.FromSeconds(5));
		}

		[Fact]
		public async Task DialogsConfirm_AutoSizesTight_OutOfTheBox()
		{
			var sys = TestWindowSystemBuilder.CreateTestSystem(80, 40);
			var task = SharpConsoleUI.Dialogs.Dialogs.ConfirmAsync(sys, "Q", "Short?", "Yes", "No");
			sys.Render.UpdateDisplay();

			var win = sys.Windows.Values.Single();
			Assert.True(win.Height < 11, $"standalone confirm should auto-size tighter than the old 11, got {win.Height}");
			Assert.True(win.Height >= ControlDefaults.FlowAutoSizeMinHeight);

			var cancel = win.FindControl<SharpConsoleUI.Controls.ButtonControl>("flow-confirm-cancel");
			cancel!.PerformClickForTest();
			await task.WaitAsync(TimeSpan.FromSeconds(5));
		}

		[Fact]
		public async Task ModalWindowHost_NoAutoSize_NullHint_UsesBumpedDefault13()
		{
			var sys = TestWindowSystemBuilder.CreateTestSystem(80, 40);
			var host = new ModalWindowHost(sys, parent: null);
			var content = new TallTextStepContent(lineCount: 1);
			// No autoSizeHeight, no heightHint → fixed default (bumped to 13).
			var chrome = new FlowChrome("Def", widthHint: 50,
				buttons: new[] { new FlowButton("OK", FlowVerdict.Next) });

			var task = host.PresentAsync(content, chrome, CancellationToken.None);
			sys.Render.UpdateDisplay();
			Assert.Equal(13, sys.Windows.Values.Single().Height);

			FlowTestHelpers.ClickButtonByName(sys, "flow-host-btn-OK");
			await task.WaitAsync(TimeSpan.FromSeconds(5));
		}
	}
}
