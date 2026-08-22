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
implements five `extern "C"` functions — plus an optional sixth to receive an event channel
(§17) — and speaks a small, fixed JSON vocabulary.

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
| **Brain** | Pure logic | Rust / C / Zig | C-compatible library exporting 5 functions (+1 optional) |
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

A native plugin exports five required C functions, and may export a sixth to receive the host's
event channel (§17). Names are fixed; symbol lookup is by name.

```c
/* ABI handshake. Return the ABI version this library was built against.
   Checked BEFORE plugin_describe; a mismatch aborts the load. */
int32_t     plugin_abi_version(void);

/* Self-description. Return a UTF-8 JSON manifest (see §6).
   MUST be a freshly allocated string: the host copies it out and hands the pointer
   straight back to plugin_free. Returning a static/const literal is a bug — the
   library's own free() will be called on memory it does not own. */
const char* plugin_describe(void);

/* Invoke a named operation. `op` and `args_json` are host-owned, valid for the call.
   Return a UTF-8 JSON result envelope (see §9), owned by the library. */
const char* plugin_invoke(const char* op, const char* args_json);

/* Release a string previously returned by plugin_kind / plugin_describe / plugin_invoke. */
void        plugin_free(const char* ptr);

/* Pre-load probe. Return the UTF-8 set of kinds this library declares, newline-separated
   (see §6.1). Freshly allocated; released via plugin_free.

   This is the ONLY function callable before plugin_abi_version — it is what tells a host
   whether a version check is even meaningful. A file that does not export it is not a
   ConsoleEx plugin, which is the whole point: the host learns that by resolving a symbol,
   without ever executing plugin_invoke. */
const char* plugin_kind(void);

/* OPTIONAL, sixth export. Receives the host's event channel — the plugin's only way to talk
   back (§17). Called once, after the manifest is validated and before any plugin_invoke.
   A library that does not export it is fully valid; it simply raises no events. */
typedef void (*host_event_fn)(void* ctx, const char* service, const char* name,
                              const char* payload_json);
void        plugin_set_host(host_event_fn on_event, void* ctx);
```

### 5.0 How the managed side binds these symbols

**Not `[LibraryImport]`, and not `[DllImport]`.** Both bind a *compile-time* library name to
static methods, and this design does neither thing: it loads arbitrary user-supplied paths, several
libraries at once, every one exporting the same five symbol names. A static `plugin_invoke` import
has no way to dispatch to `libmath.so` versus `libgit.so`.

The mechanism is the one §5.1 step 2 already implies — resolve per library, at runtime:

```csharp
nint lib = NativeLibrary.Load(path);
nint sym = NativeLibrary.GetExport(lib, "plugin_invoke");

// Held per container. AOT-clean: a function pointer, not a delegate, so nothing is generated
// at runtime and nothing needs rooting.
private readonly delegate* unmanaged[Cdecl]<byte*, byte*, byte*> _invoke =
    (delegate* unmanaged[Cdecl]<byte*, byte*, byte*>)sym;
```

**Every string-returning export is typed as `byte*`, never `string`.** This is not a style
preference — it is the difference between working and corrupting the heap. A `string` return with
`StringMarshalling.Utf8` makes the marshaller free the returned pointer with **its own allocator**
after copying, which both bypasses `plugin_free` and calls `CoTaskMemFree` on memory the plugin
allocated with `malloc`. That is precisely the mismatched-allocator fault §10 exists to prevent,
introduced by the marshalling layer rather than by the plugin.

So the shim copies and frees explicitly, exactly as §5.1 phase 4 describes:

```csharp
byte* p = _invoke(opUtf8, argsUtf8);
if (p is null) throw new InvalidOperationException(/* §9.3: a plugin must always return an envelope */);
try     { json = Marshal.PtrToStringUTF8((nint)p)!; }   // copy out
finally { _free(p); }                                    // the plugin's own allocator
```

Arguments go the other way as caller-owned UTF-8 — a stack buffer or a pinned array, valid only
for the duration of the call (§10).

`Controls/Terminal/PtyNative.cs` uses `[DllImport]` against a fixed library and is not a
precedent for this path; it has one library, known at compile time.

---

## 5.1 The protocol, end to end

The whole contract is four calls in a fixed order. There is no state on the host side beyond
what one `NativePluginContainer` holds, and no callback from native back into managed.

### Phase 1 — Load handshake

Happens once, in the `NativePluginContainer` constructor. It either completes fully or throws;
there is no partially-loaded plugin.

```
1. NativeLibrary.Load(path)                     → fail: DllNotFoundException, propagated
2. resolve the 5 symbols by name                → any missing: EntryPointNotFoundException
3. const char* k = plugin_kind()                 → the probe (§6.2); plugin_free(k)
4. int v = plugin_abi_version()                 → the handshake
5. if (v != AbiVersion) throw                   → refuse the load, do not call further
6. const char* json = plugin_describe()          → the manifest
7. parse + validate manifest, then plugin_free(json)
8. probe kinds == union of manifest kinds?       → mismatch: throw (§6.2)
9.  resolve plugin_set_host (OPTIONAL)           → absent: the plugin raises no events
10. materialize one NativeServiceShim per service

Then later, in Initialize(windowSystem) — NOT in the constructor:

11. plugin_set_host(on_event, ctx)               → the channel, once there is a window
                                                    system to deliver events to (§17.3)
```

Step 3 is redundant when the caller already probed via `NativePluginProbe.TryReadKinds` — but
the container cannot assume it did, and step 8 needs the probe's answer to verify the manifest
agrees with it. The cost is one extra string round-trip per load, once.

**Version check precedes everything except the probe.** `plugin_abi_version` is called before
`plugin_describe`, so a library built against a future ABI is rejected before the host tries
to parse a manifest whose shape it may not understand. Its signature can never change — it and `plugin_kind`
are the only things both sides agree on before agreeing on anything else. (`plugin_kind` comes
first only because it answers a cruder question: *is this a plugin at all?* It returns a string
under the ownership rule of §10, which is why the probe needs `plugin_free` resolvable too.)

The host's `AbiVersion` is a single constant (`1` for this spec). The rule is deliberately
**exact equality, not a floor**: a v1 host cannot know whether a v2 manifest carries a field
whose absence would silently change behaviour. When v2 exists, a host may choose to accept
`{1, 2}` explicitly, per version, with code that knows both shapes.

**Cleanup, and the refcount.** `NativeLibrary.Load` refcounts by path, which has three
consequences the container must handle rather than discover:

- **A failed load frees what it loaded.** If any step after step 1 throws, the container calls
  `NativeLibrary.Free` before propagating. The library's initializers have already run — that cannot
  be undone — but a rejected file does not stay mapped for the life of the process.
- **`TryReadKinds` frees too.** Probing a directory must not leave every candidate `.so` mapped, so
  the probe loads, reads, and frees within the call.
- **Loading the same path twice shares one library.** Two containers over one file get one mapping,
  so the second `plugin_set_host` **overwrites the first callback** and the first container stops
  receiving events. The container therefore refuses a path already loaded, throwing rather than
  silently producing a half-working pair. A host wanting two instances of the same plugin needs two
  files.

**What the load throws.** Each failure names its cause, so a host can tell "not our file" from
"our file, built wrong": a missing library or unresolvable dependency propagates
`DllNotFoundException`; an absent export gives `EntryPointNotFoundException`; and every other
refusal — ABI version mismatch, unparseable or invalid manifest, probe/manifest disagreement —
is `InvalidOperationException` with a message naming the library and the reason. There is no
custom exception type: a host catching a load failure almost always treats them alike, and the
three that differ are already distinguished by the framework's own types.

Validation at step 6 rejects a manifest that parses but is unusable — duplicate service names
within one library, duplicate operation names within a service, a parameter whose `type` is
outside the closed vocabulary (§7), or an operation whose `returnType` is outside it. Failing
here means an authoring mistake surfaces at load, not at the first call in production.

### Phase 2 — Discovery / registration

The container is an `IPlugin`, so it registers through the existing path with no new API:

```csharp
windowSystem.PluginStateService.LoadPlugin(new NativePluginContainer("./libmath.so"));
```

`PluginStateService.LoadPlugin` calls `plugin.Initialize(windowSystem)`, then walks
`plugin.GetServicePlugins()` and keys each into its service map by `ServiceName`
(`_services[servicePlugin.ServiceName] = servicePlugin`). Two consequences worth stating,
because both are inherited behaviour rather than choices made here:

- **Registration is last-wins, not an error.** Loading a second plugin that declares an
  existing `ServiceName` silently replaces the first. That is the framework's existing
  semantics for managed plugins; native plugins do not change it. A host that cares should
  check `HasService(name)` before loading.
- **`GetServicePlugins()` is the override that matters.** `PluginBase` also exposes an
  obsolete `GetServices()` returning the legacy `PluginService` type; `NativePluginContainer`
  overrides only `GetServicePlugins()` and inherits the empty default for everything else —
  themes, controls, windows, action providers. That inherited emptiness *is* the "don't probe,
  don't offer" principle: there is no ABI export for those, so the container cannot accidentally
  claim them.

Discovery from the caller's side is then entirely the existing, unmodified API — a native
service is indistinguishable from a managed one:

```csharp
var svc = windowSystem.PluginStateService.GetService("MathService");   // IPluginService?
foreach (var op in svc!.GetAvailableOperations())                       // manifest, rehydrated
    Console.WriteLine($"{op.Name}({string.Join(", ", op.Parameters.Select(p => p.Name))})");
```

