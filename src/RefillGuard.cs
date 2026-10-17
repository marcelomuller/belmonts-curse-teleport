namespace SaveRoomTeleport;

// Watches HP and potions for a few seconds after arrival and only logs a rise; it never changes them.
// An earlier version undid rises here and ended up cancelling the refill from saving at the save room the
// player had just arrived in. Load-time refills are fixed once on arrival (Game.RestoreVitals), and no
// refill after that has been seen; this log is how one would show up.
internal sealed class RefillGuard
{
    const float Duration = 5f;

    readonly Vitals _arrived;
    float _remaining = Duration;
    bool _logged;

    internal RefillGuard(Vitals arrived) => _arrived = arrived;

    // Returns false once the watch period is over.
    internal bool Tick(float deltaTime)
    {
        _remaining -= deltaTime;
        if (Game.Player == null) return false;

        var now = Game.ReadVitals();
        if (!_logged && (now.Potions > _arrived.Potions || now.Overdrive > _arrived.Overdrive || now.Health > _arrived.Health + 0.5f))
        {
            _logged = true;
            Plugin.L.LogInfo($"vitals rose after arrival: {_arrived} → {now} (saving at the save room does this; left as is)");
        }
        return _remaining > 0;
    }
}
