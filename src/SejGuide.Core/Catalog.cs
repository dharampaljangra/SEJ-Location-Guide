namespace SejGuide.Core;

public enum SejKind { Conventional, Improved, ImprovedSharpCurve, ThickWeb, Combination, BufferRails }

public sealed class SejDrawing
{
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public SejKind Kind { get; init; }
    /// <summary>"52", "60", "52/60" (combination) or "" when the manual does not state it.</summary>
    public string Rail { get; init; } = "";
    public string Gap { get; init; } = "";
    /// <summary>Lowest curve (degrees) for which the drawing is meant (0 = straight).</summary>
    public double MinDegree { get; init; }
    /// <summary>Sharpest curve permitted (degrees). NaN = not stated in IRPWM.</summary>
    public double MaxDegree { get; init; }
    public bool NeedsCte { get; init; }
    public bool NeedsPce { get; init; }
    public string Reference { get; init; } = "";
    public string Note { get; init; } = "";

    public string Label => Kind == SejKind.BufferRails ? Name : $"{Code} – {Name}";
}

/// <summary>Data tables taken from IRPWM 2024 (corrected up to ACS-16) and RDSO Report CT-48 (Revised, Nov 2025).</summary>
public static class Catalog
{
    public const string Source = "IRPWM 2024 (First Reprint) corrected up to ACS-16; RDSO Report CT-48 (Revised) Nov 2025";

    public static readonly string[] Zones = { "I", "II", "III", "IV" };
    public const string Rail52 = "52 kg/m";
    public const string Rail60 = "60 kg/m";
    public const string RailJunction = "52 kg/m ↔ 60 kg/m junction";
    public static readonly string[] RailSections = { Rail60, Rail52, RailJunction };

    public static readonly List<SejDrawing> Drawings = new()
    {
        new() { Code = "RT-8822", Name = "Thick Web SEJ (TWSEJ)", Kind = SejKind.ThickWeb, Rail = "", Gap = "As per drawing",
                MaxDegree = 0.5, Reference = "IRPWM 328(1)(c)",
                Note = "To be provided progressively in place of SEJ/ISEJ during renewals, replacements and new works. Straight track and curves up to 0.5° (3500 m R). Rail section: check on drawing." },
        new() { Code = "RT-4165", Name = "Conventional SEJ, 60 kg", Kind = SejKind.Conventional, Rail = "60", Gap = "80 mm",
                MaxDegree = 0.5, Reference = "IRPWM 328(1)(a), Annex 2/4(B)" },
        new() { Code = "RT-4160", Name = "Conventional SEJ, 52 kg", Kind = SejKind.Conventional, Rail = "52", Gap = "80 mm",
                MaxDegree = 0.5, Reference = "IRPWM 328(1)(a), Annex 2/4(B)" },
        new() { Code = "RT-6902", Name = "Improved SEJ, 60 kg, single gap (SG 80)", Kind = SejKind.Improved, Rail = "60", Gap = "80 mm (single gap)",
                MaxDegree = 2.0, Reference = "IRPWM 328(1)(b), Annex 2/4(B)" },
        new() { Code = "RT-6914", Name = "Improved SEJ, 52 kg, single gap (SG 80)", Kind = SejKind.Improved, Rail = "52", Gap = "80 mm (single gap)",
                MaxDegree = 2.0, Reference = "IRPWM 328(1)(b), Annex 2/4(B)" },
        new() { Code = "RT-6922", Name = "Improved SEJ, 60 kg, double gap (DG 65)", Kind = SejKind.Improved, Rail = "60", Gap = "65 mm each (double gap)",
                MaxDegree = 2.0, Reference = "IRPWM 328(1)(b), 331(3) Note (ii), Annex 2/4(B)" },
        new() { Code = "RT-6930", Name = "Improved SEJ, 52 kg, double gap (DG 65)", Kind = SejKind.Improved, Rail = "52", Gap = "65 mm each (double gap)",
                MaxDegree = 2.0, Reference = "IRPWM 328(1)(b), 331(3) Note (ii), Annex 2/4(B)" },
        new() { Code = "RT-8924", Name = "ISEJ for curves 2°–4°, 60 kg BG (80 mm max gap)", Kind = SejKind.ImprovedSharpCurve, Rail = "60", Gap = "80 mm max",
                MinDegree = 2.0, MaxDegree = 4.0, NeedsCte = true, Reference = "IRPWM 328(1)(b)" },
        new() { Code = "RT-8926", Name = "ISEJ for curves 2°–4°, 60 kg BG (65 mm max gap)", Kind = SejKind.ImprovedSharpCurve, Rail = "60", Gap = "65 mm max",
                MinDegree = 2.0, MaxDegree = 4.0, NeedsCte = true, Reference = "IRPWM 328(1)(b)" },
        new() { Code = "T-6782", Name = "Combination SEJ, 52 kg / 60 kg", Kind = SejKind.Combination, Rail = "52/60", Gap = "80 mm",
                MaxDegree = double.NaN, Reference = "IRPWM 225(2), 326(5)(d), Annex 2/4(B) Note",
                Note = "Only at the junction of 52 kg and 60 kg rail on concrete sleepers. IRPWM does not state a curve limit – check the drawing." },
        new() { Code = "BUFFER", Name = "Buffer rails in lieu of SEJ (6.5 m rails, 7.5 mm gaps)", Kind = SejKind.BufferRails, Rail = "",
                Gap = "7.5 mm at each fish-plated joint", MaxDegree = 6.5, NeedsPce = true, Reference = "IRPWM 328(2), Fig. 3.20.1",
                Note = "Only with prior approval of PCE where SEJ is not permitted. 3 rails in Zones I & II, 4 rails in Zones III & IV. Up to 6.5° (Zones I, II) / 6° (Zones III, IV)." },
    };

