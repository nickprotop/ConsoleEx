// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.DataBinding;
using SharpConsoleUI.Layout;
using System.Drawing;

namespace SharpConsoleUI.Builders
{
	/// <summary>Fluent builder for <see cref="SpinnerControl"/>.</summary>
	public class SpinnerBuilder : IControlBuilder<SpinnerControl>
	{
		private readonly SpinnerControl _control = new();

		#region Spinner-specific Methods

		/// <summary>Sets the preset frame style.</summary>
		public SpinnerBuilder WithStyle(SpinnerStyle style) { _control.Style = style; return this; }

		/// <summary>Sets custom frames (overrides style). May contain markup.</summary>
		public SpinnerBuilder WithFrames(params string[] frames) { _control.Frames = frames; return this; }

		/// <summary>Sets the per-frame interval in milliseconds.</summary>
		public SpinnerBuilder WithInterval(int milliseconds) { _control.IntervalMs = milliseconds; return this; }

		/// <summary>Sets the foreground color for plain frames.</summary>
		public SpinnerBuilder WithColor(Color color) { _control.Color = color; return this; }

		/// <summary>Sets whether the spinner starts animating (default true).</summary>
		public SpinnerBuilder Spinning(bool spinning = true) { _control.IsSpinning = spinning; return this; }

		#endregion

		#region Standard Control Methods

		/// <summary>Sets the control name.</summary>
		public SpinnerBuilder WithName(string name) { _control.Name = name; return this; }

		/// <summary>Sets the margin.</summary>
		public SpinnerBuilder WithMargin(int left, int top, int right, int bottom)
		{ _control.Margin = new Margin(left, top, right, bottom); return this; }

		/// <summary>Sets the margin.</summary>
		public SpinnerBuilder WithMargin(Margin margin) { _control.Margin = margin; return this; }

		/// <summary>Sets the horizontal alignment.</summary>
		public SpinnerBuilder WithAlignment(HorizontalAlignment alignment)
		{ _control.HorizontalAlignment = alignment; return this; }

		/// <summary>Sets the vertical alignment.</summary>
		public SpinnerBuilder WithVerticalAlignment(VerticalAlignment alignment)
		{ _control.VerticalAlignment = alignment; return this; }

		/// <summary>Sets the visibility.</summary>
		public SpinnerBuilder Visible(bool visible) { _control.Visible = visible; return this; }

		/// <summary>Sets an arbitrary tag.</summary>
		public SpinnerBuilder WithTag(object tag) { _control.Tag = tag; return this; }

		/// <summary>Makes the control stick to the top during scrolling.</summary>
		public SpinnerBuilder StickyTop() { _control.StickyPosition = StickyPosition.Top; return this; }

		/// <summary>Makes the control stick to the bottom during scrolling.</summary>
		public SpinnerBuilder StickyBottom() { _control.StickyPosition = StickyPosition.Bottom; return this; }

		#endregion

		/// <summary>Builds the configured <see cref="SpinnerControl"/>.</summary>
		public SpinnerControl Build()
		{
			BindingHelper.ApplyDeferredBindings(this, _control);
			return _control;
		}
	}
}
