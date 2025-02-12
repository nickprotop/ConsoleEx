// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace ConsoleEx
{
    public interface IInteractiveContent
    {
        public bool IsInteractive { get; }
        public bool ProcessKey(ConsoleKeyInfo key);
    }
}
