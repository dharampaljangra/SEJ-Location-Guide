namespace SejGuide.Core;

/// <summary>
/// All SEJ location checks. Each check returns Green (OK), Amber (approval / precaution)
/// or Red (wrong selection) findings with the governing para.
/// Sources: IRPWM 2024 (corrected up to ACS-16) Paras 225, 226, 326–331;
/// RDSO Report CT-48 (Revised) Nov 2025.
/// </summary>
public static class Rules
{
    // ---------------- Groups ----------------
    public const string GroupTurnouts = "Points & Crossings (Turnouts)";
    public const string GroupLc = "Level Crossing";
    public const string GroupCurves = "Curves";
    public const string GroupBridges = "Bridges";
    public const string GroupTrack = "Track Structure & Others";
    public const string GroupFinder = "SEJ Drawing Finder";

    public static readonly string[] Groups = { GroupTurnouts, GroupLc, GroupCurves, GroupBridges, GroupTrack, GroupFinder };

    // ---------------- Common field values ----------------
    public const string AlignStraight = "Straight";
    public const string AlignCircular = "Circular curve";
    public const string AlignTransition = "Transition curve";
    public static readonly string[] Alignments = { AlignStraight, AlignCircular, AlignTransition };
    public const string SuggestSej = "Suggest the right SEJ for me";

    public static string[] SejChoices()
    {
        var list = new List<string> { SuggestSej };
        list.AddRange(Catalog.Drawings.Select(d => d.Label));
        return list.ToArray();
    }

    private const string Yes = "Yes";
    private const string No = "No";

    // ---------------- Reusable field blocks ----------------
    public static List<InputField> SejFields(string alignDefault = AlignStraight, double degDefault = 1.0, Func<Inputs, bool>? sejPickerVisible = null)
    {
        return new List<InputField>
        {
            InputField.Choice("rail", "Rail section at SEJ", Catalog.RailSections),
            InputField.Choice("zone", "Rail temperature zone", Catalog.Zones, "III", "IRPWM Fig. 3.7"),
            InputField.Choice("align", "Alignment at proposed SEJ location", Alignments, alignDefault),
            InputField.Number("deg", "Degree of curve at SEJ location", degDefault, "°",
                "BG: Radius = 1750 / Degree", i => i.Choice("align") == AlignCircular),
            InputField.Choice("sej", "Proposed SEJ / drawing", SejChoices(), SuggestSej,
                "Choose the drawing you intend to lay – the app checks it.", sejPickerVisible),
        };
    }

    public static List<InputField> BlFields(Func<Inputs, bool>? visible = null)
    {
        return new List<InputField>
        {
            InputField.Choice("sleeper", "Sleeper type", Catalog.Sleepers, null, "", visible),
            InputField.Choice("density", "Sleeper density (per km)", Catalog.Densities, null, "", visible),
            InputField.Number("blOverride", "Breathing length – site value (0 = use IRPWM Annex 3/16)", 0, "m",
                "Annex 3/16 values are indicative; enter a site-worked value if available.", visible),
        };
    }

    /// <summary>Breathing length used by checks (override if entered, else IRPWM Annex 3/16).</summary>
    public static double? BreathingLength(Inputs i)
    {
        var ov = i.Number("blOverride");
        if (ov > 0) return ov;
        return Catalog.BreathingLength(i.Choice("zone"), i.Choice("rail"), i.Choice("sleeper"), i.Choice("density"));
    }

    private static double SejDegree(Inputs i) => i.Choice("align") == AlignCircular ? Math.Max(0, i.Number("deg")) : 0;

    private static string F(double v) => v.ToString("0.##");

    // ---------------- SEJ type assessment ----------------
    /// <summary>Checks one SEJ drawing against rail section, zone and curvature.</summary>
    public static (Severity Severity, string Reason) Assess(SejDrawing d, string rail, string zone, double deg)
    {
        string where = deg <= 0 ? "straight track" : $"a {F(deg)}° curve (R ≈ {F(Math.Round(Catalog.RadiusFromDegree(deg)))} m)";

        if (d.Kind == SejKind.Combination)
        {
            if (rail != Catalog.RailJunction)
                return (Severity.Error, $"{d.Code} is a combination SEJ – only for the junction of 52 kg and 60 kg rails. Track here is {rail}.");
            if (deg > 0.5)
                return (Severity.Caution, $"{d.Code} at {where}: IRPWM does not state a curve limit for the combination SEJ – check the RDSO drawing.");
            return (Severity.Ok, $"{d.Code} is correct for the 52 kg / 60 kg junction on {where}.");
        }

        if (rail == Catalog.RailJunction)
            return (Severity.Error, "Different rail sections on either side of the SEJ: provide combination SEJ T-6782, or two 3-rail panels (39 m each) with a combination fish-plated joint between them.");

        if (d.Rail == "52" && rail != Catalog.Rail52)
            return (Severity.Error, $"{d.Code} is a 52 kg drawing, but the track is {rail}.");
        if (d.Rail == "60" && rail != Catalog.Rail60)
            return (Severity.Error, $"{d.Code} is a 60 kg drawing, but the track is {rail}.");

        switch (d.Kind)
        {
            case SejKind.Conventional:
            case SejKind.ThickWeb:
                if (deg > 0.5)
                    return (Severity.Error, $"{d.Code} (straight tongue/stock rail) is not permitted on curves sharper than 0.5° (3500 m R). Proposed location is {where}.");
                return (Severity.Ok, d.Kind == SejKind.ThickWeb
                    ? $"{d.Code} TWSEJ is permitted on {where}. Check the rail section on the drawing."
                    : $"{d.Code} is permitted on {where}.");

            case SejKind.Improved:
                if (deg > 2.0)
                    return (Severity.Error, $"{d.Code} improved SEJ is permitted on curves up to 2° only. Proposed location is {where}. For 2°–4° use RT-8924 / RT-8926 (60 kg BG) with CTE approval.");
                return (Severity.Ok, $"{d.Code} is permitted on {where}.");

            case SejKind.ImprovedSharpCurve:
                if (deg > 4.0)
                    return (Severity.Error, $"{d.Code} is permitted up to 4° only. Proposed location is {where}.");
                if (deg > 2.0)
                    return (Severity.Caution, $"{d.Code} is permitted on {where} with the approval of CTE.");
                return (Severity.Caution, $"{d.Code} is meant for curves beyond 2°. On {where} use RT-6902 (60 kg) or TWSEJ RT-8822 (up to 0.5°).");

            case SejKind.BufferRails:
                var max = Catalog.BufferRailMaxDegree(zone);
                if (deg > max)
                    return (Severity.Error, $"Buffer rails are permitted up to {F(max)}° in Zone {zone}. Proposed location is {where}.");
                return (Severity.Caution, $"Buffer rails need prior approval of PCE: {Catalog.BufferRailCount(zone)} rails of 6.5 m in Zone {zone}, 7.5 mm gap at each joint, laid on PSC with ERC, 1 m fishplates with 6 bolts.");
        }
        return (Severity.Error, "Unknown SEJ type.");
    }

    /// <summary>Drawings that may be laid for the given conditions (best first).</summary>
    public static List<SejDrawing> Permitted(string rail, string zone, double deg)
    {
        var list = new List<SejDrawing>();
        foreach (var d in Catalog.Drawings)
        {
            if (d.Kind == SejKind.BufferRails) continue;
            var (sev, _) = Assess(d, rail, zone, deg);
            if (sev == Severity.Ok) list.Add(d);
            else if (sev == Severity.Caution && (d.Kind == SejKind.Combination || (d.Kind == SejKind.ImprovedSharpCurve && deg > 2.0)))
                list.Add(d);
        }
        if (list.Count == 0)
        {
            var buffer = Catalog.FindByCode("BUFFER")!;
            if (rail != Catalog.RailJunction && Assess(buffer, rail, zone, deg).Severity != Severity.Error)
                list.Add(buffer);
        }
        return list;
    }