`GetAvailableOperations()` returns the `ServiceOperation` records built from the manifest at
load. Nothing crosses the ABI to answer it — the metadata was captured once, in phase 1.

### Phase 3 — The call

```csharp
long sum = (long)svc.Execute("Add", new Dictionary<string, object> { ["a"] = 21L, ["b"] = 21L })!;
```

Inside `NativeServiceShim.Execute`:

```
1. look up the ServiceOperation by name       → unknown: InvalidOperationException
2. validate the dictionary against it         → §13.1 strict rule; reject before crossing
3. Utf8JsonWriter → args JSON                 → dispatch on the DECLARED wire type,
                                                 never on the boxed runtime type
4. plugin_invoke(op, argsJson)                → the only P/Invoke on the hot path
```

Step 3 is what keeps the pipeline AOT-clean. The writer switches on the manifest's declared
type and calls the corresponding typed `Write*` — it never asks a boxed `object` what it is
and never reaches `JsonSerializer.Serialize(object)`, which is the reflection path that breaks
NativeAOT.

Step 2 matters for a subtler reason: **validation happens before marshalling**, so a bad call
never reaches native code. The native side may assume its args JSON is well-formed and
type-correct, which is what lets a native author write a plugin without defensive parsing in
every operation.

### Phase 4 — The return

Native returns a UTF-8 JSON envelope (§9), and the shim:

```
1. copy the UTF-8 bytes into a managed string
2. plugin_free(ptr)                           ← in a finally, always, including on throw
3. parse the envelope per §9.1
     ok:false → InvalidOperationException(error)
     ok:true  → read "value" per the operation's declared ReturnType
4. return object?  (null for a void operation)
```

Step 2 before step 3 is deliberate: the native buffer is released as soon as its bytes are
copied, so a malformed envelope throws *after* the memory is already reclaimed rather than
leaking it on the error path.

The envelope grammar, the meaning of each field, and the complete table of failure modes live in
**§9** and are not restated here — one normative home, so the two cannot drift.

**`args_json` is never NULL.** An operation with no parameters receives `"{}"` — not NULL, not an
empty string — so a plugin may dereference it unconditionally. The same holds for `op`. Making this
normative removes a defensive branch from every operation in every plugin.

Ownership is strictly one-directional and stated once: **every string crossing the boundary is
allocated by the side that produced it and freed by that same side's allocator.** Native
allocates what it returns; the shim copies it out and hands the pointer straight back via
`plugin_free`. The host's `op` and `args_json` are valid only for the duration of the call, so
a library retaining either must copy it.

### Concurrency — the host does not serialize invokes

**`plugin_invoke` may be called concurrently, from multiple threads, on the same library.** The
shim takes no lock on the invoke path; a plugin that cannot tolerate concurrent calls must say so
in its description and its callers must honour that.

This is normative rather than incidental, because §17.4 depends on it. "Cancel is just an
operation" only works if a second `plugin_invoke` can run while the first is still blocked inside a
long `Scan` — a defensive lock in the shim would silently turn `Cancel` into "wait for Scan to
finish", which is the opposite of cancelling. The absence of that lock is a feature, and the cost
of it lands where §17.5 says it lands: on the plugin author.

### What the protocol deliberately lacks

- **No host callbacks that return a value.** Native raises fire-and-forget events (§17) and can
  never wait on the host, which is what keeps the boundary auditable: nothing re-enters, nothing
  blocks, and no exception crosses.
- **No async.** `plugin_invoke` is synchronous. A long-running native operation blocks its
  caller, exactly as a long-running managed service would; the host is free to call it from a
  background thread and marshal results back with `EnqueueOnUIThread` (CLAUDE.md Rule 13).
- **No streaming or partial results.** One call, one envelope.
- **No unload.** Mirrors the managed plugin system's existing limitation (§2).

## 6. Manifest format (`plugin_describe`)

A single `services[]` section. The manifest is, in effect, a wire-format
`IReadOnlyList<ServiceOperation>` per service — it rehydrates directly into the framework's
existing metadata types (`ServiceOperation`, `ServiceParameter`).

