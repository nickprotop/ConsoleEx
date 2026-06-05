using System.Threading;
using System.Threading.Tasks;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Core;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class ControlAsyncEventsTests
{
	[Fact]
	public void Button_ClickAsync_IsInvoked()
	{
		var button = new ButtonControl { Text = "OK" };
		var ran = new ManualResetEventSlim(false);
		button.ClickAsync += (s, e) => { ran.Set(); return Task.CompletedTask; };
		button.PerformClickForTest();
		Assert.True(ran.Wait(1000));
	}

	[Fact]
	public void Checkbox_CheckedChangedAsync_IsInvoked()
	{
		var checkbox = new CheckboxControl();
		var ran = new ManualResetEventSlim(false);
		checkbox.CheckedChangedAsync += (s, e) => { ran.Set(); return Task.CompletedTask; };
		checkbox.Checked = !checkbox.Checked;
		Assert.True(ran.Wait(1000));
	}

	[Fact]
	public void Dropdown_SelectedIndexChangedAsync_IsInvoked()
	{
		var dropdown = new DropdownControl();
		dropdown.AddItem("One");
		dropdown.AddItem("Two");
		var ran = new ManualResetEventSlim(false);
		dropdown.SelectedIndexChangedAsync += (s, e) => { ran.Set(); return Task.CompletedTask; };
		dropdown.SelectedIndex = 1;
		Assert.True(ran.Wait(1000));
	}

	[Fact]
	public void Prompt_EnteredAsync_IsInvoked()
	{
		var prompt = new PromptControl();
		var ran = new ManualResetEventSlim(false);
		prompt.EnteredAsync += (s, e) => { ran.Set(); return Task.CompletedTask; };
		prompt.PerformEnterForTest();
		Assert.True(ran.Wait(1000));
	}
}
