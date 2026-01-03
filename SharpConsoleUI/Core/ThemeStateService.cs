// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.Concurrent;
using SharpConsoleUI.Themes;
using Color = Spectre.Console.Color;

namespace SharpConsoleUI.Core
{
	/// <summary>
	/// Immutable record representing a color set for a window.
	/// </summary>
	public record WindowColorSet
	{
		/// <summary>
		/// Gets the background color for the window.
		/// </summary>
		public Color BackgroundColor { get; init; }

		/// <summary>
		/// Gets the foreground (text) color for the window.
		/// </summary>
		public Color ForegroundColor { get; init; }

		/// <summary>
		/// Gets the border foreground color for the window.
		/// </summary>
		public Color BorderForegroundColor { get; init; }

		/// <summary>
		/// Gets the title foreground color for the window.
		/// </summary>
		public Color TitleForegroundColor { get; init; }

		/// <summary>
		/// Gets a value indicating whether the window is active.
		/// </summary>
		public bool IsActive { get; init; }

		/// <summary>
		/// Creates a <see cref="WindowColorSet"/> from a theme based on window state.
		/// </summary>
		/// <param name="theme">The theme to derive colors from.</param>
		/// <param name="isActive">Whether the window is currently active.</param>
		/// <param name="isModal">Whether the window is a modal dialog.</param>
		/// <returns>A new <see cref="WindowColorSet"/> with the appropriate colors.</returns>
		public static WindowColorSet FromTheme(Theme theme, bool isActive, bool isModal)
		{
			if (isModal)
			{
				return new WindowColorSet
				{
					BackgroundColor = theme.ModalBackgroundColor,
					ForegroundColor = theme.WindowForegroundColor,
					BorderForegroundColor = theme.ModalBorderForegroundColor,
					TitleForegroundColor = theme.ModalTitleForegroundColor,
					IsActive = isActive
				};
			}

			return new WindowColorSet
			{
				BackgroundColor = theme.WindowBackgroundColor,
				ForegroundColor = theme.WindowForegroundColor,
				BorderForegroundColor = isActive ? theme.ActiveBorderForegroundColor : theme.InactiveBorderForegroundColor,
				TitleForegroundColor = isActive ? theme.ActiveTitleForegroundColor : theme.InactiveTitleForegroundColor,
				IsActive = isActive
			};
		}
	}

	/// <summary>
	/// Immutable record representing a color set for a button control.
	/// </summary>
	public record ButtonColorSet
	{
		/// <summary>
		/// Gets the background color for the button.
		/// </summary>
		public Color BackgroundColor { get; init; }

		/// <summary>
		/// Gets the foreground (text) color for the button.
		/// </summary>
		public Color ForegroundColor { get; init; }

		/// <summary>
		/// Gets a value indicating whether the button is focused.
		/// </summary>
		public bool IsFocused { get; init; }

		/// <summary>
		/// Gets a value indicating whether the button is enabled.
		/// </summary>
		public bool IsEnabled { get; init; }

		/// <summary>
		/// Creates a <see cref="ButtonColorSet"/> from a theme based on button state.
		/// </summary>
		/// <param name="theme">The theme to derive colors from.</param>
		/// <param name="isFocused">Whether the button is currently focused.</param>
		/// <param name="isEnabled">Whether the button is enabled.</param>
		/// <returns>A new <see cref="ButtonColorSet"/> with the appropriate colors.</returns>
		public static ButtonColorSet FromTheme(Theme theme, bool isFocused, bool isEnabled)
		{
			if (!isEnabled)
			{
				return new ButtonColorSet
				{
					BackgroundColor = theme.ButtonDisabledBackgroundColor,
					ForegroundColor = theme.ButtonDisabledForegroundColor,
					IsFocused = false,
					IsEnabled = false
				};
			}

			if (isFocused)
			{
				return new ButtonColorSet
				{
					BackgroundColor = theme.ButtonFocusedBackgroundColor,
					ForegroundColor = theme.ButtonFocusedForegroundColor,
					IsFocused = true,
					IsEnabled = true
				};
			}

			return new ButtonColorSet
			{
				BackgroundColor = theme.ButtonBackgroundColor,
				ForegroundColor = theme.ButtonForegroundColor,
				IsFocused = false,
				IsEnabled = true
			};
		}
	}

	/// <summary>
	/// Event arguments for theme changes
	/// </summary>
	public class ThemeChangedEventArgs : EventArgs
	{
		/// <summary>
		/// The previous theme
		/// </summary>
		public Theme? PreviousTheme { get; }

		/// <summary>
		/// The new theme
		/// </summary>
		public Theme NewTheme { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="ThemeChangedEventArgs"/> class.
		/// </summary>
		/// <param name="previousTheme">The previous theme, or null if this is the initial theme.</param>
		/// <param name="newTheme">The new theme that was applied.</param>
		public ThemeChangedEventArgs(Theme? previousTheme, Theme newTheme)
		{
			PreviousTheme = previousTheme;
			NewTheme = newTheme;
		}
	}

	/// <summary>
	/// Centralized service for managing theme state.
	/// Provides change notifications when theme is updated.
	/// </summary>
	public class ThemeStateService : IDisposable
	{
		private readonly object _lock = new();
		private Theme _currentTheme;
		private readonly ConcurrentQueue<Theme> _themeHistory = new();
		private const int MaxHistorySize = 10;
		private bool _isDisposed;