```json
{
  "abiVersion": 1,
  "plugin": {
    "name": "Math",
    "version": "1.0.0",
    "author": "someone",
    "description": "Arithmetic helpers as a native plugin."
  },
  "services": [
    {
      "name": "MathService",
      "kind": "calculator",
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

### 6.1 `kind` — the user-declared contract identifier

Each service may declare a **kind**: a free-form string naming the contract it satisfies.

```json
{
  "name": "GitService",
  "kind": "scm",
  "description": "Source-control operations.",
  "operations": [ … ]
}
```

**The framework never interprets a kind.** It does not validate it against a registry, does
not reserve names, and attaches no behaviour to any value. The vocabulary belongs entirely to
the consumer: an app that understands `"scm"` decides what that obliges a plugin to implement.
This is deliberate — the moment the framework assigns meaning to a kind, it owns a taxonomy it
must version and defend forever.

`kind` is optional. A service that omits it has `Kind == null` and is simply not discoverable
by kind, which keeps the field additive: existing manifests remain valid.

**Why a string and not a type.** The obvious managed analogue — `GetService<T>()` — already
exists and is `[Obsolete("Type-based service lookup will be removed in a future version")]`,
because it cannot work reflection-free. It certainly cannot work for a native plugin, which
has no CLR type at all. A string is the only identifier that crosses a C ABI, and it is the
one the consumer can define without the framework's participation.

**Where the kind lives managed-side.** Not on `IPluginService` — adding a member there would
break every external implementer, which the no-breaking rule forbids. The kind lives on the
shim and is exposed through a separate optional interface:

```csharp
/// <summary>A service that declares the contract ("kind") it satisfies, for discovery by
/// consumers that group plugins by capability rather than by name.</summary>
public interface IKindedService
{
    /// <summary>The consumer-defined contract identifier, or null when the service declares none.</summary>
    string? Kind { get; }
}
```

`NativeServiceShim` implements it. Managed services may implement it too — the feature is not
native-only, and nothing stops a managed plugin from participating in the same discovery. A
service that does not implement it is treated as `Kind == null`.

Discovery is then a filter over the existing registry, added as new API rather than changed API:

```csharp
// All services declaring a given kind, native and managed alike.
IReadOnlyList<IPluginService> GetServicesByKind(string kind);
```

### 6.2 `plugin_kind` — the pre-load probe

`plugin_kind()` returns the **union of the kinds declared by the library's services**,
newline-separated, in any order:

```
scm
diff-provider
```

A library whose services declare no kinds returns an empty string. Duplicates are collapsed —
three services of kind `"scm"` yield one `scm` line.

**Union, not a primary kind.** One `.so` may host several services of different kinds, so a
single value would force a primary/secondary hierarchy that means nothing to a consumer asking
"does this file contain anything of kind `scm`?" The union answers exactly that question and
needs no hierarchy to explain.

**Ordering.** `plugin_kind` is the one function callable before `plugin_abi_version`, because
it is what establishes whether the file is a plugin at all. It therefore needs `plugin_free`
resolvable too — the probe resolves **two** symbols, not one, and both must be present for the
file to be considered a plugin.

**Consistency is enforced at load.** After `plugin_describe`, the container compares the
probe's set against the union of the manifest's kinds. A mismatch is a **load error**, not a
precedence question: two sources of truth that can silently diverge is how a plugin ends up
filtered into one bucket and behaving like another. The manifest is the authority; the probe
is a fast path that must agree with it.

**The helper.**

```csharp
public static class NativePluginProbe
{
    /// <summary>
    /// Reads the kinds a native library declares, without loading it as a plugin.
    /// Returns false — never throws — when the file is missing, is not a loadable native
    /// library, or does not export plugin_kind/plugin_free, i.e. when it is not a
    /// ConsoleEx plugin.
    /// </summary>
    /// <remarks>
    /// A true result means the file CLAIMS to be a ConsoleEx plugin of these kinds. It does
    /// NOT mean the plugin will load: the ABI version is not checked here, the manifest is
    /// not parsed, and the probe/manifest consistency check happens later, at load.
    ///
    /// <para>Not zero-trust. Loading a shared library runs its initializers (ELF .init_array,
    /// DllMain on Windows), so probing an untrusted file still executes some of its code. The
    /// probe is narrower than a full load — it never calls plugin_invoke — but it is not a
    /// sandbox. Do not point it at a directory you would not execute.</para>
    /// </remarks>
    public static bool TryReadKinds(string path, out IReadOnlyList<string> kinds);
}
```

Which makes the folder case a filter before any plugin is initialized:

```csharp
foreach (var file in Directory.EnumerateFiles(dir, "*.so"))
{
    if (!NativePluginProbe.TryReadKinds(file, out var kinds)) continue;  // not ours
    if (!kinds.Contains("scm")) continue;                                 // not wanted
    windowSystem.PluginStateService.LoadPlugin(new NativePluginContainer(file));
}
```

### 6.0 The `plugin` block

Required, and separate from the services it contains. `PluginBase.Info` is abstract and returns
`PluginInfo(Name, Version, Author, Description)`, so the container has to get those four values
from somewhere; deriving them from the filename would be guessing, and leaving them empty would put
an unnamed plugin in every diagnostic. All four fields are required strings — a manifest missing the
block, or any field in it, fails validation at load (§5.1 step 7).

`version` is a free-form string carried through to `PluginInfo.Version` unparsed. The framework does
not compare versions, order them, or attach meaning to their shape; that is the consumer's business,
exactly as with `kind` (§6.1).

Mapping to framework types:

- `plugin` block → `PluginBase.Info` → `PluginInfo(Name, Version, Author, Description)`
- service `name`/`description` → `IPluginService.ServiceName` / `Description`
- service `kind` (optional) → `IKindedService.Kind` on the shim; `null` when omitted (§6.1)
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

`json` is the one wire type that leaks the transport into caller code: because `Execute` takes
`Dictionary<string, object>`, a caller supplies a boxed `JsonElement` (or a `string` of valid
JSON — see §13.1) and receives a boxed `JsonElement` back. That is deliberate — it is the
escape hatch, and pretending otherwise would mean inventing a parallel object model. Prefer a
declared shape over `json` wherever the operation's arguments are actually fixed; reach for it
when they genuinely are not.

**`f64` cannot carry NaN or Infinity.** JSON has no representation for either, and
`Utf8JsonWriter.WriteNumberValue` throws rather than emitting something a parser would reject. A
plugin needing them must encode them itself — as a `string`, or inside a `json` value — and a shim
asked to write one throws `InvalidOperationException` naming the parameter, rather than letting a
`ArgumentException` surface from the writer.

**There is no null value.** `{"ok":true,"value":null}` is malformed for a non-void operation
(§9.1), so a `string`-returning `Find` cannot express "nothing found" as null. It must return an
empty string, report `ok:false` with an error, or declare `json` and return `null` inside it. This
is a deliberate limitation of a closed vocabulary with no nullable types, not an oversight.

`void` (or an omitted `returnType`) denotes no return value.

---

### 7.1 What a plugin actually has to parse

JSON appears in three roles, and they do not cost the same. Worth stating plainly, because "the
ABI is JSON" reads as a heavier requirement than it is:

| Role | Frequency | Who **parses** | Who **writes** |
| :--- | :--- | :--- | :--- |
| Manifest (`plugin_describe`) | once per load | host | plugin (a string literal) |
| Args (`plugin_invoke` in) | every call | **plugin** | host |
| Envelope (`plugin_invoke` out) | every call | host | plugin (`sprintf`-shaped) |

Only one cell is a real burden: **args parsing, plugin-side.** And it is far narrower than
"implement JSON," because by the time args arrive they are guaranteed to be:

- a **flat object** — no nesting, unless the operation declares a `json` parameter;
- drawn from the **closed vocabulary** of §7 — ten types, no surprises;
- **already validated** host-side against the manifest (§13.1) — present, correctly typed, no
  unknown keys.

So a plugin is not parsing arbitrary JSON. It is pulling a handful of known keys, of known
types, out of a flat object whose shape it declared itself. The C example's 15-line
`json_get_i64` is unglamorous but sufficient; a real plugin would use `cJSON` / `serde_json` /
`json` and write less.

**On `abiVersion` appearing twice.** The manifest's `abiVersion` and `plugin_abi_version()` state
the same fact, and §6.2 already rules that two sources of truth which can silently diverge is a load
error. The same rule applies here: the container compares them after parsing and a mismatch fails
the load. `plugin_abi_version()` stays the authority — it is checked before the manifest is parsed
at all, precisely so a future manifest shape is never handed to a host that cannot read it.

**The two exceptions, stated so they are not discovered the hard way:**

1. **The `json` wire type breaks this.** An operation declaring a `json` parameter receives
   arbitrary nested JSON and does need a real parser. That is the trade for the escape hatch —
   one type that changes a plugin's dependency footprint. Prefer declared shapes.
2. **`plugin_describe` is a hand-written JSON string literal** in every example here, and it is
   the most error-prone line in a plugin: a missing brace fails at load with a parse error and
   no compile-time help. Authors are encouraged to generate it — serialize a struct at build
   time, or emit it from the same declaration the operations are implemented from — rather than
   maintain it by hand.

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

## 9. The result envelope

`plugin_invoke` **always** returns a JSON object with an `ok` field. Never a bare value, never
`NULL`, never an empty string — success and failure share one channel, so there is no errno, no
out-parameter, and no exception crossing the boundary.

```json
{ "ok": true,  "value": 42 }
{ "ok": true }                                          // void operation
{ "ok": false, "error": "unknown operation 'Frobnicate'" }
```

### 9.1 Grammar

| Field | When | Type | Rule |
| :--- | :--- | :--- | :--- |
| `ok` | **always** | boolean | Required. Absent or non-boolean → the envelope is malformed (§9.3). |
| `value` | `ok: true`, non-void operation | per `returnType` | Required. Its JSON type must match the operation's declared `returnType` (§7). |
| `value` | `ok: true`, void operation | — | **Omitted.** `null` is also accepted and treated identically; any other value is malformed. |
| `error` | `ok: false` | string | Required, and should be non-empty. |

**Unknown fields are ignored.** A host encountering a field it does not recognise skips it
rather than failing. This is the forward-compatibility seam: a future ABI version may add
envelope fields without breaking a v1 host, and a plugin may carry diagnostic fields a host
does not read.

### 9.2 Reading `value`

The shim reads `value` **by the operation's declared `ReturnType`, not by inspecting the JSON**.
An `i64` operation reads a number as `long`; a `string` operation reads a string. This keeps
`Utf8JsonReader` on a typed path and makes the boxed `object?` predictable: the cast in
`(long)svc.Execute(...)` is safe because the manifest promised `i64`.

A mismatch — a JSON string where the manifest declared `i64` — is a **plugin bug**, and is
reported as `InvalidOperationException` naming the operation and both types, rather than
surfacing as a cast failure three frames up in the caller.

### 9.3 Every failure mode, and what the host does

| Native returns | Host behaviour |
| :--- | :--- |
| `{"ok":false,"error":"…"}` | `InvalidOperationException(error)` |
| `{"ok":false}` — no `error` | `InvalidOperationException` with a generated message naming the service and operation |
| `{"ok":true,"value":…}` matching `returnType` | the boxed value |
| `{"ok":true}` on a **void** operation | `null` |
| `{"ok":true}` on a **non-void** operation | `InvalidOperationException` — the plugin promised a value and did not send one |
| `{"ok":true,"value":…}` mismatching `returnType` | `InvalidOperationException` (§9.2) |
| `{"ok":true,"value":…}` on a **void** operation | ignored; returns `null`. A void operation's caller has nothing to receive it. |
| `NULL` pointer | `InvalidOperationException` — a plugin must always return an envelope |
| empty string, or not valid JSON | `InvalidOperationException` quoting a bounded prefix of what was returned |
| valid JSON but not an object (`42`, `[…]`, `"x"`) | `InvalidOperationException` — the envelope is an object |
| object without `ok` | `InvalidOperationException` — malformed envelope |
| `value` is not valid base64 for a `bytes` operation | `InvalidOperationException` — a value that does not match the declared type (§9.2) |

`plugin_describe` and `plugin_kind` returning `NULL` or invalid JSON are **load** failures, not call
failures: both throw `InvalidOperationException` from the container constructor (§5.1), naming the
library and which export misbehaved.

Every one of these throws `InvalidOperationException`, matching the documented contract of
`IPluginService.Execute`. The distinction between "the plugin reported a failure" and "the
plugin misbehaved" is carried in the **message**, not the exception type — a host catching
`Execute` should not have to distinguish them, and a plugin author reading the message should
immediately see which it was.

### 9.4 Why a plugin must never return `NULL`

It is the one failure mode with no diagnostic content: the host cannot tell a deliberate refusal
from a crashed allocator or a missing return path. Both worked examples that can fail this way
guard against it — the Python bridge substitutes an error envelope when its interpreter is dead,
and the Rust `out()` helper substitutes one when `CString::new` rejects an interior NUL. A C
plugin whose `malloc` fails cannot allocate an error envelope either, which is the awkward case.
Returning a stack buffer is use-after-return; returning a `const` literal is worse, because the host
hands that pointer to `plugin_free` and the plugin `free()`s storage it never allocated (§10). Both
are undefined behaviour.

The pattern that works is a **static sentinel envelope that `plugin_free` recognises and skips**:

```c
static const char OOM[] = "{\"ok\":false,\"error\":\"out of memory\"}";

const char* plugin_invoke(const char* op, const char* args_json) {
    char* buf = malloc(n);
    if (!buf) return OOM;              /* not freeable — see plugin_free */
    /* … */
}

