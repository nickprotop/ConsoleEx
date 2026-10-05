# Data Binding

SharpConsoleUI has a small, declarative data-binding layer for MVVM-style apps: you put your
state in a view model that implements `INotifyPropertyChanged`, and bind control properties to
it with `.Bind()` / `.BindTwoWay()`. When a view-model property changes, the bound control
updates itself — you never reach into a control and call `SetText(...)`.

This is one of two valid styles the framework supports. The other is a Coordinator/Controller
pattern with imperative updates (`control.SetContent(...)` from an event handler). Binding is
the more declarative, more testable path; the imperative one is also fine and is what some of
the production apps built on SharpConsoleUI use. This page documents the binding path.

---

## The shape of it

```csharp
// 1. A view model — standard .NET INotifyPropertyChanged, nothing framework-specific.
public sealed class MonitorVm : ViewModelBase   // ViewModelBase = INPC + a SetProperty helper
{
    private double _cpu;
    public double Cpu { get => _cpu; set => SetProperty(ref _cpu, value); }

    private string _status = "";
    public string Status { get => _status; set => SetProperty(ref _status, value); }
}

// 2. Bind controls to it.
var bar    = Controls.BarGraph().Build();
var status = Controls.Label("");

bar.Bind(vm, v => v.Cpu, c => c.Value);        // one-way:  vm.Cpu    → bar.Value
status.Bind(vm, v => v.Status, c => c.Text);   // one-way:  vm.Status → status.Text

// 3. Mutate the view model — the controls follow.
vm.Cpu = 73;        // the bar redraws itself
vm.Status = "OK";   // the label updates
```

