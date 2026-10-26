// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Configuration;
using SharpConsoleUI.Drawing;
using SharpConsoleUI.Events;
using SharpConsoleUI.Extensions;
using SharpConsoleUI.Layout;

namespace SharpConsoleUI.Controls
{
	/// <summary>
	/// A free-form drawing surface control that exposes CharacterBuffer drawing primitives
	/// through a local-coordinate API. Supports both async/on-demand painting via
	/// <see cref="BeginPaint"/>/<see cref="EndPaint"/> and event-driven painting via the
	/// <see cref="Paint"/> event. Both modes can be combined.
	/// </summary>
	/// <remarks>
	/// This control does not implement <see cref="IColorRoleableControl"/>, so it has no semantic role and does
	/// not honour them: it has no single themed colour surface (it renders raw user-drawn pixels).
	/// </remarks>
	public partial class CanvasControl : BaseControl, IInteractiveControl, IFocusableControl, IMouseAwareControl
	{
		#region Fields

		private int _canvasWidth;
		private int _canvasHeight;
		private CharacterBuffer _internalBuffer;
		private readonly object _bufferLock = new();
		private bool _isEnabled = true;
		private bool _autoClear;
		private bool _autoSize;
		private Color? _backgroundColorValue;
		private Color? _foregroundColorValue;

		#endregion

		#region Constructors

		/// <summary>
		/// Initializes a new CanvasControl with default dimensions.
		/// </summary>
		public CanvasControl()
			: this(ControlDefaults.DefaultCanvasWidth, ControlDefaults.DefaultCanvasHeight)
		{
		}

		/// <summary>
		/// Initializes a new CanvasControl with the specified dimensions.
		/// </summary>
		/// <param name="canvasWidth">The initial canvas width in characters.</param>
		/// <param name="canvasHeight">The initial canvas height in characters.</param>
		public CanvasControl(int canvasWidth, int canvasHeight)
		{
			_canvasWidth = Math.Max(ControlDefaults.MinCanvasSize, canvasWidth);
			_canvasHeight = Math.Max(ControlDefaults.MinCanvasSize, canvasHeight);
			_internalBuffer = new CharacterBuffer(_canvasWidth, _canvasHeight);
		}

		#endregion

		#region Properties

		/// <inheritdoc/>
		public override int? ContentWidth => _canvasWidth + Margin.Left + Margin.Right;

		/// <summary>
		/// Reports the canvas's true logical size (buffer dimensions plus margins). The base
		/// implementation hardcodes height = 1, which makes a container such as
		/// <see cref="ScrollablePanelControl"/> believe the canvas is a single row tall and so
		/// suppress vertical scrolling. Reporting the real height lets the scroller compute the
		/// correct content extent and show scrollbars.
		/// </summary>
		/// <remarks>
		/// When <see cref="AutoSize"/> is enabled, <c>_canvasWidth</c>/<c>_canvasHeight</c> track the
		/// layout bounds assigned during paint, so the reported logical size follows the assigned
		/// bounds and does not desync.
		/// </remarks>
		public override System.Drawing.Size GetLogicalContentSize() =>
			new(_canvasWidth + Margin.Left + Margin.Right,
				_canvasHeight + Margin.Top + Margin.Bottom);

		/// <summary>
		/// Gets or sets the canvas width in characters. Clamped to <see cref="ControlDefaults.MinCanvasSize"/>.
		/// Setting this recreates the internal buffer (previous content is lost).
		/// </summary>
		public int CanvasWidth
		{
			get => _canvasWidth;
			set
			{
				int clamped = Math.Max(ControlDefaults.MinCanvasSize, value);
				if (_canvasWidth == clamped) return;
				_canvasWidth = clamped;
				OnPropertyChanged();
				RecreateInternalBuffer();
				Invalidate(Invalidation.Relayout);
			}
		}

