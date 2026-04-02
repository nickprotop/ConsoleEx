using ClinicFlow.UI;

namespace ClinicFlow.Data;

internal sealed class VitalsSimulator
{
    private readonly Random _rng = new();
    private int _vitalsTickCount;
    private int _eventTickCount;
    private int _alertTickCount;

    public VitalsReading GenerateReading(Patient patient)
    {
        var prev = patient.CurrentVitals;
        var reading = new VitalsReading
        {
            Timestamp = DateTime.Now,
            HeartRate = Drift(prev.HeartRate, 2, 40, 180),
            SystolicBP = Drift(prev.SystolicBP, 3, 80, 220),
            DiastolicBP = Drift(prev.DiastolicBP, 2, 50, 130),
            SpO2 = Drift(prev.SpO2, 1, 82, 100),
            Temperature = Math.Round(prev.Temperature + (_rng.NextDouble() - 0.5) * 0.2, 1)
        };
        return reading;
    }

    public void ApplyReading(Patient patient, VitalsReading reading)
    {
        patient.CurrentVitals = reading;
        patient.VitalsHistory.Add(reading);
        if (patient.VitalsHistory.Count > UIConstants.SparklineMaxPoints)
            patient.VitalsHistory.RemoveAt(0);
    }

    public bool ShouldGenerateEvent(int elapsedMs)
    {
        _eventTickCount += elapsedMs;
        if (_eventTickCount >= UIConstants.EventGenerationIntervalMs)
        {
            _eventTickCount = 0;
            return true;
        }
        return false;
    }

    public bool ShouldGenerateAlert(int elapsedMs, PatientStatus status)
    {
        if (status == PatientStatus.Stable) return false;
        _alertTickCount += elapsedMs;
        if (_alertTickCount >= UIConstants.AlertChanceIntervalMs)
        {
            _alertTickCount = 0;
            return _rng.NextDouble() < 0.4;
        }
        return false;
    }

    public bool ShouldUpdateVitals(int elapsedMs)
    {
        _vitalsTickCount += elapsedMs;
        if (_vitalsTickCount >= UIConstants.VitalsUpdateIntervalMs)
        {
            _vitalsTickCount = 0;
            return true;
        }
        return false;
    }

    public TimelineEvent GenerateVitalsEvent(VitalsReading reading)
    {
        return new TimelineEvent
        {
            Timestamp = reading.Timestamp,
            Type = EventType.Vitals,
            Summary = "Automated vitals recorded",
            Detail = $"HR {reading.HeartRate} · BP {reading.SystolicBP}/{reading.DiastolicBP} · SpO2 {reading.SpO2}% · Temp {reading.Temperature:F1}°C"
        };
    }

    public TimelineEvent GenerateRandomEvent()
    {
        var types = new[] { EventType.Note, EventType.Medication, EventType.Order };
        var type = types[_rng.Next(types.Length)];
        return type switch
        {
            EventType.Note => new TimelineEvent
            {
                Timestamp = DateTime.Now, Type = EventType.Note,
                Summary = PickRandom(NotesSummaries),
                Detail = PickRandom(NotesDetails)
            },
            EventType.Medication => new TimelineEvent
            {
                Timestamp = DateTime.Now, Type = EventType.Medication,
                Summary = PickRandom(MedSummaries),
                Detail = PickRandom(MedDetails)
            },
            _ => new TimelineEvent
            {
                Timestamp = DateTime.Now, Type = EventType.Order,
                Summary = PickRandom(OrderSummaries),
                Detail = PickRandom(OrderDetails)
            }
        };
    }

    public TimelineEvent GenerateAlertEvent(Patient patient)
    {
        var alert = PickRandom(AlertTemplates);
        return new TimelineEvent
        {
            Timestamp = DateTime.Now,
            Type = EventType.Alert,
            Summary = alert.summary,
            Detail = alert.detail
        };
    }

    private int Drift(int current, int maxDelta, int min, int max)
    {
        return Math.Clamp(current + _rng.Next(-maxDelta, maxDelta + 1), min, max);
    }

    private string PickRandom(string[] items) => items[_rng.Next(items.Length)];
    private (string summary, string detail) PickRandom((string summary, string detail)[] items) => items[_rng.Next(items.Length)];

    private static readonly string[] NotesSummaries = { "Nursing assessment update", "Patient comfort check", "Fluid balance reviewed", "Skin integrity assessment" };
    private static readonly string[] NotesDetails = { "Patient resting comfortably. No acute complaints.", "I/O recorded. Fluid balance within target.", "Skin warm and dry. No pressure injury noted.", "Patient ambulated in hallway with assistance." };
    private static readonly string[] MedSummaries = { "Scheduled medication administered", "PRN medication given", "IV fluids adjusted", "Antibiotic dose administered" };
    private static readonly string[] MedDetails = { "PO · Scheduled dose · No adverse reaction", "IV · Rate adjusted per protocol", "PRN · Patient requested pain management", "IV · Infusion running on schedule" };
    private static readonly string[] OrderSummaries = { "Lab panel ordered", "Imaging study requested", "Consult placed", "Dietary modification ordered" };
    private static readonly string[] OrderDetails = { "Routine · Results expected within 2h", "STAT · Radiology notified", "Cardiology consult · Routine priority", "Low sodium diet · Effective immediately" };
    private static readonly (string summary, string detail)[] AlertTemplates =
    {
        ("Heart rate elevated above threshold", "Sustained tachycardia detected. Evaluate for underlying cause."),
        ("Blood pressure spike detected", "Systolic >160mmHg. Assess for symptoms. PRN antihypertensive considered."),
        ("SpO2 trending downward", "Oxygen saturation declining over last 30min. Increase monitoring frequency."),
        ("Temperature rising above 38°C", "Low-grade fever developing. Blood cultures ordered if persists."),
    };
}
