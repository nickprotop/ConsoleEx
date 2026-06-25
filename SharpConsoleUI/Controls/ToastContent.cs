// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.ComponentModel;
using System.Runtime.CompilerServices;
using SharpConsoleUI.Core;
using SharpConsoleUI.Drawing;
using SharpConsoleUI.Events;
using SharpConsoleUI.Helpers;
using SharpConsoleUI.Layout;
using SharpConsoleUI.Themes;
using Rectangle = System.Drawing.Rectangle;

namespace SharpConsoleUI.Controls
{
	/// <summary>A transient toast overlay rendered as a desktop portal. Role-themed; INPC-reactive.</summary>
	public sealed class ToastContent : PortalContentBase, IColorRoleableControl, INotifyPropertyChanged
	{
		private string _message;
		private NotificationSeverity _severity;
		private ColorRole _colorRole;
		private ThemeMode? _colorRoleMode;
		private bool _outline;
		private bool _sticky;
		private Rectangle _bounds;

		/// <summary>Initializes a new <see cref="ToastContent"/> with the given message, severity, and color role.</summary>
		/// <param name="message">The message text to display.</param>
		/// <param name="severity">The severity level supplying the icon and accent color.</param>
		/// <param name="role">The semantic color role used for theming.</param>
		public ToastContent(string message, NotificationSeverity severity, ColorRole role)
		{
			_message = message;
			_severity = severity;
			_colorRole = role;
			DismissOnOutsideClick = false;
		}

		/// <inheritdoc/>
		public event PropertyChangedEventHandler? PropertyChanged;

		/// <summary>Gets or sets the message text shown in the toast.</summary>
		public string Message { get => _message; set => SetProperty(ref _message, value); }
		/// <summary>Gets or sets the severity supplying the icon and accent color.</summary>
		public NotificationSeverity Severity { get => _severity; set => SetProperty(ref _severity, value); }
		/// <inheritdoc/>
		public ColorRole ColorRole { get => _colorRole; set => SetProperty(ref _colorRole, value); }
		/// <inheritdoc/>
		public ThemeMode? ColorRoleMode { get => _colorRoleMode; set => SetProperty(ref _colorRoleMode, value); }
		/// <inheritdoc/>
		public bool Outline { get => _outline; set => SetProperty(ref _outline, value); }
		/// <summary>Gets or sets whether the toast is sticky (does not auto-dismiss).</summary>
		public bool Sticky { get => _sticky; set => SetProperty(ref _sticky, value); }

		/// <summary>Set by ToastService from the toast's stacking slot.</summary>
		public void SetBounds(Rectangle bounds) { _bounds = bounds; Container?.Invalidate(Invalidation.Repaint); }

		/// <inheritdoc/>
		public override Rectangle GetPortalBounds() => _bounds;

		/// <inheritdoc/>
		public override bool ProcessMouseEvent(MouseEventArgs args)
		{
			if (args.HasFlag(Drivers.MouseFlags.Button1Clicked))
			{
				RaiseDismissRequested();
				return true;
			}
			return false;
		}

		/// <summary>Paints the rounded toast box with a severity-role border and a matching inner accent bar.</summary>
		protected override void PaintPortalContent(CharacterBuffer buffer, LayoutRect bounds,
			LayoutRect clipRect, Color defaultFg, Color defaultBg)
		{
			var box = BoxChars.Rounded;
			Color back = ColorResolver.ColorRoleBackground(_colorRole, Container, outline: true) ?? defaultBg;
			Color text = ColorResolver.ColorRoleTextOnBackground(_colorRole, Container, outline: true) ?? defaultFg;
			Color accent = ColorResolver.ColorRoleBorder(_colorRole, Container, outline: true) ?? text;

			buffer.FillRect(bounds, ' ', text, back);
			int x0 = bounds.X, y0 = bounds.Y, x1 = bounds.X + bounds.Width - 1, y1 = bounds.Y + bounds.Height - 1;
			buffer.SetNarrowCell(x0, y0, box.TopLeft, accent, back);
			buffer.SetNarrowCell(x1, y0, box.TopRight, accent, back);
			buffer.SetNarrowCell(x0, y1, box.BottomLeft, accent, back);
			buffer.SetNarrowCell(x1, y1, box.BottomRight, accent, back);
			for (int x = x0 + 1; x < x1; x++) { buffer.SetNarrowCell(x, y0, box.Horizontal, accent, back); buffer.SetNarrowCell(x, y1, box.Horizontal, accent, back); }
			for (int y = y0 + 1; y < y1; y++) { buffer.SetNarrowCell(x0, y, box.Vertical, accent, back); buffer.SetNarrowCell(x1, y, box.Vertical, accent, back); }

			for (int y = y0 + 1; y < y1; y++)
				buffer.SetNarrowCell(x0 + 1, y, '▌', accent, back);

			string line = string.IsNullOrEmpty(_severity.Icon) ? _message : $"{_severity.Icon}  {_message}";
			int contentX = x0 + 3;
			buffer.WriteStringClipped(contentX, y0 + 1, line, text, back,
				new LayoutRect(contentX, y0 + 1, Math.Max(0, x1 - contentX), 1));
		}

		private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
		{
			if (EqualityComparer<T>.Default.Equals(field, value)) return false;
			field = value;
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
			Container?.Invalidate(Invalidation.Repaint);
			return true;
		}
	}
}
