namespace ModShardLauncher;

/// <summary>
/// Portable folder layout. Historically every path was built from
/// <see cref="Environment.CurrentDirectory"/>, which is preserved so an existing installation
/// keeps finding its <c>Mods</c>, <c>ModSources</c>, logs and <c>Settings.json</c>.
/// </summary>
public static class AppPaths
{
    /// <summary>Root used for all relative data. The CLI can point it at a game folder.</summary>
    public static string BaseDirectory { get; set; } = Environment.CurrentDirectory;

    public static string ModsDirectory => Path.Combine(BaseDirectory, "Mods");

    public static string ModSourcesDirectory => Path.Combine(BaseDirectory, "ModSources");

    public static string LogsDirectory => Path.Combine(BaseDirectory, "logs");

    public static string SettingsFile => Path.Combine(BaseDirectory, "Settings.json");

    public static string ExportDirectory => Path.Combine(BaseDirectory, "export");

    /// <summary>Creates the writable folders the application expects to exist.</summary>
    public static void EnsureCreated()
    {
        Directory.CreateDirectory(ModsDirectory);
        Directory.CreateDirectory(ModSourcesDirectory);
        Directory.CreateDirectory(LogsDirectory);
    }
}
