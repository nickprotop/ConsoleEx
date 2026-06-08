// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Tests.Infrastructure;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class IControlHostTests
{
	[Fact]
	public void ScrollablePanelControl_IsControlHost_AddRemoveClearEnumerate()
	{
		IControlHost host = new ScrollablePanelControl { Height = 10 };
		var child = ContainerTestHelpers.CreateLabel("A");

		host.AddControl(child);
		Assert.Single(host.Children);
		Assert.Same(child, host.Children[0]);

		host.AddControl(ContainerTestHelpers.CreateLabel("B"));
		Assert.Equal(2, host.Children.Count);

		host.RemoveControl(child);
		Assert.Single(host.Children);

		host.ClearControls();
		Assert.Empty(host.Children);
	}

	[Fact]
	public void ColumnContainer_IsControlHost_AddRemoveClearEnumerate()
	{
		// ColumnContainer's native API uses AddContent/RemoveContent/Contents — IControlHost
		// forwards via explicit interface members so the public surface is unchanged.
		var column = new ColumnContainer(new HorizontalGridControl());
		IControlHost host = column;
		var child = ContainerTestHelpers.CreateLabel("A");

		host.AddControl(child);
		Assert.Single(host.Children);
		Assert.Same(child, host.Children[0]);
		Assert.Same(child, column.Contents[0]); // native API still works

		host.AddControl(ContainerTestHelpers.CreateLabel("B"));
		Assert.Equal(2, host.Children.Count);

		host.RemoveControl(child);
		Assert.Single(host.Children);

		host.ClearControls();
		Assert.Empty(host.Children);
	}

	[Fact]
	public void Window_IsControlHost_AddRemoveClearEnumerate()
	{
		var (_, window) = ContainerTestHelpers.CreateTestEnvironment();
		IControlHost host = window;
		var child = ContainerTestHelpers.CreateLabel("A");

		host.AddControl(child);
		Assert.Single(host.Children);
		Assert.Same(child, host.Children[0]);

		host.AddControl(ContainerTestHelpers.CreateLabel("B"));
		Assert.Equal(2, host.Children.Count);

		host.RemoveControl(child);
		Assert.Single(host.Children);

		host.ClearControls();
		Assert.Empty(host.Children);
	}

	[Fact]
	public void NonFlatContainers_DoNotImplementControlHost()
	{
		// Capability check: containers whose child model is not a flat IWindowControl list
		// must NOT implement IControlHost — forcing them would mean NotSupportedException lies.
		Assert.False(typeof(IControlHost).IsAssignableFrom(typeof(TabControl)));
		Assert.False(typeof(IControlHost).IsAssignableFrom(typeof(MenuControl)));
		Assert.False(typeof(IControlHost).IsAssignableFrom(typeof(ToolbarControl)));
		Assert.False(typeof(IControlHost).IsAssignableFrom(typeof(NavigationView)));
	}
}