void plugin_free(const char* p) {
    if (p == OOM) return;              /* the host cannot know; the plugin must */
    free((void*)p);
}
```

§10's ownership rule is unchanged: the plugin still frees what the plugin returned. It has simply
chosen a pointer whose free is a no-op — and only the plugin can make that choice, because only the
plugin knows which pointers are its own.

If a library truly cannot manage even that, `NULL` is the honest answer and the host reports it as
such (§9.3).

---

## 10. Memory ownership

Every string returned by `plugin_describe` / `plugin_invoke` is **native-allocated, per call**
— including `plugin_describe`, which is called once but must still return owned memory rather
than a static literal (§5). The shim:

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

**The ABI itself is purely additive.**

- No change to `IPlugin`, `IPluginService`, `PluginStateService`, or any caller.
- `Execute(string, Dictionary<string, object>?) → object?` is unchanged; the dictionary is the
  compatibility seam. Native services validate boxed dictionary values against declared types (§13).
- `IKindedService` and `GetServicesByKind` are new surface, not modified surface (§6.1).
- Managed plugins are entirely unaffected.

**§14 is not**, and is deliberately kept separate. The legacy service-model removal planned there
is a breaking change shipping in a major version. Nothing in §1–§13 depends on it, and it depends
on nothing here — the two are folded into one document because they touch the same subsystem,
not because they ship together.

---

## 13. Resolved decisions

1. **Parameter validation — STRICT.** A boxed value whose runtime type does not match the
   declared wire type is rejected with `InvalidOperationException`; no `Convert.ChangeType`,
   no silent coercion. Three reasons: the manifest already gives us the exact expected type,
   so guessing is unnecessary; `Convert.ChangeType` is reflection-driven and hostile to the
   NativeAOT gate; and strictness is the direction that can be relaxed later without breaking
   anyone, whereas lenient-then-strict silently breaks working callers. See §13.1 for the
   exact rule.
2. **Unified loader facade — deferred, not in v1.** Native loading stays an explicit
   `LoadPlugin(new NativePluginContainer(path))`. A folder-scanning facade raises
   discovery-order, partial-failure and platform-suffix (`.so` / `.dll` / `.dylib`) questions
   that are orthogonal to the ABI. Ship the ABI first; a facade over it is additive later.

### 13.1 The strict conversion rule

For each supplied parameter the shim looks up the declared wire type from the manifest and
accepts **only** these runtime types:

| Wire type | Accepted boxed CLR type |
| :--- | :--- |
| `i64` | `long`, `int`, `short`, `sbyte`, `byte`, `ushort`, `uint` (all lossless widenings to `long`) |
| `f64` | `double`, `float`, and every integer type above. Widening a `long` past 2^53 loses precision; it is accepted anyway, because rejecting it would mean second-guessing a value the caller chose to send to an `f64` parameter |
| `bool` | `bool` |
| `string` | `string` |
| `bytes` | `byte[]` |
| `i64[]`, `f64[]`, `bool[]`, `string[]` | the matching array type, or any `IEnumerable<T>` of the element type |
| `json` | `JsonElement`, or `string` containing valid JSON |

Everything else throws. Note what strict does **not** mean: lossless numeric widening is
allowed, because `["x"] = 21` (an `int` literal, the overwhelmingly common call site) must not
fail against an `i64` parameter. Rejecting that would make the API hostile for no safety gain.
What is rejected is *lossy or textual* conversion — `"42"` for an `i64`, `42.7` for an `i64`,
`1` for a `bool`.

Missing required parameter → `InvalidOperationException`. Missing optional parameter → the
manifest's `defaultValue` is used, or the parameter is omitted from the args JSON entirely
when it has none. Unknown parameter keys → rejected, so a typo surfaces immediately rather
than being silently dropped on the native side.

## 14. Planned: remove the obsolete type-based service model

The native ABI lands on a plugin system that still carries a **legacy type-based service
subsystem**, deprecated but never removed. It is dead weight the new path has to route around,
and it is the direct reason `kind` needs a side interface (§6.1) rather than a natural home.
Removing it is planned work, folded in here because it shares the same surface.

**Scope: the legacy service model, and nothing else.** Native plugins (§1–§13) are unaffected —
they are additive and depend on none of this. An earlier draft of this section also swept in eight
unrelated obsolete members found by the same audit (`Window.UseDOMLayout`, three `WindowBuilder`
aliases, four `MenuControl` colour aliases). Those are not plugin surface and have been dropped
from this plan: bundling unrelated removals into one breaking change makes it harder to justify
and harder to review. If they are ever removed, that is its own decision.

### 14.1 Why it blocks nothing but costs something

`GetService<T>()` looks up services by CLR type. That cannot work reflection-free — which is
why it is already `[Obsolete("Type-based service lookup will be removed in a future version")]`
— and it definitionally cannot work for a native plugin, which has no CLR type. So the ABI
already ignores it. The cost is not a blocker; it is that every reader of the plugin system now
meets two service models and has to work out which is live.

### 14.2 Inventory — 6 members plus plumbing, verified zero usage

| Member | Location |
| :--- | :--- |
| `record PluginService(Type, object)` | `Plugins/IPlugin.cs:52` |
| `IPlugin.GetServices()` | `Plugins/IPlugin.cs:88` |
| `PluginBase.GetServices()` | `Plugins/IPlugin.cs:131` |
| `PluginStateService.GetService<T>()` | `Core/PluginStateService.cs:478` |
| `PluginStateService.RegisteredLegacyServiceTypes` | `Core/PluginStateService.cs:240` |
| `PluginStateService.UnloadPlugin(IPlugin)` | `Core/PluginStateService.cs:397` |

Plus the private `_legacyServices` dictionary and its registration loop (`:141, 346-348, 590`),
and the two `#pragma warning disable CS0618` pairs that exist only to silence the above
(`IPlugin.cs:130-132`, `PluginStateService.cs:345-351`).

**Usage audit — zero, everywhere.** Checked across the library, `Examples/`, `Example/`, the
test suite, and all 15 sibling repos (cx*, lazy*, ServerHub, cratis-cli, dotnet-skills). Two
false positives worth recording so the next audit does not re-raise them:

- `cxgpu` appears to use `PluginService` 10 times; every hit is the *modern* `IPluginService` /
  `GetServicePlugins()`. Substring collision.
- `GetService<T>` appears in `Html/HtmlLayoutEngine.cs` and two test helpers; those are
  AngleSharp's `IBrowsingContext.GetService<T>`, not ours.

### 14.3 `UnloadPlugin` — removed, and why it is the odd one

Every other member is deprecated as *"use X instead."* `UnloadPlugin` is deprecated as *"this
does not actually work"*: it disposes the plugin but leaves its registered contributions in the
service, theme, control and window maps. A caller relying on it already has a latent bug.
Removing it converts that bug into a compile error, which is strictly better than leaving a
method whose contract admits it is wrong. Plugin unloading remains unsupported — unchanged
from today, and consistent with §2's non-goal.

### 14.4 Shipping

This is a **breaking change**: removing public members breaks binary compatibility for anything
compiled against 2.5.x, and source compatibility for anything that still calls them. Zero
observed usage lowers the risk; it does not change the classification.

It therefore ships as a **major version**, not a patch — a patch that removes public API is the
case where a consumer's routine update fails to compile, or throws `MissingMethodException` at
runtime. It should be batched with any other breaking work rather than spent alone.

> The removal is narrow by design, and that is a lesson from experience rather than caution for
> its own sake: the static `ThemeRegistry` removal was correct on the merits and still left
> consumers stranded on update. Six members with zero observed usage is a far smaller blast
> radius, but the classification is the same and the ceremony should be too.

Ordering against the ABI itself: **independent.** The ABI is purely additive and needs none of
these gone; the removal is a cleanup of the surface it lands on. Either can go first. The one
coupling worth noting is §6.1 — if `IPluginService` is ever revised in a major version,
`Kind` could move onto it directly and `IKindedService` would become unnecessary. That is a
possibility to weigh at that point, not a reason to delay either piece of work.

## 15. Considered: slimming the managed plugin system to services-only

A natural follow-on to §14: if the ABI contributes **services only**, should the managed plugin
system drop themes, controls, windows and action providers so both sides have the same shape?

**Recommendation: no.** The investigation is recorded here because the usage data it produced is
worth keeping, and because "make managed match native" is an idea that will recur.

### 15.1 What the usage audit found

**Every adopter already uses the plugin system for services only.** Across all 15 sibling repos
(cx\*, lazy\*, ServerHub, cratis-cli, dotnet-skills), the count of overrides of `GetThemes`,
`GetControls`, `GetWindows` or `GetActionProviders`, and of references to `PluginTheme`,
`PluginControl`, `PluginWindow` or `IPluginActionProvider`, is **zero**. The intuition behind
the question is correct: in practice, a plugin here is a service.

The only consumer outside the library is
`Examples/PluginShowcaseExample/ShowcasePlugin.cs`, which overrides four of the five kinds. That
file exists to demonstrate the plugin system — it was moved *out* of the library in `1ea2504`
for exactly that reason — so it is evidence of capability, not of demand.

### 15.2 Why it is still the wrong change

**A shipping library feature depends on it.** `Dialogs/StartMenuDialog.cs` builds its plugin
section from `plugin.GetWindows()` and `plugin.GetActionProviders()` (`:605-606`), then launches
the selection through `PluginStateService.CreateWindow(name)` (`:632`) and
`ExecutePluginAction(provider, action, ctx)` (`:642`). Slimming to services-only would either
delete that feature or leave it calling API that no longer exists. The cascade is wider than the
two `Get*` calls: `CreateControl`, `CreateWindow` and `ExecutePluginAction` are public members
that exist only to serve those contribution kinds.

That matters beyond the library: a start menu is the premise of a desktop-shell app, and cxshell
is one.

**The category is different from §14.** That section removes members already marked `[Obsolete]`,
with zero consumers and a documented replacement. This would remove **live, undeprecated public
API that a feature actually calls**. Zero *external* usage does not make it dead code when the
library itself is the consumer.

**The asymmetry is not an inconsistency.** §11 already resolves it honestly: the ABI exports
nothing for controls/windows/actions/themes, so a native author cannot offer them, and
`PluginBase`'s empty defaults mean `NativePluginContainer` never claims them. A managed plugin
system richer than the native one is the normal shape of an interop boundary — native plugins
are a new *source* of contributions, not a redefinition of what a plugin is. Nothing in §1–§13
is made simpler by the removal.

### 15.3 What to do with the finding instead

The zero-usage result argues for **emphasis, not deletion**: document that services are the
primary contribution kind, that the other four exist for in-process managed plugins and are
currently unused by every adopter, and that native plugins are services-only by design. That
captures the conceptual slimming with no breakage and no feature loss.

If the removal is ever revisited, the honest ordering is to settle `StartMenuDialog`'s plugin
integration first — it is the only real consumer, and its fate decides whether this is a
cleanup or a feature deletion. That is a product question, not a refactor.

## 16. Security posture

