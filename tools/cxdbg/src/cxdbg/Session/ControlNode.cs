// -----------------------------------------------------------------------
// ConsoleEx - A simple console window system for .NET Core
//
// Author: Nikolaos Protopapas
// Email: nikolaos.protopapas@gmail.com
// License: MIT
// -----------------------------------------------------------------------

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json.Nodes;

namespace Cxdbg.Session;

/// <summary>
/// One control in the target, as the tree pane's bound item.
/// </summary>
/// <remarks>
/// <para>
/// An item rather than a formatted line, so the tree is a real
/// <c>TreeTableControl</c> bound to a hierarchy: clicking works, expanding
/// works, filtering works, and the rows are diffed rather than rebuilt.
/// </para>
/// <para>
/// That last point is the reason this type exists. A pane that rebuilt its
/// rows on every poll lost the selection and the expansion state several times
/// a second. Here a poll updates properties, <see cref="INotifyPropertyChanged"/>
/// moves the cells, and the collection diff adds and removes only what
/// actually changed - so what you had selected stays selected.
/// </para>
/// </remarks>
internal sealed class ControlNode : INotifyPropertyChanged
{
	private string _got = "";
	private string _wants = "";
	private string _layout = "";
	private bool _invisible;
	private JsonObject _raw;

	/// <summary>What a node stands for, which decides how it is described.</summary>
	internal enum NodeKind
	{
		/// <summary>The desktop: the screen everything is drawn on.</summary>
		Desktop,

		/// <summary>A window.</summary>
		Window,

		/// <summary>A control, or layout scaffolding.</summary>
		Control,
	}

	internal ControlNode(string id, string type, string key, JsonObject raw,
		NodeKind kind = NodeKind.Control)
	{
		Id = id;
		Type = type;
		Key = key;
		Kind = kind;
		_raw = raw;
		Children = new ObservableCollection<ControlNode>();
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	/// <summary>The control's id, or empty for layout scaffolding.</summary>
	/// <remarks>
	/// Identity across polls, so the binding knows a node is the same node and
	/// keeps its row, its selection and its expansion.
	/// </remarks>
	internal string Id { get; }

	/// <summary>The control's type name, which is what a reader recognises.</summary>
	internal string Type { get; }

	/// <summary>
	/// What makes this the same node across polls.
	/// </summary>
	/// <remarks>
	/// Assigned ONCE, at creation, and never recomputed. Scaffolding has no id
	/// and so falls back to a key containing its position; recomputing that
	/// from the node's current index made every such node look new the moment
	/// anything shifted, so the rows were rebuilt and the selection cleared
	/// within a frame of being set.
	/// </remarks>
	internal string Key { get; }

	/// <summary>Whether this is the desktop, a window, or a control.</summary>
	/// <remarks>
	/// The three have different things worth saying about them - a desktop has
	/// a theme and panel rows, a window has a state and a root layout, a
	/// control has a measure cascade - so the panes ask what they are looking
	/// at rather than describing everything as though it were a control.
	/// </remarks>
	internal NodeKind Kind { get; }

	/// <summary>This node's children, followed change by change.</summary>
	internal ObservableCollection<ControlNode> Children { get; }

	/// <summary>Whether the tree shows this node expanded.</summary>
	/// <remarks>
	/// Held here rather than in the control, so it survives the node's row
	/// being removed and added again.
	/// </remarks>
	internal bool Expanded { get; set; } = true;

	/// <summary>The whole tree node, for the panes that want more than four columns.</summary>
	internal JsonObject Raw
	{
		get => _raw;
		private set => _raw = value;
	}

	/// <summary>What it was arranged at.</summary>
	internal string Got
	{
		get => _got;
		private set => Set(ref _got, value);
	}

	/// <summary>What it asked for, shown only where that differs.</summary>
	internal string Wants
	{
		get => _wants;
		private set => Set(ref _wants, value);
	}

	/// <summary>The layout that owns it.</summary>
	internal string Layout
	{
		get => _layout;
		private set => Set(ref _layout, value);
	}

	/// <summary>Whether it paints nothing.</summary>
	internal bool Invisible
	{
		get => _invisible;
		private set => Set(ref _invisible, value);
	}

	/// <summary>The four cells the tree shows, in column order.</summary>
	internal IEnumerable<string> Cells()
	{
		yield return Invisible ? $"[red]{Escape(Type)}[/]" : Escape(Type);
		yield return Got;
		yield return Wants;
		yield return Escape(Layout);
	}

	/// <summary>Takes a desktop's current values.</summary>
	internal void UpdateDesktop(JsonObject desktop)
	{
		Raw = desktop;

		int w = desktop["width"]?.GetValue<int>() ?? -1;
		int h = desktop["height"]?.GetValue<int>() ?? -1;

		Got = $"{w}x{h}";
		Wants = "";
		Layout = desktop["theme"]?.GetValue<string>() ?? "";
		Invisible = false;
	}

	/// <summary>Takes a window's current values.</summary>
	internal void UpdateWindow(JsonObject window)
	{
		Raw = window;

		var b = window["bounds"];
		int w = b?["w"]?.GetValue<int>() ?? -1;
		int h = b?["h"]?.GetValue<int>() ?? -1;

		Got = $"{w}x{h}";

		// A window says its state here rather than a wanted size, because that
		// is the thing about a window you actually want at a glance.
		var st = window["state"];
		var flags = new List<string>();
		if (st?["active"]?.GetValue<bool>() == true) flags.Add("[green]active[/]");
		if (st?["modal"]?.GetValue<bool>() == true) flags.Add("[yellow]modal[/]");

		Wants = string.Join(" ", flags);
		Layout = window["root"]?.GetValue<string>() ?? "";
		Invisible = w == 0 || h == 0;
	}

	/// <summary>Takes this node's current values from a fresh tree reply.</summary>
	internal void Update(JsonObject node)
	{
		Raw = node;

		var b = node["bounds"];
		var d = node["desired"];

		int w = b?["w"]?.GetValue<int>() ?? -1;
		int h = b?["h"]?.GetValue<int>() ?? -1;
		int dw = d?["w"]?.GetValue<int>() ?? w;
		int dh = d?["h"]?.GetValue<int>() ?? h;

		Got = $"{w}x{h}";

		// Only where they differ: a column of "wants" repeating what every
		// control already got buries the handful that did not get it.
		Wants = dw == w && dh == h ? "" : $"[yellow]{dw}x{dh}[/]";

		Layout = node["layout"]?.GetValue<string>() ?? "";
		Invisible = w == 0 || h == 0;
	}

	private void Set(ref string field, string value)
	{
		if (field == value) return;
		field = value;
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Cells)));
	}

	private void Set(ref bool field, bool value)
	{
		if (field == value) return;
		field = value;
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Cells)));
	}

	/// <summary>Keeps a control's own text from being read as markup.</summary>
	private static string Escape(string text) => text.Replace("[", "[[");
}