    /// <summary>Common SEJ type / alignment evaluation, added to every location check.</summary>
    public static void EvaluateSej(Inputs i, CheckResult r, bool usePicker = true)
    {
        var rail = i.Choice("rail");
        var zone = i.Choice("zone");
        var align = i.Choice("align");

        if (align == AlignTransition)
        {
            r.Error("SEJ of any type shall not be located on the transition of a curve. Shift the SEJ to straight track or to the circular portion.", "IRPWM 328(1)(d)");
            return;
        }

        var deg = SejDegree(i);
        if (align == AlignCircular && deg <= 0)
        {
            r.Error("Enter the degree of curve at the SEJ location.");
            return;
        }

        LwrCurvePermission(rail, zone, deg, r);

        var permitted = Permitted(rail, zone, deg);
        foreach (var d in permitted) r.RecommendedDrawings.Add(d.Label);

        if (!usePicker) return;

        var sel = i.Choice("sej");
        if (string.IsNullOrEmpty(sel) || sel == SuggestSej)
        {
            if (permitted.Count == 0)
                r.Error("No SEJ design and no buffer-rail arrangement is permitted at this location. Shift the SEJ.", "IRPWM 328");
            else if (permitted.All(p => p.Kind == SejKind.BufferRails))
                r.Caution("No SEJ design is permitted at this curvature. Buffer rails may be laid in lieu of SEJ with prior approval of PCE.", "IRPWM 328(2)");
            else
                r.Info("Permitted SEJ: " + string.Join(", ", permitted.Select(p => p.Code)) + ".", "IRPWM 328(1), Annex 2/4(B)");
            return;
        }

        var drawing = Catalog.FindByLabel(sel);
        if (drawing == null) { r.Error("Unknown SEJ selection."); return; }

        var (sev, reason) = Assess(drawing, rail, zone, deg);
        if (drawing.Kind == SejKind.BufferRails && permitted.Any(p => p.Kind != SejKind.BufferRails))
        {
            sev = Severity.Caution;
            reason += " An SEJ is permitted here; buffer rails are meant for locations where SEJ is not permitted, or for temporary use during maintenance/renewal.";
        }
        r.Findings.Add(new Finding(sev, reason, drawing.Reference));

        if (sev == Severity.Error && permitted.Count > 0)
            r.Info("Use instead: " + string.Join(", ", permitted.Select(p => p.Code)) + ".");

        if (sev != Severity.Error && deg <= 0.5 && drawing.Kind is SejKind.Conventional or SejKind.Improved)
            r.Info("TWSEJ RT-8822 is to be provided progressively in place of SEJ/ISEJ during renewals, replacements and new works.", "IRPWM 328(1)(c)");
    }

    /// <summary>Whether LWR/CWR itself may exist on the curve where the SEJ is proposed – IRPWM 326(2).</summary>
    public static void LwrCurvePermission(string rail, string zone, double deg, CheckResult r)
    {
        if (deg <= 0) return;
        var radius = Catalog.RadiusFromDegree(deg);
        if (rail == Catalog.Rail60)
        {
            var lim = Catalog.MaxLwrCurve60(zone);
            if (deg > lim)
                r.Error($"LWR/CWR is not permitted on curves sharper than {F(lim)}° in Zone {zone}. Terminate the LWR before this curve.", "IRPWM 326(2)(a)");
            else if (radius < 440)
                r.Caution($"Curve sharper than 440 m R: LWR with 60 kg on PSC 1660 is permitted up to {F(lim)}° in Zone {zone} only by PCE (circular or case-to-case), complying with Annexure 3/19." +
                          (deg > 6.0 ? " Cross-bracing to RDSO/T-8329 is required on curves sharper than 6°." : ""), "IRPWM 326(2)(a)");
        }
        else
        {
            if (radius >= 440) return;
            if (zone == "I" && radius >= 350)
                r.Caution("52 kg LWR on curves sharper than 440 m R (up to 350 m R) is permitted in Zone I only with these precautions: PSC 1540+ with 300 mm clean cushion; 600 mm shoulder ballast on the outside, extended 100 m beyond the tangent point; creep reference marks every 50 m.", "IRPWM 326(2)(b)");
            else
                r.Error($"LWR/CWR with 52 kg rail is not permitted on curves sharper than 440 m R{(zone == "I" ? " (350 m R in Zone I)" : "")}. Terminate the LWR before this curve.", "IRPWM 326(2)(b)");
        }
    }

    // ================= Location checks =================
    // Built lazily: static fields declared further down (e.g. GirderCases) must be initialised first.
    private static List<CheckDefinition>? _all;
    public static List<CheckDefinition> All => _all ??= Build();

    public static CheckDefinition Get(string id) => All.First(c => c.Id == id);

    private static List<CheckDefinition> Build()
    {
        var list = new List<CheckDefinition>
        {
            TurnoutTerminated(),
            TurnoutOneSide(),
            TurnoutThrough(),
            Crossover(),
            TurnInCurve(),
            LoopLine(),
            LevelCrossing(),
            Curve(),
            GirderBridge(),
            BallastedBridge(),
            ChangeOfRailSection(),
            TrackBeyondSej(),
            BltJunction(),
            Gradient(),
            InsulatedJoints(),
            DrawingFinder(),
        };
        return list;
    }

    // ---- 1. LWR terminated near turnout ----
    public const string FromSrj = "Stock Rail Joint (SRJ)";
    public const string FromCrossing = "Crossing (heel / back of crossing)";
    public const string TurnoutFishPlated = "No – turnout joints fish-plated";
    public const string TurnoutWelded = "Yes – turnout joints welded, LWR not continued (CT-48 Para 20.1)";

    private static CheckDefinition TurnoutTerminated()
    {
        var fields = new List<InputField>
        {
            InputField.Choice("from", "Distance measured from", new[] { FromSrj, FromCrossing }),
            InputField.Choice("welded", "Are the turnout joints welded?", new[] { TurnoutFishPlated, TurnoutWelded }),
            InputField.Number("dist", "Distance between SEJ and SRJ / crossing", 39),
            InputField.Toggle("elastic", "Panel between turnout and SEJ has elastic fastenings with adequate toe load"),
        };
        fields.AddRange(SejFields());
        return new CheckDefinition
        {
            Id = "PC_TERMINATED", Group = GroupTurnouts,
            Title = "LWR terminated near a turnout",
            Subtitle = "SEJ distance from SRJ / crossing",
            Reference = "IRPWM 326(8)(a); CT-48 Para 20.1",
            Fields = fields,
            Evaluate = i =>
            {
                var r = new CheckResult();
                bool welded = i.Choice("welded") == TurnoutWelded;
                double req = welded ? 40 : 39;
                double dist = i.Number("dist");
                string from = i.Choice("from") == FromSrj ? "SRJ" : "crossing";

                if (dist < req)
                    r.Error($"SEJ is only {F(dist)} m from the {from}. Minimum {F(req)} m is required" +
                            (welded ? " when the turnout joints are welded (SRJ movement ≈ 7 mm at 40 m)." : " (one three-rail panel)."),
                            welded ? "CT-48 Para 20.1, Annex II" : "IRPWM 326(8)(a)");
                else
                    r.Ok($"Distance {F(dist)} m from the {from} is not less than the required {F(req)} m.", welded ? "CT-48 Para 20.1" : "IRPWM 326(8)(a)");

                if (!welded && dist >= 39 && dist < 40)
                    r.Info("CT-48 Para 20.1 states that an SEJ cannot be provided at less than 40 m. Adopt 40 m where feasible.", "CT-48 Para 20.1");
                if (welded)
                    r.Info("Provide 32 mm holes with 25 mm bolts at heel and distance blocks to absorb the ≈7 mm SRJ movement. Treat the welded stretch as LWR for inspection and maintenance.", "CT-48 Para 20.1, 20.3(v)");

                if (!i.Toggle("elastic"))
                    r.Error("The panel between the turnout and the SEJ must have elastic fastenings with adequate toe load to arrest creep.", "IRPWM 326(8)(a)");

                r.Info("This applies on both sides: between the SRJ and the SEJ, and between the crossing and the SEJ.", "IRPWM 326(8)(a)");
                r.RecommendedLocation = $"SEJ at not less than {F(req)} m from the SRJ and from the crossing, with a 39 m three-rail panel on elastic fastenings.";
                EvaluateSej(i, r);
                return r;
            }
        };
    }