Stated plainly, because the design's own recommended idiom — scanning a directory (§6.2) — is
exactly the shape that makes this worth writing down.

### The threat model

**Loading a plugin is arbitrary code execution as the current user.** So is *probing* one:
`NativeLibrary.Load` runs the library's initializers (ELF `.init_array`, `DllMain` on Windows)
before a single ABI function is called, so `NativePluginProbe.TryReadKinds` — despite never calling
`plugin_invoke` — has already run the file's code by the time it answers. The probe is **narrower**
than a full load, not safer in kind.

There is no sandbox and there cannot be a useful one: a native library shares the host's address
space by definition. Every guarantee in this document is a **protocol** guarantee — the envelope
contains errors the plugin *reports* (§9), not faults it *causes*. A plugin that corrupts the heap,
dereferences null, or crashes inside `plugin_free` takes the process down with it, and no amount of
validation on the managed side changes that.

### What a host should do

- **Treat the plugin directory as executable code, and control write access to it accordingly.** A
  writable plugin directory is a privilege-escalation path: anything that can drop a `.so` there
  runs as the user, at next start, with no further interaction.
- **Use absolute paths.** The examples here write `"./libmyplugin.so"` for brevity; a relative path
  inherits the operating system's library search order, which on Windows includes directories an
  attacker may control.
- **Prefer an explicit allowlist over a scan** where the set of plugins is known. Scanning is
  convenient and is what §6.2 shows; it is also the mechanism that turns "someone wrote a file" into
  "someone ran code."
- **Verify integrity if the plugins are not yours** — a hash manifest, or a signature check before
  `LoadPlugin`. The framework provides no such mechanism and deliberately does not: signing policy
  belongs to the application, which knows who it trusts, and a framework-imposed scheme would be
  either too weak to rely on or too rigid to adopt.

### What the framework does guarantee

Within the boundary, and only there: strings are copied before parsing and freed exactly once
(§10); a malformed envelope is an exception rather than a corrupt value (§9.3); arguments are
validated before crossing (§13.1); a throwing event handler is caught before it can reach native
code (§17.2); and a plugin that misbehaves in one of the ways the protocol can observe is named in
the error rather than surfacing three frames up in the caller.

That is a meaningful set. It is not a security boundary, and this section exists so nobody mistakes
it for one.

## 17. Plugin → host events

The ABI so far is one-directional: the host calls in, native answers. This section adds the
return path — **one function pointer**, fire-and-forget, carrying the same name + JSON vocabulary
as `plugin_invoke`.

```c
/* Host-provided. The plugin may call this at any time, from any thread.
   `ctx` is the opaque token the host supplied; pass it back unchanged.
   `service` names which of the library's services is raising the event, or NULL for the library
   as a whole. Neither string is owned by the plugin after the call returns. */
typedef void (*host_event_fn)(void* ctx, const char* service, const char* name,
                              const char* payload_json);

/* Host hands the plugin its event channel, together with an opaque context token. Called once,
   during Initialize (§17.3) and before any plugin_invoke. A plugin that does not export this
   simply never raises events; the host resolves the symbol optionally.

   The plugin must retain both `on_event` and `ctx` and pass the token back on every call. */
void plugin_set_host(host_event_fn on_event, void* ctx);
```

That is the entire addition. The boundary becomes symmetrical:

| Direction | Mechanism | Shape |
| :--- | :--- | :--- |
| host → plugin, wants a result | `plugin_invoke` | name + JSON → envelope |
| plugin → host, no result | `host_event_fn` | name + JSON → void |

### 17.1 The framework interprets nothing

`name` is **consumer-defined**, exactly as `kind` is (§6.1). The framework does not validate it,
reserve names, or attach behaviour to any value. It marshals the event to the UI thread and
raises it; what `"progress"` or `"scan.finished"` obliges an application to do is the
application's decision.

This is deliberate and it is the same refusal made twice already in this design: the moment the
framework defines a vocabulary of host services — `log`, `progress`, `notify` as distinct ABI
entry points — it owns a taxonomy it must version and defend forever, and every addition is an
ABI break. One generic channel makes a new capability a new *name*, not a new function pointer.

An application wires the names it cares about, in a few lines:

```csharp
svc.PluginEvent += (_, e) =>
{
    switch (e.Name)
    {
        case "log":      windowSystem.LogService.LogInfo(e.Payload.GetProperty("msg").GetString()!, "Plugin"); break;
        case "progress": progressBar.Value = e.Payload.GetProperty("fraction").GetDouble();                    break;
        default:         /* unknown names are ignored, not errors */                                            break;
    }
};
```

### 17.1a Where the callback pointer comes from

The host passes a **static** `[UnmanagedCallersOnly]` method, not a delegate:

```csharp
[UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
private static unsafe void OnEvent(void* ctx, byte* service, byte* name, byte* payload)
{
    // ctx identifies WHICH container. A static cannot capture, so the mapping is a table.
    if (!Containers.TryGet((nint)ctx, out var container)) return;   // stale token: drop
    container.RaiseEvent(Utf8(service), Utf8(name), Utf8(payload)); // copies, then queues
}
```

Two details that are easy to get wrong, and fatal when got wrong:

- **A static, never `Marshal.GetFunctionPointerForDelegate`.** A delegate's function pointer is
  only valid while the delegate is GC-rooted; a plugin that outlives it calls into collected memory
  — a crash with no stack pointing at the cause. `[UnmanagedCallersOnly]` on a static needs no
  rooting and is AOT-clean, which is why the callback cannot capture and needs `ctx` at all.
- **`ctx` is a token, not a pointer to managed memory.** It is an integer key into a host-side
  table, so a plugin that fires after its container is gone finds nothing and is dropped, rather
  than dereferencing a freed object. That is also what makes the shutdown rule in §17.3 cheap.

### 17.2 Fire-and-forget, and what that buys

`host_event_fn` returns `void`. That single property is what keeps this from reopening the design:

- **No reentrancy.** The plugin cannot observe a result, so it cannot be blocked mid-call waiting
  on a host that is calling back into it.
- **No threading contract imposed on the plugin.** The host copies both strings, queues the event
  to the UI thread via `EnqueueOnUIThread`, and returns immediately. The plugin may call from any
  thread, at any time, including during `plugin_invoke`.
- **No exceptions crossing.** A managed handler that throws is caught at the boundary and logged;
  an exception propagating into native code is undefined behaviour and never allowed to.
- **No allocation contract.** Both strings are plugin-owned and valid only for the duration of the
  call. The host copies what it needs before returning. Nothing is handed back to `plugin_free`.

### 17.3 Lifecycle — when the channel is live

`plugin_set_host` is called during **`Initialize(windowSystem)`**, not in the container
constructor. That is deliberate and it closes a hole: the constructor runs before `LoadPlugin`, so
there is no window system yet — and a plugin is allowed to fire immediately and from any thread, so
an event arriving in that window would have nowhere to be queued. Handing over the channel only
once there is somewhere to deliver to removes the case entirely rather than defending against it.

At the other end, native threads may keep running after the loop stops. The host therefore
**invalidates the `ctx` token during shutdown**: an event arriving afterwards finds no container in
the table and is dropped, with a trace log. It is not an error — a plugin cannot know the host has
gone, and the ABI has no way to tell it (§17.2: nothing crosses back).

| Moment | Event behaviour |
| :--- | :--- |
| Before `Initialize` | Impossible — the plugin has no channel yet |
| Between `Initialize` and shutdown | Copied, queued to the UI thread, raised |
| After the token is invalidated | Dropped, traced |

### 17.4 Host → plugin signalling is not a mechanism

A host that wants to *tell* a plugin something — cancel, reload, change log level — does not need
new ABI. It calls an operation the plugin declared:

```json
{ "name": "Cancel", "description": "Requests the running Scan to stop.", "parameters": [], "returnType": null }
```

```csharp
// Scan is running on a background thread; ask it to stop.
scanner.Call("Cancel");
```

This works only because the host does not serialize invokes (§5.1) — a `Cancel` call has to reach
the plugin while `Scan` is still running, or it is not cancellation.

Three consequences, all of them better than a dedicated mechanism:

- **It is discoverable.** `GetAvailableOperations()` answers whether a plugin supports cancelling.
  A mandatory export would have every plugin claim the capability whether or not it honours it.
- **It is not privileged.** `Cancel` is an operation like `Add`. The framework does not know the
  word, so it cannot be wrong about what it means.
- **It generalises for free.** `Pause`, `Reload`, `SetLogLevel` need no further design.

The plugin reports what happened through the event channel — `("cancelled", …)` — closing the loop
with the mechanism it already has.

### 17.5 What the framework does not own

**The plugin's thread safety is the plugin author's problem.** Calling `Cancel` on thread B while
`Scan` runs on thread A touches the plugin's own state concurrently. A plugin that declares an
operation intended to be called during another is asserting that this is safe; one that is not
should say so in its description.

The framework provides the channel and guarantees its own side of it — the event is copied,
marshalled and raised safely, and a throwing handler cannot reach native code. It does not
provide locking, a cancellation token, an operation registry, or any guarantee about what a
plugin does with concurrent calls. Providing rails for a locomotive we did not build is how a
framework acquires responsibility for behaviour it cannot observe or fix.

This mirrors §11's stance on controls and windows: the boundary stays small not because more is
impossible, but because everything past it belongs to someone else.

### 17.6 Managed surface

`IPluginService` cannot gain a member without breaking every external implementer (§6.1), so the
event lives on a side interface that `NativeServiceShim` implements — and that a managed plugin
may implement too, since nothing here is native-only:

