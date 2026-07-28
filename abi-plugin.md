# ABI-Agnostic Plugins — Design Spec

**Status:** Draft / proposal
**Scope:** Native (unmanaged) plugins that contribute **services only**.
**Target:** `SharpConsoleUI` (net8.0 / net9.0 / net10.0, `IsAotCompatible=true`).

---

## 1. Summary

Today every `SharpConsoleUI` plugin is a managed .NET type implementing `IPlugin`. This
spec adds a second **source** of plugin contributions: a native shared library
(`.so` / `.dll` / `.dylib`) written in any language with a C ABI (Rust, C, Zig, …).

A native library is exposed to the framework as a **pure logic engine**. The managed
framework remains the **sole owner of rendering, layout, input, and the window system**.
The native side never sees a .NET type, namespace, or the `ConsoleWindowSystem`. It
implements four `extern "C"` functions and speaks a small, fixed JSON vocabulary.

The design is **additive and non-breaking**: no existing interface, signature, or caller
changes. A native library is wrapped in a managed container that *is* an `IPlugin`, so the
existing `PluginStateService.LoadPlugin(IPlugin)` path handles it unchanged.

---

## 2. Goals / non-goals

**Goals**

- Let unmanaged libraries contribute **services** (`IPluginService`) with zero shared .NET types.
- Preserve the existing managed plugin system exactly (themes, controls, windows, actions, services).
- Preserve the public service API: `Execute(string, Dictionary<string, object>?) → object?`.
- Stay **NativeAOT-clean** — no reflection-based serialization, no `[RequiresDynamicCode]`,
  no new dependency that trips the `aot` CI gate.

**Non-goals (explicitly out of scope)**

- Native **controls**, **windows**, **action providers**, or **themes**. See §11.
- Hot-reload / unloading of native libraries (mirrors the managed limitation).
- A cross-language rendering / draw protocol.

---

## 3. Design principles

| Principle | Consequence |
| :--- | :--- |
| Managed owns the UI; native only "thinks" | Native contributes callable operations, never pixels. |
| Additive, non-breaking | Reuse `IPlugin` / `IPluginService` / `PluginStateService` untouched. |
| Don't probe, don't offer | No ABI export exists for controls/windows/actions/themes, so a native author cannot offer them and the container returns empty for them. |
| AOT is a hard requirement | Marshalling is manual `Utf8JsonWriter`/`Utf8JsonReader` over a closed type vocabulary — no reflection, no source-gen context, no Newtonsoft. |

---

## 4. Architecture

Three layers:

| Layer | Responsibility | Language | Type |
| :--- | :--- | :--- | :--- |
| **Brain** | Pure logic | Rust / C / Zig | C-compatible library exporting 4 functions |
| **Shim** | Bridge / marshalling | C# | `NativeServiceShim : IPluginService` (generic, one class for all native services) |
| **Loader** | Registration | C# | `NativePluginContainer : PluginBase` + a discovery entry point |

### Container pattern

`NativePluginContainer` implements `IPlugin` (via `PluginBase`). It loads the native
library, probes it once, and materializes one `NativeServiceShim` per described service.

- `GetServicePlugins()` → the native service shims.
- `GetThemes()`, `GetControls()`, `GetWindows()`, `GetActionProviders()` → **empty** (`PluginBase` defaults).

Because the container *is* an `IPlugin`, it is loaded through the existing, unmodified path:

```csharp
windowSystem.PluginStateService.LoadPlugin(new NativePluginContainer("./libmyplugin.so"));
```

and consumed through the existing, unmodified retrieval API:

```csharp
var svc = windowSystem.PluginStateService.GetService("MyService");
long n  = (long)svc!.Execute("Compute", new Dictionary<string, object> { ["x"] = 21L })!;
```

### End-to-end flow

```
Host (managed)
  → IPluginService.Execute("op", dict)         // unchanged public contract
  → NativeServiceShim (managed)
       validate dict against probed metadata
       Utf8JsonWriter → args JSON               // per closed vocabulary, no reflection
  → plugin_invoke("op", argsJson)  [P/Invoke]
  → Brain (unmanaged): returns result JSON envelope
  → NativeServiceShim (managed)
       copy native string → managed, then plugin_free
       Utf8JsonReader → object? per declared ReturnType
  → Host (managed): object?
```

