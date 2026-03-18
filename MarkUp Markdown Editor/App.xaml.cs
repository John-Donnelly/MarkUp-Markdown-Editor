using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;

namespace MarkUp_Markdown_Editor;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        _window = new MainWindow(GetFilePathFromActivation());
        _window.Activate();
    }

    /// <summary>
    /// Returns the path of the first file passed via a file-type association activation,
    /// or <c>null</c> when the app was launched normally (e.g. from Start or taskbar).
    /// </summary>
    private static string? GetFilePathFromActivation()
    {
        var activatedArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
        if (activatedArgs?.Kind == ExtendedActivationKind.File
            && activatedArgs.Data is IFileActivatedEventArgs fileArgs
            && fileArgs.Files.Count > 0)
        {
            return fileArgs.Files[0].Path;
        }
        return null;
    }
}
