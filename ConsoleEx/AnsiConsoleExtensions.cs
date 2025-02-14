// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text;
using System.Text.RegularExpressions;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace ConsoleEx
{
	public static class AnsiConsoleExtensions
	{
		public static int CalculateEffectiveLength(string text)
		{
			var length = 0;
			var insideMarkup = false;

			foreach (var ch in text)
			{
				if (ch == '[')
				{
					insideMarkup = true;
				}
				else if (ch == ']')
				{
					insideMarkup = false;
				}
				else if (!insideMarkup)
				{
					length++;
				}
			}

			return Markup.Remove(text).Length;

			return length;
		}

		public static int GetStrippedStringLength(string input)
		{
			if (string.IsNullOrEmpty(input))
				return 0;

			// Remove markup tags
			var markupStripped = Regex.Replace(input, @"\[(.*?)\]", string.Empty);

			// Remove ANSI escape sequences
			var ansiStripped = Regex.Replace(markupStripped, @"\x1B\[[0-9;]*[a-zA-Z]", string.Empty);

			// Return the length of the cleaned string
			return ansiStripped.Length;
		}

		public static string TruncateSpectre(string inputStr, int maxLength)
		{
			var output = new StringBuilder();
			var stack = new Stack<string>();
			int visibleLength = 0;
			int i = 0;
			int n = inputStr.Length;

			while (i < n && visibleLength < maxLength)
			{
				if (inputStr[i] == '[')
				{
					int j = i + 1;
					// Find the closing ']'
					while (j < n && inputStr[j] != ']')
					{
						j++;
					}

					if (j >= n)
					{
						// Invalid tag - add remaining characters as visible text
						for (int k = i; k < n && visibleLength < maxLength; k++)
						{
							output.Append(inputStr[k]);
							visibleLength++;
						}
						i = n;
						break;
					}

					// Process valid tag
					string tagContent = inputStr.Substring(i + 1, j - i - 1);
					if (tagContent == "/")
					{
						// Closing tag
						if (stack.Count > 0)
						{
							stack.Pop();
						}
						output.Append("[/]");
					}
					else
					{
						// Opening tag
						stack.Push(tagContent);
						output.Append($"[{tagContent}]");
					}
					i = j + 1; // Move past the closing ']'
				}
				else
				{
					// Regular character
					output.Append(inputStr[i]);
					visibleLength++;
					i++;
				}
			}

			// Close any remaining open tags
			while (stack.Count > 0)
			{
				output.Append("[/]");
				stack.Pop();
			}

			return output.ToString();
		}

		public static List<string> ConvertMarkupToAnsi(string markup, int? width, int? height, bool overflow)
		{
			if (string.IsNullOrEmpty(markup))
				return new List<string>();

			var writer = new StringWriter();
			var console = CreateCaptureConsole(writer, overflow ? width : null, overflow ? height : null);

			if (overflow)
			{
				if (width.HasValue)
				{
					console.Profile.Width = width.Value;
				}
				if (height.HasValue)
				{
					console.Profile.Height = height.Value;
				}
			}

			console.Markup(overflow ? markup : TruncateSpectre(markup, console.Profile.Width));
			return writer.ToString().Split('\n').ToList();
		}

		public static List<string> ConvertRenderableToAnsi(IRenderable renderable, int? width, int? height, bool overflow)
		{
			if (renderable == null) return new List<string>();

			var writer = new StringWriter();
			var console = CreateCaptureConsole(writer, overflow ? width : null, overflow ? height : null);

			if (overflow)
			{
				if (width.HasValue)
				{
					console.Profile.Width = width.Value;
				}
				if (height.HasValue)
				{
					console.Profile.Height = height.Value;
				}
			}

			console.Write(renderable);
			return writer.ToString().Split('\n').ToList();
		}

		public static string ParseAnsiTags(string input)
		{
			if (string.IsNullOrEmpty(input))
				return string.Empty;

			var tagToAnsi = new Dictionary<string, string>
			{
				{ "bold", "\u001b[1m" },
				{ "/bold", "\u001b[22m" },
				{ "underline", "\u001b[4m" },
				{ "/underline", "\u001b[24m" },
				{ "foreground red", "\u001b[31m" },
				{ "foreground green", "\u001b[32m" },
				{ "foreground yellow", "\u001b[33m" },
				{ "foreground blue", "\u001b[34m" },
				{ "foreground magenta", "\u001b[35m" },
				{ "foreground cyan", "\u001b[36m" },
				{ "foreground white", "\u001b[37m" },
				{ "foreground grey", "\u001b[90m" },
				{ "background red", "\u001b[41m" },
				{ "background green", "\u001b[42m" },
				{ "background yellow", "\u001b[43m" },
				{ "background blue", "\u001b[44m" },
				{ "background magenta", "\u001b[45m" },
				{ "background cyan", "\u001b[46m" },
				{ "background white", "\u001b[47m" },
				{ "/foreground", "\u001b[39m" },
				{ "/background", "\u001b[49m" },
				{ "reset", "\u001b[0m" }
			};

			var output = new StringBuilder();
			var regex = new Regex(@"\[(.*?)\]");
			var matches = regex.Matches(input);
			int lastIndex = 0;

			foreach (Match match in matches)
			{
				output.Append(input.Substring(lastIndex, match.Index - lastIndex));
				var tag = match.Groups[1].Value.ToLower();
				if (tagToAnsi.TryGetValue(tag, out var ansiCode))
				{
					output.Append(ansiCode);
				}
				else
				{
					output.Append(match.Value); // If tag is not recognized, keep it as is
				}
				lastIndex = match.Index + match.Length;
			}

			output.Append(input.Substring(lastIndex));

			output.Append("\u001b[0m"); // Reset at the end

			return output.ToString();
		}


		private static readonly Regex TruncateAnsiRegex = new(@"\x1B\[[0-9;]*[a-zA-Z]", RegexOptions.Compiled);

		public static string TruncateAnsiString(string input, int maxVisibleLength)
		{
			if (string.IsNullOrEmpty(input) || maxVisibleLength <= 0)
				return string.Empty;

			var output = new StringBuilder();
			int visibleLength = 0;
			int index = 0;

			var matches = TruncateAnsiRegex.Matches(input);
			int matchIndex = 0;

			var openSequences = new Stack<string>();

			while (index < input.Length && visibleLength < maxVisibleLength)
			{
				if (matchIndex < matches.Count && matches[matchIndex].Index == index)
				{
					// Process ANSI escape sequence
					var match = matches[matchIndex++];
					output.Append(match.Value);
					index += match.Length;

					// Track open sequences
					if (!match.Value.Contains('m'))
					{
						openSequences.Push(match.Value);
					}
					else if (openSequences.Count > 0)
					{
						openSequences.Pop();
					}
				}
				else
				{
					// Process visible character
					output.Append(input[index]);
					visibleLength++;
					index++;
				}
			}

			// Close any remaining open sequences
			while (openSequences.Count > 0)
			{
				output.Append("\x1B[0m");
				openSequences.Pop();
			}

			output.Append("\x1B[0m");

			return output.ToString();
		}

		private static readonly Regex TrueLengthOfAnsiRegex = new(@"\x1B\[[0-9;]*[a-zA-Z]", RegexOptions.Compiled);

		public static int GetTrueLengthOfAnsi(string input)
		{
			if (string.IsNullOrEmpty(input))
				return 0;

			// Remove ANSI escape sequences
			var cleanString = TrueLengthOfAnsiRegex.Replace(input, string.Empty);

			// Return the length of the cleaned string
			return cleanString.Length;
		}

		public static IAnsiConsole CreateCaptureConsole(TextWriter writer, int? width, int? height)
		{
			var console = AnsiConsole.Create(new AnsiConsoleSettings
			{
				Ansi = AnsiSupport.Yes,
				ColorSystem = ColorSystemSupport.Detect,
				Out = new AnsiConsoleOutput(writer),
				Interactive = InteractionSupport.No,
				Enrichment = new ProfileEnrichment
				{
					UseDefaultEnrichers = false
				}
			});

			if (width.HasValue)
			{
				console.Profile.Width = width.Value;
			}
			if (height.HasValue)
			{
				console.Profile.Height = height.Value;
			}

			return console;
		}

		public static string GetAnsiCursorPosition(int left, int top)
		{
			return $"\u001b[{top + 1};{left + 1}H";
		}
	}
}
