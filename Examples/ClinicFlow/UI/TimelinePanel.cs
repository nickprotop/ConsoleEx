using ClinicFlow.Data;
using ClinicFlow.Helpers;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;

namespace ClinicFlow.UI;

internal sealed class TimelinePanel
{
    private ScrollablePanelControl _panel = null!;
    private int _selectedEventIndex = -1;
    private Patient? _currentPatient;

    // Pool of reusable controls: two per event (event markup + separator), plus one for the empty state.
    private readonly List<MarkupControl> _eventPool = new();
    private readonly List<MarkupControl> _separatorPool = new();

    public ScrollablePanelControl Panel => _panel;

    public IWindowControl Build()
    {
        _panel = Controls.ScrollablePanel()
            .WithName("timelinePanel")
            .WithBackgroundColor(UIConstants.TimelineBg)
            .WithVerticalScroll(ScrollMode.Scroll)
            .WithScrollbarPosition(ScrollbarPosition.Right)
            .WithMouseWheel(true)
            .WithAutoScroll(true)
            .WithAlignment(HorizontalAlignment.Stretch)
            .Build();

        return _panel;
    }

    public void LoadPatient(Patient patient)
    {
        _currentPatient = patient;
        _selectedEventIndex = -1;
        RebuildTimeline();
    }

    public void PrependEvent(TimelineEvent ev)
    {
        if (_currentPatient == null) return;

        _currentPatient.TimelineEvents.Insert(0, ev);

        if (_selectedEventIndex >= 0)
            _selectedEventIndex++;

        RebuildTimeline();
    }

    public void MoveSelection(int direction)
    {
        if (_currentPatient == null || _currentPatient.TimelineEvents.Count == 0)
            return;

        int count = _currentPatient.TimelineEvents.Count;
        int previousSelection = _selectedEventIndex;

        if (_selectedEventIndex < 0)
            _selectedEventIndex = direction > 0 ? 0 : count - 1;
        else
            _selectedEventIndex = Math.Clamp(_selectedEventIndex + direction, 0, count - 1);

        if (_selectedEventIndex == previousSelection)
            return;

        // Update only the two affected rows rather than rebuilding the entire timeline.
        RefreshEventControl(previousSelection);
        RefreshEventControl(_selectedEventIndex);
    }

    public void ToggleExpansion()
    {
        if (_currentPatient == null || _selectedEventIndex < 0) return;
        if (_selectedEventIndex >= _currentPatient.TimelineEvents.Count) return;

        var ev = _currentPatient.TimelineEvents[_selectedEventIndex];
        ev.IsExpanded = !ev.IsExpanded;

        // The expanded state changes the line count of this row, so a full rebuild is needed.
        RebuildTimeline();
    }

    public void Refresh()
    {
        RebuildTimeline();
    }

    public void RebuildTimeline()
    {
        if (_panel == null) return;

        _panel.ClearContents();

        if (_currentPatient == null || _currentPatient.TimelineEvents.Count == 0)
        {
            var emptyMarkup = GetOrCreateEventControl(0);
            emptyMarkup.SetContent(new List<string>
            {
                $"[{UIConstants.MutedHex}]  No events recorded.[/]"
            });
            emptyMarkup.BackgroundColor = UIConstants.TimelineBg;
            emptyMarkup.Margin = new Margin(0, 1, 0, 0);
            _panel.AddControl(emptyMarkup);
            return;
        }

        var events = _currentPatient.TimelineEvents;

        for (int i = 0; i < events.Count; i++)
        {
            var eventMarkup = GetOrCreateEventControl(i);
            ApplyEventContent(eventMarkup, events[i], i);
            eventMarkup.Margin = new Margin(0, 0, 0, 0);
            _panel.AddControl(eventMarkup);

            if (i < events.Count - 1)
            {
                var separator = GetOrCreateSeparatorControl(i);
                separator.SetContent(new List<string>
                {
                    $"[{UIConstants.MutedHex}] - - - - - - - - - - - - - - - - - - - -[/]"
                });
                separator.BackgroundColor = UIConstants.TimelineBg;
                _panel.AddControl(separator);
            }
        }
    }

    // Updates a single event row in-place without clearing the panel.
    private void RefreshEventControl(int eventIndex)
    {
        if (_currentPatient == null) return;
        if (eventIndex < 0 || eventIndex >= _currentPatient.TimelineEvents.Count) return;
        if (eventIndex >= _eventPool.Count) return;

        var ctrl = _eventPool[eventIndex];
        ApplyEventContent(ctrl, _currentPatient.TimelineEvents[eventIndex], eventIndex);
    }

    private void ApplyEventContent(MarkupControl ctrl, TimelineEvent ev, int index)
    {
        bool isSelected = index == _selectedEventIndex;
        bool isExpanded = ev.IsExpanded;

        Color bg = isSelected ? UIConstants.SelectionBg
                 : isExpanded ? UIConstants.ExpandedBg
                 : UIConstants.TimelineBg;

        string colorHex = MarkupHelpers.EventColor(ev.Type);
        string cursor = isSelected ? $"[{colorHex}]<[/]" : " ";
        string timestamp = MarkupHelpers.FormatTimestamp(ev.Timestamp);
        string typeLabel = MarkupHelpers.EventTypeLabel(ev.Type);

        var lines = new List<string>
        {
            $" {timestamp}  {typeLabel}  [{UIConstants.PrimaryHex}]{ev.Summary}[/] {cursor}"
        };

        if (isExpanded && !string.IsNullOrWhiteSpace(ev.Detail))
        {
            lines.Add($"  [{UIConstants.SecondaryHex}]{ev.Detail}[/]");
        }

        ctrl.SetContent(lines);
        ctrl.BackgroundColor = bg;
    }

    private MarkupControl GetOrCreateEventControl(int index)
    {
        while (_eventPool.Count <= index)
            _eventPool.Add(new MarkupControl(new List<string>()));
        return _eventPool[index];
    }

    private MarkupControl GetOrCreateSeparatorControl(int index)
    {
        while (_separatorPool.Count <= index)
            _separatorPool.Add(new MarkupControl(new List<string>()));
        return _separatorPool[index];
    }
}
