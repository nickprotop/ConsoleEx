// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace ConsoleEx
{
    public class PromptContent : IWIndowContent
    {
        private string _prompt;
        private string _input = string.Empty;
        private Action<PromptContent, string> _onEnter;

        public bool IsInteractive { get; private set; } = true;

        public Window? Container { get; set; }

        public string Guid { get; } = System.Guid.NewGuid().ToString();

        public PromptContent(string prompt, Action<PromptContent, string> onEnter)
        {
            _prompt = prompt;
            _onEnter = onEnter;
        }

        public List<string> RenderContent(int? width, int? height)
        {
            return RenderContent(width, height, true);
        }

        public List<string> RenderContent(int? width, int? height, bool overflow)
        {
            var content = AnsiConsoleExtensions.ConvertMarkupToAnsi(_prompt + _input, width, height, true);
            return content;
        }

        public bool ProcessKey(ConsoleKeyInfo key)
        {
            if (key.Key == ConsoleKey.Enter)
            {
                _onEnter(this, _input);
                _input = string.Empty;
				IsInteractive = false;

				Container?.Invalidate();
                return true;
            }
            else if (key.Key == ConsoleKey.Backspace && _input.Length > 0)
            {
                _input = _input.Substring(0, _input.Length - 1);
                Container?.Invalidate();
                return true;
            }
            else if (!char.IsControl(key.KeyChar))
            {
                _input += key.KeyChar;
                Container?.Invalidate();
                return true;
            }
            return false;
        }
    }
}
