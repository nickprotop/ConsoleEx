// SharpConsoleUI/Core/AsyncEventHandler.cs
namespace SharpConsoleUI.Core;

/// <summary>Async counterpart of <see cref="EventHandler{TArgs}"/> for user-facing notification events.</summary>
public delegate Task AsyncEventHandler<TArgs>(object? sender, TArgs args);
