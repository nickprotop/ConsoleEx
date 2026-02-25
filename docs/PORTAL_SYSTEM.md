# Portal System

SharpConsoleUI's portal system enables overlay rendering — content that floats above the normal window layout, unclipped by parent containers. Portals power dropdowns, context menus, tooltips, and any UI element that needs to break out of its container bounds.

## Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [Core API](#core-api)
- [Portal Positioning](#portal-positioning)
- [Portal Content Types](#portal-content-types)
- [PortalContentBase](#portalcontentbase)
- [PortalContentContainer](#portalcontentcontainer)
- [Building a Custom Portal](#building-a-custom-portal)
- [Mouse and Keyboard Routing](#mouse-and-keyboard-routing)
- [Nested Containers](#nested-containers)
- [Dismiss on Outside Click](#dismiss-on-outside-click)
- [Lifecycle Management](#lifecycle-management)
- [Quick Reference](#quick-reference)

## Overview

In a typical window layout, controls are clipped to their parent container's bounds. A dropdown list inside a toolbar, for example, would be cut off at the toolbar's edge. Portals solve this by rendering content in a separate layer on top of the normal layout tree.

```
Normal layout tree:              Portal overlay:
┌─────────────────────┐          ┌─────────────────────┐
│ Window              │          │ Window              │
│ ┌─────────────────┐ │          │                     │
│ │ Toolbar         │ │          │    ┌──────────┐     │
│ │ [File ▼] [Edit] │ │    →     │    │ New      │     │
│ └─────────────────┘ │          │    │ Open     │     │
│ ┌─────────────────┐ │          │    │ Save     │     │
│ │ Content area    │ │          │    │ Exit     │     │
│ └─────────────────┘ │          │    └──────────┘     │
└─────────────────────┘          └─────────────────────┘
```

## Architecture

### Rendering Pipeline

Portals integrate into the DOM layout system at the `LayoutNode` level:

1. **Normal pass** — The layout tree is measured, arranged, and painted as usual
2. **Portal pass** — Portal children attached to the root node are painted last, on top of everything

Portal nodes are added to the root `LayoutNode` via `AddPortalChild()`. During painting, they render after all normal children, ensuring they appear on top regardless of Z-order within the normal tree.

### Key Components

```
SharpConsoleUI/
├── Layout/
│   ├── LayoutNode.cs              # AddPortalChild/RemovePortalChild
│   ├── PortalPositioner.cs        # Smart positioning with flip/clamp
│   └── LayoutNodeFactory.cs       # Recognizes self-painting portal containers
├── Controls/
│   ├── PortalContentBase.cs       # Abstract base for all portal content
│   ├── PortalContentContainer.cs  # Container that hosts child controls
│   └── IHasPortalBounds.cs        # Interface for portal bounds
└── Windows/
    ├── Window.cs                  # CreatePortal/RemovePortal public API
    └── WindowRenderer.cs          # Portal node management
```

### Hit-Testing

The event dispatcher checks portals in reverse order (topmost first) before testing normal controls. This ensures clicks on portal overlays are handled before anything underneath.

## Core API

### Creating a Portal

```csharp
// From any control that has access to its parent Window:
var window = this.GetParentWindow();

LayoutNode? portalNode = window.CreatePortal(ownerControl, portalContent);
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `ownerControl` | `IWindowControl` | The control creating the portal (typically `this`) |
| `portalContent` | `IWindowControl` | The portal content (implements `IHasPortalBounds`) |

Returns a `LayoutNode` handle used for later removal, or `null` if creation failed.

### Removing a Portal

```csharp
window.RemovePortal(ownerControl, portalNode);
portalNode = null;
```

Always remove portals when they're no longer needed (dropdown closed, menu dismissed, etc.).

## Portal Positioning

`PortalPositioner` calculates optimal portal placement with automatic flip and screen-edge clamping.

### PortalPlacement Options

| Placement | Behavior |
|-----------|----------|
| `Below` | Open below the anchor |
| `Above` | Open above the anchor |
| `Right` | Open to the right of the anchor |
| `Left` | Open to the left of the anchor |
| `BelowOrAbove` | Try below first, flip to above if insufficient space |
| `AboveOrBelow` | Try above first, flip to below if insufficient space |
| `RightOrLeft` | Try right first, flip to left if insufficient space |
| `LeftOrRight` | Try left first, flip to right if insufficient space |

### Usage

```csharp
using SharpConsoleUI.Layout;

// Define the positioning request
var request = new PortalPositionRequest(
    Anchor: new Rectangle(controlX, controlY, controlWidth, controlHeight),
    ContentSize: new Size(desiredWidth, desiredHeight),
    ScreenBounds: new Rectangle(0, 0, screenWidth, screenHeight),
    Placement: PortalPlacement.BelowOrAbove
);

// Calculate position
PortalPositionResult result = PortalPositioner.Calculate(request);

// result.Bounds         — Final positioned rectangle
// result.ActualPlacement — The placement direction used (may differ if flipped)
// result.WasClamped     — True if bounds were adjusted to fit screen
```

## Portal Content Types

### Paint-Based (Manual Rendering)

For simple, self-contained overlays that render directly to the character buffer:

- **`DropdownPortalContent`** — Used internally by `DropdownControl`. Renders a scrollable item list with selection highlight.
- **`MenuPortalContent`** — Used internally by `MenuControl`. Renders menu items with keyboard shortcuts and separators.

These subclass `PortalContentBase` and implement `PaintPortalContent()` to draw directly.

### Container-Based (Child Controls)

For portals that need to host arbitrary child controls with full layout, focus, and input routing:

- **`PortalContentContainer`** — Hosts any combination of controls (ListControl, ButtonControl, ScrollablePanelControl, etc.) with automatic vertical stack layout.

## PortalContentBase

Abstract base class providing default implementations of `IWindowControl`, `IDOMPaintable`, `IMouseAwareControl`, and `IHasPortalBounds`.

### What It Provides

- Portal bounds reporting via `GetPortalBounds()`
- Actual position tracking (`ActualX`, `ActualY`, `ActualWidth`, `ActualHeight`)
- Mouse awareness with `CanFocusWithMouse = false` (portals don't steal window focus)
- Measurement based on portal bounds
- Automatic `PaintDOM` → `PaintPortalContent` delegation

### Subclassing

```csharp
public class MyPortalContent : PortalContentBase
{
    private Rectangle _bounds;

    public override Rectangle GetPortalBounds() => _bounds;

    public override bool ProcessMouseEvent(MouseEventArgs args)
    {
        // Handle clicks within the portal
        return false;
    }

    protected override void PaintPortalContent(CharacterBuffer buffer,
        LayoutRect bounds, LayoutRect clipRect, Color defaultFg, Color defaultBg)
    {
        // Render portal content to the buffer
    }
}
```

## PortalContentContainer

A `PortalContentBase` subclass that acts as a proper container for child controls. Unlike the paint-based portal content classes, `PortalContentContainer` provides:

- **Vertical stack layout** for child controls
- **Mouse hit-testing** with coordinate translation to children
- **Keyboard delegation** to focused child with Tab cycling
- **Focus tracking** via `IFocusTrackingContainer`
- **Container chain** support (`IContainer`) for color resolution and invalidation

### Interfaces

| Interface | Purpose |
|-----------|---------|
| `PortalContentBase` | Portal bounds, mouse awareness, DOM painting |
| `IContainer` | Color resolution, `GetConsoleWindowSystem`, `Invalidate()` |
| `IContainerControl` | `GetChildren()` for focus traversal |
| `IFocusTrackingContainer` | Tracks which child has focus |

### Creating a Portal with Controls

```csharp
var portal = new PortalContentContainer();

// Position the portal (manually or with PortalPositioner)
var result = PortalPositioner.Calculate(new PortalPositionRequest(
    Anchor: new Rectangle(anchorX, anchorY, anchorWidth, anchorHeight),
    ContentSize: new Size(30, 10),
    ScreenBounds: new Rectangle(0, 0, Console.WindowWidth, Console.WindowHeight),
    Placement: PortalPlacement.BelowOrAbove
));
portal.PortalBounds = result.Bounds;

// Add child controls
portal.AddChild(new MarkupControl("[bold]Choose an option:[/]"));

var list = new ListControl();
list.AddItem("Cut");
list.AddItem("Copy");
list.AddItem("Paste");
portal.AddChild(list);

portal.AddChild(Controls.Button().WithText("OK").Build());

// Attach to window
_portalNode = window.CreatePortal(this, portal);
portal.SetFocusOnFirstChild();
```

### Child Management

```csharp
portal.AddChild(control);      // Add a child, sets Container reference
portal.RemoveChild(control);   // Remove a child, clears Container
portal.ClearChildren();        // Remove and dispose all children
var kids = portal.Children;    // Read-only access
```

### Keyboard Input

The owner control forwards keyboard events to the portal container:

```csharp
// In owner control's ProcessKey:
public bool ProcessKey(ConsoleKeyInfo key)
{
    // Forward to portal if open
    if (_portal != null && _portal.ProcessKey(key))
        return true;

    // Handle Escape to close
    if (key.Key == ConsoleKey.Escape && _portalNode != null)
    {
        ClosePortal();
        return true;
    }

    // ... other key handling
}
```

Keyboard routing within the container:

1. Key is forwarded to the currently focused child
2. If unhandled and Tab/Shift+Tab: cycles focus among focusable children
3. If unhandled: returns `false` so the owner can handle it (e.g., Escape to close)

### Focus Management

```csharp
portal.SetFocusOnFirstChild();  // Focus first focusable child
portal.SetFocusOnLastChild();   // Focus last focusable child
```

Focus also updates automatically on mouse click — clicking a focusable child sets it as the focused child and unfocuses the previous one.

## Building a Custom Portal

Complete example of a control that opens a portal overlay:

```csharp
public class MyPopupControl : BaseControl, IInteractiveControl, IFocusableControl, IMouseAwareControl
{
    private PortalContentContainer? _portal;
    private LayoutNode? _portalNode;

    public void OpenPopup()
    {
        if (_portalNode != null) return; // Already open

        var window = this.GetParentWindow();
        if (window == null) return;

        _portal = new PortalContentContainer();

        // Position below this control
        var result = PortalPositioner.Calculate(new PortalPositionRequest(
            Anchor: new Rectangle(ActualX, ActualY, ActualWidth, ActualHeight),
            ContentSize: new Size(30, 8),
            ScreenBounds: new Rectangle(0, 0,
                window.WindowSystem.ScreenWidth,
                window.WindowSystem.ScreenHeight),
            Placement: PortalPlacement.BelowOrAbove
        ));
        _portal.PortalBounds = result.Bounds;

        // Add controls
        _portal.AddChild(new MarkupControl("[bold]Popup Title[/]"));
        _portal.AddChild(new ListControl { /* ... */ });

        _portalNode = window.CreatePortal(this, _portal);
        _portal.SetFocusOnFirstChild();
    }

    public void ClosePopup()
    {
        if (_portalNode == null) return;

        var window = this.GetParentWindow();
        window?.RemovePortal(this, _portalNode);
        _portal?.ClearChildren();
        _portal = null;
        _portalNode = null;
        Container?.Invalidate(true);
    }

    public bool ProcessKey(ConsoleKeyInfo key)
    {
        if (_portal != null && _portal.ProcessKey(key))
            return true;

        if (key.Key == ConsoleKey.Escape && _portalNode != null)
        {
            ClosePopup();
            return true;
        }

        if (key.Key == ConsoleKey.Enter && _portalNode == null)
        {
            OpenPopup();
            return true;
        }

        return false;
    }
}
```

## Mouse and Keyboard Routing

### Mouse Flow

```
1. Click at screen (X, Y)
2. WindowEventDispatcher → LayoutNode.HitTest()
   — Portals checked FIRST (reverse order, topmost wins)
3. Portal LayoutNode matched → returns PortalContentContainer
4. Coordinates translated to portal-relative
5. PortalContentContainer.ProcessMouseEvent(args)
6. Container hit-tests children by measuring heights
7. Translates to child-relative coordinates
8. Updates focused child if clicked child is focusable
9. Forwards event to child's ProcessMouseEvent
```

### Keyboard Flow

```
1. Key pressed
2. WindowEventDispatcher routes to OWNER control (not portal)
3. Owner's ProcessKey() calls portal.ProcessKey(key)
4. Portal delegates to focused child's ProcessKey()
5. If unhandled: Tab cycles focus among children
6. If still unhandled: returns false to owner
   (owner can close on Escape, etc.)
```

## Nested Containers

All existing container types work inside `PortalContentContainer`:

| Container | How It Works |
|-----------|-------------|
| `ColumnContainer` | `LayoutNodeFactory.CreateSubtree()` builds vertical stack subtree |
| `HorizontalGridControl` | Full horizontal layout with splitters |
| `TabControl` | Tab layout with header switching |
| `ScrollablePanelControl` | Self-painting — handles its own children and scrolling |
| `ToolbarControl` | Self-painting — horizontal button layout |

This works because:

- **Container chain**: `child.Container = PortalContentContainer` (implements `IContainer`), so color resolution, `GetConsoleWindowSystem`, and `Invalidate()` all propagate correctly
- **Layout**: `LayoutNodeFactory.CreateSubtree()` recursively builds proper subtrees for each container type
- **Mouse**: Portal forwards to child, child does its own internal hit-testing for nested children
- **Focus**: `IFocusTrackingContainer` ensures focus tracking through nesting

## Dismiss on Outside Click

Portals can opt in to automatic dismissal when the user clicks outside their bounds. This is useful for dropdowns, context menus, and other transient overlays.

### Enabling

Set `DismissOnOutsideClick` on the portal content:

```csharp
// Via PortalContentBase subclass
var portal = new PortalContentContainer();
portal.DismissOnOutsideClick = true;

// Or via IHasPortalBounds (default-implemented as false)
public bool DismissOnOutsideClick => true;
```

### DismissRequested Event

`PortalContentBase` exposes a `DismissRequested` event that fires **before** the portal is removed. Use it for cleanup:

```csharp
portal.DismissRequested += (sender, e) =>
{
    // Clean up state, close related UI, etc.
    _portalNode = null;
    _portal = null;
};
portal.DismissOnOutsideClick = true;

_portalNode = window.CreatePortal(this, portal);
```

### How It Works

Portals with `DismissOnOutsideClick = true` are dismissed in two scenarios:

**Outside click:**
1. On left-click or right-click, `WindowEventDispatcher` walks all portal nodes
2. For each matching portal, it checks if the click is outside `GetPortalBounds()`
3. Matching portals are collected first (to avoid modifying the tree during iteration)
4. `DismissRequested` fires on each, then `RemovePortal()` removes it
5. Normal click processing continues — the click is **not** consumed

**Window deactivation:**
1. When the window loses focus (another window is activated, or the user clicks the desktop)
2. All portals with `DismissOnOutsideClick = true` are dismissed immediately
3. `DismissRequested` fires before removal, same as outside-click dismissal

### Defaults

- `IHasPortalBounds.DismissOnOutsideClick` defaults to `false` (default interface implementation)
- `PortalContentBase.DismissOnOutsideClick` is a settable property, also defaulting to `false`
- Existing portals are completely unaffected unless they explicitly opt in

## Lifecycle Management

1. **Create** portal content and set bounds
2. **Attach** to window with `CreatePortal()`
3. **Interact** — mouse and keyboard events flow through the portal
4. **Remove** with `RemovePortal()` when dismissed
5. **Cleanup** — call `ClearChildren()` to dispose child controls

Always remove portals before disposing the owner control. Portals that outlive their owner will continue rendering but won't receive keyboard input.

## Quick Reference

| Component | Location | Purpose |
|-----------|----------|---------|
| `Window.CreatePortal()` | `Window.cs` | Create a portal overlay |
| `Window.RemovePortal()` | `Window.cs` | Remove a portal overlay |
| `PortalContentBase` | `Controls/PortalContentBase.cs` | Abstract base for portal content |
| `PortalContentContainer` | `Controls/PortalContentContainer.cs` | Container for child controls in portals |
| `IHasPortalBounds` | `Layout/IHasPortalBounds.cs` | Interface for portal bounds and dismiss opt-in |
| `PortalPositioner` | `Layout/PortalPositioner.cs` | Smart positioning with flip/clamp |
| `PortalPositionRequest` | `Layout/PortalPositioner.cs` | Positioning request record |
| `PortalPositionResult` | `Layout/PortalPositioner.cs` | Positioning result record |
| `PortalPlacement` | `Layout/PortalPositioner.cs` | Placement direction enum |

---

## See Also

- [DOM Layout System](DOM_LAYOUT_SYSTEM.md) — Layout pipeline that portals integrate with
- [Controls Reference](CONTROLS.md) — All available controls that can be hosted in portals
- [Rendering Pipeline](RENDERING_PIPELINE.md) — How portal layers compose with normal rendering