    // ---- 2. LWR continued through turnout from one side only ----
    public const string SideBlock = "LWR not continued towards block section (yard LWR continued through turnout)";
    public const string SideYard = "LWR not continued towards yard (block-section LWR continued through turnout)";

    private static CheckDefinition TurnoutOneSide()
    {
        var fields = new List<InputField>
        {
            InputField.Choice("side", "Situation", new[] { SideBlock, SideYard }),
            InputField.Number("dist", "Distance of SEJ from SRJ", 39),
            InputField.Toggle("para5", "Track structure meets CT-48 Para 5.0 (60 kg/90 UTS, PSC 1660/km, ERC Mk-V in turnout + 50 m, TWS, WCMS, ACD, 300 mm cushion)"),
        };
        fields.AddRange(SejFields());
        return new CheckDefinition
        {
            Id = "PC_ONESIDE", Group = GroupTurnouts,
            Title = "LWR through turnout – one side only",
            Subtitle = "LWR stops towards block section or towards yard",
            Reference = "CT-48 Para 20.2, 20.3",
            Fields = fields,
            Evaluate = i =>
            {
                var r = new CheckResult();
                bool towardsBlock = i.Choice("side") == SideBlock;
                string sideTxt = towardsBlock ? "block section" : "yard";
                string para = towardsBlock ? "CT-48 Para 20.2" : "CT-48 Para 20.3";
                double dist = i.Number("dist");

                if (dist < 39)
                    r.Error($"SEJ is only {F(dist)} m from the SRJ. It must be at least 39 m from the SRJ towards the {sideTxt} side.", para);
                else
                    r.Ok($"SEJ at {F(dist)} m from the SRJ towards the {sideTxt} side meets the 39 m minimum.", para);

                if (!i.Toggle("para5"))
                    r.Error("The track between the SEJs, and on the side where LWR continues, must meet all CT-48 Para 5.0 requirements before welding.", "CT-48 Para 20.3(i)");

                r.Info("Weld the internal turnout joints first (Para 8.0), then the approaches (Para 9.0), without removing the SEJ. Treat the welded track as LWR, and monitor it on the Annexure-III proforma.", "CT-48 Para 20.3(iii)–(vi)");
                r.RecommendedLocation = $"SEJ at not less than 39 m from the SRJ, towards the {sideTxt} side.";
                EvaluateSej(i, r);
                return r;
            }
        };
    }

    // ---- 3. LWR continued through turnout on both sides ----
    private static CheckDefinition TurnoutThrough()
    {
        var fields = new List<InputField>
        {
            InputField.Toggle("propose", "Is an SEJ proposed within the turnout zone or between turnouts?", false),
            InputField.Choice("zone", "Rail temperature zone", Catalog.Zones, "III"),
            InputField.Toggle("rail60", "Rails minimum 60 kg / 90 UTS"),
            InputField.Toggle("psc", "PSC sleepers 1660/km; turnout sleepers as per RDSO standard layout"),
            InputField.Toggle("erc", "ERC Mk-V (and IRS/T-55-2025 rail pads) in turnout zone and 50 m on either side"),
            InputField.Toggle("tws", "Thick Web Switches on PSC sleepers with elastic fastenings"),
            InputField.Toggle("wcms", "Weldable CMS crossing provided"),
            InputField.Toggle("acd", "Anti Creep Device (ACD) behind heel of switch, fixed at td"),
            InputField.Toggle("ballast", "Ballast cushion ≥ 300 mm (≥ 150 mm clean at all times)"),
            InputField.Toggle("welds", "Tongue-rail and lead-rail joints welded; SSD at JOH; 60 kg check rail with blocks"),
        };
        return new CheckDefinition
        {
            Id = "PC_THROUGH", Group = GroupTurnouts,
            Title = "LWR continued through turnout (both sides)",
            Subtitle = "Turnout integrated with LWR – no SEJ at turnout",
            Reference = "CT-48 Para 5.0, 9.0, 19.0, 21.0",
            Fields = fields,
            Evaluate = i =>
            {
                var r = new CheckResult();
                if (i.Toggle("propose"))
                    r.Error("No SEJ shall be provided within or near the turnout when LWR/CWR is continued through it. The turnout lies in the fixed (non-breathing) zone; SEJs of the inner and outer LWR are removed during integration.", "CT-48 Para 9.0(vii)–(x), 10.0");
                else
                    r.Ok("No SEJ at the turnout. SEJs only at the ends of the CWR.", "CT-48 Para 9.0, 10.0");

                void Need(string key, string text, string reference)
                {
                    if (!i.Toggle(key)) r.Error(text, reference);
                }
                Need("rail60", "Minimum 60 kg / 90 UTS rails are required to continue LWR through a turnout.", "CT-48 Para 5.0(i), 21.0(i)");
                Need("psc", "PSC sleepers at 1660/km are required, with turnout sleepers as per the RDSO standard layout.", "CT-48 Para 5.0(i)");
                Need("erc", "ERC Mk-V is compulsory in the turnout zone and for 50 m on either side (ERC Mk-III permitted beyond that).", "CT-48 Para 5.0(vii), 21.0(i)");
                Need("tws", "Thick Web Switches on PSC sleepers with elastic fastenings are required.", "CT-48 Para 5.0, 21.0(ii)");
                Need("wcms", "A Weldable CMS crossing is required.", "CT-48 Para 5.0, 21.0(iv); IRPWM 326(9)");
                Need("acd", "An Anti Creep Device (permitting ±7 mm) is required behind the heel of switch.", "CT-48 Para 5.0(iv), 11.0");
                Need("ballast", "A minimum 300 mm ballast cushion (150 mm clean) is required throughout the turnout.", "CT-48 Para 5.0(ii), 21.0(vi)");
                Need("welds", "Tongue-rail and lead-rail joints must be welded; SSD at JOH; 60 kg check rail.", "CT-48 Para 5.0(viii)–(ix), 21.0(v)");

                if (i.Choice("zone") == "IV")
                    r.Caution("Zone IV: LWR may be continued through turnouts, but the turnouts and CWR must be kept under close monitoring for one weather cycle, with feedback to RDSO.", "CT-48 Para 19.0");

                r.Info("Weld the last pair of joints after removing an SEJ at td, after removing all ERCs for 100 m on the LWR side and up to the turnout on the turnout side.", "CT-48 Para 9.0(vii), (x)");
                r.Info("For one weather cycle, inspect the turnout zone weekly (JE/SSE) and fortnightly (ADEN). Keep toe load ≥ 800 kg. Measure creep monthly at ATS, ANC, mid-lead, 20 m from ATS, 50 m beyond ANC, and every 500 m.", "CT-48 Para 17.0");
                r.RecommendedLocation = "No SEJ at the turnout or between turnouts. SEJs only at the ends of the CWR.";
                return r;
            }
        };
    }

