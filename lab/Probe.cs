using System;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace TeleportLab;

// Read-only logging of how the game decides where the player spawns, so the teleport can
// reproduce it instead of guessing. Nothing here changes game state.
[HarmonyPatch]
internal static class Probe
{
    static void Log(string msg) => Plugin.L.LogMessage("[probe] " + msg);

    static string V(Vector3 v) => $"({v.x:0.##}, {v.y:0.##}, {v.z:0.##})";

    internal static string SaveFields()
    {
        var p = Game.SaveLogic?.CurrentSave?.playerSave;
        if (p == null) return "no current save";
        return $"lastUsedSaveRoomId={p.lastUsedSaveRoomId} district={p.lastSaveRoomValidDistrictGuid} " +
               $"hasSavedSpawnPosition={p.hasSavedSpawnPosition} levelId={p.levelId} position={V(p.position)}";
    }

    static string LogicFields(MainCharacterSpawnLogic l)
    {
        var ids = l.spawnerIDList?.spawnerIds;
        var list = ids == null ? "null" : string.Join(",", Enumerable.Range(0, ids.Count).Select(i => ids[i]?.ToString()));
        return $"nextSpawnerID={l.nextCharacterSpawnerID} forceUseSpawner={l.forceUseCharacterSpawner} " +
               $"forcePos={l._forceSpawnPosition} spawnPos={V(l._spawnPosition)} offset={l._spawnOffset} spawnerIDList=[{list}]";
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MainCharacterSpawnLogic), nameof(MainCharacterSpawnLogic.GetRelevantCharacterSpawner))]
    static void RelevantSpawner(MainCharacterSpawnLogic __instance, MainCharacterSpawner __result)
    {
        try
        {
            var chosen = __result == null
                ? "none"
                : $"id={__result.spawnerID} go={__result.gameObject.name} scene={__result.gameObject.scene.name} pos={V(__result.GetSpawnPosition())}";
            Log($"GetRelevantCharacterSpawner → {chosen}\n    logic: {LogicFields(__instance)}\n    save: {SaveFields()}");
        }
        catch (Exception ex) { Plugin.L.LogWarning("probe RelevantSpawner: " + ex.Message); }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(MainCharacterSpawnLogic), nameof(MainCharacterSpawnLogic.PlaceMainCharacter))]
    static void Place(MainCharacterSpawnLogic __instance, Vector3 targetPosition)
    {
        try { Log($"PlaceMainCharacter at {V(targetPosition)}\n    logic: {LogicFields(__instance)}"); }
        catch (Exception ex) { Plugin.L.LogWarning("probe Place: " + ex.Message); }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(MainCharacterSpawnLogic), nameof(MainCharacterSpawnLogic.SetForcePosition))]
    static void ForcePosition(Vector3 position) => Log($"SetForcePosition {V(position)}");

    [HarmonyPrefix]
    [HarmonyPatch(typeof(SaveLogic), nameof(SaveLogic.Save))]
    static void SaveBefore(Vector3 spawnPosition, SaveOrigin saveOrigin) =>
        Log($"SaveLogic.Save origin={saveOrigin} spawnPosition={V(spawnPosition)}\n    before: {SaveFields()}");

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SaveLogic), nameof(SaveLogic.Save))]
    static void SaveAfter() => Log($"    after:  {SaveFields()}");

    [HarmonyPostfix]
    [HarmonyPatch(typeof(LevelMapSavePointItem), nameof(LevelMapSavePointItem.OnHover))]
    static void Hover(LevelMapSavePointItem __instance)
    {
        try
        {
            Log($"map save point room={__instance.itemWorldRoomId} zone={__instance.itemExplorationZoneId} " +
                $"used={__instance.HasUsedSavePoint()} pos={V(__instance.transform.position)} " +
                $"local={V(__instance.transform.localPosition)} pivot={V(__instance.GetSelectionPivot())}");
        }
        catch (Exception ex) { Plugin.L.LogWarning("probe Hover: " + ex.Message); }
    }

    // F5: every spawner currently loaded (and spawner assets the game keeps in memory).
    internal static void DumpSpawners()
    {
        var all = Resources.FindObjectsOfTypeAll(Il2CppInterop.Runtime.Il2CppType.Of<MainCharacterSpawner>());
        Log($"{all.Length} MainCharacterSpawner objects; save: {SaveFields()}");
        foreach (var o in all)
        {
            var s = o.Cast<MainCharacterSpawner>();
            var scene = s.gameObject.scene;
            Log($"  id={s.spawnerID} go={s.gameObject.name} scene={(scene.IsValid() ? scene.name : "(asset)")} pos={V(s.GetSpawnPosition())}");
        }
    }
}
