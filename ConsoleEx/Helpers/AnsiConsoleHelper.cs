﻿// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using Spectre.Console;
using Spectre.Console.Rendering;
using System.Text;
using System.Text.RegularExpressions;
using static System.Runtime.CompilerServices.RuntimeHelpers;

namespace ConsoleEx.Helpers
{
	public static class AnsiConsoleHelper
	{
		private static readonly Regex TruncateAnsiRegex = new(@"\x1B\[[0-9;]*[a-zA-Z]", RegexOptions.Compiled);

		private static Dictionary<string, string> tagToAnsi = new Dictionary<string, string>
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
			{ "foreground black", "\u001b[30m" },
			{ "background red", "\u001b[41m" },
			{ "background green", "\u001b[42m" },
			{ "background yellow", "\u001b[43m" },
			{ "background blue", "\u001b[44m" },
			{ "background magenta", "\u001b[45m" },
			{ "background cyan", "\u001b[46m" },
			{ "background white", "\u001b[47m" },
			{ "background black", "\u001b[40m" },
			{ "/foreground", "\u001b[39m" },
			{ "/background", "\u001b[49m" },
			{ "reset", "\u001b[0m" }
		};

		public static string AnsiEmptySpace(int width, Color backgroundColor)
		{
			if (width <= 0)
				return string.Empty;
			return ConvertSpectreMarkupToAnsi($"{new string(' ', width)}", width, 1, false, backgroundColor, null)[0];
		}

		public static List<string> ConvertSpectreMarkupToAnsi(string markup, int? width, int? height, bool overflow, Color? backgroundColor, Color? foregroundColor)
		{
			if (string.IsNullOrEmpty(markup))
				return new List<string>() { string.Empty };

			markup = EscapeInvalidMarkupTags(markup);

			var writer = new StringWriter();
			var console = CreateCaptureConsole(writer, width, height);

			if (width.HasValue)
			{
				console.Profile.Width = width.Value;
			}
			if (height.HasValue)
			{
				console.Profile.Height = height.Value;
			}

			if (foregroundColor == null)
			{
				if (backgroundColor != null)
				{
					var renderedMarkup = new Markup(overflow ? markup : TruncateSpectre(markup, width ?? 80), new Style(background: backgroundColor));
					console.Write(renderedMarkup);
				}
				else
				{
					var renderedMarkup = new Markup(overflow ? markup : TruncateSpectre(markup, width ?? 80));
					console.Write(renderedMarkup);
				}
			}
			else
			{
				if (backgroundColor != null)
				{
					var renderedMarkup = new Markup(overflow ? markup : TruncateSpectre(markup, width ?? 80), new Style(background: backgroundColor, foreground: foregroundColor));
					console.Write(renderedMarkup);
				}
				else
				{
					var renderedMarkup = new Markup(overflow ? markup : TruncateSpectre(markup, width ?? 80), new Style(foreground: foregroundColor));
					console.Write(renderedMarkup);
				}
			}

			List<string> result = writer.ToString().Split('\n').ToList();

			for (int i = 0; i < result.Count; i++)
			{
				result[i] = result[i].Replace("\r", "");
				result[i] = result[i].Replace("\n", "");
			}

			return overflow ? result : new List<string> { result[0] };
		}

		public static List<string> ConvertSpectreRenderableToAnsi(IRenderable renderable, int? width, int? height)
		{
			if (renderable == null) return new List<string>();

			var writer = new StringWriter();
			var console = CreateCaptureConsole(writer, width, height);

			if (width.HasValue)
			{
				console.Profile.Width = width.Value;
			}
			if (height.HasValue)
			{
				console.Profile.Height = height.Value;
			}

			console.Write(renderable);
			return writer.ToString().Split('\n').ToList();
		}

