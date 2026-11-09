using SejGuide.Core;
using Xunit;

namespace SejGuide.Tests;

public class RuleTests
{
    private static CheckResult Run(string id, Action<Inputs> set)
    {
        var def = Rules.Get(id);
        var inputs = def.DefaultInputs();
        set(inputs);
        return def.Evaluate(inputs);
    }

    private static string Label(string code) => Catalog.FindByCode(code)!.Label;

    [Fact]
    public void AllChecksEvaluateWithDefaults()
    {
        foreach (var def in Rules.All)
        {
            var r = def.Evaluate(def.DefaultInputs());
            Assert.NotEmpty(r.Findings);
            Assert.False(string.IsNullOrWhiteSpace(r.RecommendedLocation), def.Id);
        }
    }

    [Fact]
    public void CheckIdsAreUniqueAndGrouped()
    {
        Assert.Equal(Rules.All.Count, Rules.All.Select(c => c.Id).Distinct().Count());
        Assert.All(Rules.All, c => Assert.Contains(c.Group, Rules.Groups));
    }

    // ---------- Turnouts ----------
    [Theory]
    [InlineData(38, Severity.Error)]
    [InlineData(39, Severity.Ok)]
    [InlineData(45, Severity.Ok)]
    public void TurnoutTerminated_FishPlated_Needs39m(double dist, Severity expected)
    {
        var r = Run("PC_TERMINATED", i => i.Set("dist", dist).Set("rail", Catalog.Rail60).Set("sej", Label("RT-4165")));
        Assert.Equal(expected, r.Overall);
    }

    [Fact]
    public void TurnoutTerminated_Welded_Needs40m()
    {
        var r = Run("PC_TERMINATED", i => i.Set("welded", Rules.TurnoutWelded).Set("dist", 39.5));
        Assert.Equal(Severity.Error, r.Overall);
        r = Run("PC_TERMINATED", i => i.Set("welded", Rules.TurnoutWelded).Set("dist", 40));
        Assert.NotEqual(Severity.Error, r.Overall);
    }

    [Fact]
    public void TurnoutTerminated_NoElasticFastenings_IsRed()
    {
        var r = Run("PC_TERMINATED", i => i.Set("dist", 50).Set("elastic", false));
        Assert.Equal(Severity.Error, r.Overall);
    }

    [Fact]
    public void OneSide_Below39_IsRed()
    {
        Assert.Equal(Severity.Error, Run("PC_ONESIDE", i => i.Set("dist", 30)).Overall);
        Assert.NotEqual(Severity.Error, Run("PC_ONESIDE", i => i.Set("dist", 39)).Overall);
    }

    [Fact]
    public void Through_SejAtTurnout_IsRed()
    {
        Assert.Equal(Severity.Error, Run("PC_THROUGH", i => i.Set("propose", true)).Overall);
        Assert.Equal(Severity.Ok, Run("PC_THROUGH", i => i.Set("zone", "II")).Overall);
        Assert.Equal(Severity.Caution, Run("PC_THROUGH", i => i.Set("zone", "IV")).Overall);
        Assert.Equal(Severity.Error, Run("PC_THROUGH", i => i.Set("wcms", false)).Overall);
    }

    [Fact]
    public void Crossover_SejBetween_IsRed()
    {
        Assert.Equal(Severity.Error, Run("PC_CROSSOVER", i => i.Set("between", true)).Overall);
        Assert.Equal(Severity.Error, Run("PC_CROSSOVER", i => i.Set("both", false)).Overall);
        Assert.Equal(Severity.Ok, Run("PC_CROSSOVER", _ => { }).Overall);
    }

    [Fact]
    public void TurnIn_DerailingSwitchWithinBreathingLength_IsRed()
    {
        // Zone III, 60 kg, conventional, 1660 → BL 77 m
        var r = Run("PC_TURNIN", i => i.Set("zone", "III").Set("rail", Catalog.Rail60).Set("distDs", 70));
        Assert.Equal(Severity.Error, r.Overall);
        r = Run("PC_TURNIN", i => i.Set("zone", "III").Set("rail", Catalog.Rail60).Set("distDs", 80));
        Assert.NotEqual(Severity.Error, r.Overall);
    }

    [Fact]
    public void Loop_ShortOfBreathingLength_IsRed()
    {
        Assert.Equal(Severity.Error, Run("PC_LOOP", i => i.Set("zone", "IV").Set("len", 70)).Overall); // BL 79
        Assert.NotEqual(Severity.Error, Run("PC_LOOP", i => i.Set("zone", "IV").Set("len", 80)).Overall);
    }

