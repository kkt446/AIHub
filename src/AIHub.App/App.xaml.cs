using Microsoft.UI.Xaml;

namespace AIHub.App;

public partial class App : Application
{
    public static MainWindow? Window { get; private set; }

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) =>
        {
            Log("XAML: " + e.Exception);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log("DOMAIN: " + e.ExceptionObject);
    }

    static void Log(string s)
    {
        try
        {
            File.AppendAllText(
                Path.Combine(Path.GetTempPath(), "aihub-app-error.log"),
                DateTime.Now.ToString("HH:mm:ss") + " " + s + Environment.NewLine);
        }
        catch (IOException) { }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Log($"LAUNCH dir={AppContext.BaseDirectory} dotnet={Environment.Version} os={Environment.OSVersion}");
        try
        {
            Window = new MainWindow();
            Window.Activate();
        }
        catch (Exception ex)
        {
            Log("MAINWINDOW: " + ex);
            throw;
        }
    }
}
