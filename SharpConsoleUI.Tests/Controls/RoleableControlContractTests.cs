using System.ComponentModel;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.DataBinding;
using SharpConsoleUI.Themes;
using Xunit;

namespace SharpConsoleUI.Tests.Controls;

public class RoleableControlContractTests
{
	[Fact]
	public void RoleableControls_ImplementIRoleableControl()
	{
		Assert.True(new ButtonControl() is IRoleableControl);
		Assert.True(new ScrollablePanelControl() is IRoleableControl);
		Assert.True(new ListControl() is IRoleableControl);
		Assert.True(new ProgressBarControl() is IRoleableControl);
	}

	[Fact]
	public void NonRoleableControls_DoNotImplementIRoleableControl()
	{
		Assert.False(new CanvasControl() is IRoleableControl);
		Assert.False(new HtmlControl() is IRoleableControl);
		Assert.False(new HorizontalGridControl() is IRoleableControl);
	}

	[Fact]
	public void Role_RaisesPropertyChanged_ForBinding()
	{
		var button = new ButtonControl();
		string? changed = null;
		((INotifyPropertyChanged)button).PropertyChanged += (_, e) => changed = e.PropertyName;
		button.Role = ControlRole.Danger;
		Assert.Equal(nameof(ButtonControl.Role), changed);
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

	private sealed class RoleVm : INotifyPropertyChanged
	{
		private ControlRole _role = ControlRole.Default;
		public ControlRole Role
		{
			get => _role;
			set { if (_role != value) { _role = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Role))); } }
		}
		public event PropertyChangedEventHandler? PropertyChanged;
	}

	[Fact]
	public void Role_TwoWayBinding_RoundTrips()
	{
		var vm = new RoleVm { Role = ControlRole.Warning };
		var button = new ButtonControl();
		button.BindTwoWay(vm, v => v.Role, b => b.Role);
		vm.Role = ControlRole.Danger;
		Assert.Equal(ControlRole.Danger, button.Role);
		button.Role = ControlRole.Success;
		Assert.Equal(ControlRole.Success, vm.Role);
	}
}