```csharp
public sealed class PluginEventArgs : EventArgs
{
    public string ServiceName { get; }    // which service raised it
    public string Name { get; }           // consumer-defined
    public JsonElement Payload { get; }   // parsed payload; an empty element when none was sent
}

/// <summary>A service that raises events to the host.</summary>
public interface IEventRaisingService
{
    event EventHandler<PluginEventArgs>? PluginEvent;
}
```

A malformed payload is not an error the plugin author sees at the boundary: the host raises the
event with an empty `Payload` and logs the parse failure, rather than dropping the event or
throwing into native code. The name is the part that carries meaning; the payload is best-effort.

## 18. Appendix — worked examples

All examples below were **compiled and driven end to end** against a host performing the §5.1
handshake — probe → version → manifest → consistency → invoke — except the Rust one, which is
noted where it appears. Verified output is shown for each.

### 18.0 How small a plugin actually is

The examples that follow look longer than the ABI is, because each carries a hand-rolled JSON
reader to stay dependency-free. That is example scaffolding, not protocol. Stripped to the ABI
alone, a complete working plugin is **18 lines**:

```c
/* The ABI, with nothing else: no JSON parsing, no helpers. */
#include <stdint.h>
#include <stdlib.h>
#include <string.h>

static char *dup(const char *s){ char*p=malloc(strlen(s)+1); if(p) strcpy(p,s); return p; }

const char* plugin_kind(void)        { return dup("greeter"); }
int32_t     plugin_abi_version(void) { return 1; }

const char* plugin_describe(void) {
    return dup("{\"abiVersion\":1,\"services\":[{\"name\":\"Hello\",\"kind\":\"greeter\","
               "\"description\":\"Says hello.\",\"operations\":[{\"name\":\"Greet\","
               "\"description\":\"Returns a greeting.\",\"parameters\":[],"
               "\"returnType\":\"string\"}]}]}");
}

const char* plugin_invoke(const char *op, const char *args) {
    (void)args;
    if (strcmp(op, "Greet") == 0) return dup("{\"ok\":true,\"value\":\"hello\"}");
    return dup("{\"ok\":false,\"error\":\"unknown operation\"}");
}

void plugin_free(const char *p) { free((void*)p); }
```

```
$ cc -shared -fPIC -o libminimal.so minimal.c
kind  : greeter
greet : {"ok":true,"value":"hello"}
```

That is the whole contract: five functions, four of which are one line. Everything beyond it in
the longer examples is either the plugin's own logic or a JSON parser a real plugin would take
from a library (`cJSON`, `serde_json`, `json`) instead of writing.

Read `plugin_describe` as the only piece with real content — it is a string literal, and it is
where a plugin says what it is.

### 18.1 C — the reference plugin

The same five functions as §18.0, plus one real operation. Of its ~46 lines of code, **15 are
the `json_get_i64` toy parser** — present only to keep the example dependency-free. A real
plugin drops it for `cJSON` and is shorter than this.


```c
/* libmath.c — a minimal ConsoleEx native plugin.
 * Build:  cc -shared -fPIC -o libmath.so libmath.c
 *
 * Exports the five ABI functions. Every returned string is malloc'd, because the
 * host copies it out and hands the pointer straight back to plugin_free.
 */
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#define ABI_VERSION 1

/* ---- helpers ------------------------------------------------------------ */

/* Every string we hand the host must be freshly allocated (spec §5/§10). */
static char *dup_cstr(const char *s) {
    size_t n = strlen(s) + 1;
    char *p = (char *)malloc(n);
    if (p) memcpy(p, s, n);
    return p;
}

/* Toy extractor: finds "key":<number> in a flat JSON object. Real plugins use a
 * JSON library; this keeps the example dependency-free. */
static long json_get_i64(const char *json, const char *key, long fallback) {
    char pat[64];
    snprintf(pat, sizeof pat, "\"%s\":", key);
    const char *p = strstr(json, pat);
    if (!p) return fallback;
    return strtol(p + strlen(pat), NULL, 10);
}

/* ---- the ABI ------------------------------------------------------------ */

/* 1. Probe. The only function callable before the version check: it is what
 *    tells a host this file is a plugin at all. Union of the kinds our services
 *    declare, newline-separated. */
const char *plugin_kind(void) {
    return dup_cstr("calculator");
}

/* 2. Handshake. Checked before plugin_describe; a mismatch aborts the load. */
int32_t plugin_abi_version(void) {
    return ABI_VERSION;
}

/* 3. Self-description. The manifest's kinds must match what plugin_kind returned,
 *    or the host rejects the load. */
const char *plugin_describe(void) {
    return dup_cstr(
        "{\"abiVersion\":1,\"services\":[{"
          "\"name\":\"MathService\","
          "\"kind\":\"calculator\","
          "\"description\":\"Arithmetic helpers.\","
          "\"operations\":[{"
            "\"name\":\"Add\",\"description\":\"Adds two integers.\","
            "\"parameters\":["
              "{\"name\":\"a\",\"type\":\"i64\",\"required\":true},"
              "{\"name\":\"b\",\"type\":\"i64\",\"required\":true}],"
            "\"returnType\":\"i64\"}]"
        "}]}");
}

/* 4. Invoke. op and args_json are host-owned and valid only for this call, so
 *    copy anything you retain. Args are already validated against the manifest
 *    host-side (§13.1), so no defensive type-checking is needed here. */
const char *plugin_invoke(const char *op, const char *args_json) {
    if (strcmp(op, "Add") == 0) {
        long a = json_get_i64(args_json, "a", 0);
        long b = json_get_i64(args_json, "b", 0);
        char buf[64];
        snprintf(buf, sizeof buf, "{\"ok\":true,\"value\":%ld}", a + b);
        return dup_cstr(buf);
    }
    return dup_cstr("{\"ok\":false,\"error\":\"unknown operation\"}");
}

/* 5. Free. Receives only pointers we allocated above. */
void plugin_free(const char *ptr) {
    free((void *)ptr);
}
```

```
$ cc -shared -fPIC -o libmath.so libmath.c
```

Driven through the full handshake:

```
kinds       : ['calculator']
abi version : 1
service     : MathService | kind: calculator
operations  : ['Add']
consistency : probe == manifest OK
Add(21,21)  : {'ok': True, 'value': 42}
unknown op  : {'ok': False, 'error': 'unknown operation'}
```

### 18.2 Python — logic in Python, five symbols in C

Python cannot export C symbols, so a plugin written in it needs a small native stub that embeds
the interpreter. The split is worth stating plainly, because it is the shape any managed or
interpreted language will need:

- **`plugin_math.py`** — the plugin. Pure logic, no `ctypes`, no ABI knowledge. It implements
  three functions taking and returning `str`.
- **`pybridge.c`** — the stub. Owns the five C exports, the interpreter lifetime, and all
  allocation. Roughly 60 lines, and identical for every Python plugin: only `PY_MODULE` changes.

```python
"""A ConsoleEx plugin written in Python.

Pure logic, no ctypes, no ABI knowledge: it implements three functions the C
bridge calls. Everything crossing the boundary is a str.
"""
import json

KIND = "calculator"

MANIFEST = {
    "abiVersion": 1,
    "services": [{
        "name": "PyMathService",
        "kind": KIND,
        "description": "Arithmetic helpers, implemented in Python.",
        "operations": [
            {"name": "Add", "description": "Adds two integers.",
             "parameters": [{"name": "a", "type": "i64", "required": True},
                            {"name": "b", "type": "i64", "required": True}],
             "returnType": "i64"},
            {"name": "Join", "description": "Joins strings with a separator.",
             "parameters": [{"name": "parts", "type": "string[]", "required": True},
                            {"name": "sep",   "type": "string",   "required": False}],
             "returnType": "string"},
        ],
    }],
}

def plugin_kind() -> str:
    # Union of the kinds our services declare, newline-separated.
    return "\n".join(sorted({s["kind"] for s in MANIFEST["services"] if s.get("kind")}))

def plugin_describe() -> str:
    return json.dumps(MANIFEST)

def plugin_invoke(op: str, args_json: str) -> str:
    # Args arrive already validated against the manifest (spec §13.1), so this
    # only has to dispatch and compute.
    try:
        args = json.loads(args_json) if args_json else {}
        if op == "Add":
            return json.dumps({"ok": True, "value": args["a"] + args["b"]})
        if op == "Join":
            return json.dumps({"ok": True,
                               "value": args.get("sep", " ").join(args["parts"])})
        return json.dumps({"ok": False, "error": f"unknown operation '{op}'"})
    except Exception as e:                      # never let an exception cross the ABI
        return json.dumps({"ok": False, "error": f"{type(e).__name__}: {e}"})
```

