// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Text.RegularExpressions;

namespace SharpConsoleUI.Helpers
{
    /// <summary>
    /// Writes ANSI-colored exception output to a TextWriter, replacing
    /// Spectre.Console's AnsiConsole.WriteException with zero external dependencies.
    /// Intended for crash-time reporting after Console.Clear().
    /// </summary>
    public static partial class ExceptionFormatter
    {
        private const string AnsiReset = "\x1b[0m";
        private const string AnsiGrey = "\x1b[38;2;238;238;238m";
        private const string AnsiWhite = "\x1b[97m";
        private const string AnsiRed = "\x1b[91m";
        private const string AnsiCornsilk = "\x1b[38;2;255;255;215m";

        // Matches: "   at Namespace.Class.Method(ParamType param) in /path/File.cs:line 42"
        [GeneratedRegex(@"^\s+at\s+(.+?)(\(.*?\))(\s+in\s+(.+?):line\s+(\d+))?\s*$")]
        private static partial Regex StackFrameRegex();

        // Matches parameter list contents: "String param, Int32 count"
        [GeneratedRegex(@"(\w+(?:\[\])?(?:<[^>]+>)?)\s+(\w+)")]
        private static partial Regex ParameterRegex();

        /// <summary>
        /// Writes a colored exception report to the specified output, or Console.Out if none given.
        /// </summary>
        public static void WriteException(Exception ex, TextWriter? output = null)
        {
            ArgumentNullException.ThrowIfNull(ex);

            var writer = output ?? Console.Out;
            WriteExceptionChain(writer, ex, isInner: false);
            writer.Write(AnsiReset);
            writer.Flush();
        }

        private static void WriteExceptionChain(TextWriter writer, Exception ex, bool isInner)
        {
            if (isInner)
            {
                writer.WriteLine();
                writer.WriteLine($"{AnsiGrey}--- Inner Exception ---{AnsiReset}");
            }

            // Type name and message
            writer.Write($"{AnsiGrey}{ex.GetType().FullName}:{AnsiReset} ");
            writer.WriteLine($"{AnsiWhite}{ex.Message}{AnsiReset}");

            // Stack trace
            if (!string.IsNullOrEmpty(ex.StackTrace))
            {
                WriteStackTrace(writer, ex.StackTrace);
            }

            // Inner exceptions
            if (ex is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions)
                {
                    WriteExceptionChain(writer, inner, isInner: true);
                }
            }
            else if (ex.InnerException != null)
            {
                WriteExceptionChain(writer, ex.InnerException, isInner: true);
            }
        }

        private static void WriteStackTrace(TextWriter writer, string stackTrace)
        {
            var regex = StackFrameRegex();
            var paramRegex = ParameterRegex();

            foreach (var line in stackTrace.Split('\n'))
            {
                var trimmed = line.TrimEnd('\r');
                var match = regex.Match(trimmed);

                if (!match.Success)
                {
                    // Unrecognized frame — emit as-is in grey
                    if (!string.IsNullOrWhiteSpace(trimmed))
                    {
                        writer.WriteLine($"{AnsiGrey}{trimmed}{AnsiReset}");
                    }
                    continue;
                }

                var method = match.Groups[1].Value;
                var paramList = match.Groups[2].Value;
                var filePath = match.Groups[4].Value;
                var lineNumber = match.Groups[5].Value;

                writer.Write($"   {AnsiGrey}at{AnsiReset} ");
                writer.Write($"{AnsiRed}{method}{AnsiReset}");

                // Parse parameters: (Type name, Type name)
                WriteParameters(writer, paramList, paramRegex);

                // File path and line number
                if (!string.IsNullOrEmpty(filePath))
                {
                    writer.Write($" {AnsiCornsilk}in{AnsiReset} ");
                    writer.Write($"{AnsiRed}{filePath}{AnsiReset}");
                    writer.Write($"{AnsiCornsilk}:line{AnsiReset} ");
                    writer.Write($"{AnsiCornsilk}{lineNumber}{AnsiReset}");
                }

                writer.WriteLine();
            }
        }

        private static void WriteParameters(TextWriter writer, string paramList, Regex paramRegex)
        {
            // Strip outer parentheses
            var inner = paramList.Length >= 2
                ? paramList[1..^1]
                : string.Empty;

            writer.Write($"{AnsiCornsilk}({AnsiReset}");

            if (!string.IsNullOrWhiteSpace(inner))
            {
                var parameters = inner.Split(',');
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (i > 0)
                    {
                        writer.Write($"{AnsiCornsilk},{AnsiReset} ");
                    }

                    var paramMatch = paramRegex.Match(parameters[i].Trim());
                    if (paramMatch.Success)
                    {
                        writer.Write($"{AnsiRed}{paramMatch.Groups[1].Value}{AnsiReset} ");
                        writer.Write($"{AnsiCornsilk}{paramMatch.Groups[2].Value}{AnsiReset}");
                    }
                    else
                    {
                        // Type-only parameter (e.g. "String[]") or unrecognized
                        writer.Write($"{AnsiRed}{parameters[i].Trim()}{AnsiReset}");
                    }
                }
            }

            writer.Write($"{AnsiCornsilk}){AnsiReset}");
        }
    }
}
