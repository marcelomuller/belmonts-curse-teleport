using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace TeleportLab;

[BepInPlugin(Guid, Name, Version)]
public class Plugin : BasePlugin
{
    public const string Guid = "marcelo.belmontscurse.teleportlab";
    public const string Name = "TeleportLab";
    public const string Version = "0.1.0";

    internal static ManualLogSource L;
    internal static ConfigEntry<bool> PrepPlayer;
    internal static ConfigEntry<bool> PrepWorld;
    internal static ConfigEntry<string> Entry;
    internal static ConfigEntry<float> SettleSeconds;
    internal static ConfigEntry<bool> TestKeys;

    public override void Load()
    {
        L = Log;
        PrepPlayer = Config.Bind("Teleport", "PrepPlayer", true,
            "Write the live player state into the in-memory save before reloading (keeps progress made since the last save room).");
        PrepWorld = Config.Bind("Teleport", "PrepWorld", true,
            "Save the current room's entity states (opened doors, broken walls...) before reloading.");
        Entry = Config.Bind("Teleport", "Entry", "ReloadSaveState",
            "Which LevelManager call performs the reload: ReloadSaveState | LoadLastSaveState | TryLoadLevelState");
        SettleSeconds = Config.Bind("Test", "SettleSeconds", 3f,
            "Seconds to wait after the level finished loading before taking the 'after' snapshot.");

        TestKeys = Config.Bind("Test", "EnableTestKeys", false,
            "Enable F6-F9 (control run / cycle mode / teleport test / run all). Off by default: they really teleport.");

        ClassInjector.RegisterTypeInIl2Cpp<Driver>();
        var go = new GameObject("TeleportLab");
        Object.DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideAndDontSave;
        go.AddComponent<Driver>();

        new Harmony(Guid).PatchAll(typeof(HoverPatches));
        new Harmony(Guid + ".probe").PatchAll(typeof(Probe));
        L.LogInfo($"{Name} {Version} loaded — F5 dump spawners; test keys F6-F9 {(TestKeys.Value ? "ON" : "off")}");
    }
}
