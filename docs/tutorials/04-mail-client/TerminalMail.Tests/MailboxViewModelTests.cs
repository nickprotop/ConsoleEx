using TerminalMail.Models;
using TerminalMail.ViewModels;
using Xunit;

namespace TerminalMail.Tests;

public class MailboxViewModelTests
{
    private static List<Folder> TwoFolders()
    {
        var inbox = new Folder { Name = "Inbox" };
        inbox.Messages.Add(new Message { From = "a@x.com", Subject = "One", Body = "b1", Date = DateTime.Today, IsRead = false });
        inbox.Messages.Add(new Message { From = "b@x.com", Subject = "Two", Body = "b2", Date = DateTime.Today, IsRead = false });
        var sent = new Folder { Name = "Sent" };
        sent.Messages.Add(new Message { From = "me@x.com", Subject = "Hello", Body = "b3", Date = DateTime.Today, IsRead = true });
        return new List<Folder> { inbox, sent };
    }

    [Fact]
    public void Construction_SelectsFirstFolder_AndPopulatesMessages()
    {
        var vm = new MailboxViewModel(TwoFolders());
        Assert.Equal("Inbox", vm.SelectedFolder!.Name);
        Assert.Equal(2, vm.Messages.Count);
    }

    [Fact]
    public void SelectFolder_ReplacesMessages()
    {
        var vm = new MailboxViewModel(TwoFolders());
        vm.SelectFolder(vm.Folders[1]); // Sent
        Assert.Single(vm.Messages);
        Assert.Equal("Hello", vm.Messages[0].Subject);
    }

    [Fact]
    public void SelectMessage_SetsSelected_AndMarksRead()
    {
        var vm = new MailboxViewModel(TwoFolders());
        var msg = vm.Messages[0];
        Assert.False(msg.IsRead);

        vm.SelectMessage(msg);

        Assert.Same(msg, vm.SelectedMessage);
        Assert.True(msg.IsRead);
    }

    [Fact]
    public void SelectedMessage_Change_RaisesPropertyChanged()
    {
        var vm = new MailboxViewModel(TwoFolders());
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.SelectMessage(vm.Messages[1]);

        Assert.Contains(nameof(MailboxViewModel.SelectedMessage), raised);
    }
}
