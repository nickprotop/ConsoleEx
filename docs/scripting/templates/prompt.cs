#!/usr/bin/env dotnet
#:package SharpConsoleUI@2.4.59

// prompt.cs — Text input with optional password masking.
//
// args:     --prompt "Label" (optional, default "> ")
//           --mask            (optional, masks input with '*' for passwords)
//           --default "TEXT"  (optional, initial value)
// stdin:    ignored
// stdout:   the entered text (exactly as typed)
// exit 0:   user pressed Enter with non-empty text
// exit 1:   user cancelled (Ctrl+Q) or submitted empty text
//
// Example:
//   name=$(dotnet run prompt.cs --prompt "Your name: ")
//   token=$(dotnet run prompt.cs --prompt "Token: " --mask)

using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Drivers;

string promptText = "> ";
string defaultValue = "";
bool mask = false;

for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--prompt" && i + 1 < args.Length) promptText = args[++i];
    else if (args[i] == "--default" && i + 1 < args.Length) defaultValue = args[++i];
    else if (args[i] == "--mask") mask = true;
}

var system = new ConsoleWindowSystem(new NetConsoleDriver(RenderMode.Buffer));
string? result = null;

var promptBuilder = Controls.Prompt()
    .WithPrompt(promptText)
    .WithInput(defaultValue)
    .OnEntered((sender, text, win) =>
    {
        result = text;
        system.Shutdown();
    });

if (mask)
    promptBuilder = promptBuilder.WithMaskCharacter('*');

var window = new WindowBuilder(system)
    .WithTitle("Input")
    .WithSize(60, 8)
    .AddControl(promptBuilder.Build())
    .Build();

system.AddWindow(window);
system.Run();

if (string.IsNullOrEmpty(result))
    return 1;

Console.Out.WriteLine(result);
return 0;
