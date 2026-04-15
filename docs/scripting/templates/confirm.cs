#!/usr/bin/env dotnet
#:package SharpConsoleUI@2.4.59

// confirm.cs — Yes/no confirmation dialog.
//
// args:     --title "TITLE" --message "MESSAGE" (both optional)
// stdin:    ignored
// stdout:   nothing
// exit 0:   user clicked Yes
// exit 1:   user clicked No or cancelled
//
// Example:
//   dotnet run confirm.cs --title "Deploy" --message "Push to production?" && ./deploy.sh

using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;

string title = "Confirm";
string message = "Are you sure?";

for (int i = 0; i < args.Length - 1; i++)
{
    if (args[i] == "--title") title = args[i + 1];
    else if (args[i] == "--message") message = args[i + 1];
}

var system = new ConsoleWindowSystem(new NetConsoleDriver(RenderMode.Buffer));
bool? confirmed = null;

var window = new WindowBuilder(system)
    .WithTitle(title)
    .WithSize(Math.Max(40, message.Length + 8), 9)
    .AddControl(Controls.Markup()
        .AddLine(message)
        .AddEmptyLine()
        .Build())
    .AddControl(Controls.HorizontalGrid()
        .Column(col => col.Flex(1.0).Add(
            Controls.Button()
                .WithText("Yes")
                .OnClick((sender, btn, win) =>
                {
                    confirmed = true;
                    system.Shutdown();
                })
                .Build()))
        .Column(col => col.Flex(1.0).Add(
            Controls.Button()
                .WithText("No")
                .OnClick((sender, btn, win) =>
                {
                    confirmed = false;
                    system.Shutdown();
                })
                .Build()))
        .Build())
    .Build();

system.AddWindow(window);
system.Run();

return confirmed == true ? 0 : 1;
