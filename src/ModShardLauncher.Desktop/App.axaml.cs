using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ModShardLauncher.Desktop.Services;
using ModShardLauncher.Desktop.ViewModels;
using ModShardLauncher.Desktop.Views;

namespace ModShardLauncher.Desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            AppServices services = AppServices.Create();
            MainWindowViewModel viewModel = new(services);
            MainWindow window = new()
            {
                DataContext = viewModel,
            };

            services.Dialogs.Attach(window);
            window.Closing += (_, _) =>
            {
                services.SaveSettings();
                services.Shutdown();
            };

            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
