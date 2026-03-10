// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.DataBinding;
using SharpConsoleUI.Imaging;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Builders;

/// <summary>
/// Fluent builder for creating ImageControl instances.
/// </summary>
public sealed class ImageControlBuilder : IControlBuilder<ImageControl>
{
	private PixelBuffer? _source;
	private ImageScaleMode? _scaleMode;
	private HorizontalAlignment _alignment = HorizontalAlignment.Left;
	private VerticalAlignment _verticalAlignment = VerticalAlignment.Top;
	private Margin _margin = new(0, 0, 0, 0);
	private bool _visible = true;
	private string? _name;
	private object? _tag;
	private StickyPosition _stickyPosition = StickyPosition.None;

	/// <summary>
	/// Sets the pixel buffer source to display.
	/// </summary>
	public ImageControlBuilder WithSource(PixelBuffer source)
	{
		_source = source;
		return this;
	}

	/// <summary>
	/// Sets the image scale mode.
	/// </summary>
	public ImageControlBuilder WithScaleMode(ImageScaleMode scaleMode)
	{
		_scaleMode = scaleMode;
		return this;
	}

	/// <summary>
	/// Sets scale mode to Fit (uniform scale preserving aspect ratio).
	/// </summary>
	public ImageControlBuilder Fit()
	{
		_scaleMode = ImageScaleMode.Fit;
		return this;
	}

	/// <summary>
	/// Sets scale mode to Fill (uniform scale, cropping excess).
	/// </summary>
	public ImageControlBuilder Fill()
	{
		_scaleMode = ImageScaleMode.Fill;
		return this;
	}

	/// <summary>
	/// Sets scale mode to Stretch (ignores aspect ratio).
	/// </summary>
	public ImageControlBuilder Stretch()
	{
		_scaleMode = ImageScaleMode.Stretch;
		return this;
	}

	/// <summary>
	/// Sets scale mode to None (original size, no scaling).
	/// </summary>
	public ImageControlBuilder NoScaling()
	{
		_scaleMode = ImageScaleMode.None;
		return this;
	}

	/// <summary>
	/// Sets the horizontal alignment.
	/// </summary>
	public ImageControlBuilder WithAlignment(HorizontalAlignment alignment)
	{
		_alignment = alignment;
		return this;
	}

	/// <summary>
	/// Sets the vertical alignment.
	/// </summary>
	public ImageControlBuilder WithVerticalAlignment(VerticalAlignment alignment)
	{
		_verticalAlignment = alignment;
		return this;
	}

	/// <summary>
	/// Sets the margin.
	/// </summary>
	public ImageControlBuilder WithMargin(int left, int top, int right, int bottom)
	{
		_margin = new Margin(left, top, right, bottom);
		return this;
	}

	/// <summary>
	/// Sets uniform margin on all sides.
	/// </summary>
	public ImageControlBuilder WithMargin(int margin)
	{
		_margin = new Margin(margin, margin, margin, margin);
		return this;
	}

	/// <summary>
	/// Sets the visibility.
	/// </summary>
	public ImageControlBuilder Visible(bool visible = true)
	{
		_visible = visible;
		return this;
	}

	/// <summary>
	/// Sets the control name for lookup.
	/// </summary>
	public ImageControlBuilder WithName(string name)
	{
		_name = name;
		return this;
	}

	/// <summary>
	/// Sets a tag object.
	/// </summary>
	public ImageControlBuilder WithTag(object tag)
	{
		_tag = tag;
		return this;
	}

	/// <summary>
	/// Sets the sticky position.
	/// </summary>
	public ImageControlBuilder WithStickyPosition(StickyPosition position)
	{
		_stickyPosition = position;
		return this;
	}

	/// <summary>
	/// Makes the control stick to the top of the window.
	/// </summary>
	public ImageControlBuilder StickyTop()
	{
		_stickyPosition = StickyPosition.Top;
		return this;
	}

	/// <summary>
	/// Makes the control stick to the bottom of the window.
	/// </summary>
	public ImageControlBuilder StickyBottom()
	{
		_stickyPosition = StickyPosition.Bottom;
		return this;
	}

	/// <summary>
	/// Builds the ImageControl instance.
	/// </summary>
	public ImageControl Build()
	{
		var control = new ImageControl
		{
			HorizontalAlignment = _alignment,
			VerticalAlignment = _verticalAlignment,
			Margin = _margin,
			Visible = _visible,
			Name = _name,
			Tag = _tag,
			StickyPosition = _stickyPosition
		};

		if (_source != null)
			control.Source = _source;
		if (_scaleMode.HasValue)
			control.ScaleMode = _scaleMode.Value;

		BindingHelper.ApplyDeferredBindings(this, control);
		return control;
	}

	/// <summary>
	/// Implicit conversion to ImageControl.
	/// </summary>
	public static implicit operator ImageControl(ImageControlBuilder builder) => builder.Build();
}