		public static IAnsiConsole CreateCaptureConsole(TextWriter writer, int? width, int? height)
		{
			var consoleOutput = new AnsiConsoleOutput(writer);
			consoleOutput.SetEncoding(Encoding.UTF8);

			var console = AnsiConsole.Create(new AnsiConsoleSettings
			{
				Ansi = AnsiSupport.Yes,
				ColorSystem = ColorSystemSupport.Detect,
				Out = consoleOutput,
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

		/// <summary>
		/// Processes a string containing Spectre Console markup and escapes invalid tags with double brackets.
		/// Valid tags remain unchanged, while invalid tags are escaped by doubling their brackets.
		/// </summary>
		/// <param name="input">The string containing Spectre markup to process</param>
		/// <returns>A string with invalid tags escaped with double brackets</returns>
		public static string EscapeInvalidMarkupTags(string input)
		{
			if (string.IsNullOrEmpty(input))
				return input;

			var output = new StringBuilder();
			int i = 0;
			int n = input.Length;

			// Known valid tag patterns in addition to our dictionary
			var validPatterns = new[]
			{
        // Generic closing tag
        "^/$",
        // Color tags (hex)
        "^#[0-9a-fA-F]{3}$",
		"^#[0-9a-fA-F]{6}$",
		"^#[0-9a-fA-F]{8}$",
        // Color tags (rgb)
        "^rgb\\(\\s*\\d+\\s*,\\s*\\d+\\s*,\\s*\\d+\\s*\\)$",
        // Named colors (beyond our basic dictionary)
        "^[a-z]+( [a-z]+)*$",
        // Default closing tags
        "^/[a-z]+$"
	};

			while (i < n)
			{
				// Check for potential tag start
				if (input[i] == '[' && i + 1 < n && input[i + 1] != '[')
				{
					int tagStart = i;
					int j = i + 1;

					// Find the closing bracket
					while (j < n && input[j] != ']')
					{
						j++;
					}

					// If we found a complete tag
					if (j < n)
					{
						string tagContent = input.Substring(tagStart + 1, j - tagStart - 1);
						bool isValidTag = false;

						// First, check against our dictionary of known tags
						if (tagToAnsi.ContainsKey(tagContent.ToLower()))
						{
							isValidTag = true;
						}
						else
						{
							// Check for "color on color" format
							if (tagContent.Contains(" on "))
							{
								string[] colorParts = tagContent.Split(new[] { " on " }, StringSplitOptions.None);
								if (colorParts.Length == 2)
								{
									string foreground = colorParts[0].Trim();
									string background = colorParts[1].Trim();

									// Check if both colors are valid
									bool validForeground = IsValidColor(foreground);
									bool validBackground = IsValidColor(background);

									isValidTag = validForeground && validBackground;
								}
							}
							// Check for color styling
							else if (tagContent.StartsWith("default"))
								isValidTag = true;
							else if (tagContent.StartsWith("foreground") || tagContent.StartsWith("background"))
							{
								string[] parts = tagContent.Split(' ', 2);
								if (parts.Length > 1)
								{
									string colorPart = parts[1].Trim();
									isValidTag = IsValidColor(colorPart);
								}
							}
							// Check for generic closing tags or other patterns
							else
							{
								// Direct color names without foreground/background prefix
								if (IsValidColor(tagContent))
								{
									isValidTag = true;
								}
								else
								{
									foreach (var pattern in validPatterns)
									{
										if (Regex.IsMatch(tagContent, pattern))
										{
											isValidTag = true;
											break;
										}
									}
								}
							}
						}

						if (isValidTag)
						{
							// Keep valid tags as they are
							output.Append(input.Substring(tagStart, j - tagStart + 1));
						}
						else
						{
							// Escape invalid tags with double brackets
							output.Append("[[").Append(tagContent).Append("]]");
						}

						i = j + 1; // Move past the closing bracket
					}
					else
					{
						// No closing bracket found, treat as plain text but escape the opening bracket
						output.Append("[[");
						i++;
					}
				}
				else if (input[i] == '[' && i + 1 < n && input[i + 1] == '[')
				{
					// Already escaped opening bracket
					output.Append("[[");
					i += 2;
				}
				else if (input[i] == ']' && i + 1 < n && input[i + 1] == ']')
				{
					// Already escaped closing bracket
					output.Append("]]");
					i += 2;
				}
				else
				{
					// Regular character
					output.Append(input[i]);
					i++;
				}
			}

			return output.ToString();
		}

		public static List<string> ParseAnsiTags(string input, int? width, bool wrap, string? backgroundColor = null, string? foregroundColor = null)
		{
			bool FillLastLine = false;

			if (string.IsNullOrEmpty(input))
				return new List<string>();

			if (foregroundColor != null)
			{
				input = input.Replace("[/foreground]", $"[foreground {foregroundColor}]", StringComparison.InvariantCultureIgnoreCase);
			}

			if (backgroundColor != null)
			{
				input = input.Replace("[/background]", $"[background {backgroundColor}]", StringComparison.InvariantCultureIgnoreCase);
			}

			input = foregroundColor == null ? $"{input}" : $"[foreground {foregroundColor}]{input}";
			input = backgroundColor == null ? $"{input}" : $"[background {backgroundColor}]{input}";

			var output = new List<string>();
			var currentLine = new StringBuilder();
			var activeTags = new Stack<string>();
			var regex = new Regex(@"\[(.*?)\]");
			var matches = regex.Matches(input);
			int lastIndex = 0;

			int currentLineLength = 0;

			if (wrap && width == null) wrap = false;

			foreach (Match match in matches)
			{
				var textSegment = input.Substring(lastIndex, match.Index - lastIndex);
				foreach (var ch in textSegment)
				{
					if (wrap && currentLineLength >= width!.Value)
					{
						output.Add(currentLine.ToString());
						currentLine.Clear();
						currentLine.Append(string.Join("", activeTags.Reverse()));
						currentLineLength = 0;
					}
					currentLine.Append(ch);
					currentLineLength++;
				}

				var tag = match.Groups[1].Value.ToLower();
				if (tagToAnsi.TryGetValue(tag, out var ansiCode))
				{
					currentLine.Append(ansiCode);
					if (tag.StartsWith("/"))
					{
						if (activeTags.Count > 0)
						{
							activeTags.Pop();
						}
					}
					else
					{
						activeTags.Push(ansiCode);
					}
				}
				else
				{
					currentLine.Append(match.Value); // If tag is not recognized, keep it as is
				}
				lastIndex = match.Index + match.Length;
			}

			// Append any remaining text after the last match
			var remainingText = input.Substring(lastIndex);
			foreach (var ch in remainingText)
			{
				if (wrap && currentLineLength >= width!.Value)
				{
					output.Add(currentLine.ToString());
					currentLine.Clear();
					currentLine.Append(string.Join("", activeTags.Reverse()));
					//currentLineLength = activeTags.Sum(tag => tag.Length);
					currentLineLength = 0;
				}
				currentLine.Append(ch);
				currentLineLength++;
			}

			if (currentLine.Length > 0)
			{
				if (FillLastLine && width.HasValue && currentLineLength < width.Value)
				{
					currentLine.Append(new string(' ', width.Value - currentLineLength));
				}
				output.Add(currentLine.ToString());
			}

			// Reset all attributes at the end of the line
			var lastLine = output.Last();
			lastLine += "\u001b[0m";
			output.RemoveAt(output.Count - 1);
			output.Add(lastLine);

			return output;
		}

		public static string SetAnsiCursorPosition(int left, int top)
		{
			return $"\u001b[{top + 1};{left + 1}H";
		}

		public static int StripAnsiStringLength(string input)
		{
			if (string.IsNullOrEmpty(input))
				return 0;

			// Remove markup tags
			//var markupStripped = Regex.Replace(input, @"\[(.*?)\]", string.Empty);

			// Remove ANSI escape sequences
			var ansiStripped = Regex.Replace(input, @"\x1B\[[0-9;]*[a-zA-Z]", string.Empty);

			// Return the length of the cleaned string
			return ansiStripped.Length;
		}

		public static int StripSpectreLength(string text)
		{
			if (string.IsNullOrEmpty(text))
				return 0;

			List<int> lines = text.Split('\n').Select(line => Markup.Remove(line).Length).ToList();

			return lines.Max();
		}

		/// <summary>
		/// Extracts a substring from an ANSI-encoded string, preserving ANSI escape sequences
		/// </summary>
		/// <param name="input">The ANSI-encoded string</param>
		/// <param name="startIndex">The position to start extraction (refers to visible characters, not including escape sequences)</param>
		/// <param name="length">The number of visible characters to extract</param>
		/// <returns>The extracted substring with all relevant ANSI escape sequences preserved</returns>
		public static string SubstringAnsi(string input, int startIndex, int length)
		{
			if (string.IsNullOrEmpty(input) || startIndex < 0 || length <= 0)
				return string.Empty;

			var output = new StringBuilder();
			int visibleCharCount = 0;
			int visibleIndex = 0;
			int i = 0;

			// Keep track of all active ANSI sequences
			var activeSequences = new List<string>();
			var matches = TruncateAnsiRegex.Matches(input);
			int lastSequenceEnd = 0;

			// First pass: Find all escape sequences before our start position
			// and track which ones are active at our starting point
			foreach (Match match in matches)
			{
				if (match.Index > lastSequenceEnd)
				{
					// Count visible characters between sequences
					visibleIndex += match.Index - lastSequenceEnd;
					if (visibleIndex > startIndex)
						break;
				}

				// Track this sequence
				string sequence = match.Value;
				if (IsResetSequence(sequence))
				{
					// Reset clears all active sequences
					activeSequences.Clear();
				}
				else if (!IsClosingSequence(sequence))
				{
					// Add to active sequences if it's an opening sequence
					activeSequences.Add(sequence);
				}
				else
				{
					// Remove the corresponding opening sequence if possible
					RemoveMatchingSequence(activeSequences, sequence);
				}

				lastSequenceEnd = match.Index + match.Length;
			}

			// Add all active sequences to the beginning of our output
			foreach (var sequence in activeSequences)
			{
				output.Append(sequence);
			}

			// Second pass: Extract the actual substring
			visibleCharCount = 0;
			visibleIndex = 0;
			i = 0;

			while (i < input.Length && visibleCharCount < length)
			{
				// Check if we're at the start of an ANSI escape sequence
				var match = matches.Cast<Match>().FirstOrDefault(m => m.Index == i);

				if (match != null)
				{
					// Found an escape sequence - always include it
					if (visibleIndex >= startIndex)
					{
						output.Append(match.Value);
					}

					i += match.Length;
				}
				else
				{
					// Regular character
					if (visibleIndex >= startIndex && visibleCharCount < length)
					{
						output.Append(input[i]);
						visibleCharCount++;
					}

					visibleIndex++;
					i++;
				}
			}

			// Add reset sequence at the end
			output.Append("\u001b[0m");

			return output.ToString();
		}

		public static string TruncateAnsiString(string input, int maxVisibleLength)
		{
			if (string.IsNullOrEmpty(input) || maxVisibleLength <= 0)
				return string.Empty;

			return SubstringAnsi(input, 0, maxVisibleLength);
		}

		public static string TruncateSpectre(string inputStr, int maxLength)
		{
			inputStr = EscapeInvalidMarkupTags(inputStr);

			var output = new StringBuilder();
			var stack = new Stack<string>();
			int visibleLength = 0;
			int i = 0;
			int n = inputStr.Length;

			while (i < n && visibleLength < maxLength)
			{
				if (inputStr[i] == '[' && (i + 1 < n && inputStr[i + 1] != '['))
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
					// Regular character or escaped '['
					if (inputStr[i] == '[' && i + 1 < n && inputStr[i + 1] == '[')
					{
						output.Append("[[");
						i += 2;
					}
					else if (inputStr[i] == ']' && i + 1 < n && inputStr[i + 1] == ']')
					{
						output.Append("]]");
						i += 2;
					}
					else
					{
						output.Append(inputStr[i]);
						i++;
					}
					visibleLength++;
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

		private static bool IsClosingSequence(string sequence)
		{
			// Check if this is a closing/resetting sequence
			return sequence.Contains("22m") || // bold off
				   sequence.Contains("24m") || // underline off
				   sequence.Contains("39m") || // default foreground
				   sequence.Contains("49m");   // default background
		}

		/// <summary>
		/// Checks if the provided color name is a known Spectre.Console color name.
		/// </summary>
		private static bool IsKnownColorName(string colorName)
		{
			// Basic color names that are commonly supported
			var knownColors = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
			{
				"default", "black", "blue", "cyan", "dark_blue", "dark_cyan",
				"dark_green", "dark_grey", "dark_magenta", "dark_red", "dark_yellow",
				"grey", "green", "magenta", "red", "white", "yellow",
				"bright_black", "bright_blue", "bright_cyan", "bright_green",
				"bright_magenta", "bright_red", "bright_white", "bright_yellow"
			};

			return knownColors.Contains(colorName);
		}

		// Helper methods for SubstringAnsi
		private static bool IsResetSequence(string sequence)
		{
			return sequence == "\u001b[0m";
		}

		/// <summary>
		/// Determines if the provided string represents a valid color in any of the supported formats.
		/// </summary>
		/// <param name="color">The color string to validate</param>
		/// <returns>True if the color is valid, false otherwise</returns>
		private static bool IsValidColor(string color)
		{
			if (string.IsNullOrWhiteSpace(color))
				return false;

			// Check if it's a known color name
			if (IsKnownColorName(color))
				return true;

			// Check for numbered color variants like "grey46" or "orange3"
			if (Regex.IsMatch(color, @"^[a-z]+\d+$", RegexOptions.IgnoreCase))
				return true;

			// Check hex format
			if (Regex.IsMatch(color, "^#[0-9a-fA-F]{3}$") ||
				Regex.IsMatch(color, "^#[0-9a-fA-F]{6}$") ||
				Regex.IsMatch(color, "^#[0-9a-fA-F]{8}$"))
				return true;

			// Check RGB format
			if (Regex.IsMatch(color, "^rgb\\(\\s*\\d+\\s*,\\s*\\d+\\s*,\\s*\\d+\\s*\\)$"))
				return true;

			return false;
		}

		private static void RemoveMatchingSequence(List<string> sequences, string closingSequence)
		{
			// Remove the corresponding opening sequence when a closing one is found
			if (closingSequence.Contains("22m"))
			{
				sequences.RemoveAll(s => s.Contains("1m"));
			}
			else if (closingSequence.Contains("24m"))
			{
				sequences.RemoveAll(s => s.Contains("4m"));
			}
			else if (closingSequence.Contains("39m"))
			{
				sequences.RemoveAll(s => s.Contains("3") && s.EndsWith("m"));
			}
			else if (closingSequence.Contains("49m"))
			{
				sequences.RemoveAll(s => s.Contains("4") && s.EndsWith("m"));
			}
		}
	}
}