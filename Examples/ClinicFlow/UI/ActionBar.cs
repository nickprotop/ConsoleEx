using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;

namespace ClinicFlow.UI;

internal enum FocusRegion { Sidebar, Timeline, Vitals }

internal sealed class ActionBar
{
    private MarkupControl _actionsMarkup = null!;
    private MarkupControl _helpMarkup = null!;
    private FocusRegion _currentRegion = FocusRegion.Sidebar;

    public IWindowControl[] Build()
    {
        _actionsMarkup = new MarkupControl(new List<string>
        {
            BuildActionsText(FocusRegion.Sidebar)
        })
        {
            BackgroundColor = UIConstants.HeaderBg,
            StickyPosition = StickyPosition.Bottom
        };

        _helpMarkup = new MarkupControl(new List<string>
        {
            $"[{UIConstants.MutedHex}]Tab: switch panel  Arrow keys: navigate  Enter/Space: select[/]"
        })
        {
            BackgroundColor = UIConstants.HeaderBg,
            StickyPosition = StickyPosition.Bottom,
            HorizontalAlignment = SharpConsoleUI.Layout.HorizontalAlignment.Right
        };

        return new IWindowControl[] { _actionsMarkup, _helpMarkup };
    }

    public void SetFocusRegion(FocusRegion region)
    {
        _currentRegion = region;
        if (_actionsMarkup != null)
        {
            _actionsMarkup.SetContent(new List<string>
            {
                BuildActionsText(region)
            });
        }
    }

    private static string BuildActionsText(FocusRegion region)
    {
        return region switch
        {
            FocusRegion.Sidebar =>
                $"[{UIConstants.AccentHex}][Enter][/] [{UIConstants.PrimaryHex}]Select Patient[/]   " +
                $"[{UIConstants.AccentHex}]\u25b4\u25be[/] [{UIConstants.PrimaryHex}]Navigate[/]",

            FocusRegion.Timeline =>
                $"[{UIConstants.WarningHex}][N][/] [{UIConstants.PrimaryHex}]Add Note[/]  " +
                $"[{UIConstants.MedicationHex}][P][/] [{UIConstants.PrimaryHex}]Prescribe[/]  " +
                $"[{UIConstants.OrdersHex}][O][/] [{UIConstants.PrimaryHex}]Order Exam[/]  " +
                $"[{UIConstants.CriticalHex}][A][/] [{UIConstants.PrimaryHex}]Alert Team[/]",

            FocusRegion.Vitals =>
                $"[{UIConstants.NormalHex}][R][/] [{UIConstants.PrimaryHex}]Refresh Vitals[/]  " +
                $"[{UIConstants.AccentHex}][H][/] [{UIConstants.PrimaryHex}]History View[/]",

            _ => string.Empty
        };
    }
}
