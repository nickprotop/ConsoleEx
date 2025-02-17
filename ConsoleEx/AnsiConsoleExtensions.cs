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
        public static int RemoveSpectreMarkupLength(string text)
        {
            return Markup.Remove(text).Length;
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

        public static List<string> ConvertSpectreMarkupToAnsi(string markup, int? width, int? height, bool overflow, Color? backgroundColor, Color? foregroundColor)
        {
            if (string.IsNullOrEmpty(markup))
                return new List<string>();

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
                    var renderedMarkup = new Markup(overflow ? markup : markup, new Style(background: backgroundColor));
                    console.Write(renderedMarkup);
                }
                else
                {
                    var renderedMarkup = new Markup(overflow ? markup : markup);
                    console.Write(renderedMarkup);
                }
            }
            else
            {
                if (backgroundColor != null)
                {
                    var renderedMarkup = new Markup(overflow ? markup : markup, new Style(background: backgroundColor, foreground: foregroundColor));
                    console.Write(renderedMarkup);
                }
                else
                {
                    var renderedMarkup = new Markup(overflow ? markup : markup, new Style(foreground: foregroundColor));
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

        public static List<string> ConvertSpectreRenderableToAnsi(IRenderable renderable, int? width, int? height, bool overflow)
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

        public static string SetAnsiCursorPosition(int left, int top)
        {
            return $"\u001b[{top + 1};{left + 1}H";
        }
    }
}