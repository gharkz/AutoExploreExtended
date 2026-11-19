using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using BepInEx.Configuration;
using Elin_AutoExplore;
using Explorer = Elin_AutoExplore.Plugin;

namespace AutoExploreExtended
{
    // AutoExplore's three restriction lists: checklist choices, syntax check, and a live reload (AutoExplore parses
    // the lists only at startup, so after a save its IgnoreList is rebuilt from the config entries).
    internal static class Restrictions
    {
        // Names are words: letters, digits, spaces, and the punctuation Elin names use ("metal block(copper)").
        private static readonly Regex Bad = new Regex(@"[^\p{L}\p{N}\s,()'\-.&]");

        internal static TextAreaOptions For(ConfigEntryBase e)
        {
            var cfg = Explorer.Instance?.AutoExplorerConfig;
            if (cfg == null) return null;
            Func<List<string>> known;
            if (e == cfg.GatheringRestrictionList) known = Gathering;
            else if (e == cfg.MiningRestrictionList) known = Mining;
            else if (e == cfg.PickupRestrictionList) known = Pickup;
            else if (e == OwnLand.List) known = Ground;
            else return null;

            var atOpen = TextArea.Entries((string)e.BoxedValue);
            List<string> cache = null;
            Func<List<string>> choices = () => cache ?? (cache = Safe(known));
            return new TextAreaOptions
            {
                Key = IgnoreNames.Key,
                Validate = text =>
                {
                    var bad = Bad.Matches(text).Cast<Match>().Select(m => m.Value).Distinct().ToList();
                    return bad.Count == 0 ? null : L.F("Not allowed: {0}. Use names separated by commas.", string.Join(" ", bad.Select(c => "\"" + c + "\"")));
                },
                Warn = text =>
                {
                    var keys = new HashSet<string>(choices().Concat(atOpen).Select(IgnoreNames.Key));
                    var unknown = TextArea.Entries(text).Where(n => !keys.Contains(IgnoreNames.Key(n))).ToList();
                    return unknown.Count == 0 ? null : L.F("Not seen here (kept anyway): {0}", string.Join(", ", unknown.Take(6)) + (unknown.Count > 6 ? "…" : ""));
                },
                Choices = () => new List<(string, List<string>)>
                {
                    (L.T("In the list"), TextArea.Entries((string)e.BoxedValue)),
                    (L.T(e == OwnLand.List ? "Suggested (common on this map)" : "Common to skip"), Useful(e, cfg, choices())),
                    (L.T("Everything else"), choices()),
                },
            };
        }

        // "Common to skip": frequent clutter. Gathering names are offered only when the game has them (plus AutoExplore's
        // own defaults); mining offers this map's most common walls; the pickup examples come from AutoExplore's help text.
        private static readonly string[] GatherUseful = { "wreck", "chemicals", "wall frame", "debris", "moss grass", "vine", "pebble",
            "stalagmite", "chunk", "stone", "rock", "big rock", "grass", "tall grass", "weed", "bush", "fern", "mushroom", "moss" };
        private static readonly string[] PickupUseful = { "rock", "tuft of grass", "pebble", "stone", "branch", "bone", "junk" };

        private static List<string> Useful(ConfigEntryBase e, AutoExplorerConfig cfg, List<string> known)
        {
            if (e == cfg.PickupRestrictionList) return PickupUseful.ToList();
            if (e == OwnLand.List) return known.GroupBy(n => n).OrderByDescending(g => g.Count()).Take(12).Select(g => g.Key).ToList();
            if (e == cfg.MiningRestrictionList)
            {
                var objs = new HashSet<string>(MiningObjs().Select(IgnoreNames.Key));
                return CommonWalls().Concat(GatherUseful.Where(n => objs.Contains(IgnoreNames.Key(n)))).ToList();
            }
            var keys = new HashSet<string>(known.Select(IgnoreNames.Key));
            var defaults = TextArea.Entries((string)e.DefaultValue);
            return GatherUseful.Where(n => keys.Contains(IgnoreNames.Key(n))).Concat(defaults).ToList();
        }

        private static List<string> Safe(Func<List<string>> f) { try { return f(); } catch (Exception x) { UnityEngine.Debug.LogException(x); return new List<string>(); } }

        // This map's walls and floors (home whitelist), as the home rules read them.
        private static List<string> Ground()
        {
            var names = new List<string>();
            if (EClass._map != null)
                MapScan.Playable(c => { if (c.HasBlock) names.Add(c.GetBlockName()); else if (c._floor != 0 && !c.sourceFloor.tileType.IsWater) names.Add(c.GetFloorName()); }); // water is never dug
            return names;
        }

        // Plants, trees, rocks and other map objects (AutoExplore matches their row names), on this map first.
        private static List<string> Gathering()
        {
            var names = new List<string>();
            if (EClass._map != null)
                MapScan.Playable(c => { if (c.obj != 0) names.Add(c.sourceObj.GetName()); });
            names.AddRange(EClass.sources.objs.rows.Select(r => r.GetName()));
            return names;
        }

        // Everything a pickaxe works on: this map's walls (block name with its material, as AutoExplore reads them),
        // plus every object the game harvests with mining (metal frames, veins, stalagmites...).
        private static List<string> Mining()
        {
            var names = new List<string>();
            if (EClass._map != null)
                MapScan.Playable(c => { if (c.HasBlock) names.Add(c.GetBlockName()); });
            names.AddRange(MiningObjs());
            return names;
        }

        private static IEnumerable<string> MiningObjs() => EClass.sources.objs.rows
            .Where(r => r.reqHarvest != null && r.reqHarvest.Length > 0 && r.reqHarvest[0] == "mining").Select(r => r.GetName());

        // Items lying on this map, by both names AutoExplore checks.
        private static List<string> Pickup()
        {
            var names = new List<string>();
            if (EClass._map != null)
                foreach (var t in EClass._map.things)
                    if (!t.isNPCProperty && !t.IsInstalled) { names.Add(t.GetName(NameStyle.Simple, 1)); names.Add(t.GetName(NameStyle.Full, -1)); }
            return names;
        }

        // The walls this map is mostly made of (usually soil or rock filler, rarely ore): the 8 most common block names.
        private static List<string> CommonWalls()
        {
            var count = new Dictionary<string, int>();
            if (EClass._map != null)
                MapScan.Playable(c => { if (c.HasBlock) { var n = c.GetBlockName(); count.TryGetValue(n, out var k); count[n] = k + 1; } });
            return count.OrderByDescending(kv => kv.Value).Take(8).Select(kv => kv.Key).ToList();
        }

        // Rebuild AutoExplore's parsed lists from its config entries so saved edits apply now.
        internal static void Reload()
        {
            var p = Explorer.Instance; var cfg = p?.AutoExplorerConfig;
            if (cfg == null) return;
            typeof(Explorer).GetProperty("IgnoreList").SetValue(p,
                new IgnoreList(cfg.GatheringRestrictionList, cfg.MiningRestrictionList, cfg.PickupRestrictionList));
        }
    }
}