    // ---- 4. Crossover ----
    private static CheckDefinition Crossover()
    {
        var fields = new List<InputField>
        {
            InputField.Toggle("between", "Is an SEJ proposed between the two turnouts of the crossover?", false),
            InputField.Toggle("both", "Both turnouts of the crossover have WCMS and Thick Web Switches"),
        };
        return new CheckDefinition
        {
            Id = "PC_CROSSOVER", Group = GroupTurnouts,
            Title = "Crossover",
            Subtitle = "CWR through both turnouts of a crossover",
            Reference = "CT-48 Para 12.2, 12.4",
            Fields = fields,
            Evaluate = i =>
            {
                var r = new CheckResult();
                if (i.Toggle("between"))
                    r.Error("No SEJ between the turnouts of a crossover. All joints of the crossover (both turnouts and the track between) are to be welded.", "CT-48 Para 12.2");
                else
                    r.Ok("No SEJ within the crossover; the whole crossover is welded.", "CT-48 Para 12.2");

                if (!i.Toggle("both"))
                    r.Error("CWR shall be continued through a crossover only when WCMS and TWS are provided on both turnouts. Otherwise, terminate the LWR with a 39 m panel and SEJ.", "CT-48 Para 12.4; IRPWM 326(8)(a)");

                r.Info("Weld the last pair of joints between the turnouts at td, after removing all ERCs between the turnouts. Provide 150 mm extra shoulder ballast on both sides in the crossover.", "CT-48 Para 5.0(iii), 12.2");
                r.RecommendedLocation = "No SEJ inside the crossover.";
                return r;
            }
        };
    }

    // ---- 5. Turn-in curve with derailing switch ----
    private static CheckDefinition TurnInCurve()
    {
        var fields = new List<InputField>
        {
            InputField.Number("distDs", "Distance from SRJ to derailing switch (turn-in curve / loop)", 100),
            InputField.Toggle("before", "SEJ placed just before the derailing switch"),
        };
        fields.AddRange(SejFields());
        fields.AddRange(BlFields());
        return new CheckDefinition
        {
            Id = "PC_TURNIN", Group = GroupTurnouts,
            Title = "Turn-in curve with derailing switch",
            Subtitle = "Diverging track of turnout ending in derailing switch",
            Reference = "CT-48 Para 12.3",
            Fields = fields,
            Evaluate = i =>
            {
                var r = new CheckResult();
                var bl = BreathingLength(i);
                if (bl == null)
                {
                    r.Error("The breathing length is not given in IRPWM Annex 3/16 for this sleeper/density/rail. Enter the site value.");
                    return r;
                }
                double distDs = i.Number("distDs");
                if (distDs < bl)
                {
                    r.Error($"The derailing switch is {F(distDs)} m from the SRJ, which is within one breathing length ({F(bl.Value)} m). CWR shall not be continued into this line. Terminate the LWR before the turnout (39 m panel + SEJ).", "CT-48 Para 12.3; IRPWM 326(8)(a)");
                    r.RecommendedLocation = "Do not continue CWR into this line. Terminate the LWR before the turnout.";
                    return r;
                }
                r.Ok($"Weld all joints for at least one breathing length ({F(bl.Value)} m) from the SRJ.", "CT-48 Para 12.3");
                if (!i.Toggle("before"))
                    r.Error("The SEJ should be provided just before the derailing switch.", "CT-48 Para 12.3");
                else
                    r.Ok("SEJ just before the derailing switch.", "CT-48 Para 12.3");
                r.Info("Alternatively, the LWR can be continued further into the turn-in curve or loop line if there is no derailing switch.", "CT-48 Para 12.3");
                r.Info("Provide extra shoulder ballast (150 mm) for up to 150 m in the turn-in curve.", "CT-48 Para 5.0(iii)");
                r.RecommendedLocation = $"SEJ just before the derailing switch, at not less than {F(bl.Value)} m (one breathing length) from the SRJ.";
                EvaluateSej(i, r);
                return r;
            }
        };
    }

    // ---- 6. Loop / other line with inferior track ----
    private static CheckDefinition LoopLine()
    {
        var fields = new List<InputField>
        {
            InputField.Number("len", "Length of CWR continued into loop from SRJ up to SEJ", 80),
            InputField.Toggle("para5", "This length meets CT-48 Para 5.0 track structure"),
        };
        fields.AddRange(SejFields());
        fields.AddRange(BlFields());
        return new CheckDefinition
        {
            Id = "PC_LOOP", Group = GroupTurnouts,
            Title = "Loop / other line not on LWR",
            Subtitle = "Loop line with inferior track structure",
            Reference = "CT-48 Para 12.5",
            Fields = fields,
            Evaluate = i =>
            {
                var r = new CheckResult();
                var bl = BreathingLength(i);
                if (bl == null)
                {
                    r.Error("The breathing length is not given in IRPWM Annex 3/16 for this sleeper/density/rail. Enter the site value.");
                    return r;
                }
                double len = i.Number("len");
                if (len < bl)
                    r.Error($"CWR must be continued at least one breathing length ({F(bl.Value)} m) into the loop from the SRJ before providing the SEJ. Entered: {F(len)} m.", "CT-48 Para 12.5");
                else
                    r.Ok($"{F(len)} m into the loop from the SRJ is not less than one breathing length ({F(bl.Value)} m).", "CT-48 Para 12.5");
                if (!i.Toggle("para5"))
                    r.Error("The track from the SRJ to one breathing length into the loop must comply with CT-48 Para 5.0.", "CT-48 Para 12.5");
                r.Info("Integrate the loop line with CWR only after the necessary upgradation of its track structure.", "CT-48 Para 12.5");
                r.RecommendedLocation = $"SEJ in the loop at not less than {F(bl.Value)} m from the SRJ.";
                EvaluateSej(i, r);
                return r;
            }
        };
    }

    // ---- 7. Level crossing ----
    private static CheckDefinition LevelCrossing()
    {
        var fields = new List<InputField>
        {
            InputField.Number("dist", "Distance from SEJ to nearest edge of level crossing", 100),
        };
        fields.AddRange(SejFields());
        fields.AddRange(BlFields());
        return new CheckDefinition
        {
            Id = "LC", Group = GroupLc,
            Title = "Level crossing near SEJ",
            Subtitle = "LC must be outside the breathing length",
            Reference = "IRPWM 326(7), Annex 3/16",
            Fields = fields,
            Evaluate = i =>
            {
                var r = new CheckResult();
                var bl = BreathingLength(i);
                if (bl == null)
                {
                    r.Error("The breathing length is not given in IRPWM Annex 3/16 for this sleeper/density/rail (e.g. wider-base PSC at 1540/km). Enter the site value.");
                    return r;
                }
                double dist = i.Number("dist");
                if (dist <= bl)
                    r.Error($"The level crossing falls within the breathing length ({F(bl.Value)} m). Shift the SEJ so that it is more than {F(bl.Value)} m from the LC.", "IRPWM 326(7)");
                else if (dist < bl * 1.1)
                    r.Caution($"The LC is only {F(dist - bl.Value)} m beyond the breathing length. Annex 3/16 values are indicative and vary with site conditions, so keep a margin (advisory).", "IRPWM 326(7), Annex 3/16 Note");
                else
                    r.Ok($"The LC is {F(dist)} m from the SEJ, outside the breathing length ({F(bl.Value)} m).", "IRPWM 326(7)");
                r.RecommendedLocation = $"SEJ more than {F(bl.Value)} m from the LC, so that the LC lies in the fixed zone of the LWR.";
                EvaluateSej(i, r);
                return r;
            }
        };
    }

