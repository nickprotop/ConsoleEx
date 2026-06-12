using System.ComponentModel;
using TerminalMail.ViewModels;
using Xunit;

namespace TerminalMail.Tests;

public class ViewModelBaseTests
{
    private sealed class Sample : ViewModelBase
    {
        private string _name = "";
        public string Name { get => _name; set => SetProperty(ref _name, value); }
    }

    [Fact]
    public void SetProperty_RaisesPropertyChanged_WhenValueChanges()
    {
        var vm = new Sample();
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.Name = "Alice";

        Assert.Contains(nameof(Sample.Name), raised);
    }

    [Fact]
    public void SetProperty_DoesNotRaise_WhenValueUnchanged()
    {
        var vm = new Sample { Name = "Bob" };
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.Name = "Bob";

        Assert.Empty(raised);
    }

    [Fact]
    public void RelayCommand_CanExecute_ReflectsPredicate()
    {
        var canRun = false;
        var cmd = new RelayCommand(_ => { }, _ => canRun);

        Assert.False(cmd.CanExecute(null));
        canRun = true;
        Assert.True(cmd.CanExecute(null));
    }

    [Fact]
    public void RelayCommand_Execute_InvokesAction()
    {
        var ran = false;
        var cmd = new RelayCommand(_ => ran = true);

        cmd.Execute(null);

        Assert.True(ran);
    }
}
