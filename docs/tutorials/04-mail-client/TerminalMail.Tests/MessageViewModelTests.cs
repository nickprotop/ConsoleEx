using TerminalMail.Models;
using TerminalMail.ViewModels;
using Xunit;

namespace TerminalMail.Tests;

public class MessageViewModelTests
{
    private static Message Sample() => new()
    {
        From = "alice@example.com",
        Subject = "Q3 roadmap",
        Body = "Hi team",
        Date = new DateTime(2026, 6, 12, 9, 14, 0),
        IsRead = false,
    };

    [Fact]
    public void ExposesUnderlyingFields()
    {
        var vm = new MessageViewModel(Sample());
        Assert.Equal("alice@example.com", vm.From);
        Assert.Equal("Q3 roadmap", vm.Subject);
        Assert.False(vm.IsRead);
    }

    [Fact]
    public void MarkRead_SetsIsRead_AndRaisesChange()
    {
        var vm = new MessageViewModel(Sample());
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.MarkRead();

        Assert.True(vm.IsRead);
        Assert.Contains(nameof(MessageViewModel.IsRead), raised);
    }

    [Fact]
    public void HeaderText_CombinesFromAndSubject()
    {
        var vm = new MessageViewModel(Sample());
        Assert.Contains("alice@example.com", vm.HeaderText);
        Assert.Contains("Q3 roadmap", vm.HeaderText);
    }
}