    // ---------- Level crossing ----------
    [Fact]
    public void LevelCrossing_WithinBreathingLength_IsRed()
    {
        // Zone II, 52 kg, 1540 → 59 m
        var r = Run("LC", i => i.Set("zone", "II").Set("rail", Catalog.Rail52).Set("density", "1540").Set("dist", 59));
        Assert.Equal(Severity.Error, r.Overall);
        r = Run("LC", i => i.Set("zone", "II").Set("rail", Catalog.Rail52).Set("density", "1540").Set("dist", 62));
        Assert.Equal(Severity.Caution, r.Overall);
        r = Run("LC", i => i.Set("zone", "II").Set("rail", Catalog.Rail52).Set("density", "1540").Set("dist", 100));
        Assert.Equal(Severity.Ok, r.Overall);
    }

    [Fact]
    public void LevelCrossing_SiteBreathingLengthOverride()
    {
        var r = Run("LC", i => i.Set("dist", 100).Set("blOverride", 120.0));
        Assert.Equal(Severity.Error, r.Overall);
    }

    [Fact]
    public void BreathingLengthTable_MatchesAnnex316()
    {
        Assert.Equal(77.0, Catalog.BreathingLength("III", Catalog.Rail60, Catalog.SleeperConventional, "1660")!.Value);
        Assert.Equal(71.0, Catalog.BreathingLength("IV", Catalog.Rail52, Catalog.SleeperConventional, "1540")!.Value);
        Assert.Equal(44.0, Catalog.BreathingLength("I", Catalog.Rail60, Catalog.SleeperWider, "1660")!.Value);
        Assert.Null(Catalog.BreathingLength("I", Catalog.Rail60, Catalog.SleeperWider, "1540"));
        Assert.Null(Catalog.BreathingLength("I", Catalog.Rail52, Catalog.SleeperWider, "1660"));
    }

    // ---------- SEJ type / curves ----------
    [Fact]
    public void Transition_AnySej_IsRed()
    {
        var r = Run("FINDER", i => i.Set("align", Rules.AlignTransition));
        Assert.Equal(Severity.Error, r.Overall);
        Assert.Empty(r.RecommendedDrawings);
    }

    [Theory]
    [InlineData("RT-4165", 0.5, Severity.Ok)]
    [InlineData("RT-4165", 0.6, Severity.Error)]
    [InlineData("RT-8822", 0.5, Severity.Ok)]
    [InlineData("RT-8822", 1.0, Severity.Error)]
    [InlineData("RT-6902", 2.0, Severity.Ok)]
    [InlineData("RT-6922", 2.5, Severity.Error)]
    [InlineData("RT-8924", 3.0, Severity.Caution)]
    [InlineData("RT-8926", 4.0, Severity.Caution)]
    [InlineData("RT-8926", 4.5, Severity.Error)]
    public void SejCurveLimits_60kg(string code, double deg, Severity expected)
    {
        var r = Run("FINDER", i => i.Set("rail", Catalog.Rail60).Set("zone", "II")
            .Set("align", deg == 0 ? Rules.AlignStraight : Rules.AlignCircular).Set("deg", deg).Set("sej", Label(code)));
        Assert.Equal(expected, r.Overall);
    }

    [Fact]
    public void WrongRailSectionDrawing_IsRed()
    {
        var r = Run("FINDER", i => i.Set("rail", Catalog.Rail60).Set("sej", Label("RT-4160")));
        Assert.Equal(Severity.Error, r.Overall);
        r = Run("FINDER", i => i.Set("rail", Catalog.Rail52).Set("align", Rules.AlignCircular).Set("deg", 3.0).Set("sej", Label("RT-8924")));
        Assert.Equal(Severity.Error, r.Overall);
    }

    [Fact]
    public void CombinationSej_OnlyAtJunction()
    {
        Assert.Equal(Severity.Error, Run("FINDER", i => i.Set("rail", Catalog.Rail60).Set("sej", Label("T-6782"))).Overall);
        Assert.Equal(Severity.Ok, Run("FINDER", i => i.Set("rail", Catalog.RailJunction).Set("sej", Label("T-6782"))).Overall);
        Assert.Equal(Severity.Error, Run("FINDER", i => i.Set("rail", Catalog.RailJunction).Set("sej", Label("RT-4165"))).Overall);
    }

    [Fact]
    public void Finder_SuggestsCorrectDrawings()
    {
        var r = Run("FINDER", i => i.Set("rail", Catalog.Rail52).Set("align", Rules.AlignCircular).Set("deg", 1.5));
        Assert.Contains(Label("RT-6914"), r.RecommendedDrawings);
        Assert.Contains(Label("RT-6930"), r.RecommendedDrawings);
        Assert.DoesNotContain(Label("RT-4160"), r.RecommendedDrawings);
        Assert.DoesNotContain(Label("RT-6902"), r.RecommendedDrawings);
    }

