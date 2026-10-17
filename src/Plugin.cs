using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace SaveRoomTeleport;

[BepInPlugin(Guid, Name, Version)]
public class Plugin : BasePlugin
{
    public const string Guid = "marcelo.belmontscurse.saveroomteleport";
    public const string Name = "SaveRoomTeleport";
    public const string Version = "1.2.1";

    internal static ManualLogSource L;
    internal static ConfigEntry<bool> Enabled;
    internal static ConfigEntry<bool> UseSavePoint;
    internal static ConfigEntry<bool> PreserveStats;
    internal static ConfigEntry<MainGameLoadingType> LoadingType;
    internal static ConfigEntry<string> FooterLabel;
    internal static ConfigEntry<string> PopupTitle;
    internal static ConfigEntry<string> PopupMessage;

    public override void Load()
    {
        L = Log;
        Enabled = Config.Bind("General", "Enabled", true,
            "Allow teleporting to used save rooms from the area map, including other areas.");
        UseSavePoint = Config.Bind("General", "ArrivalUsesSavePoint", true,
            "Arriving counts as using the destination's save point, like saving there by hand: refills HP and " +
            "potions, revives monsters and saves the game. Turn off to travel without resting.");
        PreserveStats = Config.Bind("General", "PreserveHealthAndPotions", true,
            "When ArrivalUsesSavePoint is off (or the save point is not found): keep HP and heal potions, " +
            "including overdrive charges, at their pre-teleport values. Without it a reload can refill HP and " +
            "potions or drop overdrive charges.");
        LoadingType = Config.Bind("Advanced", "LoadingType", MainGameLoadingType.LoadSave,
            "How the level is reloaded. Tested: LoadSave, Default. Avoid Death and FastTravel.");
        FooterLabel = Config.Bind("Text", "MapPrompt", "Teleport", "Label next to the interact button on the map.");
        PopupTitle = Config.Bind("Text", "PopupTitle", "Teleport", "Confirmation popup title.");
        PopupMessage = Config.Bind("Text", "PopupMessage", "Teleport to this save room?", "Confirmation popup message.");

        ClassInjector.RegisterTypeInIl2Cpp<Teleporter>();
        var go = new GameObject(Name);
        Object.DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideAndDontSave;
        go.AddComponent<Teleporter>();

        L.LogInfo($"{Name} {Version} loaded");
    }
}
