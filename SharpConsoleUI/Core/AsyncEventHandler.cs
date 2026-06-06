// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

// SharpConsoleUI/Core/AsyncEventHandler.cs
namespace SharpConsoleUI.Core;

/// <summary>Async counterpart of <see cref="EventHandler{TArgs}"/> for user-facing notification events.</summary>
public delegate Task AsyncEventHandler<TArgs>(object? sender, TArgs args);
