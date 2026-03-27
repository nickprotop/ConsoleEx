// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;
using System.Drawing;

namespace SharpConsoleUI.Core
{
	/// <summary>
	/// Options for creating a desktop portal.
	/// </summary>
	/// <param name="Content">The root control to render in the portal.</param>
	/// <param name="Bounds">Screen-space position and size of the portal.</param>
	/// <param name="DismissOnClickOutside">Whether clicking outside the portal dismisses it.</param>
	/// <param name="ConsumeClickOnDismiss">Whether the dismissing click is consumed or passed through.</param>
	/// <param name="DimBackground">Whether to dim the screen behind the portal.</param>
	/// <param name="OnDismiss">Callback invoked when the portal is dismissed.</param>
	/// <param name="Owner">The control that owns this portal (for identification/cleanup).</param>
	/// <param name="BufferSize">Buffer size for rendering. Defaults to Bounds size.</param>
	/// <param name="BufferOrigin">Screen coordinate that buffer (0,0) maps to. Defaults to Bounds.Location.</param>
	public record DesktopPortalOptions(
		IWindowControl Content,
		Rectangle Bounds,
		bool DismissOnClickOutside = true,
		bool ConsumeClickOnDismiss = true,
		bool DimBackground = false,
		Action? OnDismiss = null,
		IWindowControl? Owner = null,
		Size? BufferSize = null,
		Point? BufferOrigin = null
	);

	/// <summary>
	/// A desktop-level portal that renders above all windows.
	/// Portals are lightweight render overlays managed by DesktopPortalService.
	/// </summary>
	public class DesktopPortal
	{
		/// <summary>Unique identifier for this portal.</summary>
		public string Id { get; }

		/// <summary>The root control rendered in this portal.</summary>
		public IWindowControl Content { get; }

		/// <summary>Screen-space position and size.</summary>
		public Rectangle Bounds { get; set; }

		/// <summary>DOM tree root for layout and rendering.</summary>
		public LayoutNode RootNode { get; }

		/// <summary>Whether clicking outside dismisses this portal.</summary>
		public bool DismissOnClickOutside { get; }

		/// <summary>Whether the dismissing click is consumed or passed through.</summary>
		public bool ConsumeClickOnDismiss { get; }

		/// <summary>Whether to dim the screen behind the portal.</summary>
		public bool DimBackground { get; }

		/// <summary>Callback invoked when the portal is dismissed.</summary>
		public Action? OnDismiss { get; }

		/// <summary>The control that owns this portal.</summary>
		public IWindowControl? Owner { get; }

		/// <summary>
		/// Buffer size for rendering. When larger than Bounds, allows portal children (submenus)
		/// to extend beyond the primary content area. Defaults to Bounds size.
		/// </summary>
		public Size BufferSize { get; }

		/// <summary>
		/// Screen coordinate that buffer position (0,0) maps to.
		/// Defaults to Bounds.Location for backwards compatibility.
		/// Set to DesktopUpperLeft when the buffer needs to cover space above the portal (e.g., Start Menu).
		/// </summary>
		public Point BufferOrigin { get; }

		/// <summary>Stacking order among portals (higher = on top).</summary>
		public int ZOrder { get; }

		/// <summary>Thread-safe dirty flag — background threads may trigger Invalidate.</summary>
		public volatile bool IsDirty;

		/// <summary>Creation timestamp for debounce — prevents dismiss on the same click that created the portal.</summary>
		internal DateTime CreatedAt { get; }

		/// <summary>Whether this portal has been rendered at least once (has valid control bounds and buffer).</summary>
		public bool HasRendered => Buffer != null;

		/// <summary>Cached character buffer for rendering.</summary>
		internal CharacterBuffer? Buffer { get; set; }

		/// <summary>Cached control bounds from last render (used for selective rendering and hit testing).</summary>
		internal List<LayoutRect> ControlBounds { get; set; } = new();

		/// <summary>Control bounds from the previous render — used to compute delta regions for restoration.</summary>
		internal List<LayoutRect> PreviousControlBounds { get; set; } = new();

		/// <summary>The container implementation that connects Content to this portal.</summary>
		internal DesktopPortalContainer Container { get; }

		/// <summary>Reference to the window system.</summary>
		internal ConsoleWindowSystem WindowSystem { get; }

