# Breaking Changes

Newest first. Each entry lists what changed and how to migrate.

## Unreleased — Panel refactor (bordered MarkupControl + Panel as CollapsiblePanel)

### `MarkupControl` gains an optional border (additive, non-breaking)
`MarkupControl` now has `Border`, `BorderColor`, `Header`, `HeaderAlignment`, `UseSafeBorder`,
`Padding` (default: no border). Build via `Controls.Markup("text").WithBorder(BorderStyle.Rounded)`.
Use this for a bordered text box without a container. Existing markup behavior is unchanged when no
border is set.

### `PanelControl` is now a non-collapsible `CollapsiblePanel`
- `PanelControl` derives from `CollapsiblePanel` (was a standalone leaf/facade). Therefore
  `x is CollapsiblePanel` is now **true** for a `PanelControl`.
- The collapse API (`Collapsible`, `IsExpanded`, `Toggle()`, `Expand()`, `Collapse()`) is sealed to
  no-ops on `PanelControl` — a panel is permanently non-collapsible and expanded.
- `PanelControl.Content` / `SetContent(string)` now host a single **borderless `MarkupControl` child**
  and **replace all** existing body content. The panel itself draws the border. (Previously the
  content was a self-painted string / a wrapped inner control.)
- `Controls.Panel().Build()` still returns `PanelControl`; existing builder calls
  (`.WithContent`, `.Rounded()`, `.WithHeader`, `.WithPadding`, `.AddControl`, etc.) are unchanged.
- `PanelControl.BackgroundColor` / `ForegroundColor` remain `Color?` (nullable); `null` means
  "inherit from container/theme". `panel.BackgroundColor ?? fallback` still compiles.
- Mouse semantics (container contract): a click on **passive** panel content bubbles to the panel
  (fires `PanelControl.MouseClick`); a click on **interactive** content (a `[link]` or a child with its
  own handler) is consumed by that content.

### Migration
- No change is needed for the common `Controls.Panel().WithContent(...)` text-box usage — it still
  compiles and renders. (Verified: ServerHub, cratis-cli, and dotnet-skills build unchanged.)
- If you did a type check that assumed `PanelControl` is NOT a `CollapsiblePanel`, update it.
- If you called `Toggle()`/`Collapsible` on a `PanelControl` expecting collapse, use `CollapsiblePanel`
  directly for collapsible behavior.

### Packaging note (not an API change)
The library's `Spectre.Console` dependency floor moved to 0.55.2. Consumers pinning an older
`Spectre.Console` (e.g. 0.55.0) may see a NuGet restore floor (NU1605) and should bump to >= 0.55.2.
This is a restore-time packaging detail, not a source/API break.
