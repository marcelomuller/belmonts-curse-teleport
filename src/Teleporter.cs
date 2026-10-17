using System;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;

namespace SaveRoomTeleport;

// Confirmation → close the map → teleport → wait for the level → snap onto the save point, restore
// HP/potions. Landing in the wrong room sends the player back to where they stood.
public class Teleporter : MonoBehaviour
{
    public Teleporter(IntPtr ptr) : base(ptr) { }

    internal static Teleporter Instance;

    enum Phase { Idle, Confirming, ClosingMenus, WaitLoadStart, WaitLoadEnd, Settle, UsingSavePoint }

    const float LoadStartTimeout = 10f, LoadEndTimeout = 120f, CloseRetryDelay = 1f, RestoreDelay = 0.5f, RoomKnownTimeout = 5f;
    const int MaxCloseAttempts = 5;

    Phase _phase;
    float _timer;
    int _closeAttempts;
    Destination _to;
    Origin _origin;
    bool _returning;
    Vitals _beforeVitals;
    RefillGuard _guard;
    SavePointUse _savePointUse;
    ConfirmationPopup _popup;

    void Awake() => Instance = this;

    void RequestConfirmation(LevelMapSavePointItem savePoint)
    {
        if (_phase != Phase.Idle) return;
        _to = Destinations.Resolve(savePoint);
        if (_to == null)
        {
            Plugin.L.LogWarning($"no destination for save point {savePoint.itemWorldRoomId}");
            return;
        }

        var ui = UIManager.GetInstance();
        var boatman = ui.fastTravelMenuManager?.confirmationPopupTexts;
        var texts = new ConfirmationPopup.Texts();
        if (boatman != null)
        {
            // Localized cancel/confirm from the boatman; title and message are overwritten below.
            texts.title = boatman.title;
            texts.message = boatman.message;
            texts.cancel = boatman.cancel;
            texts.confirm = boatman.confirm;
        }
        _popup = ui.confirmationPopup;
        Enter(Phase.Confirming);
        ui.OpenConfirmPopup(texts,
            DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(new System.Action(OnCancel)),
            DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(new System.Action(OnConfirm)));
        Plugin.L.LogInfo($"confirm teleport to {_to.RoomId} ({_to.Level.name})?");
    }

    void OnCancel()
    {
        _phase = Phase.Idle;
        _popup = null;
    }

    void OnConfirm()
    {
        _popup = null;
        _closeAttempts = 0;
        _timer = CloseRetryDelay; // close right away
        _phase = Phase.ClosingMenus;
    }

    void Update()
    {
        try { Step(); }
        catch (Exception ex)
        {
            Plugin.L.LogError(ex);
            _phase = Phase.Idle;
        }
    }

    void Step()
    {
        if (_guard != null && !_guard.Tick(Time.unscaledDeltaTime)) _guard = null;

        switch (_phase)
        {
            case Phase.Idle:
                var savePoint = MapInput.Poll();
                if (savePoint != null) RequestConfirmation(savePoint);
                break;

            case Phase.Confirming:
                _timer += Time.unscaledDeltaTime;
                // Closed without a callback (e.g. the back button): treat it as a cancel.
                if (_timer > 0.5f && (_popup == null || !_popup.visible)) OnCancel();
                else ApplyPopupTexts();
                break;

            case Phase.ClosingMenus:
                if (Game.InGameplay && !UIManager.GetInstance().confirmationPopup.visible)
                {
                    _beforeVitals = Game.ReadVitals();
                    Plugin.L.LogInfo($"vitals before: {_beforeVitals}");
                    _origin = Game.CaptureOrigin();
                    _returning = false;
                    Game.Teleport(_to, Plugin.LoadingType.Value);
                    Enter(Phase.WaitLoadStart);
                    break;
                }
                _timer += Time.unscaledDeltaTime;
                // Menus close with a short animation; ask again only if nothing happened.
                if (_timer >= CloseRetryDelay)
                {
                    if (++_closeAttempts > MaxCloseAttempts)
                    {
                        Plugin.L.LogWarning("could not close the menus; teleport cancelled");
                        _phase = Phase.Idle;
                        break;
                    }
                    CloseMenus();
                    _timer = 0;
                }
                break;

            case Phase.WaitLoadStart:
                _timer += Time.unscaledDeltaTime;
                if (Game.Loading) Enter(Phase.WaitLoadEnd);
                else if (_timer > LoadStartTimeout) Fail("the reload never started");
                break;

            case Phase.WaitLoadEnd:
                _timer += Time.unscaledDeltaTime;
                if (Game.InGameplay && !Game.Loading) Enter(Phase.Settle);
                else if (_timer > LoadEndTimeout) Fail("the level did not finish loading");
                break;

            case Phase.UsingSavePoint:
                var started = _savePointUse.Tick(Time.unscaledDeltaTime);
                if (started == null) break;
                if (started == false && Plugin.PreserveStats.Value)
                {
                    Plugin.L.LogWarning("could not use the save point; keeping HP and potions instead");
                    Game.RestoreVitals(_beforeVitals);
                }
                Finish();
                break;

            case Phase.Settle:
                _timer += Time.unscaledDeltaTime;
                // The current room can be unknown for a moment after loading; judging the arrival then
                // would trigger a needless return trip.
                if (_timer >= RestoreDelay && (Destinations.CurrentRoomId() != null || _timer >= RoomKnownTimeout))
                    Arrive();
                break;
        }
    }