		/// <summary>
		/// Gets or sets the canvas height in characters. Clamped to <see cref="ControlDefaults.MinCanvasSize"/>.
		/// Setting this recreates the internal buffer (previous content is lost).
		/// </summary>
		public int CanvasHeight
		{
			get => _canvasHeight;
			set
			{
				int clamped = Math.Max(ControlDefaults.MinCanvasSize, value);
				if (_canvasHeight == clamped) return;
				_canvasHeight = clamped;
				OnPropertyChanged();
				RecreateInternalBuffer();
				Invalidate(Invalidation.Relayout);
			}
		}

		/// <summary>
		/// When true, the internal buffer is cleared after compositing each frame,
		/// so the <see cref="Paint"/> event redraws from scratch. Default is false (retained mode).
		/// </summary>
		public bool AutoClear
		{
			get => _autoClear;
			set { _autoClear = value; OnPropertyChanged(); }
		}

		/// <summary>
		/// When true, the internal buffer automatically resizes to match the layout bounds
		/// assigned by the parent container. Enable this when using Stretch/Fill alignment
		/// so the canvas adapts to the available window space.
		/// </summary>
		public bool AutoSize
		{
			get => _autoSize;
			set => SetProperty(ref _autoSize, value);
		}

		/// <summary>
		/// Gets or sets the background color. Falls back to container, then black.
		/// </summary>
		public Color BackgroundColor
		{
			get => _backgroundColorValue ?? Container?.BackgroundColor ?? Color.Black;
			set => SetProperty(ref _backgroundColorValue, (Color?)value);
		}

		/// <summary>
		/// Gets or sets the foreground color. Falls back to container, then white.
		/// </summary>
		public Color ForegroundColor
		{
			get => _foregroundColorValue ?? Container?.ForegroundColor ?? Color.White;
			set => SetProperty(ref _foregroundColorValue, (Color?)value);
		}

		/// <summary>
		/// Gets or sets whether the control is enabled and can receive input.
		/// </summary>
		public bool IsEnabled
		{
			get => _isEnabled;
			set => SetProperty(ref _isEnabled, value);
		}

		#endregion

		#region BeginPaint / EndPaint

		/// <summary>
		/// Returns a <see cref="CanvasGraphics"/> wrapping the internal buffer for async drawing.
		/// Must be paired with <see cref="EndPaint"/>. Thread-safe via monitor lock.
		/// </summary>
		/// <returns>A drawing context for the internal canvas buffer.</returns>
		public CanvasGraphics BeginPaint()
		{
			Monitor.Enter(_bufferLock);
			return new CanvasGraphics(_internalBuffer, 0, 0,
				_canvasWidth, _canvasHeight, _internalBuffer.Bounds);
		}

		/// <summary>
		/// Releases the paint lock and triggers a repaint.
		/// </summary>
		public void EndPaint()
		{
			Monitor.Exit(_bufferLock);
			Invalidate(Invalidation.Relayout);
		}

		#endregion

		#region Pixel surface

		private Drawing.PixelSurface? _pixels;

		/// <summary>
		/// A <see cref="Drawing.PixelSurface"/> sized to this canvas, for drawing in
		/// pixels rather than characters.
		/// </summary>
		/// <remarks>
		/// <para>
		/// Opt-in and lazy: nothing is allocated until something asks, so a canvas
		/// that only draws text costs nothing. Each read resizes the surface to the
		/// current canvas, which is the step easiest to forget and the one whose
		/// units are easiest to get wrong - it takes CELLS, and the surface is twice
		/// as tall in pixels.
		/// </para>
		/// <para>
		/// A convenience, not the only way. <see cref="Drawing.PixelSurface"/> is an
		/// ordinary type: allocate your own, size it to a region rather than the whole
		/// canvas, keep several, and present any of them into any canvas with
		/// <see cref="Drawing.PixelSurface.Present(Drawing.CanvasGraphics, Color)"/>.
		/// Both example games do exactly that, because one of them presents with a
		/// screen-shake offset.
		/// </para>
		/// </remarks>
		/// <example>
		/// <code>
		/// canvas.Paint += (_, e) =>
		/// {
		///     var px = canvas.Pixels;              // already the right size
		///     px.Fill(background);
		///     px.FillCircle(20, 14, 6, Color.Red);
		///     px.Text(2, 1, "[bold]signal[/]", Color.White);
		///     canvas.PresentPixels(background);
		/// };
		/// </code>
		/// </example>
		public Drawing.PixelSurface Pixels
		{
			get
			{
				_pixels ??= new Drawing.PixelSurface();
				_pixels.EnsureSize(_canvasWidth, _canvasHeight);
				return _pixels;
			}
		}

