// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace ConsoleEx
{
    public class MarkupContent : IWIndowContent
    {
        private List<string> _content;
        private List<string> _renderedContent;
        private int? _width;
        private bool _wrap = true;

        public int? Width
        { get => _width; set { _width = value; Container?.Invalidate(); } }

        public bool Wrap
        { get => _wrap; set { _wrap = value; Container?.Invalidate(); } }

        public IContainer? Container { get; set; }

        public void SetMarkup(List<string> lines)
        {
            _content = lines;
            Container?.Invalidate();
        }

        public string Guid { get; } = new Guid().ToString();

        public MarkupContent(List<string> lines)
        {
            _content = lines;
            _renderedContent = new List<string>();
        }

        public List<string> RenderContent(int? width, int? height)
        {
            _renderedContent.Clear();

            foreach (var line in _content)
            {
                var ansiLines = AnsiConsoleExtensions.ConvertSpectreMarkupToAnsi(line, _width ?? (width ?? 50), height, _wrap, Container?.BackgroundColor, Container?.ForegroundColor);
                _renderedContent.AddRange(ansiLines);
            }

            return _renderedContent;
        }

        public void Dispose()
        {
            Container = null;
        }
    }
}