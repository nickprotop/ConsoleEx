using TerminalMail.Data;
using Xunit;

namespace TerminalMail.Tests;

public class SampleInboxTests
{
    [Fact]
    public void Build_ReturnsFoldersWithMessages()
    {
        var folders = SampleInbox.Build();
        Assert.Contains(folders, f => f.Name == "Inbox");
        var inbox = folders.First(f => f.Name == "Inbox");
        Assert.True(inbox.Messages.Count >= 4);
        Assert.True(inbox.UnreadCount >= 1);
    }
}
