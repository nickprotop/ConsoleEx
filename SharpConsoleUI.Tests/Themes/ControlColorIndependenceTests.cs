// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Themes;

/// <summary>
/// Proves controls read their OWN theme members, not Button*: setting a control's member on a theme is
/// independent of Button*.
/// </summary>
public class ControlColorIndependenceTests
{
	[Fact]
	public void DropdownForeground_IsIndependentOfButtonForeground()
	{
		var t = new MutableTheme { ButtonForegroundColor = Color.Red, DropdownForegroundColor = Color.Green };
		Assert.Equal(Color.Green, ((ITheme)t).DropdownForegroundColor);
		Assert.Equal(Color.Red, ((ITheme)t).ButtonForegroundColor);
		Assert.NotEqual(((ITheme)t).ButtonForegroundColor, ((ITheme)t).DropdownForegroundColor);
	}

	[Fact]
	public void ListAndCheckbox_HaveOwnForegrounds()
	{
		var t = new MutableTheme { ListForegroundColor = Color.Blue, CheckboxForegroundColor = Color.Yellow };
		Assert.Equal(Color.Blue, ((ITheme)t).ListForegroundColor);
		Assert.Equal(Color.Yellow, ((ITheme)t).CheckboxForegroundColor);
	}
}
