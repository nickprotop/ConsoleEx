using ClinicFlow.Data;
using ClinicFlow.Helpers;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;

namespace ClinicFlow.UI;

internal sealed class PatientSidebar
{
    private readonly List<Patient> _patients;
    private ListControl _list = null!;
    private MarkupControl _footer = null!;
    private Action<int>? _onPatientSelected;

    public PatientSidebar(List<Patient> patients)
    {
        _patients = patients;
    }

    public int SelectedIndex => _list?.SelectedIndex ?? -1;

    /// <summary>
    /// Returns the ListControl for focus management.
    /// </summary>
    public IFocusableControl? FocusTarget => _list;

    // Store the callback only. The sole registration point is Build().
    public void OnPatientSelected(Action<int> callback)
    {
        _onPatientSelected = callback;
    }

    public void MoveSelection(int direction)
    {
        if (_list == null) return;
        int count = _patients.Count;
        if (count == 0) return;

        int current = _list.SelectedIndex;
        int next = Math.Clamp(current + direction, 0, count - 1);
        if (next != current)
            _list.SelectedIndex = next;
    }

    public IWindowControl[] Build()
    {
        var header = Controls.Markup()
            .AddLine($"[bold {UIConstants.AccentHex}] PATIENTS[/]")
            .WithBackgroundColor(UIConstants.HeaderBg)
            .StickyTop()
            .Build();

        _list = Controls.List()
            .WithBackgroundColor(UIConstants.BaseBg)
            .WithHighlightColors(UIConstants.BrightText, UIConstants.SelectionBg)
            .WithAlignment(HorizontalAlignment.Stretch)
            .Build();

        // Wire callback if it was registered before Build() was called
        if (_onPatientSelected != null)
            _list.SelectedIndexChanged += (_, idx) => _onPatientSelected?.Invoke(idx);

        _footer = new MarkupControl(new List<string>
        {
            $"[{UIConstants.MutedHex}] {_patients.Count} patients[/]"
        })
        {
            BackgroundColor = UIConstants.HeaderBg,
            StickyPosition = StickyPosition.Bottom
        };

        RefreshList();

        return new IWindowControl[] { header, _list, _footer };
    }

    public void RefreshList()
    {
        if (_list == null) return;

        int prevIndex = _list.SelectedIndex;
        _list.ClearItems();

        foreach (var p in _patients)
        {
            string dot = MarkupHelpers.StatusDot(p.Status);
            string label = MarkupHelpers.StatusLabel(p.Status);
            string nameLine = $"{dot} [bold {UIConstants.PrimaryHex}]{p.Name}[/]  {label}";
            string infoLine = $"  [{UIConstants.MutedHex}]Rm {p.Room} · {p.Age}{p.Sex}[/]";
            var item = new ListItem($"{nameLine}\n{infoLine}");
            _list.AddItem(item);
        }

        if (prevIndex >= 0 && prevIndex < _patients.Count)
            _list.SelectedIndex = prevIndex;

        if (_footer != null)
        {
            _footer.SetContent(new List<string>
            {
                $"[{UIConstants.MutedHex}] {_patients.Count} patients[/]"
            });
        }
    }
}
