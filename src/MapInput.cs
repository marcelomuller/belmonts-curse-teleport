using Il2CppInterop.Runtime;
using UnityEngine;

namespace SaveRoomTeleport;

// Polls the open area map instead of patching it. LevelMapItem's interact hooks have trivial bodies that
// IL2CPP folds together with thousands of unrelated methods, so a Harmony patch there crashes the game.
// Reading the map's own interact input keeps the game's binding (gamepad and keyboard) and button prompt.
internal static class MapInput
{
    const float LookupInterval = 0.5f;
    const float SelectableRefreshInterval = 1f;

    static LevelMapController _controller;
    static UIMapLevelManager _mapUi;
    static float _nextLookup, _nextSelectableRefresh;
    static string _lastSelection;

    internal static bool IsTarget(LevelMapItem item, out LevelMapSavePointItem savePoint)
    {
        savePoint = item?.TryCast<LevelMapSavePointItem>();
        return savePoint != null && savePoint.context != null && Destinations.WasUsed(savePoint);
    }

    // Returns the save point the player just confirmed with the map's interact input, if any.
    internal static LevelMapSavePointItem Poll()
    {
        if (!Plugin.Enabled.Value || !Find() || !_controller.AreControlsEnabled()) return null;

        MakeSavePointsSelectable();

        var selected = _controller.cursor.GetCurrentSelection();
        LogSelection(selected);
        if (!IsTarget(selected, out var savePoint)) return null;

        ShowPrompt();
        if (_controller._importantInputProcessedForFrame || !_controller.interactInput.action.WasPerformedThisFrame())
            return null;
        _controller._importantInputProcessedForFrame = true;
        return savePoint;
    }

    // The map cursor only selects items flagged as selectable; save points are just hoverable by default.
    static void MakeSavePointsSelectable()
    {
        if (Time.unscaledTime < _nextSelectableRefresh) return;
        _nextSelectableRefresh = Time.unscaledTime + SelectableRefreshInterval;

        var context = _controller.levelMapContext;
        if (context == null) return;
        var changed = MakeSelectable(context.staticLevelMapItems) + MakeSelectable(context.dynamicLevelMapItems);
        if (changed > 0) Plugin.L.LogInfo($"made {changed} used save point(s) selectable on the map");
    }

    static int MakeSelectable(Il2CppSystem.Collections.Generic.List<LevelMapItem> items)
    {
        if (items == null) return 0;
        var changed = 0;
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item == null || item.selectableItem || !IsTarget(item, out _)) continue;
            item.selectableItem = true;
            changed++;
        }
        return changed;
    }

    static void LogSelection(LevelMapItem selected)
    {
        var savePoint = selected?.TryCast<LevelMapSavePointItem>();
        var description = selected == null
            ? "nothing"
            : savePoint != null
                ? $"save point {savePoint.itemWorldRoomId} (used={Destinations.WasUsed(savePoint)}, sameArea={savePoint.context?.isCurrentMcLevel})"
                : selected.GetIl2CppType().Name;
        if (description == _lastSelection) return;
        _lastSelection = description;
        Plugin.L.LogInfo($"map selection: {description}");
    }

    // The game hides the interact footer for save points; show it with our label while one is selected.
    // When the selection moves on, the game refreshes the footer for the new item by itself.
    static void ShowPrompt()
    {
        if (_mapUi == null) return;
        _mapUi.hasInteractFootnoteInitBooleanView?.SetBool(true);
        _mapUi.hasInteractFootnoteBooleanView?.SetBool(true);
        var label = _mapUi.interactFootnoteText?.textDisplay;
        if (label != null && label.text != Plugin.FooterLabel.Value) label.text = Plugin.FooterLabel.Value;
    }

    // The map lives in its own scene, loaded when it opens. Searching the scene is expensive, so it only
    // happens while the game is out of normal play (a menu is open) and the map has not been found yet.
    static bool Find()
    {
        if (_controller != null && _controller.isActiveAndEnabled) return true;
        if (Game.InGameplay || Time.unscaledTime < _nextLookup) return false;
        _nextLookup = Time.unscaledTime + LookupInterval;

        _controller = Object.FindObjectOfType(Il2CppType.Of<LevelMapController>())?.TryCast<LevelMapController>();
        if (_mapUi == null)
            _mapUi = Object.FindObjectOfType(Il2CppType.Of<UIMapLevelManager>())?.TryCast<UIMapLevelManager>();

        var found = _controller != null && _controller.isActiveAndEnabled;
        if (found)
        {
            _nextSelectableRefresh = 0;
            Plugin.L.LogInfo($"map found (footer UI {(_mapUi != null ? "found" : "missing")})");
        }
        return found;
    }
}
