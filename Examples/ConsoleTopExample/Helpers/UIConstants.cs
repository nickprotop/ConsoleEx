using SharpConsoleUI;

namespace ConsoleTopExample.Helpers;

internal static class UIConstants
{
    #region Timing

    public const int RefreshIntervalMs = 1000;
    public const int PrimeDelayMs = 300;
    public const int NotificationTimeoutShortMs = 2000;
    public const int NotificationTimeoutMediumMs = 2500;
    public const int NotificationTimeoutLongMs = 3000;
    public const int BarAnimationMs = 150;
    public const int FadeInMs = 300;
    public const int TabCrossfadeMs = 200;
    public const int DetailFadeMs = 150;

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
    public const int MemMbPadLeft = 6;
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

    #region Colors — Deep Blue-Teal Palette

    // Window background gradient endpoints
    public static readonly Color BaseBg = new(0x0d, 0x11, 0x17);
    public static readonly Color BaseEnd = new(0x1a, 0x23, 0x32);

    // Semi-transparent overlays (alpha-blended over window gradient)
    public static readonly Color PanelBg = new(15, 20, 30, 200);
    public static readonly Color CardBg = new(20, 28, 40, 180);

    // Metrics grid sub-panel tinted backgrounds (semi-transparent, distinct hues)
    public static readonly Color MetricsCpuBg = new(10, 40, 60, 180);      // blue-teal tint
    public static readonly Color MetricsMemBg = new(30, 20, 55, 180);      // indigo/purple tint
    public static readonly Color MetricsNetBg = new(10, 45, 35, 180);      // teal-green tint

    // Right panel background
    public static readonly Color RightPanelBg = new(10, 16, 26, 210);

    // Status bars — darkest layer
    public static readonly Color HeaderBg = new(0x0a, 0x0e, 0x14);

    // Accent colors
    public static readonly Color Accent = Color.Cyan1;
    public static readonly Color AccentSecondary = new(0x00, 0xd4, 0xaa);

    // Text colors
    public static readonly Color PrimaryText = new(0xc8, 0xd4, 0xe0);
    public static readonly Color MutedText = new(0x4a, 0x60, 0x70);

    // Separator / divider
    public static readonly Color SeparatorColor = new(0x1e, 0x3a, 0x4a);

    // Threshold colors
    public static readonly Color Critical = new(0xff, 0x6b, 0x6b);
    public static readonly Color Warning = new(0xff, 0xd9, 0x3d);
    public static readonly Color Normal = new(0x4e, 0xcd, 0xc4);

    // Bar unfilled
    public static readonly Color BarUnfilledColor = new(0x1e, 0x2a, 0x3a);

    // Process highlight
    public static readonly Color ProcessHighlightBg = new(0x1e, 0x3a, 0x4a);
    public static readonly Color ProcessHighlightFg = Color.White;

    #endregion

    #region Gradient Definitions

    // Bar graph gradients
    public static readonly Color[] GradientHealthy = [new(0x4e, 0xcd, 0xc4), new(0xff, 0xd9, 0x3d), new(0xff, 0x6b, 0x6b)];
    public static readonly Color[] GradientBaseHealthy = [new(0x0d, 0x94, 0x88), new(0x4e, 0xcd, 0xc4)];
    public static readonly Color[] GradientIoRead = [new(0x2a, 0x60, 0x90), new(0x45, 0xb7, 0xd1)];
    public static readonly Color[] GradientIoWrite = [new(0xff, 0xd9, 0x3d), new(0xff, 0x6b, 0x6b)];
    public static readonly Color[] GradientNetUpload = [new(0x2a, 0x60, 0x90), new(0x45, 0xb7, 0xd1)];
    public static readonly Color[] GradientNetDownload = [new(0x4e, 0xcd, 0xc4), new(0x00, 0xd4, 0xaa)];

    // Sparkline gradients
    public static readonly Color[] SparkMemUsed = [new(0x1a, 0x6b, 0x4a), new(0x4e, 0xcd, 0xc4), new(0xff, 0xd9, 0x3d), new(0xff, 0x6b, 0x6b)];
    public static readonly Color[] SparkMemCached = [new(0x2a, 0x60, 0x90), new(0x45, 0xb7, 0xd1)];
    public static readonly Color[] SparkMemAvailable = [new(0x00, 0xd4, 0xaa), new(0x4e, 0xcd, 0xc4)];
    public static readonly Color[] SparkCpuUser = [new(0x4e, 0xcd, 0xc4), new(0xff, 0xd9, 0x3d), new(0xff, 0x6b, 0x6b)];
    public static readonly Color[] SparkCpuSystem = [new(0x45, 0xb7, 0xd1), new(0xff, 0xd9, 0x3d), new(0xff, 0x6b, 0x6b)];
    public static readonly Color[] SparkCpuTotal = [new(0x0d, 0x94, 0x88), new(0x4e, 0xcd, 0xc4), new(0xff, 0xd9, 0x3d), new(0xff, 0x6b, 0x6b)];
    public static readonly Color[] SparkCpuPerCore = [new(0x1a, 0x6b, 0x4a), new(0x4e, 0xcd, 0xc4), new(0xff, 0xd9, 0x3d), new(0xff, 0x6b, 0x6b)];
    public static readonly Color[] SparkNetDownload = [new(0x4e, 0xcd, 0xc4), new(0x00, 0xd4, 0xaa)];
    public static readonly Color[] SparkNetUpload = [new(0x2a, 0x60, 0x90), new(0x45, 0xb7, 0xd1)];
    public static readonly Color[] SparkStorageRead = [new(0x2a, 0x60, 0x90), new(0x45, 0xb7, 0xd1)];
    public static readonly Color[] SparkStorageWrite = [new(0xff, 0xd9, 0x3d), new(0xff, 0x6b, 0x6b)];

    #endregion

    #region Threshold Helpers

    public static string ThresholdColor(double value) => value switch
    {
        < 60 => $"#{Normal.R:x2}{Normal.G:x2}{Normal.B:x2}",
        < 85 => $"#{Warning.R:x2}{Warning.G:x2}{Warning.B:x2}",
        _    => $"#{Critical.R:x2}{Critical.G:x2}{Critical.B:x2}"
    };

    public static Color ThresholdColorValue(double value) => value switch
    {
        < 60 => Normal,
        < 85 => Warning,
        _ => Critical
    };

    #endregion
}