    // ---- 8. Curves ----
    private static CheckDefinition Curve()
    {
        var fields = SejFields(AlignCircular, 1.0);
        fields.Add(InputField.Number("curveDeg", "Degree of the adjoining curve", 4.5, "°", "", i => i.Choice("rail") == Catalog.Rail52));
        fields.Add(InputField.Number("curveLen", "Length of the adjoining curve", 300, "m", "", i => i.Choice("rail") == Catalog.Rail52));
        fields.Add(InputField.Number("distTp", "Distance of SEJ from tangent point (on straight)", 100, "m", "",
            i => i.Choice("rail") == Catalog.Rail52 && i.Choice("align") == AlignStraight));
        return new CheckDefinition
        {
            Id = "CURVE", Group = GroupCurves,
            Title = "SEJ on / near a curve",
            Subtitle = "Curvature limits for each SEJ design",
            Reference = "IRPWM 326(2), 328(1)",
            Fields = fields,
            Evaluate = i =>
            {
                var r = new CheckResult();
                EvaluateSej(i, r);

                if (i.Choice("rail") == Catalog.Rail52)
                {
                    double cDeg = i.Number("curveDeg");
                    double cR = Catalog.RadiusFromDegree(cDeg);
                    if (cDeg > 0 && cR < 440)
                    {
                        if (i.Choice("zone") != "I" || cR < 350)
                            r.Error($"The adjoining curve (≈{F(Math.Round(cR))} m R) is sharper than permitted for 52 kg LWR. Terminate the LWR before this curve.", "IRPWM 326(2)(b)");
                        else if (i.Number("curveLen") > 250)
                        {
                            if (i.Choice("align") != AlignStraight)
                                r.Caution("For a 52 kg curve sharper than 440 m R and longer than 250 m, the SEJ should preferably be on either side, in straight track 100 m from the tangent point.", "IRPWM 326(2)(b)(iv)");
                            else if (i.Number("distTp") < 100)
                                r.Caution($"The SEJ should preferably be in straight track 100 m from the tangent point. Entered: {F(i.Number("distTp"))} m.", "IRPWM 326(2)(b)(iv)");
                            else
                                r.Ok("SEJ is in straight track at least 100 m from the tangent point.", "IRPWM 326(2)(b)(iv)");
                        }
                    }
                }

                if (r.Findings.Count == 0) r.Ok("Location is permitted for the SEJ.");
                r.RecommendedLocation = "SEJ on straight track or on the circular portion within the curvature limit of the chosen design – never on the transition.";
                return r;
            }
        };
    }

    // ---- 9. Girder bridges (un-ballasted deck) ----
    public const string CaseA = "A – Bridge isolated (LWR not continued over bridge)";
    public const string CaseB = "B – LWR over bridge, rail-free fastenings";
    public const string CaseC = "C – LWR over bridge, rail-free + partly box-anchored";
    public const string CaseD = "D – SEJ at far-end approach of bridge";
    public const string CaseE = "E – Welded rails pier to pier, SEJ on each pier";
    public const string CaseF = "F – Single span, SEJs at both approaches";
    public static readonly string[] GirderCases = { CaseA, CaseB, CaseC, CaseD, CaseE, CaseF };
    public const string BearingSliding = "Sliding bearings on both ends";
    public const string BearingRollerRocker = "Roller on one end, rocker on other end";

