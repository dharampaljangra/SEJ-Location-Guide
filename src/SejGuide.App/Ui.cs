using Microsoft.Maui.Controls.Shapes;
using SejGuide.Core;

namespace SejGuide.App;

public static class Theme
{
    public static readonly Color Navy = Color.FromArgb("#0B3D6E");
    public static readonly Color Page = Color.FromArgb("#F3F6FA");
    public static readonly Color Text = Color.FromArgb("#1A1A1A");
    public static readonly Color Muted = Color.FromArgb("#5A6472");
    public static readonly Color Line = Color.FromArgb("#C9D3DE");

    public static readonly Color Red = Color.FromArgb("#B3261E");
    public static readonly Color RedBg = Color.FromArgb("#FDECEA");
    public static readonly Color Amber = Color.FromArgb("#9A6100");
    public static readonly Color AmberBg = Color.FromArgb("#FFF4DC");
    public static readonly Color Green = Color.FromArgb("#1E7B34");
    public static readonly Color GreenBg = Color.FromArgb("#E7F5EB");
    public static readonly Color Blue = Color.FromArgb("#1F5FA8");

    public static Color Fore(Severity s) => s switch
    {
        Severity.Error => Red,
        Severity.Caution => Amber,
        Severity.Ok => Green,
        _ => Blue
    };

    public static Color Back(Severity s) => s switch
    {
        Severity.Error => RedBg,
        Severity.Caution => AmberBg,
        _ => GreenBg
    };

    public static string Icon(Severity s) => s switch
    {
        Severity.Error => "⛔",
        Severity.Caution => "⚠️",
        Severity.Ok => "✅",
        _ => "ℹ️"
    };
}

public static class Ui
{
    public static Border Card(View content, string help = "", Color? background = null, Color? stroke = null)
    {
        View inner = content;
        if (!string.IsNullOrWhiteSpace(help))
        {
            var s = new VerticalStackLayout { Spacing = 4 };
            s.Add(content);
            s.Add(new Label { Text = help, FontSize = 12, TextColor = Theme.Muted });
            inner = s;
        }
        return new Border
        {
            Content = inner,
            Padding = new Thickness(12, 10),
            BackgroundColor = background ?? Colors.White,
            Stroke = stroke ?? Theme.Line,
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 10 }
        };
    }

    public static Border Banner()
    {
        var s = new VerticalStackLayout { Spacing = 4 };
        s.Add(new Label { Text = "SEJ Location Guide", FontSize = 24, FontAttributes = FontAttributes.Bold, TextColor = Colors.White });
        s.Add(new Label { Text = "Switch Expansion Joints in LWR/CWR – right location, right drawing", FontSize = 14, TextColor = Colors.White });
        s.Add(new Label { Text = "IRPWM 2024 (corrected up to ACS-16) • RDSO Report CT-48 (Revised, Nov 2025)", FontSize = 12, TextColor = Color.FromArgb("#C9D9EA") });
        var legend = new HorizontalStackLayout { Spacing = 6, Margin = new Thickness(0, 6, 0, 0) };
        legend.Add(Chip("✅ Correct", Theme.GreenBg, Theme.Green));
        legend.Add(Chip("⚠️ Approval", Theme.AmberBg, Theme.Amber));
        legend.Add(Chip("⛔ Wrong", Theme.RedBg, Theme.Red));
        s.Add(legend);
        return new Border
        {
            Content = s,
            Padding = new Thickness(16, 14),
            BackgroundColor = Theme.Navy,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 12 }
        };
    }

    public static Border Chip(string text, Color bg, Color fg) => new()
    {
        Content = new Label { Text = text, FontSize = 12, TextColor = fg, FontAttributes = FontAttributes.Bold },
        Padding = new Thickness(8, 3),
        BackgroundColor = bg,
        StrokeThickness = 0,
        StrokeShape = new RoundRectangle { CornerRadius = 10 }
    };

    public static Label GroupHeading(string text) => new()
    {
        Text = text,
        FontSize = 17,
        FontAttributes = FontAttributes.Bold,
        TextColor = Theme.Navy,
        Margin = new Thickness(2, 10, 0, 0)
    };

    public static Border MenuCard(string title, string subtitle, string reference, Func<Task> onTap)
    {
        var s = new VerticalStackLayout { Spacing = 2 };
        s.Add(new Label { Text = title, FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Theme.Text });
        if (!string.IsNullOrEmpty(subtitle))
            s.Add(new Label { Text = subtitle, FontSize = 13, TextColor = Theme.Muted });
        if (!string.IsNullOrEmpty(reference))
            s.Add(new Label { Text = reference, FontSize = 12, TextColor = Theme.Blue });

        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            ColumnSpacing = 8
        };
        grid.Add(s, 0, 0);
        grid.Add(new Label { Text = "›", FontSize = 28, TextColor = Theme.Navy, VerticalOptions = LayoutOptions.Center }, 1, 0);

        var card = Card(grid);
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await onTap();
        card.GestureRecognizers.Add(tap);
        return card;
    }

    public static View Header(string title, string reference)
    {
        var s = new VerticalStackLayout { Spacing = 2 };
        s.Add(new Label { Text = title, FontSize = 20, FontAttributes = FontAttributes.Bold, TextColor = Theme.Navy });
        if (!string.IsNullOrEmpty(reference))
            s.Add(new Label { Text = reference, FontSize = 13, TextColor = Theme.Blue });
        return s;
    }

    /// <summary>Simple bordered table built from labels.</summary>
    public static View Table(string title, string[] headers, IEnumerable<string[]> rows)
    {
        var grid = new Grid { ColumnSpacing = 1, RowSpacing = 1, BackgroundColor = Theme.Line };
        foreach (var _ in headers)
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

        int r = 0;
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        for (int c = 0; c < headers.Length; c++)
            grid.Add(Cell(headers[c], true, false), c, r);

        foreach (var row in rows)
        {
            r++;
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            for (int c = 0; c < headers.Length; c++)
                grid.Add(Cell(c < row.Length ? row[c] : "", false, r % 2 == 0), c, r);
        }

        var s = new VerticalStackLayout { Spacing = 6 };
        s.Add(new Label { Text = title, FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Theme.Navy });
        s.Add(grid);
        return s;
    }

    private static Label Cell(string text, bool header, bool shaded) => new()
    {
        Text = text,
        FontSize = header ? 12 : 12.5,
        FontAttributes = header ? FontAttributes.Bold : FontAttributes.None,
        TextColor = header ? Colors.White : Theme.Text,
        BackgroundColor = header ? Theme.Navy : (shaded ? Color.FromArgb("#EEF3F8") : Colors.White),
        Padding = new Thickness(6, 5),
        LineBreakMode = LineBreakMode.WordWrap,
        VerticalTextAlignment = TextAlignment.Center
    };
}
