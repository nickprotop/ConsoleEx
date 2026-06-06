// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;

namespace SharpConsoleUI.DataBinding;

/// <summary>
/// Marker interface for builders that support deferred data binding.
/// Implement on any builder whose <c>Build()</c> returns a <typeparamref name="TControl"/>.
/// </summary>
/// <typeparam name="TControl">The control type produced by this builder.</typeparam>
public interface IControlBuilder<out TControl> where TControl : BaseControl { }
