using MotherBase.RoomDesigner;
using RedWine.ResourceManagement;
using UnityEngine;

namespace SaveRoomTeleport;

// A save room to travel to. Position is where the reload spawns the player: the map icon for the current
// area (map coordinates match world coordinates there), the room center for another area. Either way the
// player is snapped onto the room's SavePoint on arrival.
internal sealed record Destination(string RoomId, LevelID Level, bool SameArea, Vector3 Position);

internal static class Destinations
{
    // Mirror Fast Travel loads area data with this resource context.
    const ResourceContext WorldDataContext = (ResourceContext)64;

    internal static Destination Resolve(LevelMapSavePointItem item)
    {
        var roomId = item.itemWorldRoomId?.ToString();
        var level = item.context?.level;
        if (string.IsNullOrEmpty(roomId) || level == null || !WasUsed(item)) return null;

        if (item.context.isCurrentMcLevel)
            return new Destination(roomId, level, true, item.transform.position);

        var center = RoomCenter(level, roomId);
        return center == null ? null : new Destination(roomId, level, false, center.Value);
    }

    // The map item answers for the current area; the explorer state covers every area.
    internal static bool WasUsed(LevelMapSavePointItem item)
    {
        if (item.HasUsedSavePoint()) return true;
        var roomId = item.itemWorldRoomId?.ToString();
        if (string.IsNullOrEmpty(roomId)) return false;
        var explorer = Game.Player?.gameObject.GetComponentInChildren<ExplorerComponent>(true);
        WorldRoomExplorationState state = null;
        return explorer != null && explorer.worldRoomExplorationStates.TryGetValue(roomId, out state)
               && state != null && state.isSaveRoom && state.usedRoomSavePoint;
    }

    static Vector3? RoomCenter(LevelID level, string roomId)
    {
        BlockableAssetReference<WorldData> reference = null;
        if (!Game.Levels.worldDataDictionary.TryGetItem(level, out reference) || reference == null) return null;
        var world = ResourceManager.LoadAssetByRef<WorldData>(WorldDataContext, reference);
        if (world == null) return null;

        var rooms = world.GetRooms();
        for (var i = 0; i < rooms.Count; i++)
        {
            var room = rooms[i];
            if (room.id == roomId) return RoomDesignerExtensions.GetRoomCenterPosition(room);
        }
        return null;
    }

    // The save point object in the room the player is standing in, if that room is the destination.
    internal static SavePoint SavePointIn(string roomId)
    {
        var room = LevelHelpers.GetCurrentRoom(Game.Player);
        if (room == null || room.worldRoomData?.id != roomId) return null;
        var savePoints = room.GetComponentsInChildren<SavePoint>(true);
        return savePoints.Length == 1 ? savePoints[0] : null;
    }

    internal static string CurrentRoomId() => LevelHelpers.GetCurrentRoom(Game.Player)?.worldRoomData?.id;
}
