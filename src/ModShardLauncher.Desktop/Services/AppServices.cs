using ModShardLauncher.Desktop.ViewModels;
using Serilog;
using Serilog.Events;

namespace ModShardLauncher.Desktop.Services;

/// <summary>
/// Composition root: builds the services once, wires logging and localization, and exposes the
/// long lived collaborators to the view models. No service locator lookups from inside the UI.
/// </summary>
public sealed class AppServices
{
    private AppServices(
        UserSettings settings,
        SettingsStore settingsStore,
        GameDataService gameData,
        ModCatalog catalog,
        PatchRunner patcher,
        ModCompiler compiler,
        UiLogSink logSink,
        DialogService dialogs)
    {
        Settings = settings;
        SettingsStore = settingsStore;
        GameData = gameData;
        Catalog = catalog;
        Patcher = patcher;
        Compiler = compiler;
        LogSink = logSink;
        Dialogs = dialogs;
    }

    public static AppServices Current { get; private set; } = null!;

    public UserSettings Settings { get; }

    public SettingsStore SettingsStore { get; }

    public GameDataService GameData { get; }

    public ModCatalog Catalog { get; }

    public PatchRunner Patcher { get; }

    public ModCompiler Compiler { get; }

    public UiLogSink LogSink { get; }

    public DialogService Dialogs { get; }

    public static AppServices Create()
    {
        AppPaths.EnsureCreated();

        SettingsStore settingsStore = new();
        UserSettings settings = settingsStore.Load();

        UiLogSink logSink = new();
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(Path.Combine(AppPaths.LogsDirectory, $"log_{DateTime.Now:yyyyMMdd_HHmm}.txt"))
            .WriteTo.Sink(logSink)
            .CreateLogger();

        LocalizationService.Instance.SetLanguage(LocalizationService.CultureFor(settings.Language));

        AppServices services = new(
            settings,
            settingsStore,
            new GameDataService(),
            new ModCatalog(),
            new PatchRunner(settings),
            new ModCompiler(),
            logSink,
            new DialogService());

        services.ApplyLogLevel();
        services.Catalog.Reload();
        services.Catalog.ApplyEnabledMods(settings.EnabledMods);

        Log.Information("Launching msl {{{0}}} using UTMT {{{1}}}", AppInfo.MslVersion, AppInfo.UtmtLibVersion);
        foreach (string error in services.Catalog.LoadErrors)
        {
            Log.Warning(error);
        }

        Current = services;
        return services;
    }

    public void ApplyLogLevel()
    {
        LogSink.SetMinimumLevel(Settings.EnableLogger ? LogEventLevel.Information : LogEventLevel.Warning);
    }

    public void SaveSettings()
    {
        try
        {
            SettingsStore.Save(Settings);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Cannot save the settings");
        }
    }

    public void Shutdown() => Log.CloseAndFlush();
}
