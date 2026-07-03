# Form XML Reference

> **Looking for the runtime?** The declarative loader on this page sits on top of the
> [`FormControl`](controls/FormControl.md) — read that first for the field semantics, validation
> model, and value access. This page documents the XML that *describes* a form.

SharpConsoleUI can build a form from a small declarative XML document instead of imperative
builder calls. You describe the fields in a file (or a string constant), and `FormXml` parses it and
drives the existing `FormControl` API to construct a live, fully-interactive form:

```csharp
using SharpConsoleUI.Controls.Forms;

var form = FormXml.FromXml(@"
<form title='Contact'>
  <text name='name'  label='Name:'  required='true'/>
  <text name='email' label='Email:' pattern='^[^@]+@[^@]+$' message='Enter a valid email'/>
  <buttons/>
</form>");

window.AddControl(form);
```

This is the same XAML-family idea used across UI toolkits — markup describes the tree, code supplies
the behavior. `FormXml` is a **thin call-through**: it parses the XML with the BCL `XDocument` and
calls the `FormControl` field-add methods. It invents no layout, validation, or submit logic of its
own — everything a loaded form does is exactly what the [`FormControl` runtime](controls/FormControl.md)
does. It adds no dependency and is NativeAOT-safe (no reflection, no dynamic code; a `pattern=`
compiles a runtime interpreted `Regex`).

## Contents

