using TerminalMail.Models;

namespace TerminalMail.Data;

/// <summary>In-memory seed data so the tutorial stays focused on UI + MVVM (no IMAP).</summary>
public static class SampleInbox
{
    public static List<Folder> Build()
    {
        var today = DateTime.Today;
        var inbox = new Folder { Name = "Inbox" };
        inbox.Messages.AddRange(new[]
        {
            new Message { From = "alice@example.com", Subject = "Q3 roadmap", IsFlagged = true, Date = today.AddHours(9).AddMinutes(14), IsRead = false,
                Body = "Hi team,\n\nAttached is the updated roadmap for Q3. The headline items are the new\ncompositor effects and the NavigationView polish.\n\nLet me know your thoughts before Friday.\n\n— Alice" },
            new Message { From = "bob@example.com", Subject = "Lunch?", Date = today.AddHours(8).AddMinutes(50), IsRead = false,
                Body = "Anyone up for lunch at 12:30? The new ramen place is open.\n\n— Bob" },
            new Message { From = "carol@example.com", Subject = "Re: invoice #4821", Date = today.AddDays(-1).AddHours(16), IsRead = true,
                Body = "Thanks, payment is on its way. You should see it within 2 business days.\n\nBest,\nCarol" },
            new Message { From = "dave@example.com", Subject = "Standup notes", Date = today.AddDays(-1).AddHours(10), IsRead = true,
                Body = "Notes from today's standup:\n- Shipped the gradient backgrounds\n- Started on alpha blending\n- Blocked on the PTY backend review" },
        });

        var sent = new Folder { Name = "Sent" };
        sent.Messages.Add(new Message { From = "me@example.com", Subject = "Welcome aboard", Date = today.AddDays(-2), IsRead = true,
            Body = "Welcome to the team! Ping me if you need anything to get set up." });

        var drafts = new Folder { Name = "Drafts" };
        drafts.Messages.Add(new Message { From = "me@example.com", Subject = "(no subject)", Date = today.AddHours(7), IsRead = false,
            Body = "" });

        var archive = new Folder { Name = "Archive" };

        return new List<Folder> { inbox, sent, drafts, archive };
    }
}
