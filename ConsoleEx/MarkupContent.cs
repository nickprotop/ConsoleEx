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
        private bool _overflow;

        public Window? Container { get; set; }

        public void SetMarkup(List<string> lines)
        {
            _content = lines;
            Container?.Invalidate();
        }

        public string Guid { get; } = new Guid().ToString();

        public MarkupContent(List<string> lines, bool overflow, out string guid)
        {
            _content = lines;
            _renderedContent = new List<string>();
            _overflow = overflow;

            guid = Guid;
        }

        public MarkupContent(List<string> lines, bool overflow)
        {
            _content = lines;
            _renderedContent = new List<string>();
            _overflow = overflow;
        }

        public MarkupContent(string line, bool overflow, out string guid)
        {
            _content = new List<string>() { line };
            _renderedContent = new List<string>();
            _overflow = overflow;

            guid = Guid;
        }

        public MarkupContent(string line, bool overflow)
        {
            _content = new List<string>() { line };
            _renderedContent = new List<string>();
            _overflow = overflow;
        }

        public List<string> RenderContent(int? width, int? height, bool overflow)
        {
            _renderedContent.Clear();

            foreach (var line in _content)
            {
                var ansiLines = AnsiConsoleExtensions.ConvertSpectreMarkupToAnsi(line, width, height, overflow, Container?.BackgroundColor, Container?.ForegroundColor);
                //var ansiLines = AnsiConsoleExtensions.ParseAnsiTags(line, width, overflow, Container?.BackgroundColor, Container?.ForegroundColor);
                _renderedContent.AddRange(ansiLines);
            }

            return _renderedContent;
        }

        public List<string> RenderContent(int? width, int? height)
        {
            return RenderContent(width, height, _overflow);
        }

        public void Dispose()
        {
            Container = null;
        }
    }
}