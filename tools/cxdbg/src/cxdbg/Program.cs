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


	// Every protocol verb, as its own command. One list rather than a class
	// each: the protocol decides what exists, and a hand-written class per verb
	// drifts the day it grows one. The application can be asked for its own
	// list at runtime - `cxdbg commands` - and an unknown verb is answered by
	// the server with the list it knows.
	foreach (var (verb, what, example) in ProtocolVerbs.All)
	{
		// `set` has a nicer face than --arg pairs: a switch and a value, read
		// when the value is left off. It is the same verb, worded the way a
		// person says it, so it is registered from its own class.
		if (verb == "set")
		{
			config.AddCommand<SetCommand>(verb)
				.WithDescription(what)
				.WithExample(example);
			continue;
		}

		config.AddCommand<VerbCommand>(verb)
			.WithDescription(what)
			.WithExample(example);
	}

	// The readable layer over the same verbs. `cxdbg tree` gives the reply;
	// `cxdbg show tree` gives it formatted, and `why` asks the questions whose
	// answer is a cascade.
	config.AddCommand<ShowCommand>("show")
		.WithDescription("Report something about the application, formatted to read")
		.WithExample("show", "tree")
		.WithExample("show", "reachable");

	// The acting layer. `key`, `mouse` and `size` stay available as themselves;
	// these say what a user would say.
	config.AddCommand<SendCommand>("send")
		.WithDescription("Act on the application: press a key, click, resize")
		.WithExample("send", "key", "Tab")
		.WithExample("send", "click", "20", "10")
		.WithExample("send", "resize", "80", "24");

	// Starting an app, rather than attaching to one. Everything after `--`
	// belongs to the application.
	// The live session: the TUI you enter on a socket. Every other verb asks
	// one question; this one keeps asking until you leave.
	config.AddCommand<AttachCommand>("attach")
		.WithDescription("Enter a session on a running application: the tui, or a prompt")
		.WithExample("attach", "-s", "/run/user/1000/app.sock")
		.WithExample("attach", "shell", "-s", "/run/user/1000/app.sock");

	config.AddCommand<RunCommand>("run")
		.WithDescription("Start an application with a diagnostics socket")
		.WithExample("run", "./MyApp")
		.WithExample("run", "./MyApp", "--", "--headless", "--size", "100x30")
		.WithExample("run", "./MyApp", "--attach");

	config.AddCommand<WhyCommand>("why")
		.WithDescription("Explain how something ended up the way it is")
		.WithExample("why", "size", "ChartControl#b679e5")
		.WithExample("why", "color", "Danger", "--state", "Disabled")
		.WithExample("why", "frame")
		.WithExample("why", "input");
});

// No arguments prints help rather than guessing at a socket: a debugger that
// silently attached to whatever it found would be a surprise.
if (args.Length == 0) return app.Run(new[] { "--help" });

// A verb cxdbg does not know, but the protocol might: a newer application can
// answer verbs this build has never heard of. Rather than refuse, ask the
// application - it answers an unknown verb with the list it knows, which is
// better discovery than anything cxdbg could hard-code.
if (!args[0].StartsWith('-')
	&& !ProtocolVerbs.All.Any(v => v.Verb == args[0])
	&& args[0] is not ("show" or "why" or "send" or "run" or "attach"))
{
	return VerbCommand.PassThrough(args);
}

return app.Run(args);

