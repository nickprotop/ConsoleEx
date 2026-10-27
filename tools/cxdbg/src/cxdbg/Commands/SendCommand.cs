// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.ComponentModel;
using System.Text.Json.Nodes;
using Cxdbg.Protocol;
using Spectre.Console.Cli;

namespace Cxdbg.Commands;

/// <summary>Settings for acting on the application.</summary>
public class SendSettings : SocketSettings
{
	[Description("What to send: key, click, doubleclick, rightclick, wheelup, wheeldown, move, resize")]
	[CommandArgument(0, "<WHAT>")]
	public string What { get; set; } = "key";

	[Description("The key spec, or the X coordinate / width")]
	[CommandArgument(1, "[A]")]
	public string? A { get; set; }

	[Description("The Y coordinate / height")]
	[CommandArgument(2, "[B]")]
	public string? B { get; set; }

	[Description("Print the raw reply instead of a line saying what was sent")]
	[CommandOption("--json")]
	public bool Json { get; set; }
}

/// <summary>
/// Acts on the running application, the way a user would.
/// </summary>
/// <remarks>
/// <para>
/// The readable face of <c>key</c>, <c>mouse</c> and <c>size</c>. Those three
/// stay available as themselves - <c>cxdbg key --spec Tab</c> works - but
/// nobody reaching for a debugger wants to remember that a click is the mouse
/// verb with an action field, or that the key verb's field is called spec.
/// </para>
/// <para>
/// Input is asynchronous: the application has the keystroke when this returns,
/// it has not necessarily acted on it. A caller that wants to observe the
/// effect polls <c>frame</c> until the counter moves, which is the same
/// discipline the library's own docs prescribe.
/// </para>
/// </remarks>
public sealed class SendCommand : Command<SendSettings>
{
	protected override int Execute(CommandContext context, SendSettings settings, CancellationToken ct)
		=> CommandRunner.Run(settings, settings.Json, client =>
		{
			var (verb, args, said) = Translate(settings);
			var reply = client.Send(verb, args);

			return settings.Json
				? CommandRunner.Raw(reply) + "\n"
				: said + "\n";
		});

	/// <summary>
	/// Turns what the user typed into the protocol field names.
	/// </summary>
	/// <param name="settings">The parsed command line.</param>
	/// <returns>The verb, its fields, and the line to print when it worked.</returns>
	/// <exception cref="CxdbgException">When an argument is missing or not a number.</exception>
	private static (string Verb, Dictionary<string, JsonNode?> Args, string Said) Translate(
		SendSettings settings)
	{
		string what = settings.What.ToLowerInvariant();

		switch (what)
		{
			case "key":
			case "keys":
			{
				string spec = settings.A
					?? throw CxdbgException.Usage(
						"send key needs a spec, in tmux notation: Tab, Down, C-c, S-Tab, or literal text");

				return ("key",
					new Dictionary<string, JsonNode?> { ["spec"] = spec },
					$"sent {spec}");
			}

			case "resize":
			case "size":
			{
				int w = Number(settings.A, "width", "send resize 80 24");
				int h = Number(settings.B, "height", "send resize 80 24");

				return ("size",
					new Dictionary<string, JsonNode?> { ["w"] = w, ["h"] = h },
					$"resized to {w}x{h}");
			}

			default:
			{
				// Everything else is the mouse. The action names are the protocol's
				// own, so a new one the server grows needs no change here.
				string action = what switch
				{
					"click" => "click",
					"doubleclick" => "doubleclick",
					"rightclick" => "click",
					"move" => "move",
					"press" => "press",
					"release" => "release",
					"drag" => "drag",
					"wheelup" => "wheelup",
					"wheeldown" => "wheeldown",
					_ => throw CxdbgException.Usage(
						$"don't know how to send '{settings.What}'; try key, click, doubleclick, "
						+ "rightclick, press, release, drag, move, wheelup, wheeldown, resize"),
				};

				string button = what == "rightclick" ? "right" : "left";
				int x = Number(settings.A, "x", $"send {what} 20 10");
				int y = Number(settings.B, "y", $"send {what} 20 10");

				return ("mouse",
					new Dictionary<string, JsonNode?>
					{
						["x"] = x,
						["y"] = y,
						["button"] = button,
						["action"] = action,
					},
					$"sent {action} at {x},{y}");
			}
		}
	}

	/// <summary>Reads a coordinate, saying what was wanted when it is not there.</summary>
	private static int Number(string? value, string name, string example)
	{
		if (string.IsNullOrEmpty(value))
			throw CxdbgException.Usage($"missing {name}; e.g. `cxdbg {example}`");

		if (!int.TryParse(value, out int n))
			throw CxdbgException.Usage($"{name} must be a number, got '{value}'");

		return n;
	}
}