```c
/* pybridge.c — exposes a Python module as a ConsoleEx native plugin.
 *
 * Python cannot export C symbols, so this stub embeds the interpreter and
 * forwards the five ABI functions to plugin_math.py. The Python side never
 * sees a pointer; everything crossing is a str.
 *
 * Build: cc -shared -fPIC -o libpymath.so pybridge.c $(python3-config --cflags --ldflags --embed)
 */
#include <Python.h>
#include <stdint.h>
#include <stdlib.h>
#include <string.h>

#define ABI_VERSION 1
#define PY_MODULE   "plugin_math"

static PyObject *g_mod = NULL;

static char *dup_cstr(const char *s) {
    size_t n = strlen(s) + 1;
    char *p = (char *)malloc(n);
    if (p) memcpy(p, s, n);
    return p;
}

/* Start the interpreter once, on first use. */
static int ensure_python(void) {
    if (g_mod) return 1;
    if (!Py_IsInitialized()) {
        Py_InitializeEx(0);                 /* 0 = don't install signal handlers */
        PyRun_SimpleString("import sys; sys.path.insert(0, '.')");
    }
    g_mod = PyImport_ImportModule(PY_MODULE);
    if (!g_mod) { PyErr_Clear(); return 0; }
    return 1;
}

/* Call a Python function returning str; result is malloc'd for the host. */
static char *call_py(const char *fn, const char *a1, const char *a2) {
    if (!ensure_python()) return NULL;
    PyObject *f = PyObject_GetAttrString(g_mod, fn);
    if (!f) { PyErr_Clear(); return NULL; }

    PyObject *args = a2 ? Py_BuildValue("(ss)", a1, a2)
             : a1 ? Py_BuildValue("(s)", a1)
                  : PyTuple_New(0);
    PyObject *r = PyObject_CallObject(f, args);
    Py_XDECREF(args); Py_DECREF(f);

    if (!r) { PyErr_Clear(); return NULL; }
    const char *utf8 = PyUnicode_AsUTF8(r);
    char *out = utf8 ? dup_cstr(utf8) : NULL;
    Py_DECREF(r);
    return out;
}

const char *plugin_kind(void)     { return call_py("plugin_kind", NULL, NULL); }
int32_t     plugin_abi_version(void) { return ABI_VERSION; }
const char *plugin_describe(void) { return call_py("plugin_describe", NULL, NULL); }

const char *plugin_invoke(const char *op, const char *args_json) {
    char *r = call_py("plugin_invoke", op, args_json ? args_json : "{}");
    /* A dead interpreter must still produce a valid envelope, never NULL. */
    return r ? r : dup_cstr("{\"ok\":false,\"error\":\"python bridge failure\"}");
}

void plugin_free(const char *ptr) { free((void *)ptr); }
```

```
$ cc -shared -fPIC -o libpymath.so pybridge.c $(python3-config --cflags --ldflags --embed)
```

Driven from a plain C host — `dlopen` plus the five symbols, no Python on the calling side —
which is the point: the host cannot tell this from the C plugin.

```
kinds      : calculator
abi        : 1
manifest   : {"abiVersion": 1, "services": [{"name": "PyMathService", "kind": "calcul...
Add        : {"ok": true, "value": 42}
Join       : {"ok": true, "value": "x-y-z"}
unknown    : {"ok": false, "error": "unknown operation 'Nope'"}
```

### 18.3 What the Python example demonstrates about the ABI

- **The boundary is genuinely language-agnostic.** The host does `dlopen` + five symbols; what
  lives behind them — compiled C, an embedded interpreter, a Rust `cdylib` — is invisible.
- **The bridge is boilerplate, not design work.** It is the same file for every Python plugin;
  a real deployment would ship it prebuilt and let authors write only the `.py`.
- **Exceptions must not cross the ABI.** `plugin_invoke` wraps its body in `try/except` and
  converts any failure into an `{"ok":false,"error":…}` envelope, which the shim turns back
  into `InvalidOperationException`. A Python traceback escaping into native code is undefined
  behaviour; the envelope is the only error channel (§9).
- **A dead interpreter still returns a valid envelope.** `pybridge.c` never returns `NULL` from
  `plugin_invoke` — a failed import or a missing function yields an error envelope instead, so
  the host's parse path always has something to parse.
- **Caveats this example does not solve.** The embedded interpreter is process-wide and not
  reentrant across plugins: two Python plugins in one process share one interpreter, and the
  GIL serialises their calls. Nothing here handles sub-interpreters or threading, and
  `plugin_invoke` is synchronous by contract (§5.1) — a slow Python operation blocks its caller.

### 18.4 Rust — a `cdylib`, no bridge needed

Rust compiles straight to a C-ABI shared library, so unlike Python it needs no stub. Shown last
because it is the most explicit about ownership — which is a feature: the rules the C example
follows by convention, Rust states in the type system.

**Not compiled here** — no Rust toolchain was available on the machine this spec was written on.
The manifest string it emits was validated as JSON and checked against `plugin_kind`; the code
itself is unverified against a compiler.