		/// <summary>
		/// Creates a new theme state service with the default theme
		/// </summary>
		public ThemeStateService() : this(new Theme())
		{
		}

		/// <summary>
		/// Creates a new theme state service with the specified theme
		/// </summary>
		public ThemeStateService(Theme initialTheme)
		{
			_currentTheme = initialTheme ?? throw new ArgumentNullException(nameof(initialTheme));
		}

		#region Properties

		/// <summary>
		/// Gets the current theme
		/// </summary>
		public Theme CurrentTheme
		{
			get
			{
				lock (_lock)
				{
					return _currentTheme;
				}
			}
		}

		#endregion

		#region Events

		/// <summary>
		/// Event fired when the theme changes
		/// </summary>
		public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

		/// <summary>
		/// Event fired when any theme property changes (for partial updates)
		/// </summary>
		public event EventHandler? ThemePropertyChanged;

		#endregion

		#region Theme Management

		/// <summary>
		/// Sets a new theme
		/// </summary>
		public void SetTheme(Theme newTheme)
		{
			if (newTheme == null)
				throw new ArgumentNullException(nameof(newTheme));

			lock (_lock)
			{
				var previousTheme = _currentTheme;

				if (previousTheme == newTheme)
					return;

				_currentTheme = newTheme;

				// Add to history
				_themeHistory.Enqueue(previousTheme);
				while (_themeHistory.Count > MaxHistorySize)
				{
					_themeHistory.TryDequeue(out _);
				}

				// Fire events
				FireThemeChanged(previousTheme, newTheme);
			}
		}

		/// <summary>
		/// Notifies that a theme property has changed.
		/// Call this after modifying individual theme properties.
		/// </summary>
		public void NotifyPropertyChanged()
		{
			ThreadPool.QueueUserWorkItem(_ =>
			{
				try
				{
					ThemePropertyChanged?.Invoke(this, EventArgs.Empty);
				}
				catch
				{
					// Swallow exceptions from event handlers
				}
			});
		}

		#endregion

		#region Color Resolution

		/// <summary>
		/// Gets the color set for a window based on its state
		/// </summary>
		public WindowColorSet GetWindowColors(bool isActive, bool isModal)
		{
			return WindowColorSet.FromTheme(CurrentTheme, isActive, isModal);
		}

		/// <summary>
		/// Gets the color set for a button control based on its state
		/// </summary>
		public ButtonColorSet GetButtonColors(bool isFocused, bool isEnabled)
		{
			return ButtonColorSet.FromTheme(CurrentTheme, isFocused, isEnabled);
		}

		/// <summary>
		/// Gets desktop colors
		/// </summary>
		public (Color Background, Color Foreground, char BackgroundChar) GetDesktopColors()
		{
			var theme = CurrentTheme;
			return (theme.DesktopBackgroundColor, theme.DesktopForegroundColor, theme.DesktopBackroundChar);
		}

		/// <summary>
		/// Gets status bar colors
		/// </summary>
		public (Color TopBarBackground, Color TopBarForeground, Color BottomBarBackground, Color BottomBarForeground) GetStatusBarColors()
		{
			var theme = CurrentTheme;
			return (theme.TopBarBackgroundColor, theme.TopBarForegroundColor, theme.BottomBarBackgroundColor, theme.BottomBarForegroundColor);
		}

		/// <summary>
		/// Gets input control colors
		/// </summary>
		public (Color Background, Color Foreground, Color FocusedBackground, Color FocusedForeground) GetInputColors()
		{
			var theme = CurrentTheme;
			return (theme.PromptInputBackgroundColor, theme.PromptInputForegroundColor,
			        theme.PromptInputFocusedBackgroundColor, theme.PromptInputFocusedForegroundColor);
		}

		/// <summary>
		/// Gets notification window colors
		/// </summary>
		public (Color Default, Color Info, Color Success, Color Warning, Color Danger) GetNotificationColors()
		{
			var theme = CurrentTheme;
			return (theme.NotificationWindowBackgroundColor, theme.NotificationInfoWindowBackgroundColor,
			        theme.NotificationSuccessWindowBackgroundColor, theme.NotificationWarningWindowBackgroundColor,
			        theme.NotificationDangerWindowBackgroundColor);
		}

		#endregion

		#region Debugging

		/// <summary>
		/// Gets recent theme history for debugging
		/// </summary>
		public IReadOnlyList<Theme> GetHistory()
		{
			return _themeHistory.ToArray();
		}

		/// <summary>
		/// Gets a debug string representation of current theme
		/// </summary>
		public string GetDebugInfo()
		{
			var theme = CurrentTheme;
			return $"Theme: Name={theme.Name}, " +
			       $"ActiveBorder={theme.ActiveBorderForegroundColor}, " +
			       $"WindowBg={theme.WindowBackgroundColor}";
		}

		#endregion

		#region Private Helpers

		private void FireThemeChanged(Theme previousTheme, Theme newTheme)
		{
			var args = new ThemeChangedEventArgs(previousTheme, newTheme);
			ThreadPool.QueueUserWorkItem(_ =>
			{
				try
				{
					ThemeChanged?.Invoke(this, args);
				}
				catch
				{
					// Swallow exceptions from event handlers
				}
			});
		}

		#endregion

		#region IDisposable

		/// <inheritdoc/>
		public void Dispose()
		{
			if (_isDisposed)
				return;

			_isDisposed = true;

			// Clear event handlers
			ThemeChanged = null;
			ThemePropertyChanged = null;
		}

		#endregion
	}
}