    [Fact]
    public void BeyondFourDegrees_OnlyBufferRailsWithPce()
    {
        var r = Run("FINDER", i => i.Set("rail", Catalog.Rail60).Set("zone", "II").Set("align", Rules.AlignCircular).Set("deg", 5.0));
        Assert.Single(r.RecommendedDrawings);
        Assert.Equal(Catalog.FindByCode("BUFFER")!.Label, r.RecommendedDrawings[0]);
        Assert.Equal(Severity.Caution, r.Overall);
    }

    [Fact]
    public void Lwr60_BeyondZoneLimit_IsRed()
    {
        var r = Run("FINDER", i => i.Set("rail", Catalog.Rail60).Set("zone", "III").Set("align", Rules.AlignCircular).Set("deg", 6.2));
        Assert.Equal(Severity.Error, r.Overall);
    }

    [Fact]
    public void Curve52_SharperThan440_ZoneDependent()
    {
        // 4.5° ≈ 389 m R: Zone I with precautions (amber), Zone II not permitted (red)
        var r1 = Run("CURVE", i => i.Set("rail", Catalog.Rail52).Set("zone", "I").Set("align", Rules.AlignStraight).Set("curveDeg", 4.5).Set("distTp", 100));
        Assert.NotEqual(Severity.Error, r1.Overall);
        var r2 = Run("CURVE", i => i.Set("rail", Catalog.Rail52).Set("zone", "II").Set("align", Rules.AlignStraight).Set("curveDeg", 4.5));
        Assert.Equal(Severity.Error, r2.Overall);
        var r3 = Run("CURVE", i => i.Set("rail", Catalog.Rail52).Set("zone", "I").Set("align", Rules.AlignStraight).Set("curveDeg", 4.5).Set("distTp", 60));
        Assert.Equal(Severity.Caution, r3.Overall);
    }

    // ---------- Bridges ----------
    [Fact]
    public void GirderIsolated_Needs30m()
    {
        Assert.Equal(Severity.Error, Run("BR_GIRDER", i => i.Set("case", Rules.CaseA).Set("isoLen", 25)).Overall);
        Assert.NotEqual(Severity.Error, Run("BR_GIRDER", i => i.Set("case", Rules.CaseA).Set("isoLen", 30)).Overall);
    }

    [Fact]
    public void GirderRailFree_Table1Limits()
    {
        // Zone I, 60 kg: 30 m (B), 77 m (C)
        var ok = Run("BR_GIRDER", i => i.Set("case", Rules.CaseB).Set("zone", "I").Set("rail", Catalog.Rail60).Set("len", 30).Set("dist", 100));
        Assert.NotEqual(Severity.Error, ok.Overall);
        var bad = Run("BR_GIRDER", i => i.Set("case", Rules.CaseB).Set("zone", "I").Set("rail", Catalog.Rail60).Set("len", 31).Set("dist", 100));
        Assert.Equal(Severity.Error, bad.Overall);
        var c = Run("BR_GIRDER", i => i.Set("case", Rules.CaseC).Set("zone", "I").Set("rail", Catalog.Rail60).Set("len", 77).Set("dist", 100));
        Assert.NotEqual(Severity.Error, c.Overall);
    }

    [Fact]
    public void GirderRailFree_BridgeInBreathingLength_IsRed()
    {
        var r = Run("BR_GIRDER", i => i.Set("case", Rules.CaseB).Set("zone", "II").Set("rail", Catalog.Rail52).Set("len", 20).Set("dist", 40));
        Assert.Equal(Severity.Error, r.Overall);
    }

    [Fact]
    public void GirderFarEnd_15mAndLengthLimits()
    {
        var ok = Run("BR_GIRDER", i => i.Set("case", Rules.CaseD).Set("zone", "III").Set("farSej", Catalog.FarSej190).Set("len", 70).Set("dist", 15));
        Assert.NotEqual(Severity.Error, ok.Overall);
        var tooLong = Run("BR_GIRDER", i => i.Set("case", Rules.CaseD).Set("zone", "III").Set("farSej", Catalog.FarSej190).Set("len", 71).Set("dist", 15));
        Assert.Equal(Severity.Error, tooLong.Overall);
        var wrongDist = Run("BR_GIRDER", i => i.Set("case", Rules.CaseD).Set("zone", "I").Set("farSej", Catalog.FarSej190).Set("len", 50).Set("dist", 10));
        Assert.Equal(Severity.Error, wrongDist.Overall);
        var sej120Zone3 = Run("BR_GIRDER", i => i.Set("case", Rules.CaseD).Set("zone", "III").Set("farSej", Catalog.FarSej120).Set("len", 10).Set("dist", 15));
        Assert.Equal(Severity.Error, sej120Zone3.Overall);
        var dg65 = Run("BR_GIRDER", i => i.Set("case", Rules.CaseD).Set("zone", "I").Set("rail", Catalog.Rail60).Set("farSej", Catalog.FarSejDg65).Set("len", 50).Set("dist", 15));
        Assert.NotEqual(Severity.Error, dg65.Overall);
        Assert.Contains(Label("RT-6922"), dg65.RecommendedDrawings);
    }

