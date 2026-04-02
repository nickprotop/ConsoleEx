namespace ClinicFlow.Data;

internal enum PatientStatus { Critical, Watch, Stable }

internal enum EventType { Alert, Vitals, Medication, Note, Order, Discharge }

internal sealed class VitalsReading
{
    public DateTime Timestamp { get; init; }
    public int HeartRate { get; init; }
    public int SystolicBP { get; init; }
    public int DiastolicBP { get; init; }
    public int SpO2 { get; init; }
    public double Temperature { get; init; }
}

internal sealed class TimelineEvent
{
    public DateTime Timestamp { get; init; }
    public EventType Type { get; init; }
    public required string Summary { get; init; }
    public required string Detail { get; init; }
    public bool IsExpanded { get; set; }
}

internal sealed class Patient
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Room { get; init; }
    public int Age { get; init; }
    public char Sex { get; init; }
    public PatientStatus Status { get; set; }
    public VitalsReading CurrentVitals { get; set; } = null!;
    public List<VitalsReading> VitalsHistory { get; } = new();
    public List<TimelineEvent> TimelineEvents { get; } = new();
}

internal static class SeedData
{
    public static List<Patient> CreatePatients()
    {
        var now = DateTime.Now;
        var patients = new List<Patient>
        {
            CreateTorres(now),
            CreatePark(now),
            CreateSingh(now),
            CreateMuller(now),
            CreateOkafor(now),
            CreateChen(now),
        };
        return patients;
    }

    private static Patient CreateTorres(DateTime now)
    {
        var p = new Patient
        {
            Id = "P001", Name = "M. Torres", Room = "312", Age = 67, Sex = 'F',
            Status = PatientStatus.Critical,
            CurrentVitals = new VitalsReading
            {
                Timestamp = now.AddMinutes(-2), HeartRate = 88,
                SystolicBP = 142, DiastolicBP = 91, SpO2 = 89, Temperature = 37.2
            }
        };
        SeedVitalsHistory(p, now, baseHr: 85, baseSys: 140, baseDia: 90, baseSpo2: 92, baseTemp: 37.1, drift: 5);
        p.TimelineEvents.AddRange(new[]
        {
            new TimelineEvent { Timestamp = now.AddMinutes(-2), Type = EventType.Alert,
                Summary = "SpO2 dropped to 89%",
                Detail = "Threshold breach: below 92%. O2 flow increased to 4L/min. Attending notified." },
            new TimelineEvent { Timestamp = now.AddMinutes(-15), Type = EventType.Vitals,
                Summary = "Routine check recorded",
                Detail = "HR 88 · BP 142/91 · SpO2 94% · Temp 37.2°C" },
            new TimelineEvent { Timestamp = now.AddMinutes(-45), Type = EventType.Medication,
                Summary = "Metoprolol 25mg administered",
                Detail = "PO · Scheduled dose · Next due 21:45" },
            new TimelineEvent { Timestamp = now.AddMinutes(-90), Type = EventType.Note,
                Summary = "Post-catheterization assessment",
                Detail = "Patient alert and oriented. Groin site clean, no hematoma. Pedal pulses palpable bilateral." },
            new TimelineEvent { Timestamp = now.AddMinutes(-120), Type = EventType.Order,
                Summary = "CBC + BMP ordered",
                Detail = "STAT · Results pending" },
            new TimelineEvent { Timestamp = now.AddMinutes(-150), Type = EventType.Vitals,
                Summary = "Admission vitals",
                Detail = "HR 92 · BP 148/95 · SpO2 96% · Temp 36.8°C · Resp 18 · Pain 4/10" },
            new TimelineEvent { Timestamp = now.AddMinutes(-180), Type = EventType.Medication,
                Summary = "Heparin drip initiated",
                Detail = "IV · 18 units/kg/hr · PTT due in 6h" },
            new TimelineEvent { Timestamp = now.AddMinutes(-200), Type = EventType.Note,
                Summary = "Cardiac catheterization completed",
                Detail = "LAD 90% stenosis, stent placed. RCA 40% non-obstructive. LV function preserved." },
        });
        return p;
    }