    private static CheckDefinition GirderBridge()
    {
        static bool Is(Inputs i, params string[] cases) => cases.Contains(i.Choice("case"));

        var fields = new List<InputField>
        {
            InputField.Choice("case", "Arrangement", GirderCases),
            InputField.Number("len", "Overall length of bridge", 20, "m", "", i => !Is(i, CaseA, CaseE)),
            InputField.Number("span", "Largest single span", 18.3, "m", "", i => Is(i, CaseB, CaseC)),
            InputField.Choice("bearing", "Bearings", new[] { BearingSliding, BearingRollerRocker }, null, "", i => Is(i, CaseB, CaseC, CaseE, CaseF)),
            InputField.Number("isoLen", "Well-anchored PSC track between abutment and LWR end (each side)", 30, "m", "", i => Is(i, CaseA)),
            InputField.Number("dist", "Distance of SEJ from abutment", 15, "m", "", i => Is(i, CaseB, CaseC, CaseD, CaseF)),
            InputField.Choice("farSej", "SEJ used at far-end approach", Catalog.FarEndSejTypes, null, "", i => Is(i, CaseD)),
            InputField.Toggle("rf", "Rail-free fastenings throughout the bridge between abutments", true, "", i => !Is(i, CaseA)),
            InputField.Toggle("approach", "Approach track 50 m both sides well anchored (PRC + ERC, ballast heaped to rail foot, compacted)", true, "", i => Is(i, CaseB, CaseC, CaseF)),
            InputField.Toggle("box", "4 central sleepers of each span box-anchored with fair 'V' anchors (T-1045 for 60 kg / T-10327 for 52 kg)", true, "", i => Is(i, CaseC)),
            InputField.Toggle("centre", "Girders centralised on bearings; sliding bearings inspected in March & October, lubricated every 2 years", true, "", i => Is(i, CaseC)),
            InputField.Toggle("eachPier", "SEJ provided over every pier", true, "", i => Is(i, CaseE)),
            InputField.Toggle("boxEF", "Box-anchored on 4 sleepers (fixed end for roller/rocker; central portion for sliding both ends)", true, "", i => Is(i, CaseE, CaseF)),
            InputField.Toggle("single", "Bridge is a single span", true, "", i => Is(i, CaseF)),
            InputField.Toggle("sej190", "SEJs used have 190 mm gap", true, "", i => Is(i, CaseF)),
        };
        fields.AddRange(SejFields(AlignStraight, 1.0, i => Is(i, CaseA, CaseB, CaseC)));
        fields.AddRange(BlFields(i => Is(i, CaseB, CaseC)));

        return new CheckDefinition
        {
            Id = "BR_GIRDER", Group = GroupBridges,
            Title = "Girder bridge (un-ballasted deck)",
            Subtitle = "Isolated, far-end SEJ, SEJ on piers, single span…",
            Reference = "IRPWM 331(1)–(8), Table-1, Fig. 3.6",
            Fields = fields,
            Evaluate = i =>
            {
                var r = new CheckResult();
                var c = i.Choice("case");
                var zone = i.Choice("zone");
                var rail = i.Choice("rail");
                double len = i.Number("len");
                double dist = i.Number("dist");

                if (rail == Catalog.RailJunction && c != CaseA)
                    r.Error("Two different rail sections are not permitted within an LWR/CWR.", "IRPWM 326(5)(c)");

                if (c != CaseA && !i.Toggle("rf"))
                    r.Error("Rail-free fastenings are required throughout the bridge between abutments.", "IRPWM 331(1)(a), 226(5)");

                if (c == CaseA)
                {
                    double iso = i.Number("isoLen");
                    if (iso < 30)
                        r.Error($"The bridge must be isolated from the LWR by at least 30 m of well-anchored PSC sleeper track on either side. Entered: {F(iso)} m.", "IRPWM 331(6), 226(5)");
                    else
                        r.Ok($"Isolation length {F(iso)} m ≥ 30 m on each side.", "IRPWM 331(6)");
                    r.Info("Track on the girder is fish-plated/SWR with rail-free fastenings. No fish-plated joint on the girder or within 6 m of either abutment for SWR.", "IRPWM 226(3)(a), 226(5)");
                    r.RecommendedLocation = "LWR end (SEJ) at not less than 30 m from each abutment, with well-anchored PSC sleeper track in between.";
                    EvaluateSej(i, r);
                }
                else if (c == CaseB || c == CaseC)
                {
                    bool partly = c == CaseC;
                    string para = partly ? "IRPWM 331(2)" : "IRPWM 331(1)";
                    if (i.Number("span") > 30.5)
                        r.Error("A single span over 30.5 m is not covered by this arrangement. Use Case D/E/F or isolate the bridge (Case A).", para);
                    if (i.Choice("bearing") != BearingSliding)
                        r.Error("This arrangement requires sliding bearings at both ends.", para);

                    double lim = Catalog.Table1(zone, rail, partly);
                    if (len > lim)
                        r.Error($"Bridge length {F(len)} m exceeds the Table-1 limit of {F(lim)} m (Zone {zone}, {rail}). " +
                                (partly ? "Use Case D (far-end SEJ) or isolate the bridge." : "Consider partly box-anchoring (Case C), a far-end SEJ (Case D), or isolation (Case A)."), para + ", Table-1");
                    else
                        r.Ok($"Bridge length {F(len)} m is within the Table-1 limit of {F(lim)} m.", para + ", Table-1");

                    var bl = BreathingLength(i);
                    if (bl == null)
                        r.Error("Breathing length not available in Annex 3/16 for this combination. Enter the site value.");
                    else if (dist <= bl)
                        r.Error($"The bridge falls within the breathing length ({F(bl.Value)} m). Locate the SEJ more than {F(bl.Value)} m from the bridge.", "IRPWM 331(1)(b)");
                    else
                        r.Ok($"SEJ at {F(dist)} m keeps the bridge outside the breathing length ({F(bl.Value)} m).", "IRPWM 331(1)(b)");

                    if (!i.Toggle("approach"))
                        r.Error("The approach track up to 50 m on both sides must be well anchored (PRC sleepers, ERC with adequate toe load, ballast heaped to rail foot and compacted).", "IRPWM 331(1)(b),(c)");
                    if (partly && !i.Toggle("box"))
                        r.Error("On each span, the 4 central sleepers must be box-anchored with fair 'V' type anchors.", "IRPWM 331(2)(a)");
                    if (partly && !i.Toggle("centre"))
                        r.Error("The girders must be centralised; inspect sliding bearings in March and October and lubricate them every 2 years.", "IRPWM 331(2)(c),(d)");

                    r.RecommendedLocation = bl == null ? "SEJ beyond the breathing length from the bridge." :
                        $"SEJ more than {F(bl.Value)} m away from the bridge (bridge outside the breathing length).";
                    EvaluateSej(i, r);
                }
                else if (c == CaseD)
                {
                    var farSej = i.Choice("farSej");
                    var limit = Catalog.FarEndLimit(zone, farSej);
                    if (limit == null)
                        r.Error($"A 120 mm SEJ (or DG 65 in lieu) is not provided for in Zone {zone}. Use an SEJ with 190 mm movement.", "IRPWM 331(3)");
                    else if (len > limit.Value.Length)
                        r.Error($"Bridge length {F(len)} m exceeds {F(limit.Value.Length)} m permitted for this SEJ in Zone {zone}.", "IRPWM 331(3)");
                    else
                        r.Ok($"Bridge length {F(len)} m is within {F(limit.Value.Length)} m. Initial gap at td = {F(limit.Value.GapCm)} cm.", "IRPWM 331(3)");

                    if (Math.Abs(dist - 15) > 0.5)
                        r.Error($"The SEJ is to be installed 15 m from the abutment. Entered: {F(dist)} m.", "IRPWM 331(3) Note (i)");
                    else
                        r.Ok("SEJ 15 m from the far abutment.", "IRPWM 331(3) Note (i)");

                    if (farSej == Catalog.FarSejDg65)
                    {
                        if (rail == Catalog.Rail60) r.RecommendedDrawings.Add(Catalog.FindByCode("RT-6922")!.Label);
                        else if (rail == Catalog.Rail52) r.RecommendedDrawings.Add(Catalog.FindByCode("RT-6930")!.Label);
                        r.Info("Improved SEJ with 2 gaps of 65 mm (RDSO/T-6922 for 60 kg, T-6930 for 52 kg) may be used in lieu of the IRS 120 mm SEJ.", "IRPWM 331(3) Note (ii)");
                    }
                    else
                        r.RecommendedDrawings.Add(farSej);

                    if (i.Choice("align") == AlignTransition)
                        r.Error("SEJ of any type shall not be located on the transition of a curve.", "IRPWM 328(1)(d)");
                    r.Info("Compare the SEJ gaps from the reference post with Annexure 3/9A (conventional PSC) or 3/9C (wider-base PSC).", "IRPWM 331(7)");
                    r.RecommendedLocation = "SEJ at the far-end approach, 15 m from the abutment. LWR continued over the bridge with rail-free fastenings.";
                }
                else if (c == CaseE)
                {
                    if (!i.Toggle("eachPier"))
                        r.Error("With welded rails pier to pier, an SEJ must be provided over each pier.", "IRPWM 331(4)");
                    else
                        r.Ok("SEJ over each pier.", "IRPWM 331(4)");
                    if (!i.Toggle("boxEF"))
                        r.Error(i.Choice("bearing") == BearingSliding
                            ? "With sliding bearings on both sides, box-anchor 4 sleepers in the central portion of each span."
                            : "With roller and rocker bearings, box-anchor 4 sleepers at the fixed end of the girder.", "IRPWM 331(4)");
                    r.Info("These welded panels are not entered in TMS as LWR; gap measurement at their SEJs is not warranted.", "IRPWM 331(8)");
                    r.RecommendedLocation = "SEJ over every pier, with welded rails from pier to pier.";
                }
                else // Case F
                {
                    if (!i.Toggle("single"))
                        r.Error("This arrangement is only for a single-span bridge.", "IRPWM 331(5)");
                    double lim = Catalog.SingleSpanLimit(zone);
                    if (len > lim)
                        r.Error($"Single span {F(len)} m exceeds {F(lim)} m permitted in Zone {zone}.", "IRPWM 331(5)");
                    else
                        r.Ok($"Single span {F(len)} m is within {F(lim)} m (Zone {zone}).", "IRPWM 331(5)");
                    if (Math.Abs(dist - 30) > 0.5)
                        r.Error($"SEJs are to be located 30 m away from both abutments. Entered: {F(dist)} m.", "IRPWM 331(5)");
                    else
                        r.Ok("SEJs 30 m from both abutments.", "IRPWM 331(5)");
                    if (!i.Toggle("sej190"))
                        r.Error("SEJs with a 190 mm gap are required for this arrangement.", "IRPWM 331(5)");
                    if (!i.Toggle("approach"))
                        r.Error("The approach track up to 50 m on both sides must be well anchored (PRC + ERC with minimum toe load).", "IRPWM 331(5)");
                    if (!i.Toggle("boxEF"))
                        r.Error(i.Choice("bearing") == BearingSliding
                            ? "With sliding bearings on both sides, box-anchor 4 sleepers in the central portion."
                            : "With roller and rocker bearings, box-anchor 4 sleepers at the fixed end.", "IRPWM 331(5)");
                    r.Info("Installation temperature of the welded panel = tm. Not entered in TMS as LWR.", "IRPWM 331(5), 331(8)");
                    r.RecommendedDrawings.Add(Catalog.FarSej190);
                    if (i.Choice("align") == AlignTransition)
                        r.Error("SEJ of any type shall not be located on the transition of a curve.", "IRPWM 328(1)(d)");
                    r.RecommendedLocation = "SEJs (190 mm) at 30 m from both abutments.";
                }
                return r;
            }
        };
    }

    // ---- 10. Ballasted deck / BLT bridges ----
    public const string BridgeNoBearing = "Ballasted deck without bearings (slab, box culvert, arch)";
    public const string BridgeWithBearing = "Ballasted deck with bearings";
    public const string BridgeBlt = "Ballastless track (BLT) bridge with bearings";

