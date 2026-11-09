using SejGuide.Core;

namespace SejGuide.App;

public class HomePage : ContentPage
{
    public HomePage()
    {
        Title = "SEJ Location Guide";
        BackgroundColor = Theme.Page;

        var stack = new VerticalStackLayout { Spacing = 10, Padding = new Thickness(16, 12, 16, 24) };
        stack.Add(Ui.Banner());
        stack.Add(new Label
        {
            Text = "Tap the location where the SEJ is to be provided. Enter site details – the app tells you the correct SEJ location and drawing, and raises a red alert for a wrong selection.",
            FontSize = 13,
            TextColor = Theme.Muted
        });

        foreach (var group in Rules.Groups)
        {
            stack.Add(Ui.GroupHeading(group));
            foreach (var def in Rules.All.Where(c => c.Group == group))
            {
                var d = def;
                stack.Add(Ui.MenuCard(d.Title, d.Subtitle, d.Reference, () => Navigation.PushAsync(new CheckPage(d))));
            }
        }

        stack.Add(Ui.GroupHeading("Reference & About"));
        stack.Add(Ui.MenuCard("Reference tables", "SEJ drawings, breathing lengths, bridge limits, key distances",
            "IRPWM Annex 2/4(B), 3/16, Table-1, 331(3), 331(5)", () => Navigation.PushAsync(new ReferencePage())));
        stack.Add(Ui.MenuCard("About", "Prepared by Dharam Pal Jangid, SSE (P. Way) Bundi, WCRly", "", () => Navigation.PushAsync(new AboutPage())));

        Content = new ScrollView { Content = stack };
    }
}