    private static Patient CreatePark(DateTime now)
    {
        var p = new Patient
        {
            Id = "P002", Name = "J. Park", Room = "314", Age = 45, Sex = 'M',
            Status = PatientStatus.Watch,
            CurrentVitals = new VitalsReading
            {
                Timestamp = now.AddMinutes(-5), HeartRate = 76,
                SystolicBP = 158, DiastolicBP = 96, SpO2 = 97, Temperature = 36.9
            }
        };
        SeedVitalsHistory(p, now, baseHr: 74, baseSys: 155, baseDia: 94, baseSpo2: 97, baseTemp: 36.9, drift: 3);
        p.TimelineEvents.AddRange(new[]
        {
            new TimelineEvent { Timestamp = now.AddMinutes(-5), Type = EventType.Vitals,
                Summary = "Vitals check",
                Detail = "HR 76 · BP 158/96 · SpO2 97% · Temp 36.9°C" },
            new TimelineEvent { Timestamp = now.AddMinutes(-30), Type = EventType.Medication,
                Summary = "Amlodipine 10mg administered",
                Detail = "PO · Morning dose" },
            new TimelineEvent { Timestamp = now.AddMinutes(-60), Type = EventType.Note,
                Summary = "BP trending high despite medication",
                Detail = "Consider dose adjustment. 24h ambulatory BP monitor ordered." },
            new TimelineEvent { Timestamp = now.AddMinutes(-120), Type = EventType.Order,
                Summary = "24h ambulatory BP monitor",
                Detail = "Routine · Start this afternoon" },
            new TimelineEvent { Timestamp = now.AddMinutes(-180), Type = EventType.Vitals,
                Summary = "Morning vitals",
                Detail = "HR 72 · BP 162/98 · SpO2 98% · Temp 36.8°C" },
            new TimelineEvent { Timestamp = now.AddMinutes(-240), Type = EventType.Medication,
                Summary = "Lisinopril 20mg administered",
                Detail = "PO · Evening dose" },
            new TimelineEvent { Timestamp = now.AddMinutes(-300), Type = EventType.Note,
                Summary = "Sleep assessment",
                Detail = "Patient reports poor sleep. Mild anxiety. Non-pharmacological interventions discussed." },
            new TimelineEvent { Timestamp = now.AddMinutes(-340), Type = EventType.Vitals,
                Summary = "Evening vitals",
                Detail = "HR 78 · BP 155/93 · SpO2 97% · Temp 37.0°C" },
        });
        return p;
    }

    private static Patient CreateSingh(DateTime now)
    {
        var p = new Patient
        {
            Id = "P003", Name = "R. Singh", Room = "316", Age = 72, Sex = 'M',
            Status = PatientStatus.Stable,
            CurrentVitals = new VitalsReading
            {
                Timestamp = now.AddMinutes(-10), HeartRate = 68,
                SystolicBP = 124, DiastolicBP = 78, SpO2 = 97, Temperature = 36.6
            }
        };
        SeedVitalsHistory(p, now, baseHr: 66, baseSys: 122, baseDia: 76, baseSpo2: 97, baseTemp: 36.6, drift: 2);
        p.TimelineEvents.AddRange(new[]
        {
            new TimelineEvent { Timestamp = now.AddMinutes(-10), Type = EventType.Vitals,
                Summary = "Routine vitals — all normal",
                Detail = "HR 68 · BP 124/78 · SpO2 97% · Temp 36.6°C" },
            new TimelineEvent { Timestamp = now.AddMinutes(-60), Type = EventType.Note,
                Summary = "Post-op day 3 assessment",
                Detail = "Wound healing well, no signs of infection. Ambulating independently." },
            new TimelineEvent { Timestamp = now.AddMinutes(-90), Type = EventType.Medication,
                Summary = "Acetaminophen 500mg",
                Detail = "PO · PRN pain · Patient reports pain 2/10" },
            new TimelineEvent { Timestamp = now.AddMinutes(-150), Type = EventType.Order,
                Summary = "Physical therapy consult",
                Detail = "Routine · Gait and balance assessment" },
            new TimelineEvent { Timestamp = now.AddMinutes(-210), Type = EventType.Vitals,
                Summary = "Morning vitals",
                Detail = "HR 64 · BP 120/76 · SpO2 98% · Temp 36.5°C" },
            new TimelineEvent { Timestamp = now.AddMinutes(-270), Type = EventType.Discharge,
                Summary = "Discharge planning initiated",
                Detail = "Target discharge: tomorrow AM. Home health referral placed." },
            new TimelineEvent { Timestamp = now.AddMinutes(-330), Type = EventType.Medication,
                Summary = "Enoxaparin 40mg",
                Detail = "SubQ · DVT prophylaxis · Continue until discharge" },
            new TimelineEvent { Timestamp = now.AddMinutes(-360), Type = EventType.Note,
                Summary = "Family meeting completed",
                Detail = "Discussed discharge plan and home care needs with daughter." },
        });
        return p;
    }

