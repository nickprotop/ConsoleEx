// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text.Json.Nodes;
using Cxdbg.Protocol;

namespace Cxdbg.Commands;

/// <summary>Settings for acting on the application.</summary>
internal static class SendCommand
{
	/// <summary>Runs <c>send &lt;what&gt; [a] [b]</c>.</summary>
	internal static int Execute(CommandLine line)
	{
		if (line.Rest.Count == 0)
		{
			throw CxdbgException.Usage(
				"send needs something to do: key, click, resize, and so on.");
		}

		return CommandRunner.Run(line, client =>
		{
			var (verb, args, said) = Translate(
				line.Rest[0],
				line.Rest.Count > 1 ? line.Rest[1] : null,
				line.Rest.Count > 2 ? line.Rest[2] : null);

			// The caller's own fields win, so `send click 20 10 --button right`
			// reaches the protocol even though click spells it for you.
			foreach (var (name, value) in line.Fields) args[name] = value;

			var reply = client.Send(verb, args);

			return line.Json ? Output.Json(reply) : said + "\n";
		});
	}

	/// <summary>
	/// Turns what the user typed into the protocol field names.
	/// </summary>
	/// <param name="whatWord">The action: key, click, resize, and so on.</param>
	/// <param name="a">Its first argument, if it takes one.</param>
	/// <param name="b">Its second.</param>
	/// <returns>The verb, its fields, and the line to print when it worked.</returns>
	/// <exception cref="CxdbgException">When an argument is missing or not a number.</exception>
	private static (string Verb, Dictionary<string, JsonNode?> Args, string Said) Translate(
		string whatWord, string? a, string? b)
	{
		string what = whatWord.ToLowerInvariant();

		switch (what)
		{
			case "key":
			case "keys":
				{
					string spec = a
						?? throw CxdbgException.Usage(
							"send key needs a spec, in tmux notation: Tab, Down, C-c, S-Tab, or literal text");

					return ("key",
						new Dictionary<string, JsonNode?> { ["spec"] = spec },
						$"sent {spec}");
				}

			case "resize":
			case "size":
				{
					int w = Number(a, "width", "send resize 80 24");
					int h = Number(b, "height", "send resize 80 24");

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
							$"don't know how to send '{whatWord}'; try key, click, doubleclick, "
							+ "rightclick, press, release, drag, move, wheelup, wheeldown, resize"),
					};

					string button = what == "rightclick" ? "right" : "left";
					int x = Number(a, "x", $"send {what} 20 10");
					int y = Number(b, "y", $"send {what} 20 10");

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
