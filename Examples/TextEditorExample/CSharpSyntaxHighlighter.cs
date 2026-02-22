using System.Text.RegularExpressions;
using SharpConsoleUI.Controls;
using Spectre.Console;

namespace TextEditorExample;

/// <summary>
/// Simple regex-based C# syntax highlighter for the MultilineEditControl.
/// Tokenizes keywords, type keywords, strings, comments, and numbers.
/// </summary>
public class CSharpSyntaxHighlighter : ISyntaxHighlighter
{
    private static readonly Color KeywordColor = Color.DodgerBlue2;
    private static readonly Color TypeKeywordColor = Color.MediumTurquoise;
    private static readonly Color StringColor = Color.Orange3;
    private static readonly Color CommentColor = Color.Green;
    private static readonly Color NumberColor = Color.Cyan1;

    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "abstract", "as", "base", "break", "case", "catch", "checked", "class",
        "const", "continue", "default", "delegate", "do", "else", "enum", "event",
        "explicit", "extern", "false", "finally", "fixed", "for", "foreach", "goto",
        "if", "implicit", "in", "interface", "internal", "is", "lock", "namespace",
        "new", "null", "operator", "out", "override", "params", "private", "protected",
        "public", "readonly", "ref", "return", "sealed", "sizeof", "stackalloc",
        "static", "struct", "switch", "this", "throw", "true", "try", "typeof",
        "unchecked", "unsafe", "using", "virtual", "volatile", "while", "async",
        "await", "yield", "where", "get", "set", "init", "record", "required"
    };

    private static readonly HashSet<string> TypeKeywords = new(StringComparer.Ordinal)
    {
        "bool", "byte", "char", "decimal", "double", "float", "int", "long",
        "object", "sbyte", "short", "string", "uint", "ulong", "ushort", "void",
        "var", "dynamic", "nint", "nuint"
    };

    private static readonly Regex TokenPattern = new(
        @"//.*$" +                           // Line comment
        @"|""(?:[^""\\]|\\.)*""" +           // String literal
        @"|@""(?:[^""]|"""")*""" +           // Verbatim string
        @"|\b\d+(?:\.\d+)?(?:[fFdDmM])?\b" + // Numbers
        @"|\b[a-zA-Z_]\w*\b",               // Identifiers/keywords
        RegexOptions.Compiled | RegexOptions.Multiline);

    public (IReadOnlyList<SyntaxToken> Tokens, SyntaxLineState EndState)
        Tokenize(string line, int lineIndex, SyntaxLineState startState)
    {
        return (TokenizeInternal(line, lineIndex), SyntaxLineState.Initial);
    }

    private IReadOnlyList<SyntaxToken> TokenizeInternal(string line, int lineIndex)
    {
        var tokens = new List<SyntaxToken>();
        int commentStart = -1;

        // First pass: find comment start position
        var commentMatch = Regex.Match(line, @"//");
        if (commentMatch.Success)
        {
            // Verify it's not inside a string
            bool inString = false;
            for (int i = 0; i < commentMatch.Index; i++)
            {
                if (line[i] == '"' && (i == 0 || line[i - 1] != '\\'))
                    inString = !inString;
            }
            if (!inString)
                commentStart = commentMatch.Index;
        }

        // If the entire line is a comment, return single token
        if (commentStart == 0)
        {
            tokens.Add(new SyntaxToken(0, line.Length, CommentColor));
            return tokens;
        }

        // Add comment token if present
        if (commentStart > 0)
        {
            tokens.Add(new SyntaxToken(commentStart, line.Length - commentStart, CommentColor));
        }

        var matches = TokenPattern.Matches(line);
        foreach (Match match in matches)
        {
            // Skip tokens that overlap with comment region
            if (commentStart >= 0 && match.Index >= commentStart)
                continue;

            var text = match.Value;

            if (text.StartsWith("//"))
            {
                // Already handled above
                continue;
            }
            else if (text.StartsWith("\"") || text.StartsWith("@\""))
            {
                tokens.Add(new SyntaxToken(match.Index, match.Length, StringColor));
            }
            else if (char.IsDigit(text[0]))
            {
                tokens.Add(new SyntaxToken(match.Index, match.Length, NumberColor));
            }
            else if (TypeKeywords.Contains(text))
            {
                tokens.Add(new SyntaxToken(match.Index, match.Length, TypeKeywordColor));
            }
            else if (Keywords.Contains(text))
            {
                tokens.Add(new SyntaxToken(match.Index, match.Length, KeywordColor));
            }
        }

        return tokens;
    }
}
