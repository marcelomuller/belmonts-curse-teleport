using Il2CppInterop.Runtime;
using MotherBase.ActionFramework;
using UnityEngine;

namespace SaveRoomTeleport;

// Uses the destination's save point the way the player does. The interact button starts the player's
// InteractionActionTrigger, which runs the focused mirror's interaction (animation, refill, monster reset,
// save). Calling SavePoint.UseSavePoint directly skips that action and does nothing visible.
internal sealed class SavePointUse
{
    const float FocusTimeout = 2f;

    readonly Interactible _mirror;
    float _waited;

    internal SavePointUse(SavePoint savePoint) =>
        _mirror = savePoint.GetComponentInChildren<Interactible>(true) ?? savePoint.GetComponentInParent<Interactible>();

    // Returns null while waiting for the mirror to take focus, then whether the save action started.
    internal bool? Tick(float deltaTime)
    {
        if (_mirror == null)
        {
            Plugin.L.LogWarning("the save point has no Interactible");
            return false;
        }
        if (!HasFocus())
        {
            _waited += deltaTime;
            if (_waited < FocusTimeout) return null;
            Plugin.L.LogWarning("the save point never took focus; trying to interact anyway");
        }

        var trigger = InteractTrigger();
        var actor = Game.Player.GetComponentInChildren<Actor>(true);
        if (trigger == null || actor == null)
        {
            Plugin.L.LogWarning($"cannot interact: trigger={(trigger != null)} actor={(actor != null)}");
            return false;
        }
        var started = actor.TryStartAction(Game.Player, trigger);
        Plugin.L.LogInfo($"interacting with the save point via {trigger.name}: started={started}");
        return started;
    }

    bool HasFocus()
    {
        var focusing = _mirror.focusingEntities;
        if (focusing == null) return false;
        for (var i = 0; i < focusing.Count; i++)
            if (focusing[i] == Game.Player) return true;
        return false;
    }

    static InteractionActionTrigger _trigger;

    // The trigger behind the interact button. It is an asset, so it is looked up once.
    static InteractionActionTrigger InteractTrigger()
    {
        if (_trigger != null) return _trigger;
        var all = Resources.FindObjectsOfTypeAll(Il2CppType.Of<InteractionActionTrigger>());
        for (var i = 0; i < all.Length; i++)
            Plugin.L.LogInfo($"interaction trigger [{i}]: {all[i].name}");
        _trigger = all.Length > 0 ? all[0].Cast<InteractionActionTrigger>() : null;
        return _trigger;
    }
}
