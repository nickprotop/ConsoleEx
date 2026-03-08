// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;

#pragma warning disable CS1591

namespace SharpConsoleUI.NerdFont
{
    /// <summary>
    /// Provides NerdFont detection and ASCII fallback support.
    /// </summary>
    public static class NerdFontHelper
    {
        private static bool? _isSupported;

        private static readonly string[] NerdFontTerminals =
        {
            "WezTerm", "Alacritty", "iTerm.app", "kitty", "Hyper",
            "Tabby", "Warp", "rio", "foot", "contour"
        };

        /// <summary>
        /// Gets or sets whether NerdFont icons are supported by the terminal.
        /// Auto-detected from TERM_PROGRAM and NERD_FONTS environment variables.
        /// Can be explicitly overridden.
        /// </summary>
        public static bool IsSupported
        {
            get => _isSupported ??= DetectNerdFontSupport();
            set => _isSupported = value;
        }

        /// <summary>
        /// Returns the icon if NerdFonts are supported, otherwise the ASCII fallback.
        /// </summary>
        public static string Icon(string nerdFontIcon, string asciiFallback)
        {
            return IsSupported ? nerdFontIcon : asciiFallback;
        }

        /// <summary>
        /// Common ASCII fallbacks for frequently used icons.
        /// </summary>
        public static class Fallbacks
        {
            public static string Folder => Icon(Icons.FontAwesome.Folder, "[D]");
            public static string FolderOpen => Icon(Icons.FontAwesome.FolderOpen, "[D]");
            public static string File => Icon(Icons.FontAwesome.File, "[F]");
            public static string FileCode => Icon(Icons.FontAwesome.FileCode, "[C]");
            public static string Check => Icon(Icons.FontAwesome.Check, "[x]");
            public static string Times => Icon(Icons.FontAwesome.Times, "[X]");
            public static string ArrowRight => Icon(Icons.FontAwesome.ArrowRight, ">");
            public static string ArrowLeft => Icon(Icons.FontAwesome.ArrowLeft, "<");
            public static string ArrowUp => Icon(Icons.FontAwesome.ArrowUp, "^");
            public static string ArrowDown => Icon(Icons.FontAwesome.ArrowDown, "v");
            public static string Star => Icon(Icons.FontAwesome.Star, "*");
            public static string Warning => Icon(Icons.FontAwesome.Warning, "!");
            public static string Info => Icon(Icons.FontAwesome.Info, "i");
            public static string Search => Icon(Icons.FontAwesome.Search, "?");
            public static string GitBranch => Icon(Icons.Octicons.GitBranch, "B:");
            public static string GitCommit => Icon(Icons.Octicons.GitCommit, "C:");
            public static string GitMerge => Icon(Icons.Octicons.GitMerge, "M:");
            public static string GitPullRequest => Icon(Icons.Octicons.GitPullRequest, "PR:");
            public static string Lock => Icon(Icons.FontAwesome.Lock, "[L]");
            public static string Unlock => Icon(Icons.FontAwesome.Unlock, "[U]");
            public static string Home => Icon(Icons.FontAwesome.Home, "~");
            public static string Gear => Icon(Icons.FontAwesome.Gear, "@");
            public static string Terminal => Icon(Icons.FontAwesome.Terminal, "$");
            public static string Database => Icon(Icons.FontAwesome.Database, "DB");
            public static string Cloud => Icon(Icons.FontAwesome.Cloud, "C:");
            public static string Save => Icon(Icons.FontAwesome.Save, "S:");
            public static string Trash => Icon(Icons.FontAwesome.Trash, "D:");
            public static string Edit => Icon(Icons.FontAwesome.Edit, "E:");
            public static string Copy => Icon(Icons.FontAwesome.Copy, "Cp");
            public static string Plus => Icon(Icons.FontAwesome.Plus, "+");
            public static string Minus => Icon(Icons.FontAwesome.Minus, "-");
            public static string Play => Icon(Icons.FontAwesome.Play, "|>");
            public static string Pause => Icon(Icons.FontAwesome.Pause, "||");
            public static string Stop => Icon(Icons.FontAwesome.Stop, "[]");
            public static string Bug => Icon(Icons.FontAwesome.Bug, "BG");
            public static string Shield => Icon(Icons.FontAwesome.Shield, "SH");
            public static string Rocket => Icon(Icons.FontAwesome.Rocket, "=>");
            public static string PowerlineRight => Icon(Icons.Powerline.RightArrow, ">");
            public static string PowerlineLeft => Icon(Icons.Powerline.LeftArrow, "<");
            public static string PowerlineBranch => Icon(Icons.Powerline.Branch, "B:");
        }

        private static bool DetectNerdFontSupport()
        {
            var nfEnv = Environment.GetEnvironmentVariable("NERD_FONTS");
            if (nfEnv != null)
                return nfEnv == "1" || nfEnv.Equals("true", StringComparison.OrdinalIgnoreCase);

            var termProgram = Environment.GetEnvironmentVariable("TERM_PROGRAM") ?? "";
            foreach (var terminal in NerdFontTerminals)
            {
                if (termProgram.Contains(terminal, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
