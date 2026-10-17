using System.Collections.Generic;
using MotherBase.EntityFramework;
using UnityEngine;

namespace SaveRoomTeleport;

// HP and heal potions as the HUD shows them; overdrive charges sit above the normal potion maximum.
internal readonly record struct Vitals(float Health, int Potions, int Overdrive)
{
    public override string ToString() => $"HP {Health:0.#}, potions {Potions}{(Overdrive > 0 ? $"+{Overdrive}" : "")}";
}

// Where the player stood before a teleport, so a failed arrival can bring them back.
internal sealed record Origin(LevelID Level, Vector3 Position, string SaveRoomId);

// Thin accessors over the game's singletons, plus the save edits that make a reload land somewhere.
internal static class Game
{
    internal static SaveLogic SaveLogic => SingletonScriptableObject<SaveLogic>.Get();
    internal static MainCharacterSpawnLogic SpawnLogic => SingletonScriptableObject<MainCharacterSpawnLogic>.Get();
    internal static GameStateManager States => GameStateManager.GetInstance();
    internal static LevelManager Levels => LevelManager.Get();
    internal static Entity Player => States?._mainCharacterEntity;
    internal static LevelID CurrentLevel => Levels?.currentLevelVariable?.CurrentValue;

    internal static bool InGameplay =>
        States != null && Player != null && States.IsGameplayState() && Levels != null && !Levels.IsPreparingLevel();

    internal static bool Loading =>
        States != null && (States.IsLoadingState() || (Levels != null && Levels.IsPreparingLevel()));

    static StatsStateComponent Stats => Player.gameObject.GetComponentInChildren<StatsStateComponent>(true);

    static int StatInt<T>(StatsStateComponent stats) where T : Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase =>
        Mathf.RoundToInt(stats.GetStatObject(Il2CppInterop.Runtime.Il2CppType.Of<T>())?.GetFloat(Player) ?? -1);

    // Live HP and potions from the stat system, which matched the HUD in every test. The save's charge count
    // lags until the player saves, so it is never used.
    internal static Vitals ReadVitals()
    {
        var health = Player.gameObject.GetComponentInChildren<Damageable>(true);
        var stats = Stats;
        return new Vitals(health?.currentHealth ?? 0,
            stats == null ? 0 : StatInt<HealthPotionCurrent>(stats),
            stats == null ? 0 : StatInt<HealthPotionOverdriveCurrent>(stats));
    }

    // Puts HP and potions back to the pre-teleport values. A reload can refill them (same-area reloads refill
    // like loading a save) or drop overdrive charges. Only values that differ are written, because writing a
    // stat does not refresh the HUD by itself.
    internal static void RestoreVitals(Vitals before)
    {
        var now = ReadVitals();
        var stats = Stats;
        var health = Player.gameObject.GetComponentInChildren<Damageable>(true);
        if (stats == null || health == null) return;

        bool Differs(float current, float wanted) => Mathf.Abs(current - wanted) > 0.5f;
        var changed = false;
        if (Differs(now.Potions, before.Potions))
        {
            stats.SetFloat(Il2CppInterop.Runtime.Il2CppType.Of<HealthPotionCurrent>(), before.Potions);
            changed = true;
        }
        if (Differs(now.Overdrive, before.Overdrive))
        {
            stats.SetFloat(Il2CppInterop.Runtime.Il2CppType.Of<HealthPotionOverdriveCurrent>(), before.Overdrive);
            changed = true;
        }
        if (Differs(now.Health, before.Health))
        {
            health.currentHealth = Mathf.RoundToInt(before.Health);
            changed = true;
        }
        if (!changed) return;

        RefreshHud();
        Plugin.L.LogInfo($"restored vitals: {now} → {ReadVitals()} (wanted {before})");
    }

    static HealthPotionUIController _potionHud;
    static HUDControllerHealth _healthHud;

    // Redraws the HUD from the stats, the way the game does when the player spawns. Only the display reacts
    // to these handlers; game state is untouched. The HUD lives for the whole session, so it is looked up once.
    static void RefreshHud()
    {
        _potionHud ??= Object.FindObjectOfType(Il2CppInterop.Runtime.Il2CppType.Of<HealthPotionUIController>())
            ?.TryCast<HealthPotionUIController>();
        _healthHud ??= Object.FindObjectOfType(Il2CppInterop.Runtime.Il2CppType.Of<HUDControllerHealth>())
            ?.TryCast<HUDControllerHealth>();

        var vitals = ReadVitals();
        var max = Player.gameObject.GetComponentInChildren<HealthPotionsComponent>(true)?.maxHealthPotion ?? vitals.Potions;
        _potionHud?.ProcessInitHealthPotionsEvent(new EntityInitHealthPotionsEvent
        {
            isPlayer = true,
            e = Player,
            maxPotionsCount = max,
            currentPotionsCount = vitals.Potions,
            currentOverdrivePotionsCount = vitals.Overdrive,
        });
        _healthHud?.ProcessInitHealthEvent(new EntityInitHealthEvent
        {
            isPlayer = true,
            e = Player,
            damageable = Player.gameObject.GetComponentInChildren<Damageable>(true),
        });
    }

    internal static Origin CaptureOrigin() =>
        new(CurrentLevel, Player.transform.position, SaveLogic.CurrentSave.playerSave.lastUsedSaveRoomId);

    // Points the in-memory save at the destination and reloads; nothing is written to disk for a same-area
    // trip. The game spawns at playerSave.position when hasSavedSpawnPosition is set; without it, it falls
    // back to the opening cinematic spawner at the start of the game.
    internal static void Teleport(Destination to, MainGameLoadingType mode)
    {
        if (to.SameArea)
        {
            SaveLogic.SaveRoom(true);
            SaveLogic.GeneratePlayerSave(Player, SaveOrigin.Flow);
        }
        else
        {
            // The save the game makes on its own area transitions: stores the current area's state so it
            // survives unloading. Like those transitions, this one writes the save file.
            SaveLogic.SaveNoPosition(Player, SaveOrigin.Flow);
        }

        Plugin.L.LogInfo($"teleport → {to.RoomId} in {to.Level.name} at {to.Position} ({mode}, sameArea={to.SameArea})");
        Load(to.Level, to.Position, to.RoomId, mode);
    }

    internal static void Return(Origin origin, MainGameLoadingType mode)
    {
        Plugin.L.LogWarning($"returning to {origin.Level?.name} at {origin.Position}");
        Load(origin.Level, origin.Position, origin.SaveRoomId, mode);
    }

    static void Load(LevelID level, Vector3 position, string saveRoomId, MainGameLoadingType mode)
    {
        var save = SaveLogic.CurrentSave.playerSave;
        save.levelId = level.guid;
        save.position = position;
        save.hasSavedSpawnPosition = true;
        save.lastUsedSaveRoomId = saveRoomId;

        if (level == CurrentLevel)
        {
            Levels.ReloadSaveState(mode);
            return;
        }
        SpawnLogic.SetForcePosition(position, null);
        if (!Levels.TryLoadLevelState(level, mode, null))
        {
            SpawnLogic.ClearForcePosition();
            throw new System.InvalidOperationException($"the game refused to load {level.name}");
        }
    }

    // Puts the player exactly on the room's save point, as the game does after the room streams in.
    internal static bool SnapTo(SavePoint savePoint)
    {
        var player = Player.transform;
        var target = savePoint.transform.position;
        target.z = player.position.z;
        target.y += 0.01f;
        if (Vector3.Distance(player.position, target) <= 3f) return false;
        SpawnLogic.PlaceMainCharacter(target, player.rotation);
        return true;
    }
}
