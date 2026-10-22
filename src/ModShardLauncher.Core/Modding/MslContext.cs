using ModShardLauncher.Mods;
using UndertaleModLib;
using UndertaleModLib.Models;

namespace ModShardLauncher;

/// <summary>
/// Ambient state shared by the static <see cref="Msl"/> API.
/// <para>
/// Historically every helper reached into <c>ModLoader.Data</c> / <c>Main.Instance</c>.
/// The host (<see cref="GameDataService"/> / <see cref="PatchRunner"/>) now owns the lifetime and
/// attaches the game data here before any mod code runs, which keeps the public API static
/// (mod compatibility) while making the state explicit and testable.
/// </para>
/// <para>
/// Storage intentionally aliases the legacy <see cref="DataLoader.data"/> field and
/// <see cref="ModLoader"/> tables: old mods write those members directly, and the API must observe
/// the same objects.
/// </para>
/// </summary>
public static class MslContext
{
    private static bool _attached;

    /// <summary>The data.win currently being patched.</summary>
    /// <exception cref="InvalidOperationException">No game data has been attached yet.</exception>
    public static UndertaleData Data
    {
        get
        {
            if (!_attached)
            {
                throw new InvalidOperationException("No game data (data.win) is currently loaded.");
            }

            return DataLoader.data;
        }
    }

    public static bool HasData => _attached;

    /// <summary>The weapon table loaded by <see cref="GameTableCodec.Initialize"/> (legacy storage).</summary>
    public static List<string> Weapons => ModLoader.Weapons;

    /// <summary>The equipment description table loaded by <see cref="GameTableCodec.Initialize"/> (legacy storage).</summary>
    public static List<string> WeaponDescriptions => ModLoader.WeaponDescriptions;

    internal static List<Menu> Menus = new();
    internal static List<(string, string[])> Credits = new();
    internal static List<(string, UndertaleRoom.GameObject)> Disclaimers = new();

    /// <summary>Callbacks registered by mods for the script engine protocol (legacy storage).</summary>
    public static Dictionary<string, Action<string>> ScriptCallbacks => ModLoader.ScriptCallbacks;

    internal static void Attach(UndertaleData data)
    {
        DataLoader.data = data;
        _attached = true;
    }

    internal static void Detach()
    {
        DataLoader.data = new UndertaleData();
        _attached = false;
    }

    internal static void AddCredit(string modNameShort, string[] authors) =>
        Credits.Add((modNameShort, authors));

    internal static void AddDisclaimer(string modNameShort, UndertaleRoom.GameObject overlay) =>
        Disclaimers.Add((modNameShort, overlay));

    internal static void AddMenu(string name, params UIComponent[] components) =>
        Menus.Add(new Menu(name, components));

    internal static void ResetPatchState()
    {
        Menus = new List<Menu>();
        Credits = new List<(string, string[])>();
        Disclaimers = new List<(string, UndertaleRoom.GameObject)>();
    }
}