    // The map lives in the game menu even though GameMenuManager.IsShowing() reports false while it is open,
    // so Close() is called unconditionally (as Mirror Fast Travel does). If that has not worked after a couple
    // of attempts, the map's own menu layer is popped the way the back button does it.
    void CloseMenus()
    {
        var ui = UIManager.GetInstance();
        if (ui == null) return;

        if (_closeAttempts <= 2)
        {
            GameMenuManager menu = null;
            var found = ui.TryGetMenuManager(out menu) && menu != null;
            Plugin.L.LogInfo($"closing the game menu (attempt {_closeAttempts}, found={found})");
            if (found) menu.Close();
            return;
        }

        var layer = ui.menuStack?.GetCurrent();
        var mapRoot = UIMapManager.Get()?.transform;
        var underMapUi = layer != null && mapRoot != null && layer.transform.IsChildOf(mapRoot);
        var namedMap = layer != null && layer.gameObject.name.IndexOf("map", StringComparison.OrdinalIgnoreCase) >= 0;
        Plugin.L.LogInfo(
            $"popping the map layer (attempt {_closeAttempts}): layer={(layer != null ? layer.gameObject.name : "none")} " +
            $"underMapUi={underMapUi} namedMap={namedMap} activeState={Game.States?.GetActiveStateId()}");
        if (underMapUi || namedMap) layer.Pop();
    }

    void Enter(Phase phase)
    {
        _phase = phase;
        _timer = 0;
    }

    void Arrive()
    {
        Game.SpawnLogic.ClearForcePosition();

        var room = Destinations.CurrentRoomId();
        if (!_returning && room != _to.RoomId)
        {
            Plugin.L.LogWarning($"expected to arrive in {_to.RoomId}, got {room}");
            _returning = true;
            Game.Return(_origin, Plugin.LoadingType.Value);
            Enter(Phase.WaitLoadStart);
            return;
        }

        SavePoint savePoint = null;
        if (!_returning)
        {
            savePoint = Destinations.SavePointIn(_to.RoomId);
            if (savePoint != null && Game.SnapTo(savePoint))
                Plugin.L.LogInfo($"snapped onto the save point at {savePoint.transform.position}");
        }

        Plugin.L.LogInfo(
            $"{(_returning ? "returned" : "arrived")} at {Game.Player.transform.position} in {room} — " +
            $"{_beforeVitals} → {Game.ReadVitals()}");

        // Arriving counts as saving there by hand: the game refills, revives monsters and saves. A return trip
        // puts the player back where they stood, not at a save point, so it never uses one.
        var useSavePoint = !_returning && Plugin.UseSavePoint.Value;
        if (useSavePoint && savePoint != null)
        {
            _savePointUse = new SavePointUse(savePoint);
            Enter(Phase.UsingSavePoint);
            return;
        }
        if (Plugin.PreserveStats.Value)
        {
            if (useSavePoint) Plugin.L.LogWarning($"no save point found in {room}; keeping HP and potions instead");
            Game.RestoreVitals(_beforeVitals);
            _guard = new RefillGuard(Game.ReadVitals());
        }
        Finish();
    }

    void Fail(string reason)
    {
        Plugin.L.LogError($"teleport to {_to?.RoomId} failed: {reason}");
        Game.SpawnLogic.ClearForcePosition();
        Finish();
    }

    void Finish()
    {
        _origin = null;
        _to = null;
        _returning = false;
        _savePointUse = null;
        _phase = Phase.Idle;
    }

    void ApplyPopupTexts()
    {
        if (_popup == null) return;
        SetText(_popup.titleLabel, Plugin.PopupTitle.Value);
        SetText(_popup.messageLabel, Plugin.PopupMessage.Value);
    }

    static void SetText(Component holder, string text)
    {
        var label = holder?.GetComponentInChildren<TMP_Text>(true);
        if (label != null && label.text != text) label.text = text;
    }
}
