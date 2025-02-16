using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleEx
{
    public class ButtonContent : IWIndowContent, IInteractiveContent
    {
        private int? _width;
        private string _text = "Button";
        private string? _cachedContent;
        private bool _focused;
        private bool _enabled = true;
        public string Guid => throw new NotImplementedException();

        public Action<ButtonContent>? OnClick { get; set; }

        public IContainer? Container { get; set; }

        public int? Width
        { get => _width; set { _width = value; Container?.Invalidate(); } }

        public bool IsEnabled
        {
            get => _enabled;
            set
            {
                _cachedContent = null;
                _enabled = value;
                Container?.Invalidate();
            }
        }

        public bool HasFocus
        {
            get => _focused;
            set
            {
                _cachedContent = null;
                _focused = value;
            }
        }

        public string Text
        {
            get => _text;
            set
            {
                _text = value;
                _cachedContent = null;
                Container?.Invalidate();
            }
        }

        public void Dispose()
        {
            Container = null;
        }

        public (int Left, int Top)? GetCursorPosition()
        {
            return null;
        }

        public bool ProcessKey(ConsoleKeyInfo key)
        {
            if (key.Key == ConsoleKey.Enter)
            {
                OnClick?.Invoke(this);
                return true;
            }

            return false;
        }

        public List<string> RenderContent(int? width, int? height, bool overflow)
        {
            if (_cachedContent != null)
            {
                return new List<string>() { _cachedContent };
            }

            int buttonWidth = width ?? _width ?? 10; // Default width if not specified
            string text = $"{(_focused ? ">" : "")}{_text}{(_focused ? "<" : "")}";
            int padding = (buttonWidth - text.Length - 2) / 2; // Calculate padding for centering text

            if (padding < 0) padding = 0; // Ensure padding is not negative

            string buttonText = $"{new string(' ', padding)}{text}{new string(' ', padding)}";

            // Adjust if the total length is less than the button width
            if (buttonText.Length < buttonWidth)
            {
                buttonText = buttonText.PadRight(buttonWidth - 1);
            }

            List<string> renderedAnsi = AnsiConsoleExtensions.ConvertSpectreMarkupToAnsi("[[" + buttonText + "]]", buttonWidth, height, false, Container?.BackgroundColor, Container?.ForegroundColor);
            _cachedContent = renderedAnsi.First();
            return renderedAnsi;
        }
    }
}