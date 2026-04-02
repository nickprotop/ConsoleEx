using ClinicFlow.Data;
using ClinicFlow.UI;

namespace ClinicFlow.Helpers;

internal static class MarkupHelpers
{
    public static string FormatTimestamp(DateTime timestamp)
    {
        return $"[{UIConstants.MutedHex}]{timestamp:HH:mm}[/]";
    }

    public static string EventTypeLabel(EventType type) => type switch
    {
        EventType.Alert => $"[bold {UIConstants.CriticalHex}]!! ALERT[/]",
        EventType.Vitals => $"[{UIConstants.NormalHex}]<3 VITALS[/]",
        EventType.Medication => $"[{UIConstants.MedicationHex}]Rx MED[/]",
        EventType.Note => $"[{UIConstants.WarningHex}]>> NOTE[/]",
        EventType.Order => $"[{UIConstants.OrdersHex}]+ ORDER[/]",
        EventType.Discharge => $"[{UIConstants.DischargeHex}]-> DISCH[/]",
        _ => $"[{UIConstants.SecondaryHex}]  EVENT[/]"
    };

    public static string EventColor(EventType type) => type switch
    {
        EventType.Alert => UIConstants.CriticalHex,
        EventType.Vitals => UIConstants.NormalHex,
        EventType.Medication => UIConstants.MedicationHex,
        EventType.Note => UIConstants.WarningHex,
        EventType.Order => UIConstants.OrdersHex,
        EventType.Discharge => UIConstants.DischargeHex,
        _ => UIConstants.SecondaryHex
    };

    public static string StatusLabel(PatientStatus status) => status switch
    {
        PatientStatus.Critical => $"[{UIConstants.CriticalHex}]CRIT[/]",
        PatientStatus.Watch => $"[{UIConstants.WarningHex}]WATCH[/]",
        PatientStatus.Stable => $"[{UIConstants.NormalHex}]STABLE[/]",
        _ => ""
    };

    public static string StatusDot(PatientStatus status) => status switch
    {
        PatientStatus.Critical => $"[{UIConstants.CriticalHex}]o[/]",
        PatientStatus.Watch => $"[{UIConstants.WarningHex}]o[/]",
        PatientStatus.Stable => $"[{UIConstants.NormalHex}]o[/]",
        _ => " "
    };

    public static string FormatVitalLine(string label, string value, string unit, string colorHex)
    {
        return $"  [{UIConstants.SecondaryHex}]{label,-16}[/][bold {colorHex}]{value}[/] [{UIConstants.MutedHex}]{unit}[/]";
    }
}