- [The `<form>` root](#the-form-root)
- [Field elements](#field-elements)
  - [`<text>`](#text)
  - [`<multiline>`](#multiline)
  - [`<checkbox>`](#checkbox)
  - [`<dropdown>`](#dropdown)
  - [`<radio>`](#radio)
  - [`<slider>`](#slider)
- [Structure elements](#structure-elements)
  - [`<section>`](#section)
  - [`<row>`](#row)
  - [`<buttons>`](#buttons)
- [Validation attributes](#validation-attributes)
- [Named validators (`rule=`)](#named-validators-rule)
- [Loading: `FromXml` / `FromXmlFile`](#loading-fromxml--fromxmlfile)
- [Errors: `FormXmlException`](#errors-formxmlexception)
- [v1 non-goals](#v1-non-goals)
- [Worked examples](#worked-examples)

## The `<form>` root

The root element **must** be `<form>` — any other root throws a [`FormXmlException`](#errors-formxmlexception).
Child elements are added to the form in document order.

```xml
<form title='Settings' columnGap='1' rowGap='0'>
  <!-- fields, sections, rows, buttons -->
</form>
```

| Attribute | Type | Default | Description |
|-----------|------|---------|-------------|
| `title` | string | — | Accepted for readability/labeling. Note: `FormControl` renders no built-in title band — host the form in a titled window if you want a heading. |
| `columnGap` | int | `0` | Sets `FormControl.ColumnGap` (space between the label and editor columns). |
| `rowGap` | int | `0` | Sets `FormControl.RowGap` (space between rows). |

## Field elements

Each field element requires a `name` (the value key returned by `GetValues()`). A field with no
`name` throws. A `label` is optional (defaults to empty). Every field also accepts an optional
`hint` that renders a dim line beneath the editor.

### `<text>`

Single-line text field (backed by `PromptControl`). Supports all [validation attributes](#validation-attributes).

```xml
<text name='host' label='Host:' initial='localhost' required='true' hint='e.g. localhost'/>
```

| Attribute | Type | Default | Description |
|-----------|------|---------|-------------|
| `name` | string | — (required) | Value key |
| `label` | string | `""` | Label text |
| `initial` | string | `""` | Initial text |
| `hint` | string | — | Dim hint below the editor |
| *(validation attrs)* | — | — | See [Validation attributes](#validation-attributes) |

### `<multiline>`

Multi-line editor (backed by `MultilineEditControl`).

```xml
<multiline name='notes' label='Notes:' height='4' hint='Markdown accepted'/>
```

| Attribute | Type | Default | Description |
|-----------|------|---------|-------------|
| `name` | string | — (required) | Value key |
| `label` | string | `""` | Label text |
| `initial` | string | `""` | Initial content |
| `height` | int | `3` | Editor viewport height in rows |
| `hint` | string | — | Dim hint |

> Validation attributes on `<multiline>` are **ignored** in this version — see [non-goals](#v1-non-goals).

### `<checkbox>`

Boolean checkbox (backed by `CheckboxControl`). Its label rides in the editor column. Value is
`"true"` / `"false"`.

```xml
<checkbox name='ssl' label='Use SSL/TLS' initial='true' hint='encrypt the connection'/>
```

| Attribute | Type | Default | Description |
|-----------|------|---------|-------------|
| `name` | string | — (required) | Value key |
| `label` | string | `""` | Checkbox label (editor column) |
| `initial` | bool | `false` | Initial checked state |
| `hint` | string | — | Dim hint |

### `<dropdown>`

Single-select dropdown (backed by `DropdownControl`). Options come from a comma-separated `options`
list.

```xml
<dropdown name='driver' label='Driver:' options='PostgreSQL,MySQL,SQLite' initial='PostgreSQL'/>
```

| Attribute | Type | Default | Description |
|-----------|------|---------|-------------|
| `name` | string | — (required) | Value key |
| `label` | string | `""` | Label text |
| `options` | comma-separated | `""` | Selectable options (empty entries dropped) |
| `initial` | string | — | Initially selected value |
| `hint` | string | — | Dim hint |

> Validation attributes on `<dropdown>` are **ignored** — a dropdown always holds one of its options.

### `<radio>`

Single-select radio group. Options come from a comma-separated `options` list; `initial` selects one.

```xml
<radio name='mode' label='Mode:' options='Read-write,Read-only,Replica' initial='Read-write'/>
```

| Attribute | Type | Default | Description |
|-----------|------|---------|-------------|
| `name` | string | — (required) | Value key |
| `label` | string | `""` | Label text |
| `options` | comma-separated | `""` | Option strings (value = label) |
| `initial` | string | — | Initially selected option |

> Validation attributes on `<radio>` are **ignored** — a radio group always resolves to an option.

### `<slider>`

Numeric slider (backed by `SliderControl`). The slider clamps to its `min`/`max` range.

```xml
<slider name='timeout' label='Timeout (s):' min='0' max='60' initial='30' hint='connection timeout'/>
```

| Attribute | Type | Default | Description |
|-----------|------|---------|-------------|
| `name` | string | — (required) | Value key |
| `label` | string | `""` | Label text |
| `min` | number | `0` | Minimum value |
| `max` | number | `100` | Maximum value |
| `initial` | number | `0` | Initial value |
| `hint` | string | — | Dim hint |

> Validation attributes on `<slider>` are **ignored** — the slider already clamps to its range.

## Structure elements

### `<section>`

Inserts a full-width section header row. Every field nested **inside** the `<section>` element belongs
to that section; the section is closed automatically at the element's end.

```xml
<section title='Advanced' collapsible='true' collapsed='true'>
  <checkbox name='ssl' label='Use SSL/TLS'/>
  <slider name='timeout' label='Timeout (s):' min='0' max='60' initial='30'/>
</section>
```

| Attribute | Type | Default | Description |
|-----------|------|---------|-------------|
| `title` | string | — | Section header text |
| `collapsible` | bool | `false` | Adds a ▸/▾ toggle to collapse the section |
| `collapsed` | bool | `false` | Start collapsed (fields hidden). Only meaningful with `collapsible='true'` |

### `<row>`

Packs its child field elements side by side onto a single grid row (each field keeps its own
label/editor pair). Children are the same [field elements](#field-elements).

```xml
<row>
  <text name='first' label='First:'/>
  <text name='last'  label='Last:'/>
</row>
```

`<row>` takes no attributes of its own.

### `<buttons>`

Adds the right-aligned action button row. The OK button calls `Submit()`; the Cancel button raises
`Cancelled`.

```xml
<buttons ok='Connect' cancel='Cancel' showCancel='true'/>
```

| Attribute | Type | Default | Description |
|-----------|------|---------|-------------|
| `ok` | string | `"OK"` | OK button caption |
| `cancel` | string | `"Cancel"` | Cancel button caption |
| `showCancel` | bool | `true` | Whether to include the Cancel button |

## Validation attributes

These attributes apply to **`<text>`** fields (and `<multiline>` in principle, but multiline is
unvalidated in this version — see [non-goals](#v1-non-goals)). Each present attribute contributes one
rule; the field's value passes only when every rule passes. Empty values skip all built-in rules;
only `required` fails on empty (so an optional field with a `type`/`pattern`/`min`/`max`/`maxLength`
constraint is still allowed to be blank).

| Attribute | Type | Rule |
|-----------|------|------|
| `required` | bool | Value must be non-empty (`"Required"`). |
| `type` | `int` \| `number` | Value must parse as a whole number (`int`) or a number (`number`). |
| `pattern` | regex | Value must match the regular expression (compiled at load time via `new Regex(pattern)`). |
| `min` | number | Numeric value must be **≥** `min`. |
| `max` | number | Numeric value must be **≤** `max`. |
| `minLength` | int | String length must be **≥** `minLength`. |
| `maxLength` | int | String length must be **≤** `maxLength`. |
| `message` | string | Overrides the default error text for **every** rule on the field. |

```xml
<text name='port' label='Port:' type='int' min='1' max='65535' required='true'
      message='Enter a port between 1 and 65535'/>
```

## Named validators (`rule=`)

For validation that XML can't express (cross-field checks, DNS lookups, custom parsing), attach a
`rule='name'` to a `<text>` field and supply the C# implementation through the `namedValidators`
registry passed to `FromXml` / `FromXmlFile`. The registry maps a name to a
`Func<string?, string?>` that returns `null` when the value is acceptable, or an error message
otherwise. A `rule=` whose name is **not** in the registry throws a
[`FormXmlException`](#errors-formxmlexception).

```csharp
var registry = new Dictionary<string, Func<string?, string?>>
{
    ["validDsn"] = v =>
        string.IsNullOrEmpty(v) || v.Contains('=')
            ? null
            : "Expected key=value pairs, e.g. Host=localhost;Port=5432",
};

var form = FormXml.FromXml(@"
<form>
  <text name='dsn' label='DSN:' rule='validDsn'/>
  <buttons/>
</form>", registry);
```

A `rule=` composes with the built-in [validation attributes](#validation-attributes) — the named
validator runs alongside `required`, `type`, `pattern`, and the others on the same field.

## Loading: `FromXml` / `FromXmlFile`

```csharp
// From a string:
FormControl form = FormXml.FromXml(xml);
FormControl form = FormXml.FromXml(xml, namedValidators);

// From a file (root must be <form>):
FormControl form = FormXml.FromXmlFile("forms/connect.xml");
FormControl form = FormXml.FromXmlFile("forms/connect.xml", namedValidators);
```

Both return a ready `FormControl`. Wire submission/cancellation and read values with the runtime API —
`OnSubmit` isn't an XML concept, so subscribe in code:

```csharp
form.Submitted += (_, values) => Save(values["host"], values["port"]);
form.Cancelled += (_, _) => window.Close();
```

## Errors: `FormXmlException`

`FormXml` never fails silently. It throws a `FormXmlException` (with line/position context where the
XML provides it) when:

- the XML is **malformed** (wraps the underlying `XmlException`),
- the root element is **not** `<form>`,
- an element is **unknown** (e.g. `<slidr>`),
- a field is **missing** its required `name` attribute,
- an integer/number attribute (`columnGap`, `height`, `min`, …) is present but **unparseable**,
- a `rule='name'` references a validator **not** found in the registry.

Unknown **elements** throw a `FormXmlException`, but unknown **attributes** are silently ignored (for
forward compatibility).

```csharp
try
{
    var form = FormXml.FromXml(xml, registry);
}
catch (FormXmlException ex)
{
    // ex.Message includes "(line N, position M)" where available
    logger.LogError(ex, "Bad form descriptor");
}
```

## v1 non-goals

The current loader deliberately stops here:

- **No `<field>` custom-editor element.** The `FormControl.AddField` escape hatch (arbitrary editor +
  value getter) has no XML mapping — custom editors are wired in code.
- **No `ToXml` / export.** The mapping is XML → form only; there's no form → XML serializer.
- **Validation on `<dropdown>`, `<radio>`, `<slider>` is ignored.** Those `FormControl` overloads take
  no validator, and each type already constrains its own value.
- **`<multiline>` is unvalidated.** The multiline overload takes no validator, so validation attributes
  on `<multiline>` have no effect.

## Worked examples

### 1. Simple contact form

```csharp
var form = FormXml.FromXml(@"
<form title='Contact'>
  <text name='name'  label='Name:'  required='true'/>
  <text name='email' label='Email:' pattern='^[^@\s]+@[^@\s]+\.[^@\s]+$'
        message='Enter a valid email address'/>
  <dropdown name='category' label='Category:' options='Bug,Feature,Question'/>
  <multiline name='message' label='Message:' height='5' hint='Describe the issue'/>
  <buttons/>
</form>");

form.Submitted += (_, values) =>
    windowSystem.NotificationStateService.ShowNotification(
        "Submitted", $"{values["name"]} — {values["category"]}", NotificationSeverity.Success);

window.AddControl(form);
```

### 2. Connection form with a collapsed Advanced section, validation, and a named rule

```csharp
var registry = new Dictionary<string, Func<string?, string?>>
{
    ["validDsn"] = v =>
        string.IsNullOrEmpty(v) || v.Contains('=')
            ? null
            : "Expected key=value pairs (Host=…;Port=…)",
};

var form = FormXml.FromXml(@"
<form title='Connection' columnGap='1'>
  <text name='host' label='Host:' initial='localhost' required='true' hint='e.g. localhost'/>
  <text name='dsn'  label='Extra DSN:' rule='validDsn' hint='key=value;key=value'/>
  <dropdown name='driver' label='Driver:' options='PostgreSQL,MySQL,SQLite' initial='PostgreSQL'/>

  <section title='Advanced' collapsible='true' collapsed='true'>
    <text name='port' label='Port:' type='int' min='1' max='65535' initial='5432'
          message='Enter a port between 1 and 65535'/>
    <checkbox name='ssl' label='Use SSL/TLS'/>
    <radio name='mode' label='Mode:' options='Read-write,Read-only,Replica' initial='Read-write'/>
    <slider name='timeout' label='Timeout (s):' min='0' max='60' initial='30'/>
  </section>

  <buttons ok='Connect' cancel='Cancel'/>
</form>", registry);

form.Submitted += (_, values) => Connect(values["host"], values["port"], values["driver"]);
form.Cancelled += (_, _) => window.Close();

window.AddControl(form);
```

---

See also: [FormControl](controls/FormControl.md) · [MARKUP_SYNTAX](MARKUP_SYNTAX.md) · [CONTROLS](CONTROLS.md)

[Back to Main Documentation](../README.md)
