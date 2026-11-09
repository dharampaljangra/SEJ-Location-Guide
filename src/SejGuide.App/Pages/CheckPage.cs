using System.Globalization;
using System.Text;
using Microsoft.Maui.Controls.Shapes;
using SejGuide.Core;

namespace SejGuide.App;

/// <summary>Generic page: builds the form for any check, evaluates live and raises a red alert on wrong selection.</summary>
public class CheckPage : ContentPage
{
    private readonly CheckDefinition _def;
    private readonly Inputs _inputs;
    private readonly Dictionary<string, View> _rows = new();
    private readonly VerticalStackLayout _resultHost = new() { Spacing = 6 };
    private readonly ScrollView _scroll;
    private CheckResult? _last;

    public CheckPage(CheckDefinition def)
    {
        _def = def;
        _inputs = def.DefaultInputs();
        Title = def.Title;
        BackgroundColor = Theme.Page;

        var form = new VerticalStackLayout { Spacing = 10, Padding = new Thickness(16, 12, 16, 32) };
        form.Add(Ui.Header(def.Title, def.Reference));
        if (!string.IsNullOrEmpty(def.Subtitle))
            form.Add(new Label { Text = def.Subtitle, FontSize = 13, TextColor = Theme.Muted });

        foreach (var f in def.Fields)
        {
            var row = BuildRow(f);
            _rows[f.Key] = row;
            form.Add(row);
        }

        var check = new Button
        {
            Text = "CHECK SELECTION",
            BackgroundColor = Theme.Navy,
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            FontSize = 16,
            CornerRadius = 8,
            HeightRequest = 52,
            Margin = new Thickness(0, 6, 0, 0)
        };
        check.Clicked += async (_, _) => await OnCheck();
        form.Add(check);

        form.Add(_resultHost);

        var copy = new Button
        {
            Text = "Copy result (for inspection note)",
            BackgroundColor = Colors.White,
            TextColor = Theme.Navy,
            BorderColor = Theme.Navy,
            BorderWidth = 1,
            CornerRadius = 8
        };
        copy.Clicked += async (_, _) =>
        {
            if (_last == null) return;
            await Clipboard.Default.SetTextAsync(ResultText());
            await DisplayAlert("Copied", "The result has been copied to the clipboard.", "OK");
        };
        form.Add(copy);

        _scroll = new ScrollView { Content = form };
        Content = _scroll;
        Refresh();
    }

    private View BuildRow(InputField f)
    {
        var label = new Label
        {
            Text = f.Label + (string.IsNullOrEmpty(f.Unit) ? "" : $" ({f.Unit})"),
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Theme.Text
        };

        switch (f.Kind)
        {
            case FieldKind.Choice:
            {
                var picker = new Picker
                {
                    Title = f.Label,
                    ItemsSource = f.Options.ToList(),
                    SelectedItem = f.DefaultChoice,
                    TextColor = Theme.Text,
                    TitleColor = Theme.Muted,
                    BackgroundColor = Colors.White
                };
                picker.SelectedIndexChanged += (_, _) =>
                {
                    if (picker.SelectedItem is string v)
                    {
                        _inputs.Set(f.Key, v);
                        Refresh();
                    }
                };
                var s = new VerticalStackLayout { Spacing = 4 };
                s.Add(label);
                s.Add(picker);
                return Ui.Card(s, f.Help);
            }
            case FieldKind.Number:
            {
                var entry = new Entry
                {
                    Text = f.DefaultNumber.ToString(CultureInfo.InvariantCulture),
                    Keyboard = Keyboard.Numeric,
                    TextColor = Theme.Text,
                    BackgroundColor = Colors.White
                };
                entry.TextChanged += (_, e) =>
                {
                    if (TryParse(e.NewTextValue, out var v))
                    {
                        _inputs.Set(f.Key, v);
                        Refresh();
                    }
                };
                var s = new VerticalStackLayout { Spacing = 4 };
                s.Add(label);
                s.Add(entry);
                return Ui.Card(s, f.Help);
            }
            default:
            {
                label.FontAttributes = FontAttributes.None;
                label.VerticalOptions = LayoutOptions.Center;
                var sw = new Switch
                {
                    IsToggled = f.DefaultToggle,
                    OnColor = Theme.Green,
                    VerticalOptions = LayoutOptions.Center
                };
                sw.Toggled += (_, e) =>
                {
                    _inputs.Set(f.Key, e.Value);
                    Refresh();
                };
                var g = new Grid
                {
                    ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                    ColumnSpacing = 8
                };
                g.Add(label, 0, 0);
                g.Add(sw, 1, 0);
                return Ui.Card(g, f.Help);
            }
        }
    }

