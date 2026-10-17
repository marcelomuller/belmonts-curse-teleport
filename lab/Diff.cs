using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace TeleportLab;

// Compares two JSON snapshots. Arrays are compared as multisets so a list that only got
// reordered does not show up, while an item that vanished does.
internal static class Diff
{
    // Paths expected to change on any teleport/reload; reported, but not flagged.
    static readonly string[] Expected =
    {
        "playTimeInSeconds", "saveDate", "position", "levelId", "lastUsedSaveRoomId", "hasSavedSpawnPosition",
        "isFastTraveling", "previousLoadingLevel", "currentFastTravelLocalizationLevel", "lastMainMapPosition",
        "lastMainMapLevelId", "metricsValues", "gameStats", "uiState", "subBiomeHistory", "shouldLoadTemporaryPlayerState",
    };

    internal record Change(string Path, string Kind, string Before, string After, bool Expected);

    internal static List<Change> Compare(string beforeJson, string afterJson)
    {
        var a = Flatten(beforeJson);
        var b = Flatten(afterJson);
        var changes = new List<Change>();

        foreach (var path in a.Keys.Union(b.Keys).OrderBy(p => p, StringComparer.Ordinal))
        {
            a.TryGetValue(path, out var va);
            b.TryGetValue(path, out var vb);
            if (va == vb) continue;

            var kind = Classify(path, va, vb);
            var expected = Expected.Any(e => path == e || path.StartsWith(e + ".") || path.StartsWith(e + "[]"));
            changes.Add(new Change(path, kind, va ?? "∅", vb ?? "∅", expected));
        }
        return changes;
    }

    static string Classify(string path, string before, string after)
    {
        if (path.Contains("[]="))
            return after == null ? "LOST" : before == null ? "gained" : "changed";
        if (before == null) return "added";
        if (after == null) return "LOST";
        if (double.TryParse(before, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
            double.TryParse(after, NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
            return y < x ? "DECREASED" : "increased";
        if (before == "true" && after == "false") return "FLAG CLEARED";
        return "changed";
    }

    internal static string Report(string title, List<Change> changes)
    {
        var sb = new StringBuilder();
        var flagged = changes.Where(c => !c.Expected).ToList();
        sb.AppendLine($"# {title}");
        sb.AppendLine($"{changes.Count} differences, {flagged.Count} outside the expected set");
        sb.AppendLine();
        sb.AppendLine("## Unexpected");
        foreach (var c in flagged) sb.AppendLine(Line(c));
        sb.AppendLine();
        sb.AppendLine("## Expected (position, playtime, ...)");
        foreach (var c in changes.Where(c => c.Expected)) sb.AppendLine(Line(c));
        return sb.ToString();
    }

    static string Line(Change c) => $"[{c.Kind}] {c.Path}: {Trim(c.Before)} → {Trim(c.After)}";

    static string Trim(string s) => s.Length > 160 ? s[..160] + "…" : s;

    // Objects flatten to dotted paths. Each array element becomes a key "path[]=<element json>" whose
    // value is its occurrence count, which turns array comparison into a multiset comparison.
    static Dictionary<string, string> Flatten(string json)
    {
        var result = new Dictionary<string, string>();
        using var doc = JsonDocument.Parse(json);
        Walk(doc.RootElement, "", result);
        return result;
    }

    static void Walk(JsonElement e, string path, Dictionary<string, string> into)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var p in e.EnumerateObject())
                    Walk(p.Value, path.Length == 0 ? p.Name : $"{path}.{p.Name}", into);
                break;
            case JsonValueKind.Array:
                var counts = new Dictionary<string, int>();
                foreach (var item in e.EnumerateArray())
                {
                    var raw = item.GetRawText();
                    counts[raw] = counts.TryGetValue(raw, out var n) ? n + 1 : 1;
                }
                into[$"{path}.length"] = counts.Values.Sum().ToString();
                foreach (var (raw, n) in counts)
                    into[$"{path}[]={raw}"] = n.ToString();
                break;
            default:
                into[path] = e.ValueKind == JsonValueKind.String ? e.GetString() : e.GetRawText();
                break;
        }
    }
}
