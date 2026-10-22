using System;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using ModShardLauncher.Desktop.Cli;

namespace ModShardLauncher.Desktop;

internal static class Program
{
    /// <summary>
    /// Single entry point for both modes: command line arguments run the headless tool, no
    /// arguments start the Avalonia desktop application.
    /// <para>
    /// The executable uses the console subsystem so shells wait for it and see the CLI exit code.
    /// When starting the GUI, the console is detached immediately so no black window stays open
    /// (when launched from Explorer) and the terminal stays usable (when launched from a shell).
    /// </para>
    /// </summary>
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            FreeConsole();
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        try
        {
            Console.OutputEncoding = new UTF8Encoding(false);
        }
        catch
        {
            // Redirected stdio may not support encoding changes.
        }

        return CliApplication.RunAsync(args).GetAwaiter().GetResult();
    }

    // Avalonia configuration; also used by the visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();

    [DllImport("kernel32.dll")]
    private static extern bool FreeConsole();
}