		/// <summary>
		/// Whether <see cref="Pixels"/> has been used, without creating it.
		/// </summary>
		/// <remarks>
		/// Reading <see cref="Pixels"/> allocates. A caller that only wants to know
		/// whether there is anything to present asks this instead.
		/// </remarks>
		public bool HasPixelSurface => _pixels != null;

		/// <summary>
		/// Composites <see cref="Pixels"/> into the canvas and repaints.
		/// </summary>
		/// <param name="voidColor">Shown where the surface does not reach.</param>
		/// <remarks>
		/// Takes the same paint lock as <see cref="BeginPaint"/>, because it writes
		/// the same buffer. Does nothing if the surface was never used.
		/// </remarks>
		public void PresentPixels(Color voidColor) => PresentPixels(0, 0, voidColor);

		/// <summary>
		/// Composites <see cref="Pixels"/> into the canvas, displaced by an offset.
		/// </summary>
		/// <param name="offsetX">Horizontal displacement, in pixels.</param>
		/// <param name="offsetY">Vertical displacement, in pixels.</param>
		/// <param name="voidColor">Shown where the surface does not reach.</param>
		public void PresentPixels(int offsetX, int offsetY, Color voidColor)
		{
			if (_pixels == null) return;

			var g = BeginPaint();
			try
			{
				_pixels.Present(g, offsetX, offsetY, voidColor);
			}
			finally
			{
				EndPaint();
			}
		}

		#endregion

		#region Painting

		/// <summary>
		/// Triggers a repaint without painting.
		/// </summary>
		public void Refresh()
			=> Invalidate(Invalidation.Relayout);

		/// <summary>
		/// Clears the internal buffer with the control's background color.
		/// </summary>
		public void Clear()
		{
			lock (_bufferLock)
			{
				_internalBuffer.Clear(BackgroundColor);
			}
			Invalidate(Invalidation.Relayout);
		}

		/// <summary>
		/// Clears the internal buffer with the specified background color.
		/// </summary>
		/// <param name="bg">The background color to fill with.</param>
		public void Clear(Color bg)
		{
			lock (_bufferLock)
			{
				_internalBuffer.Clear(bg);
			}
			Invalidate(Invalidation.Relayout);
		}

		#endregion

		#region Events

		/// <summary>
		/// Fires during each render cycle after the internal buffer is composited.
		/// The <see cref="CanvasPaintEventArgs.Graphics"/> context draws directly to the window buffer.
		/// </summary>
		public event EventHandler<CanvasPaintEventArgs>? Paint;

		/// <summary>
		/// Fires when the canvas is left-clicked, with canvas-local coordinates.
		/// </summary>
		/// <remarks>
		/// A click is reported on RELEASE. When the press itself is the event that
		/// matters - a fire button, a drag handle, starting a selection - use
		/// <see cref="CanvasMouseDown"/>, which fires the moment the button goes
		/// down.
		/// </remarks>
		public event EventHandler<CanvasMouseEventArgs>? CanvasMouseClick;