    [Fact]
    public void GirderSingleSpan_30mAndZoneLimit()
    {
        Assert.NotEqual(Severity.Error, Run("BR_GIRDER", i => i.Set("case", Rules.CaseF).Set("zone", "IV").Set("len", 75).Set("dist", 30)).Overall);
        Assert.Equal(Severity.Error, Run("BR_GIRDER", i => i.Set("case", Rules.CaseF).Set("zone", "IV").Set("len", 76).Set("dist", 30)).Overall);
        Assert.Equal(Severity.Error, Run("BR_GIRDER", i => i.Set("case", Rules.CaseF).Set("zone", "I").Set("len", 100).Set("dist", 15)).Overall);
    }

    [Fact]
    public void GirderEachPier_RequiresSejOnEveryPier()
    {
        Assert.Equal(Severity.Error, Run("BR_GIRDER", i => i.Set("case", Rules.CaseE).Set("eachPier", false)).Overall);
    }

    [Fact]
    public void BallastedBridge_Rules()
    {
        Assert.Equal(Severity.Ok, Run("BR_BALLASTED", i => i.Set("type", Rules.BridgeNoBearing)).Overall);
        Assert.Equal(Severity.Caution, Run("BR_BALLASTED", i => i.Set("type", Rules.BridgeWithBearing).Set("rsi", true)).Overall);
        Assert.Equal(Severity.Error, Run("BR_BALLASTED", i => i.Set("type", Rules.BridgeWithBearing).Set("iso", 20)).Overall);
    }

    // ---------- Track structure ----------
    [Fact]
    public void ChangeOfSection_Rules()
    {
        Assert.Equal(Severity.Ok, Run("TS_SECTION", i => i.Set("opt", Rules.SectionCombination)).Overall);
        Assert.Equal(Severity.Error, Run("TS_SECTION", i => i.Set("opt", Rules.SectionOrdinarySej)).Overall);
        Assert.Equal(Severity.Error, Run("TS_SECTION", i => i.Set("opt", Rules.SectionWithinLwr)).Overall);
        Assert.Equal(Severity.Error, Run("TS_SECTION", i => i.Set("opt", Rules.SectionPanels).Set("p52", 26)).Overall);
    }

    [Fact]
    public void BeyondSej_Needs39m()
    {
        Assert.Equal(Severity.Error, Run("TS_BEYOND", i => i.Set("len", 26)).Overall);
        Assert.Equal(Severity.Ok, Run("TS_BEYOND", i => i.Set("len", 39)).Overall);
    }

    [Fact]
    public void Blt_Needs30mAnd60kg()
    {
        Assert.Equal(Severity.Error, Run("TS_BLT", i => i.Set("dist", 20)).Overall);
        Assert.Equal(Severity.Error, Run("TS_BLT", i => i.Set("rail", Catalog.Rail52)).Overall);
        Assert.NotEqual(Severity.Error, Run("TS_BLT", i => i.Set("dist", 30)).Overall);
    }

    [Fact]
    public void Gradient_Rules()
    {
        Assert.Equal(Severity.Ok, Run("TS_GRADIENT", i => i.Set("grade", 150)).Overall);
        Assert.Equal(Severity.Error, Run("TS_GRADIENT", i => i.Set("grade", 80)).Overall); // no PCE
        Assert.Equal(Severity.Caution, Run("TS_GRADIENT", i => i.Set("grade", 80).Set("pce", true)).Overall);
        Assert.Equal(Severity.Error, Run("TS_GRADIENT", i => i.Set("zone", "IV").Set("grade", 70).Set("pce", true)).Overall);
        Assert.Equal(Severity.Error, Run("TS_GRADIENT", i => i.Set("rail", Catalog.Rail52).Set("grade", 90).Set("pce", true)).Overall);
        Assert.Equal(Severity.Error, Run("TS_GRADIENT", i => i.Set("grade", 70).Set("pce", true).Set("curveDeg", 5.5)).Overall);
    }

    [Fact]
    public void InsulatedJoints_Rules()
    {
        Assert.Equal(Severity.Ok, Run("TS_IJ", i => i.Set("type", Rules.IjDac)).Overall);
        Assert.Equal(Severity.Error, Run("TS_IJ", i => i.Set("type", Rules.IjG3L).Set("newWork", true)).Overall);
        Assert.Equal(Severity.Error, Run("TS_IJ", i => i.Set("type", Rules.IjFishPlated)).Overall);
    }
}
