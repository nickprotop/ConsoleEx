using Spectre.Console;

namespace ConsoleTopExample.Helpers;

internal static class UIConstants
{
    #region Timing

    public const int RefreshIntervalMs = 1000;
    public const int PrimeDelayMs = 300;
    public const int NotificationTimeoutShortMs = 2000;
    public const int NotificationTimeoutMediumMs = 2500;
    public const int NotificationTimeoutLongMs = 3000;

    #endregion

    #region History

    public const int MaxHistoryPoints = 50;

    #endregion

    #region Layout Thresholds

    public const int MemoryLayoutThresholdWidth = 80;
    public const int CpuLayoutThresholdWidth = 80;
    public const int NetworkLayoutThresholdWidth = 80;
    public const int StorageLayoutThresholdWidth = 90;

    #endregion

    #region Column Widths

    public const int FixedTextColumnWidth = 40;
    public const int SeparatorColumnWidth = 1;
    public const int ProcessDetailColumnWidth = 40;
    public const int ProcessActionsModalWidth = 70;
    public const int ProcessActionsModalHeight = 18;

    #endregion

    #region Graph Sizing

    public const int MetricsBarWidth = 12;
    public const int TabBarWidth = 35;
    public const int CpuCoreBarWidth = 16;
    public const int SparklineHeight = 6;
    public const int CpuCoreSparklineHeight = 4;
    public const int NetworkCombinedSparklineHeight = 10;
    public const int StorageIoSparklineHeight = 8;

    #endregion

    #region Label Widths

    public const int MetricsCpuLabelWidth = 6;
    public const int MetricsMemLabelWidth = 12;
    public const int MetricsNetLabelWidth = 8;
    public const int MemoryBarLabelWidth = 9;
    public const int CpuBarLabelWidth = 10;
    public const int CpuCoreLabelWidth = 3;
    public const int NetworkBarLabelWidth = 10;
    public const int StorageBarLabelWidth = 10;

    #endregion

    #region Process List Formatting

    public const int PidPadLeft = 8;
    public const int CpuPercentPadLeft = 7;
    public const int MemPercentPadLeft = 7;
    public const int TopConsumerCount = 5;
    public const int InterfaceNameMaxLength = 15;
    public const int InterfaceNameTruncLength = 12;

    #endregion

    #region Button Widths

    public const int TerminateButtonWidth = 14;
    public const int ForceKillButtonWidth = 14;
    public const int SigtermButtonWidth = 12;
    public const int SigkillButtonWidth = 12;
    public const int CloseButtonWidth = 10;
    public const int ActionsButtonWidth = 15;
    public const int SortDropdownWidth = 20;

    #endregion

    #region Colors

    public static readonly Color WindowBackground = Color.Grey11;
    public static readonly Color WindowForeground = Color.Grey93;
    public static readonly Color StatusBarBackground = Color.Grey15;
    public static readonly Color StatusBarForeground = Color.Grey93;
    public static readonly Color BottomBarForeground = Color.Grey70;
    public static readonly Color SeparatorColor = Color.Grey23;
    public static readonly Color MetricsBoxBackground = Color.Grey15;
    public static readonly Color MetricsBoxForeground = Color.Grey93;
    public static readonly Color DetailPanelBackground = Color.Grey19;
    public static readonly Color DetailPanelForeground = Color.Grey93;
    public static readonly Color AccentColor = Color.Cyan1;
    public static readonly Color BarUnfilledColor = Color.Grey35;
    public static readonly Color SparklineBackground = Color.Grey15;
    public static readonly Color RuleColor = Color.Grey23;
    public static readonly Color ProcessHighlightBg = Color.Grey35;
    public static readonly Color ProcessHighlightFg = Color.White;

    #endregion

    #region Separator Markup

    public const string SectionSeparator = "[grey23]────────────────────────────────────────[/]";

    #endregion
}