    public static SejDrawing? FindByLabel(string label) => Drawings.FirstOrDefault(d => d.Label == label);
    public static SejDrawing? FindByCode(string code) => Drawings.FirstOrDefault(d => d.Code == code);

    // ---------------- Breathing length, IRPWM Annexure 3/16 (PRC sleepers) ----------------
    public const string SleeperConventional = "PSC conventional";
    public const string SleeperWider = "PSC wider base";
    public static readonly string[] Sleepers = { SleeperConventional, SleeperWider };
    public static readonly string[] Densities = { "1660", "1540" };

    // key: zone|density -> (60 conv, 52 conv, 60 wider; 0 = not given)
    private static readonly Dictionary<string, int[]> Breathing = new()
    {
        ["I|1540"] = new[] { 60, 52, 0 },
        ["I|1660"] = new[] { 58, 50, 44 },
        ["II|1540"] = new[] { 69, 59, 0 },
        ["II|1660"] = new[] { 66, 57, 52 },
        ["III|1540"] = new[] { 74, 64, 0 },
        ["III|1660"] = new[] { 77, 66, 59 },
        ["IV|1540"] = new[] { 82, 71, 0 },
        ["IV|1660"] = new[] { 79, 68, 56 },
    };

    /// <summary>Indicative breathing length (m) from Annexure 3/16, or null when the table has no value.</summary>
    public static double? BreathingLength(string zone, string rail, string sleeper, string density)
    {
        if (!Breathing.TryGetValue($"{zone}|{density}", out var row)) return null;
        if (sleeper == SleeperWider)
            return rail == Rail60 && row[2] > 0 ? row[2] : null;
        if (rail == Rail60) return row[0];
        if (rail == Rail52) return row[1];
        // junction: be conservative, use the larger value
        return Math.Max(row[0], row[1]);
    }

    // ---------------- Table-1, IRPWM 331(1) & (2) ----------------
    /// <summary>Max overall bridge length (m). partlyBoxAnchored=false → 331(1), true → 331(2).</summary>
    public static double Table1(string zone, string rail, bool partlyBoxAnchored)
    {
        bool r60 = rail == Rail60;
        return (zone, partlyBoxAnchored) switch
        {
            ("I", false) => r60 ? 30 : 45,
            ("I", true) => r60 ? 77 : 90,
            ("II", false) => r60 ? 11 : 27,
            ("II", true) => r60 ? 42 : 58,
            (_, false) => r60 ? 11 : 27,       // Zones III & IV
            (_, true) => r60 ? 23 : 43,
        };
    }

    // ---------------- Far-end approach SEJ, IRPWM 331(3) ----------------
    public const string FarSej190 = "SEJ with 190 mm max. movement";
    public const string FarSej120 = "SEJ with 120 mm max. movement (IRS design)";
    public const string FarSejDg65 = "Improved SEJ DG 65 (RT-6922 / RT-6930) in lieu of 120 mm";
    public static readonly string[] FarEndSejTypes = { FarSej190, FarSej120, FarSejDg65 };

    /// <summary>Returns (max bridge length m, initial gap at td in cm) or null when not permitted.</summary>
    public static (double Length, double GapCm)? FarEndLimit(string zone, string sejType)
    {
        bool is190 = sejType == FarSej190;
        if (is190)
            return zone switch
            {
                "IV" => (55, 7.0),
                "III" => (70, 7.0),
                "II" => (110, 6.5),
                _ => (160, 6.5),
            };
        return zone switch
        {
            "II" => (20, 4.0),
            "I" => (50, 4.0),
            _ => null, // 120 mm SEJ not provided for in Zones III & IV
        };
    }

    // ---------------- Single span with SEJs both sides, IRPWM 331(5) ----------------
    public static double SingleSpanLimit(string zone) => zone switch
    {
        "IV" => 75,
        "III" => 87,
        "II" => 110,
        _ => 146,
    };

    // ---------------- Curves ----------------
    /// <summary>BG radius (m) from degree of curve (30.5 m chord): R = 1750 / D.</summary>
    public static double RadiusFromDegree(double deg) => deg <= 0 ? double.PositiveInfinity : 1750.0 / deg;
    public static double DegreeFromRadius(double radius) => radius <= 0 ? 0 : 1750.0 / radius;

    /// <summary>Sharpest curve for LWR with 60 kg on PSC 1660, IRPWM 326(2)(a).</summary>
    public static double MaxLwrCurve60(string zone) => zone is "I" or "II" ? 6.5 : 6.0;

    /// <summary>Buffer rail limits, IRPWM 328(2)(b).</summary>
    public static double BufferRailMaxDegree(string zone) => zone is "I" or "II" ? 6.5 : 6.0;
    public static int BufferRailCount(string zone) => zone is "I" or "II" ? 3 : 4;
}