    private static Patient CreateMuller(DateTime now)
    {
        var p = new Patient
        {
            Id = "P004", Name = "L. Muller", Room = "318", Age = 58, Sex = 'F',
            Status = PatientStatus.Stable,
            CurrentVitals = new VitalsReading
            {
                Timestamp = now.AddMinutes(-8), HeartRate = 72,
                SystolicBP = 118, DiastolicBP = 74, SpO2 = 99, Temperature = 36.4
            }
        };
        SeedVitalsHistory(p, now, baseHr: 70, baseSys: 116, baseDia: 72, baseSpo2: 99, baseTemp: 36.4, drift: 2);
        p.TimelineEvents.AddRange(new[]
        {
            new TimelineEvent { Timestamp = now.AddMinutes(-8), Type = EventType.Vitals,
                Summary = "Telemetry check — normal sinus rhythm",
                Detail = "HR 72 · BP 118/74 · SpO2 99% · Temp 36.4°C" },
            new TimelineEvent { Timestamp = now.AddMinutes(-60), Type = EventType.Note,
                Summary = "24h Holter results reviewed",
                Detail = "No significant arrhythmias. Occasional PVCs, clinically insignificant." },
            new TimelineEvent { Timestamp = now.AddMinutes(-120), Type = EventType.Medication,
                Summary = "Aspirin 81mg",
                Detail = "PO · Daily · Taken with breakfast" },
            new TimelineEvent { Timestamp = now.AddMinutes(-180), Type = EventType.Vitals,
                Summary = "Morning vitals",
                Detail = "HR 70 · BP 116/72 · SpO2 99% · Temp 36.3°C" },
            new TimelineEvent { Timestamp = now.AddMinutes(-240), Type = EventType.Order,
                Summary = "Echocardiogram",
                Detail = "Routine · Scheduled for 14:00 today" },
            new TimelineEvent { Timestamp = now.AddMinutes(-300), Type = EventType.Note,
                Summary = "Patient reports feeling well",
                Detail = "No chest pain, palpitations, or dyspnea. Appetite good." },
            new TimelineEvent { Timestamp = now.AddMinutes(-340), Type = EventType.Vitals,
                Summary = "Evening vitals",
                Detail = "HR 68 · BP 114/70 · SpO2 99% · Temp 36.5°C" },
            new TimelineEvent { Timestamp = now.AddMinutes(-360), Type = EventType.Medication,
                Summary = "Atorvastatin 40mg",
                Detail = "PO · Daily · Statin therapy" },
        });
        return p;
    }

    private static Patient CreateOkafor(DateTime now)
    {
        var p = new Patient
        {
            Id = "P005", Name = "A. Okafor", Room = "320", Age = 81, Sex = 'M',
            Status = PatientStatus.Watch,
            CurrentVitals = new VitalsReading
            {
                Timestamp = now.AddMinutes(-3), HeartRate = 94,
                SystolicBP = 136, DiastolicBP = 84, SpO2 = 93, Temperature = 37.4
            }
        };
        SeedVitalsHistory(p, now, baseHr: 90, baseSys: 134, baseDia: 82, baseSpo2: 93, baseTemp: 37.3, drift: 4);
        p.TimelineEvents.AddRange(new[]
        {
            new TimelineEvent { Timestamp = now.AddMinutes(-3), Type = EventType.Vitals,
                Summary = "Vitals check — SpO2 borderline",
                Detail = "HR 94 · BP 136/84 · SpO2 93% · Temp 37.4°C" },
            new TimelineEvent { Timestamp = now.AddMinutes(-20), Type = EventType.Medication,
                Summary = "Furosemide 40mg IV",
                Detail = "IV push · Diuresis for fluid overload" },
            new TimelineEvent { Timestamp = now.AddMinutes(-45), Type = EventType.Alert,
                Summary = "Weight up 2kg from yesterday",
                Detail = "Fluid retention worsening. I/O balance +800mL. Diuretic adjustment ordered." },
            new TimelineEvent { Timestamp = now.AddMinutes(-90), Type = EventType.Note,
                Summary = "Increased peripheral edema noted",
                Detail = "2+ pitting edema bilateral lower extremities. Crackles bilateral bases." },
            new TimelineEvent { Timestamp = now.AddMinutes(-150), Type = EventType.Order,
                Summary = "BNP level",
                Detail = "STAT · Monitor for CHF exacerbation" },
            new TimelineEvent { Timestamp = now.AddMinutes(-210), Type = EventType.Medication,
                Summary = "Carvedilol 12.5mg",
                Detail = "PO · BID · Heart failure regimen" },
            new TimelineEvent { Timestamp = now.AddMinutes(-270), Type = EventType.Vitals,
                Summary = "Morning vitals",
                Detail = "HR 88 · BP 132/80 · SpO2 94% · Temp 37.2°C" },
            new TimelineEvent { Timestamp = now.AddMinutes(-330), Type = EventType.Note,
                Summary = "Dietary non-compliance discussed",
                Detail = "Patient admits to high sodium intake. Dietitian consult placed." },
            new TimelineEvent { Timestamp = now.AddMinutes(-360), Type = EventType.Order,
                Summary = "Chest X-ray",
                Detail = "Routine · Assess for pulmonary edema" },
        });
        return p;
    }

