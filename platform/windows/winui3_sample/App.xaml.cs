using Microsoft.UI.Xaml;

namespace GodotWinUI3Sample;

public partial class App : Application
{
    // Exposed so pages can pass the host HWND to Godot via WindowNative.
    public static Window? MainWindow { get; private set; }

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow = new MainWindow();
        MainWindow.Activate();
    }
}