    private static CheckDefinition BallastedBridge()
    {
        static bool WithBearing(Inputs i) => i.Choice("type") != BridgeNoBearing;
        var fields = new List<InputField>
        {
            InputField.Choice("type", "Type of bridge", new[] { BridgeNoBearing, BridgeWithBearing, BridgeBlt }),
            InputField.Toggle("rsi", "RSI analysis satisfactory and LWR permitted by CBE / CAO(C) (RDSO BS-126 Rev 1)", false, "", WithBearing),
            InputField.Number("iso", "Well-anchored PSC track isolating the bridge from LWR (each side)", 30, "m", "", i => WithBearing(i) && !i.Toggle("rsi")),
        };
        fields.AddRange(SejFields(AlignStraight, 1.0, i => WithBearing(i) && !i.Toggle("rsi")));
        return new CheckDefinition
        {
            Id = "BR_BALLASTED", Group = GroupBridges,
            Title = "Ballasted deck / BLT bridge",
            Subtitle = "Slabs, box culverts, arches, bridges with bearings",
            Reference = "IRPWM 226(3), 329, 330",
            Fields = fields,
            Evaluate = i =>
            {
                var r = new CheckResult();
                var type = i.Choice("type");
                if (type == BridgeNoBearing)
                {
                    r.Ok("LWR/CWR may be continued over a ballasted deck bridge without bearings. No SEJ is required at the bridge.", "IRPWM 329, 226(3)(c)");
                    r.RecommendedLocation = "No SEJ required at this bridge.";
                    return r;
                }
                if (i.Toggle("rsi"))
                {
                    r.Caution("LWR/CWR may be continued as permitted by CBE/CAO(C) on the basis of RSI analysis. Follow any special arrangements stipulated (RDSO BS-114).", "IRPWM 330");
                    r.RecommendedLocation = "SEJ location as per the approved RSI design.";
                    return r;
                }
                r.Info("Without satisfactory RSI analysis and approval, LWR is not continued. Provide SWR on the bridge.", "IRPWM 330, 226(3)(b)");
                double iso = i.Number("iso");
                if (iso < 30)
                    r.Error($"The SWR on the bridge must be isolated from the LWR/CWR by at least 30 m of well-anchored PSC sleeper track on either side. Entered: {F(iso)} m.", "IRPWM 226(3)(b)(i)");
                else
                    r.Ok($"Isolation {F(iso)} m ≥ 30 m on either side.", "IRPWM 226(3)(b)(i)");
                r.RecommendedLocation = "LWR end (SEJ) at not less than 30 m from the bridge on each side.";
                EvaluateSej(i, r);
                return r;
            }
        };
    }

    // ---- 11. Change of rail section ----
    public const string SectionCombination = "Combination SEJ to RDSO Drg. T-6782";
    public const string SectionPanels = "Two 3-rail panels (39 m each) with combination fish-plated joint";
    public const string SectionOrdinarySej = "Ordinary SEJ of one rail section at the junction";
    public const string SectionWithinLwr = "Two rail sections within the same LWR";
    public const string SleeperConcrete = "Concrete (PSC) sleepers";
    public const string SleeperOther = "Other sleepers";

    private static CheckDefinition ChangeOfRailSection()
    {
        var fields = new List<InputField>
        {
            InputField.Choice("opt", "Arrangement proposed", new[] { SectionCombination, SectionPanels, SectionOrdinarySej, SectionWithinLwr }),
            InputField.Choice("sleepers", "Sleepers", new[] { SleeperConcrete, SleeperOther }),
            InputField.Number("p52", "Length of 52 kg panel", 39, "m", "", i => i.Choice("opt") == SectionPanels),
            InputField.Number("p60", "Length of 60 kg panel", 39, "m", "", i => i.Choice("opt") == SectionPanels),
            InputField.Choice("align", "Alignment at SEJ location", Alignments),
            InputField.Number("deg", "Degree of curve at SEJ location", 1.0, "°", "", i => i.Choice("align") == AlignCircular),
        };
        return new CheckDefinition
        {
            Id = "TS_SECTION", Group = GroupTrack,
            Title = "Change of rail section (52 kg ↔ 60 kg)",
            Subtitle = "Different rail sections on either side of SEJ",
            Reference = "IRPWM 225(2), 326(5)(c),(d)",
            Fields = fields,
            Evaluate = i =>
            {
                var r = new CheckResult();
                var opt = i.Choice("opt");
                if (opt == SectionWithinLwr)
                    r.Error("Two different rail sections are not permitted within an LWR/CWR.", "IRPWM 326(5)(c)");
                else if (opt == SectionOrdinarySej)
                    r.Error("An ordinary SEJ cannot join two rail sections. Use combination SEJ T-6782, or two 39 m three-rail panels with a combination fish-plated joint.", "IRPWM 225(2), 326(5)(d)");
                else if (opt == SectionCombination)
                {
                    r.Ok("Combination SEJ T-6782 (52 kg / 60 kg, 80 mm gap) is correct.", "IRPWM 326(5)(d), Annex 2/4(B)");
                    r.RecommendedDrawings.Add(Catalog.FindByCode("T-6782")!.Label);
                    if (i.Choice("align") == AlignCircular && i.Number("deg") > 0.5)
                        r.Caution("IRPWM does not state a curve limit for the combination SEJ. Check RDSO drawing T-6782 before laying on a curve.", "IRPWM 328(1)");
                }
                else
                {
                    double p52 = i.Number("p52"), p60 = i.Number("p60");
                    if (p52 < 39 || p60 < 39)
                        r.Error($"Each panel must be a 3-rail panel of 39 m (entered: 52 kg {F(p52)} m, 60 kg {F(p60)} m).", "IRPWM 326(5)(d)");
                    else
                        r.Ok("Two 39 m panels, one of each rail section, with a combination fish-plated joint between them.", "IRPWM 326(5)(d)");
                }
                if (i.Choice("sleepers") != SleeperConcrete && opt is SectionCombination or SectionPanels)
                    r.Caution("IRPWM 326(5)(d) covers LWRs on concrete sleepers. LWR/CWR is to be laid on approved PSC sleepers (326(5)(a)).", "IRPWM 326(5)(a),(d)");
                if (i.Choice("align") == AlignTransition)
                    r.Error("SEJ of any type shall not be located on the transition of a curve.", "IRPWM 328(1)(d)");
                r.RecommendedLocation = "Combination SEJ T-6782 at the junction, or two 39 m panels (52 kg + 60 kg) with a combination fish-plated joint between them.";
                return r;
            }
        };
    }

    // ---- 12. Track beyond SEJ ----
    private static CheckDefinition TrackBeyondSej()
    {
        var fields = new List<InputField>
        {
            InputField.Number("len", "Length beyond SEJ with same track structure as LWR", 39),
        };
        return new CheckDefinition
        {
            Id = "TS_BEYOND", Group = GroupTrack,
            Title = "Fish-plated track / SWR beyond SEJ",
            Subtitle = "Continuity of track structure",
            Reference = "IRPWM 326(6)",
            Fields = fields,
            Evaluate = i =>
            {
                var r = new CheckResult();
                double len = i.Number("len");
                if (len < 39)
                    r.Error($"The same track structure as the LWR must continue for at least 39 m (three rail lengths) beyond the SEJ. Entered: {F(len)} m.", "IRPWM 326(6)");
                else
                    r.Ok($"{F(len)} m of the same track structure beyond the SEJ (≥ 39 m).", "IRPWM 326(6)");
                r.RecommendedLocation = "Continue the LWR track structure for at least 39 m beyond the SEJ.";
                return r;
            }
        };
    }