    private static Patient CreateChen(DateTime now)
    {
        var p = new Patient
        {
            Id = "P006", Name = "S. Chen", Room = "322", Age = 34, Sex = 'F',
            Status = PatientStatus.Stable,
            CurrentVitals = new VitalsReading
            {
                Timestamp = now.AddMinutes(-12), HeartRate = 64,
                SystolicBP = 112, DiastolicBP = 68, SpO2 = 99, Temperature = 36.5
            }
        };
        SeedVitalsHistory(p, now, baseHr: 62, baseSys: 110, baseDia: 66, baseSpo2: 99, baseTemp: 36.5, drift: 2);
        p.TimelineEvents.AddRange(new[]
        {
            new TimelineEvent { Timestamp = now.AddMinutes(-12), Type = EventType.Vitals,
                Summary = "All vitals within normal limits",
                Detail = "HR 64 · BP 112/68 · SpO2 99% · Temp 36.5°C" },
            new TimelineEvent { Timestamp = now.AddMinutes(-60), Type = EventType.Note,
                Summary = "No recurrence of arrhythmia",
                Detail = "Telemetry shows normal sinus rhythm for past 18h. Patient asymptomatic." },
            new TimelineEvent { Timestamp = now.AddMinutes(-120), Type = EventType.Medication,
                Summary = "Flecainide 50mg",
                Detail = "PO · BID · Anti-arrhythmic" },
            new TimelineEvent { Timestamp = now.AddMinutes(-180), Type = EventType.Vitals,
                Summary = "Morning vitals",
                Detail = "HR 62 · BP 110/66 · SpO2 99% · Temp 36.4°C" },
            new TimelineEvent { Timestamp = now.AddMinutes(-240), Type = EventType.Note,
                Summary = "Cardiology consult note",
                Detail = "Paroxysmal SVT, converted with adenosine. Start flecainide, monitor 48h." },
            new TimelineEvent { Timestamp = now.AddMinutes(-300), Type = EventType.Order,
                Summary = "Electrolyte panel",
                Detail = "Routine · Baseline before anti-arrhythmic" },
            new TimelineEvent { Timestamp = now.AddMinutes(-330), Type = EventType.Alert,
                Summary = "SVT episode — HR 186",
                Detail = "Adenosine 6mg IV rapid push, converted to NSR within 12 seconds." },
            new TimelineEvent { Timestamp = now.AddMinutes(-350), Type = EventType.Vitals,
                Summary = "Admission vitals",
                Detail = "HR 178 · BP 98/62 · SpO2 97% · Temp 36.6°C · Patient reports palpitations and dizziness" },
        });
        return p;
    }

    private static void SeedVitalsHistory(Patient p, DateTime now, int baseHr, int baseSys, int baseDia, int baseSpo2, double baseTemp, int drift)
    {
        var rng = new Random(p.Id.GetHashCode());
        for (int i = 19; i >= 0; i--)
        {
            p.VitalsHistory.Add(new VitalsReading
            {
                Timestamp = now.AddMinutes(-i * 18),
                HeartRate = baseHr + rng.Next(-drift, drift + 1),
                SystolicBP = baseSys + rng.Next(-drift, drift + 1),
                DiastolicBP = baseDia + rng.Next(-drift / 2, drift / 2 + 1),
                SpO2 = Math.Clamp(baseSpo2 + rng.Next(-drift / 2, drift / 2 + 1), 80, 100),
                Temperature = Math.Round(baseTemp + (rng.NextDouble() - 0.5) * drift * 0.1, 1)
            });
        }
    }
}
