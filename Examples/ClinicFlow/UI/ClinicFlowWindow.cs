using ClinicFlow.Data;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;

namespace ClinicFlow.UI;

internal sealed class ClinicFlowWindow
{
    private readonly ConsoleWindowSystem _windowSystem;
    private readonly List<Patient> _patients;

    private PatientSidebar _sidebar = null!;
    private TimelinePanel _timeline = null!;
    private VitalsPanel _vitals = null!;
    private ActionBar _actionBar = null!;
    private Window _window = null!;

    private FocusRegion _focusRegion = FocusRegion.Sidebar;
    private int _activePatientIndex = -1;

    private readonly VitalsSimulator _simulator = new();

    public ClinicFlowWindow(ConsoleWindowSystem windowSystem, List<Patient> patients)
    {
        _windowSystem = windowSystem;
        _patients = patients;
    }

    public void Create()
    {
        _sidebar = new PatientSidebar(_patients);
        _timeline = new TimelinePanel();
        _vitals = new VitalsPanel();
        _actionBar = new ActionBar();

        // Wire sidebar callback
        _sidebar.OnPatientSelected(OnPatientSelected);

        var sidebarControls = _sidebar.Build();
        var timelineControl = _timeline.Build();
        var vitalsControls = _vitals.Build();
        var actionBarControls = _actionBar.Build();

        // Build top header grid
        var clockMarkup = new MarkupControl(new List<string>
        {
            $"[{UIConstants.MutedHex}]{DateTime.Now:HH:mm:ss}  Dr. Chen[/]"
        })
        {
            BackgroundColor = UIConstants.HeaderBg,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        clockMarkup.Name = "headerClock";

        var headerGrid = Controls.HorizontalGrid()
            .WithAlignment(HorizontalAlignment.Stretch)
            .StickyTop()
            .Column(col =>
            {
                col.Flex(UIConstants.SidebarFlex + UIConstants.TimelineFlex);
                col.Add(new MarkupControl(new List<string>
                {
                    $"[bold {UIConstants.AccentHex}]ClinicFlow[/] [{UIConstants.MutedHex}]| Ward 3B — Cardiology[/]"
                })
                {
                    BackgroundColor = UIConstants.HeaderBg
                });
            })
            .Column(col =>
            {
                col.Flex(UIConstants.VitalsFlex);
                col.Add(clockMarkup);
            })
            .Build();

        headerGrid.BackgroundColor = UIConstants.HeaderBg;

        // Build main content grid
        var mainGrid = Controls.HorizontalGrid()
            .WithAlignment(HorizontalAlignment.Stretch)
            .WithVerticalAlignment(VerticalAlignment.Fill)
            .Column(col =>
            {
                col.Flex(UIConstants.SidebarFlex);
                foreach (var ctrl in sidebarControls)
                    col.Add(ctrl);
            })
            .Column(col =>
            {
                col.Flex(UIConstants.TimelineFlex);
                col.Add(timelineControl);
            })
            .Column(col =>
            {
                col.Flex(UIConstants.VitalsFlex);
                foreach (var ctrl in vitalsControls)
                    col.Add(ctrl);
            })
            .Build();

        // Build bottom action bar grid
        var bottomGrid = Controls.HorizontalGrid()
            .WithAlignment(HorizontalAlignment.Stretch)
            .StickyBottom()
            .Column(col =>
            {
                col.Flex(UIConstants.SidebarFlex + UIConstants.TimelineFlex);
                col.Add(actionBarControls[0]);
            })
            .Column(col =>
            {
                col.Flex(UIConstants.VitalsFlex);
                col.Add(actionBarControls[1]);
            })
            .Build();

        bottomGrid.BackgroundColor = UIConstants.HeaderBg;

        // Build separators
        var topSeparator = Controls.RuleBuilder()
            .WithColor(UIConstants.SeparatorColor)
            .StickyTop()
            .Build();

        var bottomSeparator = Controls.RuleBuilder()
            .WithColor(UIConstants.SeparatorColor)
            .StickyBottom()
            .Build();

        _window = new WindowBuilder(_windowSystem)
            .Borderless()
            .Maximized()
            .HideTitle()
            .HideTitleButtons()
            .Resizable(false)
            .Movable(false)
            .Closable(false)
            .Minimizable(false)
            .Maximizable(false)
            .WithForegroundColor(UIConstants.PrimaryText)
            .WithBackgroundColor(UIConstants.BaseBg)
            .WithAsyncWindowThread(UpdateLoopAsync)
            .OnKeyPressed(HandleKeyPress)
            .AddControl(headerGrid)
            .AddControl(topSeparator)
            .AddControl(mainGrid)
            .AddControl(bottomSeparator)
            .AddControl(bottomGrid)
            .Build();

        _windowSystem.AddWindow(_window);

        OnPatientSelected(0);
        // Set initial focus to sidebar list
        ApplyFocusRegion();
    }

    private void OnPatientSelected(int index)
    {
        if (index < 0 || index >= _patients.Count) return;
        _activePatientIndex = index;

        var patient = _patients[index];
        _timeline.LoadPatient(patient);
        _vitals.Update(patient);
    }

    private void HandleKeyPress(object? sender, KeyPressedEventArgs e)
    {
        var key = e.KeyInfo;

        // Global keys — always available
        switch (key.Key)
        {
            case ConsoleKey.Escape:
            case ConsoleKey.F10:
                _windowSystem.Shutdown(0);
                e.Handled = true;
                return;

            case ConsoleKey.Q when key.Modifiers == 0:
                _windowSystem.Shutdown(0);
                e.Handled = true;
                return;

            case ConsoleKey.Tab:
                if ((key.Modifiers & ConsoleModifiers.Shift) != 0)
                    CycleFocusRegion(-1);
                else
                    CycleFocusRegion(1);
                e.Handled = true;
                return;
        }

        // Region-specific keys
        switch (_focusRegion)
        {
            case FocusRegion.Sidebar:
                HandleSidebarKey(key, e);
                break;

            case FocusRegion.Timeline:
                HandleTimelineKey(key, e);
                break;

            case FocusRegion.Vitals:
                HandleVitalsKey(key, e);
                break;
        }
    }

    private void HandleSidebarKey(ConsoleKeyInfo key, KeyPressedEventArgs e)
    {
        switch (key.Key)
        {
            case ConsoleKey.UpArrow:
                _sidebar.MoveSelection(-1);
                e.Handled = true;
                break;

            case ConsoleKey.DownArrow:
                _sidebar.MoveSelection(1);
                e.Handled = true;
                break;

            case ConsoleKey.Enter:
            case ConsoleKey.Spacebar:
                // Selection already fires via ListControl.SelectedIndexChanged
                // but explicitly confirm the current selection
                int idx = _sidebar.SelectedIndex;
                if (idx >= 0)
                    OnPatientSelected(idx);
                e.Handled = true;
                break;
        }
    }

    private void HandleTimelineKey(ConsoleKeyInfo key, KeyPressedEventArgs e)
    {
        switch (key.Key)
        {
            case ConsoleKey.UpArrow:
                _timeline.MoveSelection(-1);
                e.Handled = true;
                break;

            case ConsoleKey.DownArrow:
                _timeline.MoveSelection(1);
                e.Handled = true;
                break;

            case ConsoleKey.Enter:
                _timeline.ToggleExpansion();
                e.Handled = true;
                break;

            case ConsoleKey.N when key.Modifiers == 0 && _activePatientIndex >= 0:
                _timeline.PrependEvent(CreateManualEvent(EventType.Note, "Clinical note entered", "Physician note added manually."));
                e.Handled = true;
                break;

            case ConsoleKey.P when key.Modifiers == 0 && _activePatientIndex >= 0:
                _timeline.PrependEvent(CreateManualEvent(EventType.Medication, "Medication prescribed", "Prescription entered manually by Dr. Chen."));
                e.Handled = true;
                break;

            case ConsoleKey.O when key.Modifiers == 0 && _activePatientIndex >= 0:
                _timeline.PrependEvent(CreateManualEvent(EventType.Order, "Exam ordered", "Clinical order placed by Dr. Chen."));
                e.Handled = true;
                break;

            case ConsoleKey.A when key.Modifiers == 0 && _activePatientIndex >= 0:
                _timeline.PrependEvent(CreateManualEvent(EventType.Alert, "Alert issued", "Manual alert sent to care team."));
                e.Handled = true;
                break;
        }
    }

    private void HandleVitalsKey(ConsoleKeyInfo key, KeyPressedEventArgs e)
    {
        switch (key.Key)
        {
            case ConsoleKey.R when key.Modifiers == 0 && _activePatientIndex >= 0:
                _vitals.Update(_patients[_activePatientIndex]);
                e.Handled = true;
                break;

            case ConsoleKey.H when key.Modifiers == 0 && _activePatientIndex >= 0:
                // Refresh vitals with full history (same as R for now)
                _vitals.Update(_patients[_activePatientIndex]);
                e.Handled = true;
                break;
        }
    }

    private static TimelineEvent CreateManualEvent(EventType type, string summary, string detail)
    {
        return new TimelineEvent
        {
            Timestamp = DateTime.Now,
            Type = type,
            Summary = summary,
            Detail = detail
        };
    }

    private void CycleFocusRegion(int direction)
    {
        int count = Enum.GetValues<FocusRegion>().Length;
        int current = (int)_focusRegion;
        int next = ((current + direction) % count + count) % count;
        _focusRegion = (FocusRegion)next;
        _actionBar.SetFocusRegion(_focusRegion);
        ApplyFocusRegion();
    }

    /// <summary>
    /// Sets real keyboard focus on the control matching the current focus region.
    /// </summary>
    private void ApplyFocusRegion()
    {
        IFocusableControl? target = _focusRegion switch
        {
            FocusRegion.Sidebar => _sidebar.FocusTarget,
            FocusRegion.Timeline => _timeline.Panel as IFocusableControl,
            _ => null
        };

        if (target != null)
            _window.FocusManager.SetFocus(target, FocusReason.Keyboard);
    }

    private async Task UpdateLoopAsync(Window window, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(UIConstants.VitalsUpdateIntervalMs, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            // Snapshot the active index on the background thread
            int snapshotIndex = _activePatientIndex;
            if (snapshotIndex < 0 || snapshotIndex >= _patients.Count)
                continue;

            var patient = _patients[snapshotIndex];

            // Generate data on background thread
            bool shouldUpdateVitals = _simulator.ShouldUpdateVitals(UIConstants.VitalsUpdateIntervalMs);
            VitalsReading? reading = shouldUpdateVitals ? _simulator.GenerateReading(patient) : null;
            if (reading != null)
                _simulator.ApplyReading(patient, reading);

            TimelineEvent? randomEvent = null;
            if (_simulator.ShouldGenerateEvent(UIConstants.VitalsUpdateIntervalMs))
                randomEvent = _simulator.GenerateRandomEvent();

            TimelineEvent? alertEvent = null;
            if (_simulator.ShouldGenerateAlert(UIConstants.VitalsUpdateIntervalMs, patient.Status))
                alertEvent = _simulator.GenerateAlertEvent(patient);

            var now = DateTime.Now;

            // Marshal all UI mutations to the UI thread
            _windowSystem.EnqueueOnUIThread(() =>
            {
                // Re-read the active index inside the lambda to guard against patient switches
                var currentPatient = _activePatientIndex == snapshotIndex
                    ? _patients[snapshotIndex]
                    : null;

                if (currentPatient == null)
                    return; // Patient changed while update was in flight — skip

                if (reading != null)
                    _vitals.Update(currentPatient);

                if (randomEvent != null)
                    _timeline.PrependEvent(randomEvent);

                if (alertEvent != null)
                    _timeline.PrependEvent(alertEvent);

                // Update the clock display
                var clockControl = window.FindControl<MarkupControl>("headerClock");
                if (clockControl != null)
                {
                    clockControl.SetContent(new List<string>
                    {
                        $"[{UIConstants.MutedHex}]{now:HH:mm:ss}  Dr. Chen[/]"
                    });
                }
            });
        }
    }
}
