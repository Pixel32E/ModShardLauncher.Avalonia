using ModShardLauncher.Mods;
using UndertaleModLib;
using UndertaleModLib.Models;

namespace ModShardLauncher;

/// <summary>
/// Ambient state shared by the static <see cref="Msl"/> API.
/// <para>
/// Historically every helper reached into <c>ModLoader.Data</c> / <c>Main.Instance</c>.
/// The host (<c>GameDataService</c> / <c>PatchRunner</c>) now owns the lifetime and attaches the
/// game data here before any mod code runs, which keeps the public API static (mod compatibility)
/// while making the state explicit and testable.
/// </para>
/// </summary>
public static class MslContext
{
    private static UndertaleData? _data;

    /// <summary>The data.win currently being patched.</summary>
    /// <exception cref="InvalidOperationException">No game data has been attached yet.</exception>
    public static UndertaleData Data =>
        _data ?? throw new InvalidOperationException("No game data (data.win) is currently loaded.");

    public static bool HasData => _data is not null;

    /// <summary>The weapon table loaded by <see cref="GameTableCodec.Initialize"/>.</summary>
    public static List<string> Weapons = new();

    /// <summary>The equipment description table loaded by <see cref="GameTableCodec.Initialize"/>.</summary>
    public static List<string> WeaponDescriptions = new();

    internal static List<Menu> Menus = new();
    internal static List<(string, string[])> Credits = new();
    internal static List<(string, UndertaleRoom.GameObject)> Disclaimers = new();

    /// <summary>Callbacks registered by mods for the script engine protocol.</summary>
    public static Dictionary<string, Action<string>> ScriptCallbacks = new();

    internal static void Attach(UndertaleData data) => _data = data;

    internal static void Detach() => _data = null;

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