    private static bool TryParse(string? text, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        return double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private bool IsVisible(InputField f) => f.VisibleWhen?.Invoke(_inputs) ?? true;

    private void Refresh()
    {
        foreach (var f in _def.Fields)
            if (_rows.TryGetValue(f.Key, out var row))
                row.IsVisible = IsVisible(f);

        _last = _def.Evaluate(_inputs);
        RenderResult(_last);
    }

    private void RenderResult(CheckResult r)
    {
        _resultHost.Children.Clear();
        var sev = r.Overall;

        var s = new VerticalStackLayout { Spacing = 8 };
        s.Add(new Label
        {
            Text = $"{Theme.Icon(sev)}  {r.Headline}",
            FontSize = 17,
            FontAttributes = FontAttributes.Bold,
            TextColor = Theme.Fore(sev)
        });

        if (!string.IsNullOrEmpty(r.RecommendedLocation))
        {
            s.Add(new Label { Text = "Correct SEJ location", FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = Theme.Navy });
            s.Add(new Label { Text = r.RecommendedLocation, FontSize = 14, TextColor = Theme.Text });
        }

        if (r.RecommendedDrawings.Count > 0)
        {
            s.Add(new Label { Text = "Permitted SEJ / drawing", FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = Theme.Navy });
            foreach (var d in r.RecommendedDrawings)
                s.Add(new Label { Text = "• " + d, FontSize = 14, TextColor = Theme.Text });
        }

        s.Add(new BoxView { HeightRequest = 1, Color = Theme.Line, Margin = new Thickness(0, 2) });

        foreach (var f in r.Findings.OrderByDescending(x => x.Severity))
        {
            var line = new VerticalStackLayout { Spacing = 1 };
            line.Add(new Label
            {
                Text = $"{Theme.Icon(f.Severity)} {f.Text}",
                FontSize = 14,
                TextColor = f.Severity == Severity.Info ? Theme.Text : Theme.Fore(f.Severity),
                FontAttributes = f.Severity == Severity.Error ? FontAttributes.Bold : FontAttributes.None
            });
            if (!string.IsNullOrEmpty(f.Reference))
                line.Add(new Label { Text = f.Reference, FontSize = 12, FontAttributes = FontAttributes.Italic, TextColor = Theme.Blue, Margin = new Thickness(22, 0, 0, 0) });
            s.Add(line);
        }

        _resultHost.Add(new Border
        {
            Content = s,
            Padding = new Thickness(14, 12),
            BackgroundColor = Theme.Back(sev),
            Stroke = Theme.Fore(sev),
            StrokeThickness = sev == Severity.Error ? 3 : 1.5,
            StrokeShape = new RoundRectangle { CornerRadius = 10 }
        });
    }

    private async Task OnCheck()
    {
        Refresh();
        if (_last == null) return;

        await _scroll.ScrollToAsync(_resultHost, ScrollToPosition.Start, true);

        switch (_last.Overall)
        {
            case Severity.Error:
                try { HapticFeedback.Default.Perform(HapticFeedbackType.LongPress); } catch { /* not supported */ }
                await DisplayAlert("⛔ RED ALERT – WRONG SELECTION",
                    string.Join("\n\n", _last.Errors.Select(e => "• " + e.Text + Ref(e))), "OK");
                break;
            case Severity.Caution:
                await DisplayAlert("⚠️ Approval / precaution required",
                    string.Join("\n\n", _last.Findings.Where(x => x.Severity == Severity.Caution).Select(e => "• " + e.Text + Ref(e))), "OK");
                break;
            default:
                await DisplayAlert("✅ Correct selection", _last.RecommendedLocation, "OK");
                break;
        }
    }

    private static string Ref(Finding f) => string.IsNullOrEmpty(f.Reference) ? "" : $"  [{f.Reference}]";

    private string ResultText()
    {
        var r = _last!;
        var sb = new StringBuilder();
        sb.AppendLine($"SEJ LOCATION CHECK – {_def.Title}");
        sb.AppendLine($"Date: {DateTime.Now:dd-MM-yyyy HH:mm}");
        sb.AppendLine();
        sb.AppendLine("Site details:");
        foreach (var f in _def.Fields.Where(IsVisible))
        {
            string v = f.Kind switch
            {
                FieldKind.Choice => _inputs.Choice(f.Key),
                FieldKind.Number => _inputs.Number(f.Key).ToString("0.##", CultureInfo.InvariantCulture) + (string.IsNullOrEmpty(f.Unit) ? "" : " " + f.Unit),
                _ => _inputs.Toggle(f.Key) ? "Yes" : "No"
            };
            sb.AppendLine($"  {f.Label}: {v}");
        }
        sb.AppendLine();
        sb.AppendLine($"Result: {r.Headline}");
        if (!string.IsNullOrEmpty(r.RecommendedLocation)) sb.AppendLine($"Correct SEJ location: {r.RecommendedLocation}");
        if (r.RecommendedDrawings.Count > 0) sb.AppendLine("Permitted SEJ / drawing: " + string.Join("; ", r.RecommendedDrawings));
        foreach (var f in r.Findings.OrderByDescending(x => x.Severity))
            sb.AppendLine($"  [{f.Severity}] {f.Text}{Ref(f)}");
        sb.AppendLine();
        sb.AppendLine("SEJ Location Guide – IRPWM 2024 (ACS-16) & RDSO CT-48 (Nov 2025)");
        return sb.ToString();
    }
}
