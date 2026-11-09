namespace SejGuide.Core;

/// <summary>Result colour shown to the field engineer.</summary>
public enum Severity
{
    Info = 0,     // blue  – guidance only
    Ok = 1,       // green – compliant
    Caution = 2,  // amber – permitted only with approval / precaution
    Error = 3     // red   – wrong selection, not permitted
}

public sealed class Finding
{
    public Finding(Severity severity, string text, string reference = "")
    {
        Severity = severity;
        Text = text;
        Reference = reference;
    }

    public Severity Severity { get; }
    public string Text { get; }
    public string Reference { get; }
}

public sealed class CheckResult
{
    public List<Finding> Findings { get; } = new();

    /// <summary>Where the SEJ should be placed (plain words).</summary>
    public string RecommendedLocation { get; set; } = "";

    /// <summary>SEJ drawings that are permitted for the entered conditions.</summary>
    public List<string> RecommendedDrawings { get; } = new();

    public Severity Overall
    {
        get
        {
            var s = Severity.Info;
            foreach (var f in Findings)
                if (f.Severity > s) s = f.Severity;
            if (s == Severity.Info && Findings.Count > 0) s = Severity.Ok;
            return s;
        }
    }

    public string Headline => Overall switch
    {
        Severity.Error => "WRONG SELECTION – NOT PERMITTED",
        Severity.Caution => "PERMITTED WITH APPROVAL / PRECAUTIONS",
        _ => "CORRECT – AS PER IRPWM / CT-48"
    };

    public void Ok(string text, string reference = "") => Findings.Add(new Finding(Severity.Ok, text, reference));
    public void Info(string text, string reference = "") => Findings.Add(new Finding(Severity.Info, text, reference));
    public void Caution(string text, string reference = "") => Findings.Add(new Finding(Severity.Caution, text, reference));
    public void Error(string text, string reference = "") => Findings.Add(new Finding(Severity.Error, text, reference));

    public IEnumerable<Finding> Errors => Findings.Where(f => f.Severity == Severity.Error);
}

public enum FieldKind { Choice, Number, Toggle }

public sealed class InputField
{
    public string Key { get; init; } = "";
    public string Label { get; init; } = "";
    public FieldKind Kind { get; init; }
    public string[] Options { get; init; } = Array.Empty<string>();
    public string DefaultChoice { get; init; } = "";
    public double DefaultNumber { get; init; }
    public bool DefaultToggle { get; init; }
    public string Unit { get; init; } = "";
    public string Help { get; init; } = "";
    /// <summary>Field is shown only when this returns true (null = always shown).</summary>
    public Func<Inputs, bool>? VisibleWhen { get; init; }

    public static InputField Choice(string key, string label, string[] options, string? def = null, string help = "", Func<Inputs, bool>? visible = null)
        => new() { Key = key, Label = label, Kind = FieldKind.Choice, Options = options, DefaultChoice = def ?? options[0], Help = help, VisibleWhen = visible };

    public static InputField Number(string key, string label, double def, string unit = "m", string help = "", Func<Inputs, bool>? visible = null)
        => new() { Key = key, Label = label, Kind = FieldKind.Number, DefaultNumber = def, Unit = unit, Help = help, VisibleWhen = visible };

    public static InputField Toggle(string key, string label, bool def = true, string help = "", Func<Inputs, bool>? visible = null)
        => new() { Key = key, Label = label, Kind = FieldKind.Toggle, DefaultToggle = def, Help = help, VisibleWhen = visible };
}

/// <summary>Values entered by the user, keyed by field key.</summary>
public sealed class Inputs
{
    private readonly Dictionary<string, object> _values = new();

    public Inputs() { }

    public Inputs(IEnumerable<InputField> fields)
    {
        foreach (var f in fields)
        {
            switch (f.Kind)
            {
                case FieldKind.Choice: _values[f.Key] = f.DefaultChoice; break;
                case FieldKind.Number: _values[f.Key] = f.DefaultNumber; break;
                case FieldKind.Toggle: _values[f.Key] = f.DefaultToggle; break;
            }
        }
    }

    public Inputs Set(string key, object value) { _values[key] = value; return this; }

    public string Choice(string key) => _values.TryGetValue(key, out var v) && v is string s ? s : "";
    public double Number(string key) => _values.TryGetValue(key, out var v) ? Convert.ToDouble(v) : 0;
    public bool Toggle(string key) => _values.TryGetValue(key, out var v) && v is bool b && b;
    public bool Has(string key) => _values.ContainsKey(key);
}

public sealed class CheckDefinition
{
    public string Id { get; init; } = "";
    public string Group { get; init; } = "";
    public string Title { get; init; } = "";
    public string Subtitle { get; init; } = "";
    public string Reference { get; init; } = "";
    public List<InputField> Fields { get; init; } = new();
    public Func<Inputs, CheckResult> Evaluate { get; init; } = _ => new CheckResult();

    public Inputs DefaultInputs() => new(Fields);
}
