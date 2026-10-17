using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Il2CppInterop.Runtime;
using MotherBase.EntityFramework;
using UnityEngine;

namespace TeleportLab;

// Thin accessors over the game's singletons, plus snapshotting.
internal static class Game
{
    static SaveLogic _saveLogic;

    internal static SaveLogic SaveLogic
    {
        get
        {
            if (_saveLogic != null) return _saveLogic;
            var found = Resources.FindObjectsOfTypeAll(Il2CppType.Of<SaveLogic>());
            if (found != null && found.Length > 0) _saveLogic = found[0].Cast<SaveLogic>();
            return _saveLogic;
        }
    }

    internal static GameStateManager States => GameStateManager.GetInstance();
    internal static LevelManager Levels => LevelManager.Get();
    internal static Entity Player => States?._mainCharacterEntity;

    internal static bool InGameplay =>
        States != null && Player != null && States.IsGameplayState() && Levels != null && !Levels.IsPreparingLevel();

    internal static bool Loading =>
        States != null && (States.IsLoadingState() || (Levels != null && Levels.IsPreparingLevel()));

    // Live player state: a copy of the current save overwritten by every savable player component.
    // The game's real CurrentSave is not touched.
    internal static string LivePlayerJson()
    {
        var current = SaveLogic.CurrentSave.playerSave;
        var copy = JsonUtility.FromJson(JsonUtility.ToJson(current), Il2CppType.Of<PlayerSave>()).Cast<PlayerSave>();

        var writers = 0;
        foreach (var mb in Player.gameObject.GetComponentsInChildren<MonoBehaviour>(true))
        {
            var savable = mb.TryCast<IPlayerComponentSavable>();
            if (savable == null) continue;
            try
            {
                savable.WritePlayerSave(copy, SaveOrigin.Flow);
                writers++;
            }
            catch (Exception ex)
            {
                Plugin.L.LogWarning($"{mb.GetIl2CppType().Name}.WritePlayerSave failed: {ex.Message}");
            }
        }
        // Without writers the "live" snapshot would silently be the last save, which hides losses.
        if (writers == 0) throw new InvalidOperationException("no IPlayerComponentSavable found on the player");
        Plugin.L.LogInfo($"live snapshot: {writers} savable player components");
        return JsonUtility.ToJson(copy);
    }

    // Current world's runtime state plus the progression board, which lives beside the player save.
    internal static string WorldJson() =>
        $"{{\"runtimeWorld\":{JsonUtility.ToJson(SaveLogic.RuntimeWorldSave)}," +
        $"\"progressionBoard\":{JsonUtility.ToJson(SaveLogic.CurrentSave.progressionBoardSave)}}}";

    internal static string DiskSavePath() =>
        Path.Combine(Application.persistentDataPath, $"BuildSave_{SaveLogic.GetCurrentSaveIndex()}.sav");

    internal static string DiskSaveHash()
    {
        var path = DiskSavePath();
        if (!File.Exists(path)) return "missing";
        using var sha = SHA1.Create();
        return Convert.ToHexString(sha.ComputeHash(File.ReadAllBytes(path)))[..12];
    }

    internal static string LastUsedSaveRoomId => SaveLogic.CurrentSave.playerSave.lastUsedSaveRoomId?.ToString();

    internal static readonly MainGameLoadingType[] TestModes =
    {
        // FastTravel cost a heal charge; Death is excluded by design.
        MainGameLoadingType.LoadSave,
        MainGameLoadingType.Default,
    };

    // Writes live state into the in-memory save (never to disk) and points the spawn at the target save room.
    // The game spawns at playerSave.position when hasSavedSpawnPosition is set; without it, it falls back to
    // the opening cinematic spawner at the start of the game.
    internal static void Prepare(string targetRoomId, Vector3 targetPosition)
    {
        if (Plugin.PrepWorld.Value) SaveLogic.SaveRoom(true);
        if (Plugin.PrepPlayer.Value) SaveLogic.GeneratePlayerSave(Player, SaveOrigin.Flow);

        var save = SaveLogic.CurrentSave.playerSave;
        save.lastUsedSaveRoomId = targetRoomId;
        save.position = targetPosition;
        save.hasSavedSpawnPosition = true;
    }

    internal static void Reload(string targetRoomId, MainGameLoadingType mode)
    {
        Plugin.L.LogInfo($"teleport → room {targetRoomId} via {Plugin.Entry.Value}({mode}) prepPlayer={Plugin.PrepPlayer.Value} prepWorld={Plugin.PrepWorld.Value}");
        switch (Plugin.Entry.Value)
        {
            case "LoadLastSaveState":
                Levels.LoadLastSaveState(mode);
                break;
            case "TryLoadLevelState":
                var ok = Levels.TryLoadLevelState(SaveLogic.GetSavedCurrentLevel(), mode, null);
                Plugin.L.LogInfo($"TryLoadLevelState returned {ok}");
                break;
            default:
                Levels.ReloadSaveState(mode);
                break;
        }
    }
}
