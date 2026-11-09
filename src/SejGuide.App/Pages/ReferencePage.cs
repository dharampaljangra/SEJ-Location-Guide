using System.Globalization;
using SejGuide.Core;

namespace SejGuide.App;

public class ReferencePage : ContentPage
{
    public ReferencePage()
    {
        Title = "Reference tables";
        BackgroundColor = Theme.Page;

        var s = new VerticalStackLayout { Spacing = 16, Padding = new Thickness(12, 12, 12, 32) };
        s.Add(Ui.Header("Reference tables", Catalog.Source));

        s.Add(Ui.Table("Key SEJ distances",
            new[] { "Location", "SEJ position", "Para" },
            new List<string[]>
            {
                new[] { "LWR terminated near turnout", "≥ 39 m from SRJ and from crossing (40 m if turnout joints welded)", "IRPWM 326(8)(a); CT-48 20.1" },
                new[] { "LWR through turnout – one side only", "≥ 39 m from SRJ towards the side not on LWR", "CT-48 20.2, 20.3" },
                new[] { "LWR through turnout – both sides / crossover", "No SEJ at turnout", "CT-48 9.0, 12.2" },
                new[] { "Turn-in curve with derailing switch", "Just before derailing switch; switch ≥ 1 breathing length from SRJ", "CT-48 12.3" },
                new[] { "Loop with inferior track", "≥ 1 breathing length into loop from SRJ", "CT-48 12.5" },
                new[] { "Level crossing", "LC outside breathing length", "IRPWM 326(7)" },
                new[] { "Track beyond SEJ", "Same structure ≥ 39 m", "IRPWM 326(6)" },
                new[] { "52 kg curve > 250 m, sharper than 440 m R (Zone I)", "Straight track, 100 m from tangent point", "IRPWM 326(2)(b)(iv)" },
                new[] { "Transition curve", "Never", "IRPWM 328(1)(d)" },
                new[] { "BLT / ballasted junction", "≥ 30 m, in ballasted track", "IRPWM 326(5)(g)(v)" },
                new[] { "Girder bridge isolated", "≥ 30 m well-anchored PSC each side", "IRPWM 331(6)" },
                new[] { "Girder bridge, rail-free fastenings", "Bridge outside breathing length; 50 m approaches anchored", "IRPWM 331(1), (2)" },
                new[] { "Girder bridge, far-end SEJ", "15 m from abutment", "IRPWM 331(3)" },
                new[] { "Girder bridge, welded pier to pier", "SEJ over every pier", "IRPWM 331(4)" },
                new[] { "Single span girder bridge", "190 mm SEJ, 30 m from both abutments", "IRPWM 331(5)" },
                new[] { "Ballasted deck with bearings (SWR)", "≥ 30 m well-anchored PSC each side", "IRPWM 226(3)(b)" },
            }));

        s.Add(Ui.Table("SEJ drawings",
            new[] { "Drawing", "Description", "Rail", "Gap", "Curve limit" },
            Catalog.Drawings.Select(d => new[]
            {
                d.Kind == SejKind.BufferRails ? "Buffer rails" : d.Code,
                d.Name + (d.NeedsCte ? " – CTE approval" : "") + (d.NeedsPce ? " – PCE approval" : ""),
                d.Rail switch { "" => "See drawing", "52/60" => "52/60 junction", var x => x + " kg" },
                d.Gap,
                CurveLimit(d)
            })));

        var bl = new List<string[]>();
        foreach (var z in Catalog.Zones)
            foreach (var dens in new[] { "1540", "1660" })
                bl.Add(new[]
                {
                    z, dens,
                    Fmt(Catalog.BreathingLength(z, Catalog.Rail60, Catalog.SleeperConventional, dens)),
                    Fmt(Catalog.BreathingLength(z, Catalog.Rail52, Catalog.SleeperConventional, dens)),
                    Fmt(Catalog.BreathingLength(z, Catalog.Rail60, Catalog.SleeperWider, dens)),
                });
        s.Add(Ui.Table("Breathing length (m) – IRPWM Annex 3/16 (indicative)",
            new[] { "Zone", "Density", "60 kg conv.", "52 kg conv.", "60 kg wider" }, bl));

        s.Add(Ui.Table("Table-1: max bridge length (m) – IRPWM 331(1), (2)",
            new[] { "Zone", "Rail-free 60 kg", "Rail-free 52 kg", "Partly box-anch. 60 kg", "Partly box-anch. 52 kg" },
            Catalog.Zones.Select(z => new[]
            {
                z,
                Fmt(Catalog.Table1(z, Catalog.Rail60, false)), Fmt(Catalog.Table1(z, Catalog.Rail52, false)),
                Fmt(Catalog.Table1(z, Catalog.Rail60, true)), Fmt(Catalog.Table1(z, Catalog.Rail52, true))
            })));

        s.Add(Ui.Table("Far-end SEJ (15 m from abutment) – IRPWM 331(3)",
            new[] { "Zone", "190 mm SEJ: bridge / gap at td", "120 mm SEJ: bridge / gap at td" },
            Catalog.Zones.Select(z => new[]
            {
                z, FarEnd(z, Catalog.FarSej190), FarEnd(z, Catalog.FarSej120)
            })));

        s.Add(Ui.Table("Single span with 190 mm SEJs 30 m from both abutments – IRPWM 331(5)",
            new[] { "Zone", "Max single span (m)" },
            Catalog.Zones.Select(z => new[] { z, Fmt(Catalog.SingleSpanLimit(z)) })));

        s.Add(Ui.Table("LWR through turnouts – CT-48 (Nov 2025) requirements",
            new[] { "Item", "Requirement" },
            new List<string[]>
            {
                new[] { "Track", "Min. 60 kg / 90 UTS, PSC 1660/km" },
                new[] { "Fastenings", "ERC Mk-V in turnout zone + 50 m each side (rail pads IRS/T-55-2025)" },
                new[] { "Switch / crossing", "Thick Web Switch, Weldable CMS crossing, ACD (±7 mm)" },
                new[] { "Ballast", "300 mm cushion (150 mm clean); +150 mm shoulder in crossover, 150 m in turn-in curve" },
                new[] { "Zone IV", "Permitted with close monitoring for one weather cycle" },
                new[] { "Creep posts", "ATS, ANC, mid-lead, 20 m from ATS, 50 m beyond ANC, every 500 m" },
            }));

        Content = new ScrollView { Content = s };
    }

    private static string Fmt(double? v) => v.HasValue ? v.Value.ToString("0.#", CultureInfo.InvariantCulture) : "–";

    private static string FarEnd(string zone, string type)
    {
        var l = Catalog.FarEndLimit(zone, type);
        return l == null ? "Not provided" : $"{Fmt(l.Value.Length)} m / {Fmt(l.Value.GapCm)} cm";
    }

    private static string CurveLimit(SejDrawing d) => d.Kind switch
    {
        SejKind.ImprovedSharpCurve => "> 2° to 4°",
        SejKind.BufferRails => "6.5° (Z I, II) / 6° (Z III, IV)",
        _ => double.IsNaN(d.MaxDegree) ? "Not stated" : $"≤ {Fmt(d.MaxDegree)}°"
    };
}
