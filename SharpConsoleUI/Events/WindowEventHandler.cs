// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

namespace SharpConsoleUI.Events;

/// <summary>
/// Represents a method that handles an event with window context access.
/// This delegate provides access to the parent window, enabling event handlers
/// to interact with other controls via FindControl&lt;T&gt;().
/// </summary>
/// <typeparam name="TEventArgs">The type of the event data (e.g., ListItem, int, bool, string).</typeparam>
/// <param name="sender">The source of the event (typically the control that raised the event).</param>
/// <param name="e">The event data containing information about the event.</param>
/// <param name="window">The parent window containing the sender control.</param>
public delegate void WindowEventHandler<in TEventArgs>(object? sender, TEventArgs e, Window window);
