using ModShardLauncher.Mods;
using Serilog;

namespace ModShardLauncher;

/// <summary>
/// Discovers the content of <c>Mods/</c> and <c>ModSources/</c>, loads every <c>.sml</c> package and
/// instantiates its <see cref="Mod"/> entry point. Replaces the static ModLoader file scan.
/// </summary>
public sealed class ModCatalog
{
    private readonly string _modsDirectory;
    private readonly string _sourcesDirectory;
    private readonly List<ModFile> _mods = new();
    private readonly List<ModSource> _modSources = new();
    private readonly List<string> _loadErrors = new();

    public ModCatalog(string? modsDirectory = null, string? sourcesDirectory = null)
    {
        _modsDirectory = modsDirectory ?? AppPaths.ModsDirectory;
        _sourcesDirectory = sourcesDirectory ?? AppPaths.ModSourcesDirectory;
    }

    public IReadOnlyList<ModFile> Mods => _mods;

    public IReadOnlyList<ModSource> ModSources => _modSources;

    /// <summary>Non fatal problems (for example a package without a Mod subclass) for the UI to surface.</summary>
    public IReadOnlyList<string> LoadErrors => _loadErrors;

    public event EventHandler? Changed;

    /// <summary>Rescans both folders. Enabled flags and mod identities are preserved across reloads.</summary>
    public void Reload()
    {
        Directory.CreateDirectory(_modsDirectory);
        Directory.CreateDirectory(_sourcesDirectory);


        foreach (ModFile mod in _mods)
        {
            mod.Stream?.Close();
        }

        _modSources.Clear();
        _loadErrors.Clear();

        foreach (string directory in EnumerateModSourceDirectories())
        {
            _modSources.Add(new ModSource
            {
                Name = Path.GetFileName(directory),
                Path = directory,
            });
        }

        List<ModFile> loaded = new();
        foreach (string file in Directory.GetFiles(_modsDirectory, "*.sml"))
        {
            ModFile? modFile = null;
            try
            {
                modFile = SmlPackageReader.Read(file);
            }
            catch (Exception ex)
            {
                Log.Information(ex, "Cannot read the mod {0}", file);
            }

            if (modFile is null)
            {
                continue;
            }

            if (modFile.Assembly is null)
            {
                continue;
            }

            Type? modType = Array.Find(modFile.Assembly.GetTypes(), t => t.IsSubclassOf(typeof(Mod)));
            if (modType is null)
            {
                string message = $"Loading error: {modFile.Assembly.GetName().Name} This Mod need a Mod class";
                _loadErrors.Add(message);
                Log.Error(message);
                continue;
            }

            if (Activator.CreateInstance(modType) is not Mod mod)
            {
                continue;
            }

            mod.LoadAssembly();
            mod.ModFiles = modFile;
            modFile.instance = mod;
            modFile.ReloadRequested = Reload;

            ModFile? previous = _mods.Find(t => t.Name == modFile.Name);
            if (previous is not null)
            {
                modFile.Enabled = previous.Enabled;
            }

            loaded.Add(modFile);
        }

        _mods.Clear();
        _mods.AddRange(loaded);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Re-applies the enabled list persisted in <c>Settings.json</c>.</summary>
    public void ApplyEnabledMods(IEnumerable<string> names)
    {
        foreach (string name in names)
        {
            ModFile? modFile = _mods.Find(t => t.Name == name);
            if (modFile is not null)
            {
                modFile.Enabled = true;
            }
            else
            {
                Log.Warning("Mod {0} not found", name);
            }
        }
    }

    private IEnumerable<string> EnumerateModSourceDirectories()
    {
        // A mod source is a folder containing at least one .csproj at its root.
        return Directory
            .GetDirectories(_sourcesDirectory)
            .Where(x => Directory.EnumerateFiles(x, "*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault() != null);
    }
}