---

## 5. The native ABI

A native plugin exports exactly four C functions. Names are fixed; symbol lookup is by name.

```c
/* ABI handshake. Return the ABI version this library was built against.
   Checked BEFORE plugin_describe; a mismatch aborts the load. */
int32_t     plugin_abi_version(void);

/* Self-description. Return a UTF-8 JSON manifest (see §6).
   The returned pointer is owned by the library and released via plugin_free. */
const char* plugin_describe(void);

/* Invoke a named operation. `op` and `args_json` are host-owned, valid for the call.
   Return a UTF-8 JSON result envelope (see §9), owned by the library. */
const char* plugin_invoke(const char* op, const char* args_json);

/* Release a string previously returned by plugin_describe / plugin_invoke. */
void        plugin_free(const char* ptr);
```

Managed side uses **`[LibraryImport]`** (source-generated, trim/AOT-clean UTF-8 marshalling).
The repository already ships native interop (`Controls/Terminal/PtyNative.cs` via `[DllImport]`);
`[LibraryImport]` is the AOT-preferred form for this new path.

---

## 6. Manifest format (`plugin_describe`)

A single `services[]` section. The manifest is, in effect, a wire-format
`IReadOnlyList<ServiceOperation>` per service — it rehydrates directly into the framework's
existing metadata types (`ServiceOperation`, `ServiceParameter`).

```json
{
  "abiVersion": 1,
  "services": [
    {
      "name": "MathService",
      "description": "Arithmetic helpers.",
      "operations": [
        {
          "name": "Add",
          "description": "Adds two integers.",
          "parameters": [
            { "name": "a", "type": "i64", "required": true },
            { "name": "b", "type": "i64", "required": true }
          ],
          "returnType": "i64"
        }
      ]
    }
  ]
}
```

Mapping to framework types:

- service `name`/`description` → `IPluginService.ServiceName` / `Description`
- `operations[]` → `GetAvailableOperations()` returning `ServiceOperation(Name, Description, Parameters, ReturnType?)`
- `parameters[]` → `ServiceParameter(Name, Type, Required, DefaultValue?, Description?)`
- `returnType` omitted / `null` → a void operation (`ReturnType == null`)

---

## 7. Wire type vocabulary

The manifest declares types as **strings**. `ServiceParameter.Type` / `ServiceOperation.ReturnType`
are `System.Type`, so the shim maps each wire type to a CLR type and a marshalling rule. The set
is **closed** — this table *is* the ABI contract. Keep it small; every addition is marshalling
code on both sides.

| Wire type | `System.Type` | JSON representation |
| :--- | :--- | :--- |
| `i64` | `long` | number |
| `f64` | `double` | number |
| `bool` | `bool` | boolean |
| `string` | `string` | string (UTF-8) |
| `bytes` | `byte[]` | base64 string |
| `i64[]` / `f64[]` / `bool[]` / `string[]` | array types | JSON array |
| `json` | `System.Text.Json.JsonElement` | arbitrary JSON (escape hatch for nested objects) |

`void` (or an omitted `returnType`) denotes no return value.

---

## 8. Marshalling & AOT

- The shim **never** calls `JsonSerializer.Serialize(object)` — that is the reflection path that
  breaks NativeAOT. It walks known-typed values with `Utf8JsonWriter` and reads results with
  `Utf8JsonReader`, dispatching on the **declared** wire type (from the manifest), not on the
  runtime type of a boxed value.
- No `JsonSerializerContext` / source generator is required, because operation *shapes* live in
  *data*, not in compile-time types.
- No `Newtonsoft.Json` — it is `[RequiresUnreferencedCode]` + `[RequiresDynamicCode]` and would
  fail the `aot` CI gate.
- Result: the pipeline is analyzer-clean and preserves `IsAotCompatible=true`.

---

## 9. Error handling

`plugin_invoke` returns a **result envelope**:

```json
{ "ok": true,  "value": 42 }
{ "ok": false, "error": "unknown operation 'Frobnicate'" }
```

- `ok: false` → the shim throws `InvalidOperationException(error)`, matching the documented
  contract of `IPluginService.Execute` (unknown operation / invalid parameters).