    // ---- 13. Ballastless track junction ----
    private static CheckDefinition BltJunction()
    {
        var fields = new List<InputField>
        {
            InputField.Number("dist", "Distance of SEJ from BLT / ballasted track junction", 30),
            InputField.Toggle("inBallasted", "SEJ is in the ballasted track (not on BLT)"),
            InputField.Toggle("wall", "Ballast retaining wall for 100 m from the junction"),
            InputField.Toggle("creep", "Creep posts every 100 m over 500 m of ballasted approach"),
        };
        fields.AddRange(SejFields());
        return new CheckDefinition
        {
            Id = "TS_BLT", Group = GroupTrack,
            Title = "Junction with ballastless track (BLT)",
            Subtitle = "LWR on BLT isolated from ballasted track",
            Reference = "IRPWM 326(5)(g)",
            Fields = fields,
            Evaluate = i =>
            {
                var r = new CheckResult();
                if (i.Choice("rail") != Catalog.Rail60)
                    r.Error("The minimum rail section for LWR/CWR on BLT is 60 kg/m.", "IRPWM 326(5)(g)(ii)");
                double dist = i.Number("dist");
                if (dist < 30)
                    r.Error($"The SEJ must be at least 30 m from the BLT/ballasted junction. Entered: {F(dist)} m.", "IRPWM 326(5)(g)(v)");
                else
                    r.Ok($"SEJ {F(dist)} m from the junction (≥ 30 m).", "IRPWM 326(5)(g)(v)");
                if (!i.Toggle("inBallasted"))
                    r.Error("The SEJ shall be provided in the ballasted track.", "IRPWM 326(5)(g)(v)");
                if (!i.Toggle("wall"))
                    r.Error("A ballast retaining wall is to be provided for 100 m from the junction.", "IRPWM 326(5)(g)(iv)");
                if (!i.Toggle("creep"))
                    r.Caution("Monitor creep over 500 m of the ballasted approach with creep posts every 100 m. If creep exceeds 10 mm in 500 m, destress 1000 m.", "IRPWM 326(5)(g)(vii)");
                r.Info("Compare the SEJ gap towards BLT with Annexure-3/9. If the limit is reached, check the BLT fastening clamping force.", "IRPWM 326(5)(g)(vi)");
                r.RecommendedLocation = "SEJ in the ballasted track, at not less than 30 m from the BLT junction.";
                EvaluateSej(i, r);
                return r;
            }
        };
    }

    // ---- 14. Gradient ----
    private static CheckDefinition Gradient()
    {
        var fields = new List<InputField>
        {
            InputField.Number("grade", "Gradient: 1 in", 200, ""),
            InputField.Choice("rail", "Rail section", new[] { Catalog.Rail60, Catalog.Rail52 }),
            InputField.Choice("density", "Sleeper density (PSC, per km)", Catalog.Densities),
            InputField.Choice("zone", "Rail temperature zone", Catalog.Zones, "III"),
            InputField.Number("curveDeg", "Degree of curve on this gradient (0 if straight)", 0, "°"),
            InputField.Toggle("pce", "PCE permission obtained (circular / case-to-case) and Annexure 3/19 complied", false,
                "", i => i.Number("grade") > 0 && i.Number("grade") < 100),
        };
        return new CheckDefinition
        {
            Id = "TS_GRADIENT", Group = GroupTrack,
            Title = "Gradient at SEJ / LWR location",
            Subtitle = "Steepest grade permitted for LWR",
            Reference = "IRPWM 326(2)(a), 326(3), 328(1)",
            Fields = fields,
            Evaluate = i =>
            {
                var r = new CheckResult();
                double g = i.Number("grade");
                var zone = i.Choice("zone");
                if (g <= 0) { r.Error("Enter the gradient as 1 in N (N > 0)."); return r; }

                if (g >= 100)
                    r.Ok($"1 in {F(g)} is not steeper than 1 in 100 – permitted for LWR/CWR.", "IRPWM 326(3)");
                else
                {
                    double steepest = zone == "IV" ? 80 : 65;
                    bool eligible = i.Choice("rail") == Catalog.Rail60 && i.Choice("density") == "1660";
                    if (!eligible)
                        r.Error("Gradients steeper than 1 in 100 are permitted only for 60 kg rail on PSC sleepers at 1660/km.", "IRPWM 326(3)");
                    else if (g < steepest)
                        r.Error($"1 in {F(g)} is steeper than the limit of 1 in {F(steepest)} for Zone {zone}.", "IRPWM 326(3)");
                    else if (!i.Toggle("pce"))
                        r.Error($"1 in {F(g)} needs PCE permission (circular or case-to-case), subject to Annexure 3/19.", "IRPWM 326(3)");
                    else
                        r.Caution($"1 in {F(g)} is permitted with PCE approval. The reduction in td under Para 335 is not applicable.", "IRPWM 326(3)");

                    double cDeg = i.Number("curveDeg");
                    double maxWithGrade = zone == "IV" ? 4 : 5;
                    if (cDeg > maxWithGrade && g < 100)
                        r.Error($"A curve of {F(cDeg)}° with a gradient steeper than 1 in 100 exceeds the limit ({F(maxWithGrade)}° with the steepest gradient in Zone {zone}).", "IRPWM 326(2)(a)");
                }
                r.Info("Gradient is an obligatory point to be considered while fixing the exact location of the SEJ.", "IRPWM 328(1)");
                r.RecommendedLocation = "Fix the SEJ where LWR is permitted for the gradient (and associated curve).";
                return r;
            }
        };
    }

    // ---- 15. Insulated joints / track circuits ----
    public const string IjDac = "Digital Axle Counter (DAC)";
    public const string IjG3L = "Glued joint G3(L)";
    public const string IjFishPlated = "Fish-plated insulated joint within LWR";
    public const string IjOtherGlued = "Glued joint other than G3(L)";

    private static CheckDefinition InsulatedJoints()
    {
        var fields = new List<InputField>
        {
            InputField.Choice("type", "Track-circuit insulation near SEJ", new[] { IjDac, IjG3L, IjFishPlated, IjOtherGlued }),
            InputField.Toggle("newWork", "This is a new work", false),
        };
        return new CheckDefinition
        {
            Id = "TS_IJ", Group = GroupTrack,
            Title = "Insulated joints / track circuits",
            Subtitle = "DAC or G3(L) glued joints in LWR",
            Reference = "IRPWM 327, 328(1)",
            Fields = fields,
            Evaluate = i =>
            {
                var r = new CheckResult();
                var t = i.Choice("type");
                bool nw = i.Toggle("newWork");
                if (t == IjDac)
                    r.Ok("Digital Axle Counters are correct (mandatory in all new works).", "IRPWM 327");
                else if (t == IjG3L)
                {
                    if (nw) r.Error("Digital Axle Counters shall be provided in all new works.", "IRPWM 327");
                    else r.Ok("G3(L) glued joints are permitted in LWR/CWR in other works. Replace them progressively with DAC.", "IRPWM 327");
                }
                else if (t == IjFishPlated)
                    r.Error("Fish-plated insulated joints are not permitted within LWR/CWR. Use DAC or G3(L) glued joints.", "IRPWM 327");
                else
                    r.Error("Only G3(L) type glued joints are permitted in LWR/CWR (DAC in new works).", "IRPWM 327");
                r.Info("Consider the location of insulated joints while fixing the exact location of the SEJ.", "IRPWM 328(1)");
                r.RecommendedLocation = "Fix the SEJ taking the insulated joint / axle-counter locations into account.";
                return r;
            }
        };
    }

    // ---- 16. Drawing finder ----
    private static CheckDefinition DrawingFinder()
    {
        return new CheckDefinition
        {
            Id = "FINDER", Group = GroupFinder,
            Title = "SEJ drawing finder",
            Subtitle = "Right SEJ drawing for rail, zone and curvature",
            Reference = "IRPWM 225, 328, Annex 2/4(B)",
            Fields = SejFields(),
            Evaluate = i =>
            {
                var r = new CheckResult();
                EvaluateSej(i, r);
                if (r.Findings.Count == 0) r.Ok("Location is permitted.");
                r.RecommendedLocation = "See the permitted drawings below.";
                return r;
            }
        };
    }
}
