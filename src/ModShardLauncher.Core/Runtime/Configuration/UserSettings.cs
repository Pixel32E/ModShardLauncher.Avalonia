using Newtonsoft.Json;

namespace ModShardLauncher;

/// <summary>
/// Persisted user preferences. Field names and the JSON shape are kept identical to the WPF
/// version so an existing <c>Settings.json</c> keeps working after the upgrade.
/// </summary>
public class UserSettings
{
    public string Language = "English";
    public bool EnableLogger = true;
    public List<string> EnabledMods = new();
}

/// <summary>Loads and saves <see cref="UserSettings"/> from <see cref="AppPaths.SettingsFile"/>.</summary>
public sealed class SettingsStore
{
    public UserSettings Load()
    {
        if (!File.Exists(AppPaths.SettingsFile))
        {
            return new UserSettings();
        }

        string json = File.ReadAllText(AppPaths.SettingsFile);
        return JsonConvert.DeserializeObject<UserSettings>(json) ?? new UserSettings();
    }

    public void Save(UserSettings settings)
    {
        File.WriteAllText(AppPaths.SettingsFile, JsonConvert.SerializeObject(settings));
    }
}
