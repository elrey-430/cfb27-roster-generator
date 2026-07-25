using Avalonia;

namespace RosterGenerator.Gui;

/// <summary>Entry point for the desktop application.</summary>
public static class Program
{
    /// <summary>Starts the app.</summary>
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    /// <summary>Avalonia configuration, also used by design tooling.</summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
