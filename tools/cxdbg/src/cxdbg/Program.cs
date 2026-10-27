// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using Cxdbg.Commands;
using Spectre.Console.Cli;

var app = new CommandApp();

app.Configure(config =>
{
    config.SetApplicationName("cxdbg");
    config.SetApplicationVersion("1.0.0");

    config.AddCommand<PingCommand>("ping")
        .WithDescription("Check that an application is listening, and say which")
        .WithExample("ping")
        .WithExample("ping", "--socket", "/tmp/myapp.sock");
});

// No arguments prints help rather than guessing at a socket: a debugger that
// silently attached to whatever it found would be a surprise.
return app.Run(args.Length == 0 ? new[] { "--help" } : args);
