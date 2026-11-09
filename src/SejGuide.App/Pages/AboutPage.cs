using SejGuide.Core;

namespace SejGuide.App;

public class AboutPage : ContentPage
{
    public AboutPage()
    {
        Title = "About";
        BackgroundColor = Theme.Page;

        var s = new VerticalStackLayout { Spacing = 12, Padding = new Thickness(16, 12, 16, 32) };
        s.Add(Ui.Banner());

        var info = new VerticalStackLayout { Spacing = 6 };
        info.Add(new Label { Text = $"Version {AppInfo.Current.VersionString}", FontSize = 14, TextColor = Theme.Muted });
        info.Add(new Label
        {
            Text = "Guides P. Way field engineers to select the right location and the right Switch Expansion Joint (SEJ) for LWR/CWR, with a red alert for any wrong selection.",
            FontSize = 14,
            TextColor = Theme.Text
        });
        s.Add(Ui.Card(info));

        var author = new VerticalStackLayout { Spacing = 4 };
        author.Add(new Label { Text = "Prepared by", FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = Theme.Navy });
        author.Add(new Label { Text = "Dharam Pal Jangid", FontSize = 17, FontAttributes = FontAttributes.Bold, TextColor = Theme.Text });
        author.Add(new Label { Text = "Senior Section Engineer (P. Way), Bundi\nWest Central Railway", FontSize = 14, TextColor = Theme.Text });
        author.Add(new Label { Text = "For any queries and suggestions: Mob. 7976273600", FontSize = 14, TextColor = Theme.Text });
        s.Add(Ui.Card(author));

        var src = new VerticalStackLayout { Spacing = 4 };
        src.Add(new Label { Text = "Sources", FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = Theme.Navy });
        src.Add(new Label { Text = "• Indian Railways Permanent Way Manual 2024 (First Reprint), corrected up to ACS-16 – Paras 225, 226, 326–331, Annexures 2/4(B), 3/16, Table-1", FontSize = 13, TextColor = Theme.Text });
        src.Add(new Label { Text = "• RDSO Report No. CT-48 (Revised) Nov 2025 – Continuation of LWR through Turnouts (RB letter dated 18.11.2025)", FontSize = 13, TextColor = Theme.Text });
        s.Add(Ui.Card(src));

        s.Add(Ui.Card(new Label
        {
            Text = "This app is a guidance aid. Always verify against the latest IRPWM correction slips, RDSO drawings with their latest alterations, and approvals of competent authority before execution.",
            FontSize = 12,
            TextColor = Theme.Muted
        }));

        s.Add(new Label
        {
            Text = $"© {DateTime.Now.Year} Dharam Pal Jangid. All rights reserved.",
            FontSize = 12,
            TextColor = Theme.Muted,
            HorizontalOptions = LayoutOptions.Center
        });

        Content = new ScrollView { Content = s };
    }
}
