// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace ConsoleEx
{
    public interface IWIndowContent : IDisposable
    {
        public IContainer? Container { get; set; }
        public int? Width { get; set; }

        public List<string> RenderContent(int? width, int? height);
    }
}