using System.Collections.Generic;
using System.Linq;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class ToastContentTests
{
	[Fact]
	public void MessageSetter_RaisesPropertyChanged_GuardsSameValue()
	{
		var c = new ToastContent("a", NotificationSeverity.Info, ColorRole.Info);
		var names = new List<string?>();
		c.PropertyChanged += (_, e) => names.Add(e.PropertyName);
		c.Message = "b";
		c.Message = "b"; // same → no raise
		Assert.Equal(new[] { nameof(ToastContent.Message) }, names.Where(n => n == nameof(ToastContent.Message)).ToArray());
	}

	[Fact]
	public void ImplementsRoleableContract()
	{
		var c = new ToastContent("a", NotificationSeverity.Success, ColorRole.Success);
		c.ColorRole = ColorRole.Warning;
		c.Outline = true;
		c.ColorRoleMode = ThemeMode.Dark;
		Assert.Equal(ColorRole.Warning, c.ColorRole);
		Assert.True(c.Outline);
		Assert.Equal(ThemeMode.Dark, c.ColorRoleMode);
	}

	[Fact]
	public void Click_RaisesDismissRequested()
	{
		var c = new ToastContent("a", NotificationSeverity.Info, ColorRole.Info);
		c.SetBounds(new System.Drawing.Rectangle(0, 0, 20, 3));
		bool dismissed = false;
		c.DismissRequested += (_, _) => dismissed = true;
		c.ProcessMouseEvent(new SharpConsoleUI.Events.MouseEventArgs(
			new List<SharpConsoleUI.Drivers.MouseFlags> { SharpConsoleUI.Drivers.MouseFlags.Button1Clicked },
			new System.Drawing.Point(2, 1), new System.Drawing.Point(2, 1), new System.Drawing.Point(2, 1)));
		Assert.True(dismissed);
	}
}
