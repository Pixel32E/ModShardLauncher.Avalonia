using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ModShardLauncher.Desktop.Views;
using Serilog;

namespace ModShardLauncher.Desktop.Services;

/// <summary>All window/file interactions the view models are allowed to perform.</summary>
public sealed class DialogService
{
    private Window? _owner;

    public void Attach(Window owner) => _owner = owner;

    public async Task<string?> PickOpenDataFileAsync()
    {
        if (_owner is null)
        {
            return null;
        }

        IReadOnlyList<IStorageFile> files = await _owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = LocalizationService.Instance["MenuOpenData"],
            AllowMultiple = false,
            FileTypeFilter = DataFileTypes,
        });

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public async Task<string?> PickSaveDataFileAsync()
    {
        if (_owner is null)
        {
            return null;
        }

        IStorageFile? file = await _owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = LocalizationService.Instance["MenuSaveAs"],
            SuggestedFileName = "data.win",
            DefaultExtension = "win",
            ShowOverwritePrompt = true,
            FileTypeChoices = DataFileTypes,
        });

        return file?.TryGetLocalPath();
    }

    public async Task ShowErrorAsync(string title, string message, string? details = null)
    {
        if (_owner is null)
        {
            return;
        }

        ErrorDialog dialog = new(title, message, details);
        await dialog.ShowDialog(_owner);
    }

    public async Task ShowInfoAsync(string title, string message)
    {
        if (_owner is null)
        {
            return;
        }

        MessageDialog dialog = new(title, message, confirm: false);
        await dialog.ShowDialog(_owner);
    }

    public async Task<bool> ConfirmAsync(string title, string message)
    {
        if (_owner is null)
        {
            return false;
        }

        MessageDialog dialog = new(title, message, confirm: true);
        return await dialog.ShowDialog<bool>(_owner);
    }

    public void ShowScriptConsole()
    {
        if (_owner is null)
        {
            return;
        }

        ScriptConsoleWindow window = new();
        window.Show(_owner);
    }

    public void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Cannot open the folder {0}", path);
        }
    }

    private static FilePickerFileType[] DataFileTypes =>
    [
        new("GameMaker data files")
        {
            Patterns = ["*.win", "*.unx", "*.ios", "*.droid", "audiogroup*.dat"],
        },
        FilePickerFileTypes.All,
    ];
}
