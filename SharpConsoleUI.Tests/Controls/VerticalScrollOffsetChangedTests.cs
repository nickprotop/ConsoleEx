// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Ozerov Maxim
// Email: ozerov.maxim.vl@ya.ru
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Generic;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class VerticalScrollOffsetChangedTests
{
	private static ScrollablePanelControl CreateRenderedPanel()
	{
		var panel = new ScrollablePanelControl { Height = 10 };
		for (int i = 0; i < 20; i++)
			panel.AddControl(ContainerTestHelpers.CreateLabel($"Line {i}"));

		var (_, window) = ContainerTestHelpers.CreateTestEnvironment();
		window.AddControl(panel);
		window.RenderAndGetVisibleContent();
		return panel;
	}

	[Fact]
	public void VerticalScrollOffsetChanged_CarriesPreviousAndCurrentOffsets()
	{
		var panel = CreateRenderedPanel();
		var changes = new List<ScrollOffsetChangedEventArgs>();
		panel.VerticalScrollOffsetChanged += (_, args) => changes.Add(args);

		panel.ScrollVerticalBy(3);
		panel.ScrollVerticalBy(-1);

		Assert.Collection(
			changes,
			first =>
			{
				Assert.Equal(0, first.PreviousOffset);
				Assert.Equal(3, first.Offset);
			},
			second =>
			{
				Assert.Equal(3, second.PreviousOffset);
				Assert.Equal(2, second.Offset);
			});
	}

	[Fact]
	public void VerticalScrollOffsetChanged_DoesNotFireWhenOffsetIsUnchanged()
	{
		var panel = CreateRenderedPanel();
		int fireCount = 0;
		panel.VerticalScrollOffsetChanged += (_, _) => fireCount++;

		panel.ScrollVerticalBy(-1);

		Assert.Equal(0, fireCount);
	}
}
