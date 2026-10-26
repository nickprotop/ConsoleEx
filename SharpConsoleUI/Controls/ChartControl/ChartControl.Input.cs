// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System;
using SharpConsoleUI.Events;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// The input surface of the chart.
	/// </summary>
	/// <remarks>
	/// The members live here from the first slice because the interface list is part
	/// of the class declaration; the behaviour behind them arrives with hover,
	/// selection and zoom. Until then every handler declines the event rather than
	/// consuming it - a control that sets Handled on input it ignored breaks window
	/// dragging for whatever is underneath it.
	/// </remarks>
	public partial class ChartControl
	{
		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseClick;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseDoubleClick;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseRightClick;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseEnter;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseLeave;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseMove;

		/// <inheritdoc/>
		public bool WantsMouseEvents => _enableHover || _enableSelection || _enableZoom;

		/// <inheritdoc/>
		public bool CanFocusWithMouse => _enableSelection;

		/// <inheritdoc/>
		public bool CanReceiveFocus => _enabled && _enableSelection;

		/// <inheritdoc/>
		public bool HasFocus => ComputeHasFocus();

		/// <summary>Raises the mouse events this control exposes.</summary>
		/// <remarks>
		/// Present so the event fields are not written-but-never-read while the
		/// behaviour is still being built; each gains a caller in its own slice.
		/// </remarks>
		private void RaiseMouse(EventHandler<MouseEventArgs>? handler, MouseEventArgs args) =>
			handler?.Invoke(this, args);
	}
}