		/// <summary>
		/// Fires the moment the left button goes down, with canvas-local coordinates.
		/// </summary>
		/// <remarks>
		/// <para>
		/// <see cref="CanvasMouseClick"/> is reported on release, which is the right
		/// default for a button but feels late for anything that should respond to
		/// the press: a game's fire control, the start of a drag, or painting a
		/// stroke. This fires on the press instead.
		/// </para>
		/// <para>
		/// Press, release and click arrive as separate events, so a full press and
		/// release raises <see cref="CanvasMouseDown"/>, then
		/// <see cref="CanvasMouseUp"/>, then <see cref="CanvasMouseClick"/>. Handle
		/// one or the other, not both, unless you want the action twice.
		/// </para>
		/// <para>
		/// Every press raises this, including the second and third of a fast run:
		/// a multi-click carries its press as well as the derived gesture. A caller
		/// that needs to tell them apart reads
		/// <see cref="CanvasMouseEventArgs.OriginalArgs"/>.
		/// </para>
		/// </remarks>
		public event EventHandler<CanvasMouseEventArgs>? CanvasMouseDown;

		/// <summary>
		/// Fires when the left button is released over the canvas, with canvas-local
		/// coordinates.
		/// </summary>
		/// <remarks>
		/// The other half of <see cref="CanvasMouseDown"/>: together they bracket a
		/// drag. A release that completes a click is also followed by
		/// <see cref="CanvasMouseClick"/>.
		/// </remarks>
		public event EventHandler<CanvasMouseEventArgs>? CanvasMouseUp;

		/// <summary>
		/// Fires when the mouse moves over the canvas, with canvas-local coordinates.
		/// </summary>
		public event EventHandler<CanvasMouseEventArgs>? CanvasMouseMove;

		/// <summary>
		/// Fires when the canvas is right-clicked, with canvas-local coordinates.
		/// </summary>
		public event EventHandler<CanvasMouseEventArgs>? CanvasMouseRightClick;

		/// <summary>
		/// Fires when a key is pressed while the canvas has focus.
		/// </summary>
		public event EventHandler<ConsoleKeyInfo>? CanvasKeyPressed;

		/// <summary>Async counterpart of <see cref="CanvasKeyPressed"/>.</summary>
		public event Core.AsyncEventHandler<ConsoleKeyInfo>? CanvasKeyPressedAsync;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseClick;

		/// <inheritdoc/>
#pragma warning disable CS0067, CS0414
		public event EventHandler<MouseEventArgs>? MouseDoubleClick;
#pragma warning restore CS0067, CS0414

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseRightClick;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseEnter;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseLeave;

		/// <inheritdoc/>
		public event EventHandler<MouseEventArgs>? MouseMove;

		#endregion

		#region IFocusableControl

		/// <inheritdoc/>
		public bool HasFocus
		{
			get => ComputeHasFocus();
		}

		/// <inheritdoc/>
		public bool CanReceiveFocus => IsEnabled;

		#endregion

		#region Private Helpers

		private void RecreateInternalBuffer()
		{
			lock (_bufferLock)
			{
				_internalBuffer = new CharacterBuffer(_canvasWidth, _canvasHeight);
			}
		}

		#endregion

		#region Disposal

		/// <inheritdoc/>
		protected override void OnDisposing()
		{
			Paint = null;
			CanvasMouseClick = null;
			CanvasMouseDown = null;
			CanvasMouseUp = null;
			CanvasMouseMove = null;
			CanvasMouseRightClick = null;
			CanvasKeyPressed = null;
			CanvasKeyPressedAsync = null;
			MouseClick = null;
			MouseDoubleClick = null;
			MouseRightClick = null;
			MouseEnter = null;
			MouseLeave = null;
			MouseMove = null;
		}

		#endregion

		/// <inheritdoc/>
		public override void DescribeState(IStateWriter w)
		{
			ArgumentNullException.ThrowIfNull(w);
			base.DescribeState(w);

			// The drawing surface in its own units, which are not the cell bounds the
			// layout tree reports - a pixel canvas has two rows per cell.
			w.Write("canvasWidth", CanvasWidth);
			w.Write("canvasHeight", CanvasHeight);
		}

	}
}