You only need `INotifyPropertyChanged` on the **source** (the view model). Every control already
implements it (see [Why controls are INPC](#why-controls-are-inpc)), so the *target* side is
handled for you.

A minimal `ViewModelBase` (the tutorials use exactly this):

```csharp
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}
```

`SetProperty` only raises the change event when the value actually changes, and
`[CallerMemberName]` means you never type a property name as a string.

---

## One-way binding

`Bind` pushes source → target. The control updates whenever the bound view-model property raises
`PropertyChanged`; the initial value is applied immediately when you call `Bind`.

```csharp
bar.Bind(vm, v => v.Cpu, c => c.Value);
```

The two lambdas are member-access expressions: `v => v.Cpu` names the source property,
`c => c.Value` names the control property. Both sides must be the same type.

### With a converter

When the source and target types differ — or you want to format a value — pass a converter:

```csharp
// Message? -> display string
header.Bind(vm, v => v.SelectedMessage, c => c.Text,
            msg => msg is null ? "[grey50]Select a message[/]" : msg.HeaderText);

// double -> formatted markup
networkLabel.Bind(vm, v => v.NetworkKBps, c => c.Text,
                  v => $"[bold yellow]Network:[/] {v:F1} KB/s");
```

---

## Two-way binding

`BindTwoWay` keeps source and target in lockstep in **both** directions: set the view-model
property and the control updates; the user edits the control and the view model updates.

```csharp
// PromptControl exposes its text as .Input (not .Text).
prompt.BindTwoWay(vm, v => v.Name, c => c.Input);
```

Two-way binding listens to `PropertyChanged` on **both** sides, with a re-entrancy guard so the
two updates don't loop. A converter pair is available when the types differ:

```csharp
edit.BindTwoWay(vm, v => v.Count, c => c.Text,
                toTarget: n => n.ToString(),
                toSource: s => int.TryParse(s, out var n) ? n : 0);
```

### Which control properties are two-way-bindable?

Two-way binding requires the **control** property to raise `PropertyChanged`. These do (a
representative list — all backed by the control's change notification):

| Control | Two-way property |
|---|---|
| `CheckboxControl` | `Checked` |
| `SliderControl` | `Value` |
| `RangeSliderControl` | `LowValue`, `HighValue` |
| `DropdownControl` | `SelectedIndex` |
| `DatePickerControl` | `SelectedDate` |
| `TimePickerControl` | `SelectedTime` |
| `ListControl` | `SelectedIndex` |
| `TabControl` | `ActiveTabIndex` |
| `NavigationView` | `SelectedIndex` |
| `PromptControl` | `Input` |
| `MultilineEditControl` | `Content` |
| `TableControl` | `SelectedRowIndex` |
| `CollapsiblePanel` | `IsExpanded` |
| `TreeNode` | `IsExpanded`, `Text`, `TextColor`, `Tag` |

These all fire `PropertyChanged` on **both** the property setter and interactive (keyboard/mouse)
mutation, so typing in a prompt, dragging a slider, or selecting a row flows back to the view
model. (Display-only values like `MarkupControl.Text`, `ProgressBarControl.Value`, and
`BarGraphControl.Value` are one-way targets.)

---

## Binding on builders and menu items

`Bind` / `BindTwoWay` are also available fluently on control builders (the binding is applied
when `Build()` runs) and on `MenuItem`:

```csharp
// Builder — deferred until Build().
var bar = Controls.BarGraph()
    .Bind(vm, v => v.Cpu, c => c.Value)
    .Build();

// MenuItem — e.g. enable/disable from a CanExecute flag.
menuItem.Bind(vm, v => v.CanSave, m => m.IsEnabled);
```

---

## Binding a tree table to items

`Bind` connects one property to one property. A [TreeTableControl](controls/TreeTableControl.md)
shows a whole hierarchy, so it binds to items with `BindItems`: you say where the roots are, how
to get an item's children, and what cells an item shows.

```csharp
public sealed class WorkItemVm : ViewModelBase
{
    private string _title = "";
    public string Title { get => _title; set => SetProperty(ref _title, value); }

    private string _owner = "";
    public string Owner { get => _owner; set => SetProperty(ref _owner, value); }

    private bool _isOpen = true;
    public bool IsOpen { get => _isOpen; set => SetProperty(ref _isOpen, value); }

    public ObservableCollection<WorkItemVm> Children { get; } = new();
}

var table = Controls.TreeTable()
    .AddColumn("Title")
    .AddColumn("Owner")
    .BindItems(backlog.Roots,                                   // ObservableCollection<WorkItemVm>
        childrenOf: item => item.Children,
        cellsOf: item => [item.Title, item.Owner],
        configure: options =>
        {
            options.IsExpanded = item => item.IsOpen;           // view model -> row
            options.IsExpandedChanged = (item, open) => item.IsOpen = open;   // row -> view model
        })
    .Build();
```

- Each row's `Tag` is its item, so `table.SelectedRow?.Tag` is the selected view model.
- A collection that implements `INotifyCollectionChanged` is followed change by change: an added
  item gets a row in its place, a removed one loses its row and its subtree, a moved one keeps its
  row, with its selection and expansion. A reset keeps the rows of the items still there.
- An item that implements `INotifyPropertyChanged` updates its row's cells when it changes, and
  its children when `childrenOf` returns another collection.
- `options.ItemComparer` decides which items are the same item across a reset or a replace;
  `options.HasUnrealizedChildren` loads an item's children only when its row is first opened;
  `options.UpdateRow` styles a row after its cells are set.
- Every change is one batch, so one recompute. Rows added to the table in other ways keep their
  places among the bound ones. Calling `BindItems` again replaces the binding and its rows.

Like `Bind`, `BindItems` marshals nothing: change the items and collections on the UI thread.

---

## Lifetime and disposal

Each binding is an `IDisposable` subscription stored in the control's `Bindings` collection.
**You don't manage it manually:** `BaseControl.Dispose()` disposes the collection, and a window
disposes all of its controls when it closes. So bindings are torn down automatically when the
control (and its window) goes away — a binding never keeps a closed window alive.

If you bind to a long-lived view model from a short-lived control, this is exactly what you want:
the binding dies with the control, not with the view model.

---

## Why controls are INPC

Every control derives from `BaseControl`, which implements `INotifyPropertyChanged` and exposes a
`SetProperty` helper used throughout the control library. That is why:

- the **target** side of a binding needs no work from you, and
- **two-way** binding works against control properties out of the box.

When you write your own custom control and want a property to be two-way-bindable, raise
`OnPropertyChanged(nameof(Prop))` from its setter (and from any interactive mutation path), the
same way the built-in controls do.

---

## NativeAOT and trimming

The binding layer is **AOT- and trim-safe**. `Bind` / `BindTwoWay` compile member-access
`Expression<Func<>>` trees, but under NativeAOT (`IsDynamicCodeSupported=false`)
`System.Linq.Expressions` falls back to its **interpreter** instead of `Reflection.Emit`, so the
bindings run correctly in a native binary. The library's AOT smoke gate exercises this path. See
[AOT.md](AOT.md). `BindItems` takes plain delegates, so it compiles nothing at all.

---

## See also

- [Tutorial 4 — Terminal Mail Client](tutorials/04-mail-client.md): a full MVVM app (master-detail
  binding, a two-way compose dialog, a `TableControl` data source).
- [Tutorial 5 — Terminal Music Player](tutorials/05-music-player.md): one-way bindings with
  converters driving a "Now Playing" view.
- [AOT.md](AOT.md): the AOT/trim story, including the binding interpreter fallback.