- A null pointer or unparseable envelope → `InvalidOperationException` with a diagnostic message.

---

## 10. Memory ownership

Every string returned by `plugin_describe` / `plugin_invoke` is **native-allocated**. The shim:

1. Copies the UTF-8 bytes into a managed `string`.
2. Calls `plugin_free(ptr)` in a `finally` block — always, including on exception.

The host never allocates strings the native side frees, and vice versa. Args passed *into*
`plugin_invoke` are host-owned and valid only for the duration of the call; the library must copy
anything it retains.

---

## 11. Out of scope — and why

Native plugins contribute **services only**. The ABI has **no export** for the following, so a
native author cannot offer them and the container returns empty:

- **Controls / Windows.** `IWindowControl` is a per-frame render + input + layout + focus surface.
  Bridging it means a serialized draw/input protocol (a binary command buffer, not JSON-per-frame),
  which reintroduces the coupling and hot-path costs this design deliberately avoids. Separate,
  larger effort; not planned here.
- **Action providers.** Structurally identical to services (`ActionDescriptor` ≅ `ServiceOperation`,
  `ExecuteAction` ≅ `Execute`), so the same machinery *could* host them later at low cost — but they
  typically act on the UI, which would require a host-callback surface. Deferred.
- **Themes.** `ITheme` is pure data but ~150 framework-specific, still-growing slots — the opposite
  of a small stable vocabulary, and a theme has no logic that benefits from running native. If native
  plugins should ship themes, the right vehicle is a **declarative theme file** (seed palette + mode +
  a few overrides, expanded managed-side by `PaletteThemeGenerator`), loaded from disk — **not** the
  P/Invoke ABI. Tracked separately; out of scope here.

---

## 12. Backward compatibility

- No change to `IPlugin`, `IPluginService`, `PluginStateService`, or any caller.
- `Execute(string, Dictionary<string, object>?) → object?` is unchanged; the dictionary is the
  compatibility seam. Native services validate boxed dictionary values against declared types (§13).
- Managed plugins are entirely unaffected. The change is purely additive.

---

## 13. Open questions

1. **Parameter validation — strict vs lenient.** When a caller passes a boxed value whose type
   doesn't match the declared wire type (e.g. `"42"` for an `i64`): reject with
   `InvalidOperationException` (**strict**, recommended — the manifest gives us the type), or attempt
   `Convert.ChangeType` (**lenient**). Strict cannot be quietly relaxed later without risk, so decide now.
2. **Unified loader facade.** Ship a single discovery entry point (point at a folder → load managed
   assemblies *and* native libraries, funnel both through `LoadPlugin(IPlugin)`) in v1, or leave native
   loading as an explicit `LoadPlugin(new NativePluginContainer(path))` call for now?

---

## 14. Appendix — minimal native plugin (sketch)

```c
// libmath.c  —  compile to libmath.so
#include <string.h>
#include <stdlib.h>

int32_t plugin_abi_version(void) { return 1; }

const char* plugin_describe(void) {
    return strdup(
      "{\"abiVersion\":1,\"services\":[{"
      "\"name\":\"MathService\",\"description\":\"Arithmetic helpers.\","
      "\"operations\":[{\"name\":\"Add\",\"description\":\"Adds two integers.\","
      "\"parameters\":[{\"name\":\"a\",\"type\":\"i64\",\"required\":true},"
      "{\"name\":\"b\",\"type\":\"i64\",\"required\":true}],\"returnType\":\"i64\"}]"
      "}]}");
}

const char* plugin_invoke(const char* op, const char* args_json) {
    // parse args_json, compute, return envelope (real code uses a JSON lib)
    if (strcmp(op, "Add") == 0)
        return strdup("{\"ok\":true,\"value\":42}");
    return strdup("{\"ok\":false,\"error\":\"unknown operation\"}");
}

void plugin_free(const char* ptr) { free((void*)ptr); }
```

Managed usage — indistinguishable from a managed service:

```csharp
windowSystem.PluginStateService.LoadPlugin(new NativePluginContainer("./libmath.so"));

var math = windowSystem.PluginStateService.GetService("MathService");
long sum = (long)math!.Execute("Add", new Dictionary<string, object>
{
    ["a"] = 21L,
    ["b"] = 21L
})!;
```
