using SharpConsoleUI;
using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drivers;
using TerminalMail.Data;
using TerminalMail.UI;
using TerminalMail.ViewModels;

var ws = new ConsoleWindowSystem(
    new NetConsoleDriver(RenderMode.Buffer),
    options: new ConsoleWindowSystemOptions(
        ShowTopPanel: false,
        ShowBottomPanel: false));

var mailbox = new MailboxViewModel(SampleInbox.Build());
new MailWindow(ws, mailbox).Create();

return ws.Run();