		/// <summary>
		/// Creates a new desktop portal.
		/// </summary>
		internal DesktopPortal(DesktopPortalOptions options, int zOrder, ConsoleWindowSystem windowSystem)
		{
			Id = Guid.NewGuid().ToString();
			Content = options.Content;
			Bounds = options.Bounds;
			DismissOnClickOutside = options.DismissOnClickOutside;
			ConsumeClickOnDismiss = options.ConsumeClickOnDismiss;
			DimBackground = options.DimBackground;
			OnDismiss = options.OnDismiss;
			Owner = options.Owner;
			ZOrder = zOrder;
			WindowSystem = windowSystem;
			IsDirty = true;
			CreatedAt = DateTime.Now;
			BufferSize = options.BufferSize ?? new Size(Bounds.Width, Bounds.Height);
			BufferOrigin = options.BufferOrigin ?? new Point(Bounds.X, Bounds.Y);

			Container = new DesktopPortalContainer(this);

			// Build LayoutNode tree
			Content.Container = Container;
			RootNode = new LayoutNode(Content);
			RootNode.IsVisible = true;

			// Measure with loose constraints using buffer size (may be larger than Bounds)
			var constraints = LayoutConstraints.Loose(BufferSize.Width, BufferSize.Height);
			RootNode.Measure(constraints);

			// Arrange at portal bounds so the root control fills the declared area.
			// Controls that want to be smaller will respect their own sizing within this rect.
			int rootOffsetX = Bounds.X - BufferOrigin.X;
			int rootOffsetY = Bounds.Y - BufferOrigin.Y;
			RootNode.Arrange(new LayoutRect(rootOffsetX, rootOffsetY, Bounds.Width, Bounds.Height));

			// Compute initial control bounds for hit-testing before first render
			RootNode.Visit(node =>
			{
				if (node.Control != null && !node.AbsoluteBounds.IsEmpty)
				{
					ControlBounds.Add(node.AbsoluteBounds);
				}
			});
		}
	}

	/// <summary>
	/// IContainer and IPortalHost implementation for desktop portals.
	/// Routes invalidation to the portal's dirty flag and provides portal hosting for submenus.
	/// </summary>
	internal class DesktopPortalContainer : IContainer, IPortalHost
	{
		/// <summary>The portal this container wraps.</summary>
		public DesktopPortal Portal { get; }

		private readonly Dictionary<IWindowControl, LayoutNode> _controlToNodeMap = new();

		public DesktopPortalContainer(DesktopPortal portal)
		{
			Portal = portal;
		}

		#region IContainer

		/// <inheritdoc/>
		public Color BackgroundColor
		{
			get => Portal.WindowSystem.Theme.DesktopBackgroundColor;
			set { /* Portal background is controlled by the portal system */ }
		}

		/// <inheritdoc/>
		public Color ForegroundColor
		{
			get => Portal.WindowSystem.Theme.DesktopForegroundColor;
			set { /* Portal foreground is controlled by the portal system */ }
		}

		/// <inheritdoc/>
		public ConsoleWindowSystem? GetConsoleWindowSystem => Portal.WindowSystem;

		/// <inheritdoc/>
		public bool IsDirty
		{
			get => Portal.IsDirty;
			set => Portal.IsDirty = value;
		}

		/// <inheritdoc/>
		public void Invalidate(bool redrawAll, IWindowControl? callerControl = null)
		{
			Portal.IsDirty = true;
		}

		/// <inheritdoc/>
		public int? GetVisibleHeightForControl(IWindowControl control) => null;

		#endregion

		#region IPortalHost

		/// <inheritdoc/>
		public LayoutNode? CreatePortal(IWindowControl ownerControl, IWindowControl portalContent)
		{
			// Connect portal content into the invalidation chain
			portalContent.Container = this;

			var portalNode = new LayoutNode(portalContent);
			portalNode.IsVisible = true;

			// Measure using buffer size (allows submenus to extend beyond portal bounds)
			var constraints = LayoutConstraints.Loose(Portal.BufferSize.Width, Portal.BufferSize.Height);
			portalNode.Measure(constraints);

			// Get the portal's desired position
			Rectangle portalBounds;
			if (portalContent is IHasPortalBounds positioned)
			{
				portalBounds = positioned.GetPortalBounds();
			}
			else
			{
				portalBounds = new Rectangle(0, 0, portalNode.DesiredSize.Width, portalNode.DesiredSize.Height);
			}

			var portalRect = new LayoutRect(portalBounds.X, portalBounds.Y, portalBounds.Width, portalBounds.Height);
			portalNode.Arrange(portalRect);

			// Add to root node as portal child
			Portal.RootNode.AddPortalChild(portalNode);
			_controlToNodeMap[portalContent] = portalNode;

			Portal.IsDirty = true;
			return portalNode;
		}

		/// <inheritdoc/>
		public void RemovePortal(IWindowControl ownerControl, LayoutNode portalNode)
		{
			Portal.RootNode.RemovePortalChild(portalNode);
			if (portalNode.Control != null)
			{
				portalNode.Control.Container = null;
				_controlToNodeMap.Remove(portalNode.Control);
			}

			Portal.IsDirty = true;
		}

		#endregion
	}
}
