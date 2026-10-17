using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TeleportLab;

// Runs test cases: snapshot → (teleport) → wait for the level to load → snapshot → diff → report.
public class Driver : MonoBehaviour
{
    public Driver(IntPtr ptr) : base(ptr) { }

    enum Phase { Idle, WaitLoadStart, WaitLoadEnd, Settle }

    // null mode = control run (no teleport) to measure what changes by itself.
    readonly Queue<MainGameLoadingType?> _queue = new();
    int _modeIndex;
    Phase _phase;
    float _timer;
    MainGameLoadingType? _current;
    Vector3 _targetPosition;
    string _target, _beforePlayer, _beforeWorld, _beforeDisk, _status = "idle";

    static readonly string OutDir = Path.Combine(Paths.BepInExRootPath, "TeleportLab");

    MainGameLoadingType SelectedMode => Game.TestModes[_modeIndex];

    void Update()
    {
        try
        {
            HandleKeys();
            Step();
        }
        catch (Exception ex)
        {
            Plugin.L.LogError(ex);
            _status = "error: " + ex.Message;
            _queue.Clear();
            _phase = Phase.Idle;
        }
    }

    void HandleKeys()
    {
        var kb = Keyboard.current;
        if (kb == null || _phase != Phase.Idle) return;

        if (kb.f5Key.wasPressedThisFrame)
        {
            Probe.DumpSpawners();
            _status = "spawners dumped to the log";
            return;
        }
        if (!Plugin.TestKeys.Value) return;

        if (kb.f7Key.wasPressedThisFrame)
        {
            _modeIndex = (_modeIndex + 1) % Game.TestModes.Length;
            _status = $"mode: {SelectedMode}";
        }
        else if (kb.f6Key.wasPressedThisFrame) Enqueue(new MainGameLoadingType?[] { null });
        else if (kb.f8Key.wasPressedThisFrame) Enqueue(new MainGameLoadingType?[] { SelectedMode });
        else if (kb.f9Key.wasPressedThisFrame)
        {
            var all = new List<MainGameLoadingType?> { null };
            foreach (var m in Game.TestModes) all.Add(m);
            Enqueue(all);
        }
    }

    [HideFromIl2Cpp]
    void Enqueue(IEnumerable<MainGameLoadingType?> cases)
    {
        if (!Game.InGameplay)
        {
            _status = "not in gameplay — load a save and close menus first";
            return;
        }
        foreach (var c in cases) _queue.Enqueue(c);
    }

    void Step()
    {
        switch (_phase)
        {
            case Phase.Idle:
                if (_queue.Count > 0 && Game.InGameplay) Begin(_queue.Dequeue());
                break;

            case Phase.WaitLoadStart:
                _timer += Time.unscaledDeltaTime;
                if (Game.Loading) { _phase = Phase.WaitLoadEnd; _timer = 0; }
                else if (_timer > 10f) Finish("load never started (mode ignored?)");
                break;

            case Phase.WaitLoadEnd:
                _timer += Time.unscaledDeltaTime;
                if (Game.InGameplay && !Game.Loading) { _phase = Phase.Settle; _timer = 0; }
                else if (_timer > 120f) Finish("load did not finish in 120s");
                break;

            case Phase.Settle:
                _timer += Time.unscaledDeltaTime;
                if (_timer >= Plugin.SettleSeconds.Value) Finish(null);
                break;
        }
    }

    void Begin(MainGameLoadingType? mode)
    {
        _current = mode;
        if (mode != null && !HoverPatches.HoveredIsValid)
        {
            _status = "hover a used save room of this area on the map first";
            _queue.Clear();
            return;
        }
        _target = HoverPatches.HoveredRoomId ?? Game.LastUsedSaveRoomId;
        _targetPosition = HoverPatches.HoveredPosition;
        _beforePlayer = Game.LivePlayerJson();
        _beforeDisk = Game.DiskSaveHash();
        _timer = 0;

        if (mode == null)
        {
            _beforeWorld = Game.WorldJson();
            _status = "control run (no teleport)…";
            _phase = Phase.Settle;
            return;
        }

        _status = $"teleporting with {mode}…";
        // World snapshot after the room is saved, so it reflects what the teleport hands to the reload.
        Game.Prepare(_target, _targetPosition);
        _beforeWorld = Game.WorldJson();
        Game.Reload(_target, mode.Value);
        _phase = Phase.WaitLoadStart;
    }

    void Finish(string failure)
    {
        var label = _current?.ToString() ?? "Control";
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(OutDir);
        var path = Path.Combine(OutDir, $"{stamp}_{label}.md");

        string summary;
        if (failure != null)
        {
            File.WriteAllText(path, $"# {label}\nFAILED: {failure}\n");
            summary = $"{label}: FAILED — {failure}";
        }
        else
        {
            var afterPlayer = Game.LivePlayerJson();
            var afterWorld = Game.WorldJson();
            var afterDisk = Game.DiskSaveHash();
            var player = Diff.Compare(_beforePlayer, afterPlayer);
            var world = Diff.Compare(_beforeWorld, afterWorld);
            var landed = Game.LastUsedSaveRoomId;

            var at = Game.Player.transform.position;
            var header =
                $"target room: {_target}\nlastUsedSaveRoomId after: {landed}\n" +
                (_current == null ? "" : $"target position: {_targetPosition}  landed at: {at}  distance: {Vector3.Distance(at, _targetPosition):0.00}\n") +
                $"disk save: {_beforeDisk} → {afterDisk}{(_beforeDisk != afterDisk ? "  (GAME WROTE THE SAVE FILE)" : "")}\n" +
                $"entry: {Plugin.Entry.Value}, prepPlayer={Plugin.PrepPlayer.Value}, prepWorld={Plugin.PrepWorld.Value}\n\n";
            File.WriteAllText(path, header + Diff.Report($"{label} — player", player) + "\n" + Diff.Report($"{label} — world", world));
            File.WriteAllText(Path.ChangeExtension(path, ".before.json"), _beforePlayer);
            File.WriteAllText(Path.ChangeExtension(path, ".after.json"), afterPlayer);

            int Flagged(List<Diff.Change> cs) => cs.FindAll(c => !c.Expected).Count;
            summary = $"{label}: player {Flagged(player)} unexpected / {player.Count} total, world {Flagged(world)} unexpected / {world.Count} total";
        }

        Plugin.L.LogMessage($"[result] {summary} → {path}");
        _status = summary;
        _phase = Phase.Idle;
    }

    void OnGUI()
    {
        var keys = Plugin.TestKeys.Value ? $"F6 control | F7 mode: {SelectedMode} | F8 run | F9 run all" : "test keys off";
        var text = $"TeleportLab  F5 dump spawners | {keys}\n" +
                   $"target: {HoverPatches.HoveredRoomId ?? "(hover a save room on the map)"}{(HoverPatches.HoveredRoomId != null && !HoverPatches.HoveredIsValid ? " — not a valid target" : "")}   queue: {_queue.Count}\n{_status}";
        GUI.Label(new Rect(12, 12, 900, 70), text);
    }
}
