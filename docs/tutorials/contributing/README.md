# Contributing to SharpConsoleUI

Want to contribute *to* the framework — not just build apps with it? This is your starting point. These tutorials walk you through building real, mergeable additions end to end, so you can go from "I have never seen this codebase" to an open pull request without a multi-day reverse-engineering detour.

If you are here to build an *application*, you want the [app tutorials](../README.md) instead.

## Areas open for contribution

Everything below is **additive only — no breaking changes** (see [CONTRIBUTING.md](../../../CONTRIBUTING.md)). SharpConsoleUI has real NuGet users, so we add new APIs rather than change existing ones. Within that rule, **PRs are welcome — no need to ask first.**

**🟢 Dive right in**
- **New controls** — especially composites (arrange existing controls). The biggest, safest surface.
- **Themes** — a new theme is essentially a palette; low risk, immediate visual payoff.
- **Builders** — a new control needs a `Controls.<Name>()` builder.
- **Docs, examples, and DemoApp pages** — great first PRs.

**🟡 Welcome — just stay strictly additive** (these touch behavior other apps rely on, so add new variants; never change existing ones)
- **Dialogs & flows** — a new dialog primitive or flow step alongside the existing ones.
- **Syntax highlighters** — a new language highlighter.
- **Markup tags / parsing extensions** — a new inline tag.

## Pick a tutorial

| Tutorial | You'll build | Best if… |
|---|---|---|
| [1. Composite Controls](01-composite-controls.md) | A control that arranges existing controls | You want the safest first contribution — you can't break the render engine. |
| [2. Adding a Control](02-adding-a-control.md) | `BadgeControl` — a primitive from scratch | You want to learn the real machinery: layout, the reactive property contract, color roles. |
| [3. Dialogs](03-dialogs.md) | `Dialogs.PickAsync<T>` — a new modal | You want to extend the dialog system additively. |

**New to the internals?** Start with **[Composite Controls](01-composite-controls.md)** — it's the lowest-risk path.

## Reference material

These tutorials link out to the deeper reference docs where you need them:
- [DOM Layout System](../../DOM_LAYOUT_SYSTEM.md) — Measure → Arrange → Paint
- [Threading & Async](../../THREADING_AND_ASYNC.md) — the UI-thread contract
- [Themes](../../THEMES.md) and Color Roles
- [Patterns](../../patterns.md) — the reactive property contract and conventions
- [Builders](../../BUILDERS.md) — the fluent builder pattern
- [Flows](../../FLOWS.md) and [Dialogs](../../DIALOGS.md) — the dialog subsystem
- [Code Quality](../../CODE_QUALITY.md) — the standards your PR is reviewed against

---

[Back to Tutorials](../README.md)
