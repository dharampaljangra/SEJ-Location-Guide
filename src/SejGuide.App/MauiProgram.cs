namespace SejGuide.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();
        return builder.Build();
    }
}

public class App : Application
{
    public App()
    {
        // Explicit colours are used throughout, so keep a light theme for legibility in the field.
        UserAppTheme = AppTheme.Light;
        MainPage = new NavigationPage(new HomePage())
        {
            BarBackgroundColor = Theme.Navy,
            BarTextColor = Colors.White
        };
    }
}
