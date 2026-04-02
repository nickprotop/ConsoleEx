using SharpConsoleUI;

namespace ClinicFlow.UI;

internal static class UIConstants
{
    #region Timing

    public const int VitalsUpdateIntervalMs = 4000;
    public const int EventGenerationIntervalMs = 20000;
    public const int AlertChanceIntervalMs = 50000;

    #endregion

    #region Layout

    public const double SidebarFlex = 0.18;
    public const double TimelineFlex = 0.57;
    public const double VitalsFlex = 0.25;
    public const int SparklineHeight = 3;
    public const int SparklineMaxPoints = 20;
    public const int TimelineTimestampWidth = 8;

    #endregion

    #region Backgrounds

    public static readonly Color BaseBg = new Color(0x1a, 0x1a, 0x2e);
    public static readonly Color TimelineBg = new Color(0x1e, 0x1e, 0x36);
    public static readonly Color HeaderBg = new Color(0x16, 0x16, 0x2a);
    public static readonly Color SelectionBg = new Color(0x1c, 0x2a, 0x30);
    public static readonly Color ExpandedBg = new Color(0x22, 0x22, 0x3c);

    #endregion

    #region Semantic Colors

    public static readonly Color Critical = new Color(0xff, 0x6b, 0x6b);
    public static readonly Color Warning = new Color(0xff, 0xd9, 0x3d);
    public static readonly Color Normal = new Color(0x4e, 0xcd, 0xc4);
    public static readonly Color Medication = new Color(0xa7, 0x8b, 0xfa);
    public static readonly Color Orders = new Color(0x45, 0xb7, 0xd1);
    public static readonly Color Accent = new Color(0x00, 0xd4, 0xaa);
    public static readonly Color Discharge = new Color(0x88, 0x88, 0x88);

    #endregion

    #region Text Colors

    public static readonly Color MutedText = new Color(0x66, 0x66, 0x66);
    public static readonly Color SecondaryText = new Color(0x88, 0x88, 0x88);
    public static readonly Color PrimaryText = new Color(0xc8, 0xc8, 0xd4);
    public static readonly Color BrightText = Color.White;
    public static readonly Color SeparatorColor = new Color(0x2a, 0x2a, 0x4a);

    #endregion

    #region Vitals Thresholds

    public static string HrColor(int hr) => hr switch
    {
        < 50 => CriticalHex,
        < 60 => WarningHex,
        <= 100 => NormalHex,
        <= 120 => WarningHex,
        _ => CriticalHex
    };

    public static string BpColor(int systolic) => systolic switch
    {
        < 130 => NormalHex,
        < 160 => WarningHex,
        _ => CriticalHex
    };

    public static string SpO2Color(int spo2) => spo2 switch
    {
        >= 95 => NormalHex,
        >= 92 => WarningHex,
        _ => CriticalHex
    };

    public static string TempColor(double temp) => temp switch
    {
        < 36.0 => CriticalHex,
        <= 37.5 => NormalHex,
        <= 38.5 => WarningHex,
        _ => CriticalHex
    };

    public const string CriticalHex = "#ff6b6b";
    public const string WarningHex = "#ffd93d";
    public const string NormalHex = "#4ecdc4";
    public const string MedicationHex = "#a78bfa";
    public const string OrdersHex = "#45b7d1";
    public const string AccentHex = "#00d4aa";
    public const string DischargeHex = "#888888";
    public const string MutedHex = "#666666";
    public const string SecondaryHex = "#888888";
    public const string PrimaryHex = "#c8c8d4";

    #endregion
}
