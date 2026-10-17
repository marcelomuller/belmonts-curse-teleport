using HarmonyLib;
using UnityEngine;

namespace TeleportLab;

// Remembers the last save point hovered on the map; it becomes the teleport target.
// Map coordinates are world coordinates: the SaveRoom05 icon sits at (516, 28) and saving there
// records a spawn position of (516.21, 28.01).
[HarmonyPatch]
internal static class HoverPatches
{
    internal static string HoveredRoomId;
    internal static Vector3 HoveredPosition;
    internal static bool HoveredIsValid;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(LevelMapSavePointItem), nameof(LevelMapSavePointItem.OnHover))]
    private static void OnHover(LevelMapSavePointItem __instance)
    {
        var id = __instance.itemWorldRoomId?.ToString();
        if (string.IsNullOrEmpty(id) || id == HoveredRoomId)
            return;
        HoveredRoomId = id;
        HoveredPosition = __instance.transform.position;
        // Same level only for now: the reload keeps the current levelId.
        HoveredIsValid = __instance.HasUsedSavePoint() && __instance.context.isCurrentMcLevel;
        Plugin.L.LogInfo($"hovered save point room: {id} at {HoveredPosition} (valid target={HoveredIsValid})");
    }
}
