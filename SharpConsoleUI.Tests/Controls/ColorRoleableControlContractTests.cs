using System.ComponentModel;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.DataBinding;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class ColorRoleableControlContractTests
{
	[Fact]
	public void RoleableControls_ImplementIRoleableControl()
	{
		Assert.True(new ButtonControl() is IColorRoleableControl);
		Assert.True(new ScrollablePanelControl() is IColorRoleableControl);
		Assert.True(new ListControl() is IColorRoleableControl);
		Assert.True(new ProgressBarControl() is IColorRoleableControl);
	}

	[Fact]
	public void NonRoleableControls_DoNotImplementIRoleableControl()
	{
		Assert.False(new CanvasControl() is IColorRoleableControl);
		Assert.False(new HtmlControl() is IColorRoleableControl);
		Assert.False(new HorizontalGridControl() is IColorRoleableControl);
	}

	[Fact]
	public void Role_RaisesPropertyChanged_ForBinding()
	{
		var button = new ButtonControl();
		string? changed = null;
		((INotifyPropertyChanged)button).PropertyChanged += (_, e) => changed = e.PropertyName;
		button.ColorRole = ColorRole.Danger;
		Assert.Equal(nameof(ButtonControl.ColorRole), changed);
	}

	[Fact]
	public void Outline_RaisesPropertyChanged_ForBinding()
	{
		var button = new ButtonControl();
		string? changed = null;
		((INotifyPropertyChanged)button).PropertyChanged += (_, e) => changed = e.PropertyName;
		button.Outline = true;
		Assert.Equal(nameof(ButtonControl.Outline), changed);
	}

	private sealed class ColorRoleVm : INotifyPropertyChanged
	{
		private ColorRole _role = ColorRole.Default;
		public ColorRole ColorRole
		{
			get => _role;
			set { if (_role != value) { _role = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ColorRole))); } }
		}
		public event PropertyChangedEventHandler? PropertyChanged;
	}

	[Fact]
	public void Role_TwoWayBinding_RoundTrips()
	{
		var vm = new ColorRoleVm { ColorRole = ColorRole.Warning };
		var button = new ButtonControl();
		button.BindTwoWay(vm, v => v.ColorRole, b => b.ColorRole);
		vm.ColorRole = ColorRole.Danger;
		Assert.Equal(ColorRole.Danger, button.ColorRole);
		button.ColorRole = ColorRole.Success;
		Assert.Equal(ColorRole.Success, vm.ColorRole);
	}
}