```rust
//! A ConsoleEx native plugin in Rust — no dependencies, no serde.
//!
//! Cargo.toml:
//!   [lib]
//!   crate-type = ["cdylib"]
//!
//! Build: cargo build --release   →  target/release/libmath_rs.so

use std::ffi::{CStr, CString};
use std::os::raw::{c_char, c_int};

const ABI_VERSION: c_int = 1;

/// Hand a Rust string to the host as an owned C string.
///
/// `into_raw` transfers ownership out of Rust: the allocation now belongs to the
/// host, which returns it via `plugin_free`. Nothing here may be a `&'static str`
/// pointer — the host frees what it receives.
fn out(s: String) -> *const c_char {
    match CString::new(s) {
        Ok(c) => c.into_raw(),
        // A NUL byte in the payload is the one thing CString rejects. Never return
        // null from an ABI function; emit a valid error envelope instead.
        Err(_) => CString::new(r#"{"ok":false,"error":"interior NUL in result"}"#)
            .unwrap()
            .into_raw(),
    }
}

/// Borrow a host-owned C string for the duration of a call.
unsafe fn borrow<'a>(p: *const c_char) -> &'a str {
    if p.is_null() {
        return "";
    }
    CStr::from_ptr(p).to_str().unwrap_or("")
}

/// Toy extractor: `"key":<int>` in a flat JSON object. A real plugin uses serde_json.
fn json_i64(json: &str, key: &str) -> i64 {
    let pat = format!("\"{key}\":");
    match json.find(&pat) {
        Some(i) => {
            let rest = &json[i + pat.len()..];
            let end = rest
                .find(|c: char| !c.is_ascii_digit() && c != '-')
                .unwrap_or(rest.len());
            rest[..end].parse().unwrap_or(0)
        }
        None => 0,
    }
}

// ---- the five ABI functions ------------------------------------------------

/// 1. Probe — the only function callable before the version check.
#[no_mangle]
pub extern "C" fn plugin_kind() -> *const c_char {
    out("calculator".to_string())
}

/// 2. Handshake.
#[no_mangle]
pub extern "C" fn plugin_abi_version() -> c_int {
    ABI_VERSION
}

/// 3. Manifest. Its kinds must match `plugin_kind`, or the host rejects the load.
#[no_mangle]
pub extern "C" fn plugin_describe() -> *const c_char {
    // One line, no post-processing: stripping whitespace from a pretty-printed
    // literal would also strip the spaces inside description strings.
    out(concat!(
        r#"{"abiVersion":1,"services":[{"#,
        r#""name":"RustMathService","#,
        r#""kind":"calculator","#,
        r#""description":"Arithmetic helpers, implemented in Rust.","#,
        r#""operations":[{"#,
        r#""name":"Add","description":"Adds two integers.","#,
        r#""parameters":["#,
        r#"{"name":"a","type":"i64","required":true},"#,
        r#"{"name":"b","type":"i64","required":true}],"#,
        r#""returnType":"i64"}]}]}"#
    ).to_string())
}

/// 4. Invoke. Args are host-validated against the manifest (§13.1) before arrival.
///
/// `catch_unwind` is not decoration: a panic unwinding across an `extern "C"`
/// boundary is undefined behaviour. Any panic becomes an error envelope instead,
/// mirroring the Python bridge's `try/except`.
#[no_mangle]
pub extern "C" fn plugin_invoke(op: *const c_char, args_json: *const c_char) -> *const c_char {
    let result = std::panic::catch_unwind(|| {
        let op = unsafe { borrow(op) };
        let args = unsafe { borrow(args_json) };
        match op {
            "Add" => format!(
                r#"{{"ok":true,"value":{}}}"#,
                json_i64(args, "a") + json_i64(args, "b")
            ),
            other => format!(r#"{{"ok":false,"error":"unknown operation '{other}'"}}"#),
        }
    });
    out(result.unwrap_or_else(|_| {
        r#"{"ok":false,"error":"panic in plugin_invoke"}"#.to_string()
    }))
}

/// 5. Free. Reclaims ownership of a pointer we handed out, then drops it.
#[no_mangle]
pub extern "C" fn plugin_free(ptr: *const c_char) {
    if !ptr.is_null() {
        unsafe { drop(CString::from_raw(ptr as *mut c_char)) };
    }
}
```

Three things Rust makes explicit that C leaves to discipline:

- **`CString::into_raw` / `from_raw`** *is* the §10 ownership rule. `into_raw` moves the
  allocation out of Rust; `from_raw` in `plugin_free` reclaims it so `drop` uses the matching
  allocator. Returning a `&'static str` pointer instead is the same bug as returning a literal
  from C, but here the type system makes it hard to write by accident.
- **`catch_unwind` is mandatory, not defensive.** A panic unwinding across `extern "C"` is
  undefined behaviour. It plays the role the Python bridge's `try/except` plays: any failure
  becomes an error envelope, because §9's envelope is the only error channel.
- **`CString::new` can fail** on an interior NUL — the one case where a Rust `String` is not a
  valid C string. It returns an envelope rather than null, since §9 requires a parseable result.

### 18.5 Managed usage — load, inspect, call, read the result

A native service is reached through the framework's existing API. Nothing below is new surface:
`LoadPlugin`, `GetService`, `GetAvailableOperations` and `Execute` are the same members a managed
plugin uses, which is the point of the container pattern (§4).

#### Loading

```csharp
// One explicit load. The constructor performs the full handshake (§5.1 phase 1) — resolve
// symbols, check the ABI version, parse and validate the manifest — and either completes or
// throws. There is no partially-loaded plugin.
windowSystem.PluginStateService.LoadPlugin(new NativePluginContainer("./libmath.so"));
```

Failures at this point are authoring or deployment problems, and each names its cause:

```csharp
try
{
    windowSystem.PluginStateService.LoadPlugin(new NativePluginContainer(path));
}
catch (DllNotFoundException)        { /* file missing, or its own dependencies are */ }
catch (EntryPointNotFoundException) { /* not a ConsoleEx plugin: a required export is absent */ }
catch (InvalidOperationException)   { /* ABI version mismatch, or an invalid manifest */ }
```

#### Inspecting what the plugin offers

The manifest was captured at load, so this crosses no ABI boundary — it is reading metadata that
already lives managed-side:

```csharp
var math = windowSystem.PluginStateService.GetService("MathService");
if (math is null) return;                       // not loaded, or a different ServiceName

foreach (ServiceOperation op in math.GetAvailableOperations())
{
    string args = string.Join(", ", op.Parameters.Select(p =>
        p.Required ? $"{p.Type.Name} {p.Name}" : $"{p.Type.Name} {p.Name} = {p.DefaultValue}"));

    Console.WriteLine($"{op.ReturnType?.Name ?? "void"} {op.Name}({args})");
}
// Int64 Add(Int64 a, Int64 b)
// String Format(Int64 value, String prefix = "")
// void Reset()
```

`ReturnType == null` means a void operation — that distinction matters when reading the result.

#### Calling with parameters

Arguments are a `Dictionary<string, object>`, validated against the manifest **before** anything
crosses the boundary (§13.1). The declared type governs; the boxed runtime type only has to be
losslessly convertible to it. The raw form is shown here because it is the contract; §18.6 has
helpers that remove the ceremony (`math.Call<long>("Add", ("a", 21L), ("b", 21))`):

```csharp
long sum = (long)math.Execute("Add", new Dictionary<string, object>
{
    ["a"] = 21L,     // long — exact match for i64
    ["b"] = 21        // int  — accepted: lossless widening to i64 (§13.1)
})!;
```

A parameter the manifest marks optional may simply be omitted, and its declared `defaultValue`
is used:

```csharp
// Format(value, prefix = "") — prefix omitted
string plain  = (string)math.Execute("Format", new() { ["value"] = 42L })!;

// …or supplied
string fancy  = (string)math.Execute("Format", new() { ["value"] = 42L, ["prefix"] = "#" })!;
```

An operation with no parameters takes `null`, and a void operation returns `null`:

```csharp
math.Execute("Reset");                            // returns null; nothing to read
```

#### Reading the result

`Execute` returns `object?`, and the cast is safe because the manifest declared the type — the
shim reads the value **by the declared `ReturnType`, never by inspecting the JSON** (§9.2). One
cast per wire type:

```csharp
long    n     = (long)      svc.Execute("Count",   null)!;          // i64
double  ratio = (double)    svc.Execute("Ratio",   null)!;          // f64
bool    ok    = (bool)      svc.Execute("IsReady", null)!;          // bool
string  name  = (string)    svc.Execute("Name",    null)!;          // string
byte[]  blob  = (byte[])    svc.Execute("Read",    null)!;          // bytes  (base64 on the wire)
long[]  ids   = (long[])    svc.Execute("Ids",     null)!;          // i64[]
JsonElement tree = (JsonElement)svc.Execute("Tree", null)!;         // json   (the escape hatch)
```

The `!` is warranted for a non-void operation: §9.3 makes "the plugin promised a value and did
not send one" a throw, not a null return. Only a void operation returns null.

#### Handling failure

Everything that can go wrong at call time surfaces as `InvalidOperationException`, matching the
documented contract of `IPluginService.Execute`. The **message** distinguishes a plugin that
reported a failure from one that misbehaved (§9.3):

```csharp
try
{
    var result = math.Execute("Divide", new() { ["a"] = 1L, ["b"] = 0L });
}
catch (InvalidOperationException ex)
{
    // "division by zero"                        → the plugin returned {"ok":false,"error":…}
    // "unknown operation 'Divde'"               → caller typo, rejected before the ABI
    // "parameter 'b': expected i64, got String" → §13.1, rejected before the ABI
    // "operation 'Divide' declared i64 but returned a string" → plugin bug (§9.2)
    log.Warn(ex.Message);
}
```

The first three are recoverable and actionable by the caller. The fourth is a bug in the plugin,
and reads as one — which is why the shim reports the mismatch itself rather than letting it
surface as a `InvalidCastException` three frames up.

#### Discovery by kind

Filtering a folder before loading anything, so an unwanted plugin is never initialized (§6.2):

```csharp
foreach (var file in Directory.EnumerateFiles(pluginDir, "*.so"))
{
    if (!NativePluginProbe.TryReadKinds(file, out var kinds)) continue;  // not ours
    if (!kinds.Contains("calculator")) continue;                          // not wanted
    windowSystem.PluginStateService.LoadPlugin(new NativePluginContainer(file));
}
```

Or, after loading, grouping whatever is registered — native and managed alike, since any service
may implement `IKindedService` (§6.1):

```csharp
foreach (IPluginService svc in windowSystem.PluginStateService.GetServicesByKind("calculator"))
    Console.WriteLine($"{svc.ServiceName}: {svc.Description}");
```

#### Threading

`plugin_invoke` is synchronous (§5.1), so a long-running operation blocks its caller exactly as a
managed one would. Call it off the UI thread and marshal the result back (CLAUDE.md rule 13):

```csharp
_ = Task.Run(() =>
{
    var result = (string)heavy.Execute("Analyze", new() { ["path"] = file })!;
    windowSystem.EnqueueOnUIThread(() => output.SetContent(result));
});
```

### 18.6 `PluginServiceExtensions` — calling without the dictionary

`Execute(string, Dictionary<string, object>?)` is the contract, and it stays exactly as it is:
it is the compatibility seam (§12), and every managed plugin already implements it. But it is
noisy at the call site, and the noise is all ceremony:

```csharp
long sum = (long)math.Execute("Add", new Dictionary<string, object> { ["a"] = 21L, ["b"] = 21 })!;
```

Two extension methods remove it. They are **extensions, not interface members** — adding a member
to `IPluginService` would break every external implementer, the same rule that put `Kind` on a
side interface (§6.1). They build the dictionary and delegate; **all validation stays in the
shim**, so a helper can never drift from the strict rules of §13.1.

```csharp
public static class PluginServiceExtensions
{
    // Named — explicit, order-independent, the recommended form.
    public static T Call<T>(this IPluginService svc, string op, params (string Name, object Value)[] args);
    public static void Call(this IPluginService svc, string op, params (string Name, object Value)[] args);

    // Positional — binds by the manifest's declared parameter order.
    public static T CallPositional<T>(this IPluginService svc, string op, params object[] args);
    public static void CallPositional(this IPluginService svc, string op, params object[] args);
}
```

#### Named — the default

```csharp
long   sum   = math.Call<long>("Add", ("a", 21L), ("b", 21));
string fancy = math.Call<string>("Format", ("value", 42L), ("prefix", "#"));
math.Call("Reset");                                    // void overload, no args
```

Order-independent, and a wrong name fails the same way it always did — `InvalidOperationException`
naming the parameter, from the shim.

#### Positional — shortest, and narrower on purpose

```csharp
long sum = math.CallPositional<long>("Add", 21L, 21);   // → a: 21, b: 21
```

Arguments bind to `GetAvailableOperations()[op].Parameters` in declaration order, which the
manifest preserves as an ordered list.

**The hazard, and the guard.** Positional binding couples a caller to declaration order: a plugin
author who swaps two same-typed parameters breaks every positional caller, with no compile error
and — without a guard — no runtime complaint either, just silently transposed arguments. That is
the one failure mode this design will not accept quietly, so `CallPositional` refuses the cases
where a mistake could pass unnoticed:

| Situation | Behaviour |
| :--- | :--- |
| More arguments than declared parameters | `InvalidOperationException` |
| Fewer arguments than **required** parameters | `InvalidOperationException` |
| Fewer than total, remainder all optional | allowed — the optionals take their manifest defaults |
| Any argument's type mismatches its position | `InvalidOperationException` from the shim (§13.1), naming the parameter |

The type check is what makes this survivable in practice: transposing two parameters of
*different* types is caught immediately by name in the error message. Transposing two of the
**same** type is not detectable by any mechanism, which is why the named form is the default and
this one is the deliberate shortcut.

**When to reach for it.** A stable, well-known operation called in a tight loop, or an operation
with one parameter where naming it adds nothing:

```csharp
bool ready = gpu.CallPositional<bool>("IsReady");
long bytes = gpu.CallPositional<long>("Read", handle);
```

Prefer `Call` everywhere else.

#### What the generic return does, and does not, do

`T` is a **cast, not a conversion**. The shim still reads the value by the operation's declared
`ReturnType` (§9.2); `Call<T>` only saves the caller writing the cast, and throws
`InvalidCastException` if `T` disagrees with what the manifest declared — which is a caller bug,
distinct from the plugin bug §9.2 reports.

For a void operation use the non-generic overload. Calling `Call<T>` on one throws, rather than
returning `default(T)`: a caller asking for a value from an operation that declares none has
made a mistake worth surfacing.

**None of this is type safety.** `("a", 21L)` is still not checked against the manifest at compile
time, and cannot be — the manifest is data, not types. These helpers remove ceremony; they do not
remove the possibility of a typo'd parameter name. That is the same trade the string-keyed
`GetService(name)` already makes.
