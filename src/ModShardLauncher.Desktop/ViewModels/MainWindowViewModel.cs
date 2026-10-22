using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ModShardLauncher;
using ModShardLauncher.Desktop.Services;
using ModShardLauncher.Mods;
using Serilog;

namespace ModShardLauncher.Desktop.ViewModels;

/// <summary>
/// Shell view model: navigation, global commands, busy/status state and the live log feed.
/// Page view models receive it so they can reuse the same commands instead of duplicating them.
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly AppServices _services;
    private string _statusKey = "StatusReady";
    private object?[] _statusArgs = Array.Empty<object?>();
    private string _busyKey = string.Empty;

    public MainWindowViewModel(AppServices services)
    {
        _services = services;

        Home = new HomeViewModel(this);
        Mods = new ModsViewModel(this);
        Sources = new ModSourcesViewModel(this);
        SettingsPage = new SettingsViewModel(this);

        NavigationItems =
        [
            new NavItem(LocalizationService.Instance.Text("NavHome")),
            new NavItem(LocalizationService.Instance.Text("NavMods")),
            new NavItem(LocalizationService.Instance.Text("NavSources")),
            new NavItem(LocalizationService.Instance.Text("NavSettings")),
        ];

        foreach (LogEntry entry in services.LogSink.History)
        {
            LogEntries.Add(entry);
        }

        services.LogSink.EntryWritten += OnLogEntry;
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;

        _currentPage = Home;
        SetStatus("StatusReady");
        DataFileName = services.GameData.DataPath.Length > 0 ? Path.GetFileName(services.GameData.DataPath) : null;
    }

    public AppServices Services => _services;

    public UserSettings Settings => _services.Settings;

    public HomeViewModel Home { get; }

    public ModsViewModel Mods { get; }

    public ModSourcesViewModel Sources { get; }

    public SettingsViewModel SettingsPage { get; }

    public ObservableCollection<LogEntry> LogEntries { get; } = new();

    public ObservableCollection<NavItem> NavigationItems { get; }

    /// <summary>Raised when a log line was appended, so the view can scroll to it.</summary>
    public event Action? LogAppended;

    [ObservableProperty]
    private object? _currentPage;

    [ObservableProperty]
    private int _selectedNavIndex;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _busyText = string.Empty;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string? _dataFileName;

    [ObservableProperty]
    private bool _isLogVisible = true;

    public string VersionText => $"{AppInfo.ProductName} {AppInfo.MslVersion}";

    public string DataFileDisplay => DataFileName ?? LocalizationService.Instance["HomeNoData"];

    public string ModsCountDisplay => LocalizationService.Instance.Format("HomeStatsModsValue", _services.Catalog.Mods.Count);

    public string EnabledCountDisplay => LocalizationService.Instance.Format("HomeStatsEnabledValue", _services.Catalog.Mods.Count(m => m.Enabled));

    public string SourcesCountDisplay => LocalizationService.Instance.Format("HomeStatsSourcesValue", _services.Catalog.ModSources.Count);

    public string ModsStatusText => LocalizationService.Instance.Format("StatusModsCount", _services.Catalog.Mods.Count);

    public string EnabledStatusText => LocalizationService.Instance.Format("StatusEnabledCount", _services.Catalog.Mods.Count(m => m.Enabled));

    public bool HasData => _services.GameData.IsLoaded;

    public bool AreModsPresent => _services.Catalog.Mods.Count > 0;

    public string LogTitle => LocalizationService.Instance["LogTitle"];

    public string LogClearLabel => LocalizationService.Instance["LogClear"];

    public string LogEmptyText => LocalizationService.Instance["LogEmpty"];

    public string DataHeaderText => LocalizationService.Instance["StatusBarData"];

    partial void OnSelectedNavIndexChanged(int value) => CurrentPage = value switch
    {
        1 => Mods,
        2 => Sources,
        3 => SettingsPage,
        _ => Home,
    };

    // ----- navigation -------------------------------------------------------

    [RelayCommand]
    private void ShowHome() => SelectedNavIndex = 0;

    [RelayCommand]
    private void ShowMods() => SelectedNavIndex = 1;

    [RelayCommand]
    private void ShowSources() => SelectedNavIndex = 2;

    [RelayCommand]
    private void ShowSettings() => SelectedNavIndex = 3;

    [RelayCommand]
    private void ToggleLog() => IsLogVisible = !IsLogVisible;

    // ----- file / patch operations -----------------------------------------

    [RelayCommand]
    public async Task OpenDataAsync()
    {
        string? path = await _services.Dialogs.PickOpenDataFileAsync();
        if (path is null)
        {
            return;
        }

        await RunBusyAsync("StatusLoading", async () =>
        {
            await _services.GameData.LoadAsync(path);
            DataFileName = Path.GetFileName(path);
            await ReloadCatalogCoreAsync();
            SetStatus("StatusLoaded", DataFileName);
        });
    }

    /// <summary>Patches every enabled mod, then asks for the destination file (same order as the WPF build).</summary>
    [RelayCommand]
    public async Task PatchAndSaveAsync()
    {
        if (!_services.GameData.IsLoaded)
        {
            await _services.Dialogs.ShowErrorAsync(
                LocalizationService.Instance["DialogErrorTitle"],
                LocalizationService.Instance["LoadDataWarning"]);
            return;
        }

        await RunBusyAsync("StatusPatching", async () =>
        {
            PatchDiagnostic? diagnostic;
            try
            {
                diagnostic = _services.Patcher.Patch(_services.Catalog.Mods);
            }
            catch (Exception ex)
            {
                // The legacy code lets this path escape; keep the app alive and report it.
                Log.Error(ex, "Patching failed");
                await _services.Dialogs.ShowErrorAsync(
                    LocalizationService.Instance["DialogErrorTitle"],
                    ex.Message,
                    ex.ToString());
                SetStatus("StatusPatchFailed");
                return;
            }

            if (diagnostic is null)
            {
                Log.Information("Successfully patch vanilla");
                string? destination = await _services.Dialogs.PickSaveDataFileAsync();
                if (destination is not null)
                {
                    await _services.GameData.SaveAsync(destination);
                    string? directory = Path.GetDirectoryName(destination);
                    if (directory is not null)
                    {
                        LootTableRegistry.SaveLootTables(directory);
                    }

                    SetStatus("StatusSaved", Path.GetFileName(destination));
                }
                else
                {
                    Log.Information("Saved cancelled.");
                    SetStatus("StatusReady");
                }
            }
            else
            {
                Log.Information("Failed patching vanilla");
                await _services.Dialogs.ShowErrorAsync(diagnostic.Title, diagnostic.Summary, diagnostic.Details);
                SetStatus("StatusPatchFailed");
            }

            // Parity with the original: reload the vanilla file after every patch attempt.
            if (!string.IsNullOrEmpty(_services.GameData.DataPath))
            {
                await _services.GameData.LoadAsync(_services.GameData.DataPath);
            }

            await ReloadCatalogCoreAsync();
        });
    }

    [RelayCommand]
    public async Task ReloadModsAsync()
    {
        await RunBusyAsync("StatusReloading", async () =>
        {
            await ReloadCatalogCoreAsync();
            SetStatus("StatusReady");
        });
    }

    [RelayCommand]
    public async Task CompileAsync(ModSource source)
    {
        await RunBusyAsync("StatusCompiling", async () =>
        {
            ModCompileResult result = _services.Compiler.Compile(source.Path);
            if (result.Succeeded)
            {
                await ReloadCatalogCoreAsync();
                SetStatus("StatusCompiled", source.Name);
            }
            else
            {
                await _services.Dialogs.ShowErrorAsync(
                    LocalizationService.Instance["DialogErrorTitle"],
                    LocalizationService.Instance.Format("CompileFailed", source.Name));
                SetStatus("StatusCompileFailed");
            }
        });
    }

    // ----- misc commands ----------------------------------------------------

    [RelayCommand]
    private void OpenModsFolder() => OpenFolder(AppPaths.ModsDirectory);

    [RelayCommand]
    private void OpenSourcesFolder() => OpenFolder(AppPaths.ModSourcesDirectory);

    [RelayCommand]
    private void OpenLogsFolder() => OpenFolder(AppPaths.LogsDirectory);

    [RelayCommand]
    private void ShowScriptConsole()
    {
        if (!ModInterfaceServer.TryStart(1333, out string? error))
        {
            _ = _services.Dialogs.ShowInfoAsync(
                LocalizationService.Instance["DialogErrorTitle"],
                error ?? string.Empty);
            return;
        }

        Log.Information(LocalizationService.Instance["ServerStarted"], 1333);
        _services.Dialogs.ShowScriptConsole();
    }

    [RelayCommand]
    public Task ShowAboutAsync() => _services.Dialogs.ShowInfoAsync(
        LocalizationService.Instance["AboutTitle"],
        LocalizationService.Instance.Format("AboutText", AppInfo.MslVersion, AppInfo.UtmtLibVersion));

    [RelayCommand]
    private void ClearLog() => LogEntries.Clear();

    [RelayCommand]
    private void Exit()
    {
        SaveSettings();
        _services.Shutdown();
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    // ----- helpers ----------------------------------------------------------

    public void OpenFolder(string path) => _services.Dialogs.OpenFolder(path);

    public void SaveSettings() => _services.SaveSettings();

    public void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(VersionText));
        OnPropertyChanged(nameof(DataFileDisplay));
        OnPropertyChanged(nameof(ModsCountDisplay));
        OnPropertyChanged(nameof(EnabledCountDisplay));
        OnPropertyChanged(nameof(SourcesCountDisplay));
        OnPropertyChanged(nameof(ModsStatusText));
        OnPropertyChanged(nameof(EnabledStatusText));
        OnPropertyChanged(nameof(LogTitle));
        OnPropertyChanged(nameof(LogClearLabel));
        OnPropertyChanged(nameof(LogEmptyText));
        OnPropertyChanged(nameof(DataHeaderText));
        RecomputeStatus();
        Mods.RefreshLocalized();
        Sources.RefreshLocalized();
        SettingsPage.RefreshLocalized();
    }

    private async Task ReloadCatalogCoreAsync()
    {
        _services.Catalog.Reload();
        await Mods.SyncAsync();
        await Sources.SyncAsync();
        OnPropertyChanged(nameof(HasData));
        OnPropertyChanged(nameof(AreModsPresent));
        OnPropertyChanged(nameof(DataFileDisplay));
        OnPropertyChanged(nameof(ModsCountDisplay));
        OnPropertyChanged(nameof(EnabledCountDisplay));
        OnPropertyChanged(nameof(SourcesCountDisplay));
        OnPropertyChanged(nameof(ModsStatusText));
        OnPropertyChanged(nameof(EnabledStatusText));
    }

    private async Task RunBusyAsync(string busyKey, Func<Task> action)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        _busyKey = busyKey;
        BusyText = LocalizationService.Instance[busyKey];

        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "The operation failed");
            await _services.Dialogs.ShowErrorAsync(
                LocalizationService.Instance["DialogErrorTitle"],
                ex.Message,
                ex.ToString());
            SetStatus("StatusFailed");
        }
        finally
        {
            IsBusy = false;
            BusyText = string.Empty;
        }
    }

    private void SetStatus(string key, params object?[] args)
    {
        _statusKey = key;
        _statusArgs = args;
        RecomputeStatus();
    }

    private void RecomputeStatus()
    {
        StatusText = _statusArgs.Length == 0
            ? LocalizationService.Instance[_statusKey]
            : LocalizationService.Instance.Format(_statusKey, _statusArgs);

        if (_busyKey.Length > 0)
        {
            BusyText = LocalizationService.Instance[_busyKey];
        }
    }

    private void OnLogEntry(LogEntry entry)
    {
        Dispatcher.UIThread.Post(() =>
        {
            LogEntries.Add(entry);
            while (LogEntries.Count > 2000)
            {
                LogEntries.RemoveAt(0);
            }

            LogAppended?.Invoke();
        });
    }
}
