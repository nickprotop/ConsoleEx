// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Diagnostics.Socket;

/// <summary>
/// Parses the key notation the diagnostics socket accepts.
/// </summary>
/// <remarks>
/// <para>
/// Borrows tmux/emacs notation because it is what anyone reaching for this already knows:
/// <c>Down</c>, <c>C-c</c>, <c>S-Tab</c>, <c>M-x</c>, <c>F5</c>, <c>Enter</c>. Anything not
/// recognised as a named key is sent as literal text, one keystroke per character, so
/// <c>"hello"</c> types hello.
/// </para>
/// <para>
/// An unrecognised name is an ERROR rather than being typed literally. Silently typing
/// <c>"Dwon"</c> into the app would be far more confusing than being told it is not a key.
/// </para>
/// </remarks>
internal static class KeySpec
{
	/// <summary>Parses a spec into the keystrokes to enqueue, or null with a reason.</summary>
	/// <param name="spec">A key name, a modified key, or quoted literal text.</param>
	/// <param name="error">Why it could not be parsed.</param>
	/// <returns>The keystrokes, or null.</returns>
	internal static List<ConsoleKeyInfo>? Parse(string spec, out string? error)
	{
		error = null;
		spec = spec.Trim();

		// Quoted - literal text, one keystroke per character.
		if (spec.Length >= 2 && spec[0] == '"' && spec[^1] == '"')
		{
			var text = spec[1..^1];
			var literal = new List<ConsoleKeyInfo>(text.Length);
			foreach (char ch in text)
				literal.Add(Literal(ch));
			return literal;
		}

		bool ctrl = false, alt = false, shift = false;

		// Strip modifier prefixes: C- M- S-, in any order.
		while (spec.Length > 2 && spec[1] == '-')
		{
			switch (char.ToUpperInvariant(spec[0]))
			{
				case 'C': ctrl = true; break;
				case 'M': alt = true; break;
				case 'S': shift = true; break;
				default:
					error = $"unknown modifier '{spec[0]}-'; known: C- (ctrl), M- (alt), S- (shift)";
					return null;
			}
			spec = spec[2..];
		}

		// A single character with modifiers, e.g. C-c.
		if (spec.Length == 1 && (ctrl || alt || shift))
		{
			char ch = spec[0];
			var key = char.IsLetter(ch)
				? (ConsoleKey)char.ToUpperInvariant(ch)
				: (ConsoleKey)0;

			// Ctrl+letter produces the control character the terminal would send.
			char keyChar = ctrl && char.IsLetter(ch)
				? (char)(char.ToUpperInvariant(ch) - 'A' + 1)
				: ch;

			return new List<ConsoleKeyInfo> { new(keyChar, key, shift, alt, ctrl) };
		}

		if (!Named.TryGetValue(spec, out var named))
		{
			// A bare multi-character token that is not a key name is a typo, not text -
			// sending it literally would be a confusing way to find that out.
			if (spec.Length > 1)
			{
				error = $"unknown key '{spec}'; use a known name, or quote it to type it as text. "
					  + $"Known: {string.Join(", ", Named.Keys.Order())}";
				return null;
			}

			return new List<ConsoleKeyInfo> { Literal(spec[0]) };
		}

		return new List<ConsoleKeyInfo> { new(named.KeyChar, named.Key, shift, alt, ctrl) };
	}

	private static ConsoleKeyInfo Literal(char ch)
	{
		var key = char.IsLetter(ch) ? (ConsoleKey)char.ToUpperInvariant(ch)
			: char.IsDigit(ch) ? (ConsoleKey)ch
			: (ConsoleKey)0;
		return new ConsoleKeyInfo(ch, key, char.IsUpper(ch), false, false);
	}

	private static readonly Dictionary<string, (ConsoleKey Key, char KeyChar)> Named =
		new(StringComparer.OrdinalIgnoreCase)
		{
			["Up"] = (ConsoleKey.UpArrow, '\0'),
			["Down"] = (ConsoleKey.DownArrow, '\0'),
			["Left"] = (ConsoleKey.LeftArrow, '\0'),
			["Right"] = (ConsoleKey.RightArrow, '\0'),
			["Enter"] = (ConsoleKey.Enter, '\r'),
			["Escape"] = (ConsoleKey.Escape, '\u001b'),
			["Esc"] = (ConsoleKey.Escape, '\u001b'),
			["Tab"] = (ConsoleKey.Tab, '\t'),
			["Space"] = (ConsoleKey.Spacebar, ' '),
			["Backspace"] = (ConsoleKey.Backspace, '\b'),
			["Delete"] = (ConsoleKey.Delete, '\0'),
			["Home"] = (ConsoleKey.Home, '\0'),
			["End"] = (ConsoleKey.End, '\0'),
			["PageUp"] = (ConsoleKey.PageUp, '\0'),
			["PageDown"] = (ConsoleKey.PageDown, '\0'),
			["Insert"] = (ConsoleKey.Insert, '\0'),
			["F1"] = (ConsoleKey.F1, '\0'),
			["F2"] = (ConsoleKey.F2, '\0'),
			["F3"] = (ConsoleKey.F3, '\0'),
			["F4"] = (ConsoleKey.F4, '\0'),
			["F5"] = (ConsoleKey.F5, '\0'),
			["F6"] = (ConsoleKey.F6, '\0'),
			["F7"] = (ConsoleKey.F7, '\0'),
			["F8"] = (ConsoleKey.F8, '\0'),
			["F9"] = (ConsoleKey.F9, '\0'),
			["F10"] = (ConsoleKey.F10, '\0'),
			["F11"] = (ConsoleKey.F11, '\0'),
			["F12"] = (ConsoleKey.F12, '\0'),
		};
}
