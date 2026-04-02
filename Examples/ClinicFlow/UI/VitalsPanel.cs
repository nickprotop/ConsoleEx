using ClinicFlow.Data;
using ClinicFlow.Helpers;
using SharpConsoleUI;
using SharpConsoleUI.Builders;
using SharpConsoleUI.Controls;
using SharpConsoleUI.Layout;

namespace ClinicFlow.UI;

internal sealed class VitalsPanel
{
    private MarkupControl _vitalsMarkup = null!;
    private SparklineControl _hrSparkline = null!;
    private SparklineControl _bpSparkline = null!;
    private SparklineControl _spo2Sparkline = null!;

    public IWindowControl[] Build()
    {
        _vitalsMarkup = new MarkupControl(new List<string>
        {
            MarkupHelpers.FormatVitalLine("Heart Rate", "--", "bpm", UIConstants.NormalHex),
            MarkupHelpers.FormatVitalLine("Blood Pressure", "--/--", "mmHg", UIConstants.NormalHex),
            MarkupHelpers.FormatVitalLine("SpO2", "--", "%", UIConstants.NormalHex),
            MarkupHelpers.FormatVitalLine("Temperature", "--", "°C", UIConstants.NormalHex),
        })
        {
            BackgroundColor = UIConstants.BaseBg
        };

        var vitalsPanel = Controls.ScrollablePanel()
            .Rounded()
            .WithHeader(" Current Vitals")
            .WithBorderColor(UIConstants.SeparatorColor)
            .WithBackgroundColor(UIConstants.BaseBg)
            .WithVerticalScroll(ScrollMode.None)
            .WithAlignment(HorizontalAlignment.Stretch)
            .WithMargin(1, 1, 1, 0)
            .AddControl(_vitalsMarkup)
            .Build();

        _hrSparkline = Controls.Sparkline()
            .WithHeight(UIConstants.SparklineHeight)
            .WithBarColor(UIConstants.Normal)
            .WithBackgroundColor(UIConstants.BaseBg)
            .Build();

        _bpSparkline = Controls.Sparkline()
            .WithHeight(UIConstants.SparklineHeight)
            .WithBarColor(UIConstants.Warning)
            .WithBackgroundColor(UIConstants.BaseBg)
            .Build();

        _spo2Sparkline = Controls.Sparkline()
            .WithHeight(UIConstants.SparklineHeight)
            .WithBarColor(UIConstants.Critical)
            .WithBackgroundColor(UIConstants.BaseBg)
            .Build();

        var trendsPanel = Controls.ScrollablePanel()
            .Rounded()
            .WithHeader(" Trends (6h)")
            .WithBorderColor(UIConstants.SeparatorColor)
            .WithBackgroundColor(UIConstants.BaseBg)
            .WithVerticalScroll(ScrollMode.None)
            .WithAlignment(HorizontalAlignment.Stretch)
            .WithMargin(1, 1, 1, 1)
            .AddControl(_hrSparkline)
            .AddControl(_bpSparkline)
            .AddControl(_spo2Sparkline)
            .Build();

        return new IWindowControl[]
        {
            vitalsPanel,
            trendsPanel
        };
    }

    public void Update(Patient patient)
    {
        var v = patient.CurrentVitals;

        _vitalsMarkup.SetContent(new List<string>
        {
            MarkupHelpers.FormatVitalLine("Heart Rate", v.HeartRate.ToString(), "bpm",
                UIConstants.HrColor(v.HeartRate)),
            MarkupHelpers.FormatVitalLine("Blood Pressure",
                $"{v.SystolicBP}/{v.DiastolicBP}", "mmHg",
                UIConstants.BpColor(v.SystolicBP)),
            MarkupHelpers.FormatVitalLine("SpO2", v.SpO2.ToString(), "%",
                UIConstants.SpO2Color(v.SpO2)),
            MarkupHelpers.FormatVitalLine("Temperature", v.Temperature.ToString("F1"), "°C",
                UIConstants.TempColor(v.Temperature)),
        });

        var history = patient.VitalsHistory;
        if (history.Count > 0)
        {
            _hrSparkline.SetDataPoints(history.Select(h => (double)h.HeartRate));
            _bpSparkline.SetDataPoints(history.Select(h => (double)h.SystolicBP));
            _spo2Sparkline.SetDataPoints(history.Select(h => (double)h.SpO2));
        }
    }
}
