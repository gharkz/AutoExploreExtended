using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using Elin_AutoExplore;
using HarmonyLib;
using Newtonsoft.Json;

namespace AutoExploreExtended
{
    internal enum MaterialFilter { Off, Blacklist, Whitelist }

    // Material filter for AutoExplore's pickaxe work (mining skill: walls, veins, rocks) and axe work (lumberjack
    // skill: trees, wood). Each tool has a mode (Off / Blacklist / Whitelist) and its own named blacklists and
    // whitelists; one of each is active. blacklist_1 and whitelist_1 always exist. Lists live in a JSON file
    // beside the config; modes and active names are config entries. Applied where AutoExplore builds its mining
    // and harvest tasks, on top of its name lists.
    internal static class Materials
    {
        private const int Mining = 220, Lumberjack = 225, Digging = 230;

        internal sealed class ToolLists
        {
            public Dictionary<string, List<string>> Blacklists = new Dictionary<string, List<string>>();
            public Dictionary<string, List<string>> Whitelists = new Dictionary<string, List<string>>();
            public string ActiveBlacklist = "blacklist_1", ActiveWhitelist = "whitelist_1";
        }

        private sealed class Data { public ToolLists Pickaxe = new ToolLists(), Axe = new ToolLists(), Shovel = new ToolLists(); }

        private static Data data;
        private static string file;
        private static ConfigEntry<MaterialFilter> pickMode, axeMode, digMode;
        private static HashSet<string> pickActive = new HashSet<string>(), axeActive = new HashSet<string>(), digActive = new HashSet<string>();

        internal static void Bind(ConfigFile c)
        {
            pickMode = c.Bind("Materials", "PickaxeFilter", MaterialFilter.Off, "Pickaxe work (walls, veins, rocks): Off, Blacklist (skip the active blacklist) or Whitelist (only the active whitelist).");
            axeMode = c.Bind("Materials", "AxeFilter", MaterialFilter.Off, "Axe work (trees, wood): Off, Blacklist or Whitelist.");
            digMode = c.Bind("Materials", "ShovelFilter", MaterialFilter.Off, "Shovel work (floors, tracks): Off, Blacklist or Whitelist.");
            file = Path.Combine(Path.GetDirectoryName(c.ConfigFilePath), "gharkz.autoexploreextended.materials.json");
            try { data = File.Exists(file) ? JsonConvert.DeserializeObject<Data>(File.ReadAllText(file)) : null; } catch { data = null; }
            data = data ?? new Data();
            if (data.Shovel == null) data.Shovel = new ToolLists();
            Ensure(data.Pickaxe); Ensure(data.Axe); Ensure(data.Shovel);
            Refresh();
        }

        private static void Ensure(ToolLists t)
        {
            if (t.Blacklists.Count == 0) t.Blacklists["blacklist_1"] = new List<string>();
            if (t.Whitelists.Count == 0) t.Whitelists["whitelist_1"] = new List<string>();
            if (!t.Blacklists.ContainsKey(t.ActiveBlacklist ?? "")) t.ActiveBlacklist = t.Blacklists.Keys.First();
            if (!t.Whitelists.ContainsKey(t.ActiveWhitelist ?? "")) t.ActiveWhitelist = t.Whitelists.Keys.First();
        }

        private static void Save()
        {
            Refresh();
            try { File.WriteAllText(file, JsonConvert.SerializeObject(data, Formatting.Indented)); }
            catch (Exception e) { UnityEngine.Debug.LogException(e); }
        }

        // The active list for each tool's current mode, as material ids.
        private static void Refresh()
        {
            pickActive = Active(data.Pickaxe, pickMode.Value);
            axeActive = Active(data.Axe, axeMode.Value);
            digActive = Active(data.Shovel, digMode.Value);
        }

        private static HashSet<string> Active(ToolLists t, MaterialFilter mode) => new HashSet<string>(
            (mode == MaterialFilter.Whitelist ? t.Whitelists[t.ActiveWhitelist] : mode == MaterialFilter.Blacklist ? t.Blacklists[t.ActiveBlacklist] : new List<string>()).Select(Key));

        // Entries may be the game's material name ("birch") or its id ("wood_birch"); both resolve to the id.
        private static Dictionary<string, string> byName;
        private static string Key(string s)
        {
            var k = s.Trim().ToLowerInvariant();
            if (byName == null && EClass.sources?.materials != null)
            {
                byName = new Dictionary<string, string>();
                foreach (var r in EClass.sources.materials.rows)
                    if (!string.IsNullOrEmpty(r.alias)) { var n = r.GetName().Trim().ToLowerInvariant(); if (!byName.ContainsKey(n)) byName[n] = r.alias.ToLowerInvariant(); }
            }
            return byName != null && byName.TryGetValue(k, out var alias) ? alias : k;
        }

        // The material the game itself uses for this harvest (same choice as BaseTaskHarvest.SetTarget).
        private static SourceMaterial.Row Of(BaseTaskHarvest t)
        {
            var p = t.pos;
            switch (t.harvestType)
            {
                case BaseTaskHarvest.HarvestType.Obj: return p.cell.isObjDyed ? p.sourceObj.DefaultMaterial : p.cell.matObj;
                case BaseTaskHarvest.HarvestType.Thing: return t.target?.material;
                case BaseTaskHarvest.HarvestType.Floor: return p.cell.HasBridge ? p.matBridge : p.matFloor;
                case BaseTaskHarvest.HarvestType.Block: return p.HasBlock ? p.matBlock : p.matFloor;
                default: return null;
            }
        }

        internal static bool Allowed(BaseTaskHarvest t)
        {
            if (t == null) return true;
            MaterialFilter mode; HashSet<string> set;
            if (t.idEle == Mining) { mode = pickMode.Value; set = pickActive; }
            else if (t.idEle == Lumberjack) { mode = axeMode.Value; set = axeActive; }
            else if (t.idEle == Digging) { mode = digMode.Value; set = digActive; }
            else return true;
            if (mode == MaterialFilter.Off) return true;
            var mat = Of(t);
            if (mat == null) return true;
            bool listed = set.Contains(mat.alias.ToLowerInvariant());
            return mode == MaterialFilter.Blacklist ? !listed : listed;
        }

        [HarmonyPatch(typeof(AIActionFinder), "GetMineTask")]
        private static class MinePatch { private static void Postfix(ref TaskMine __result) { if (__result != null && !Allowed(__result)) __result = null; } }

        [HarmonyPatch(typeof(AIActionFinder), "GetHarvestTask")]
        private static class HarvestPatch { private static void Postfix(ref TaskHarvest __result) { if (__result != null && !Allowed(__result)) __result = null; } }

        // ---- Settings window -------------------------------------------------------------------------------------

        // Rows for the Tools tab's Materials section. refresh rebuilds the settings window.
        internal static IEnumerable<UiRow> Rows(Action refresh)
        {
            // What this section does, up front (the heading alone reads as "Materials? Huh...").
            // One row per sentence: list rows have a fixed height, so a two-line row would crowd its neighbours.
            yield return new UiRow { Label = L.T("AutoExplore skips targets whose material does not pass the selected tool's filter."), Note = true, Info = true };
            yield return new UiRow { Label = L.T("Blacklist skips the listed materials; Whitelist takes only them."), Note = true, Info = true };
            foreach (var (label, mode, lists, kind) in new[] { ("Pickaxe", pickMode, data.Pickaxe, "pick"), ("Axe", axeMode, data.Axe, "axe"), ("Shovel", digMode, data.Shovel, "dig") })
            {
                yield return new UiRow { Label = L.T(label) + L.T(kind == "axe" ? " (trees, wood)" : kind == "dig" ? " (floors, tracks)" : " (walls, veins, rocks)"), Heading = true };
                yield return new UiRow
                {
                    Label = L.T("Filter"), Cells = new[] { "", L.T(mode.Value.ToString()), "" },
                    CellClick = new Action<UiRow, int>[] { null, (r, _) => UiTable.Choose(L.F("{0} filter", L.T(label)),
                        Enum.GetValues(typeof(MaterialFilter)).Cast<MaterialFilter>().Select(m => (L.T(m.ToString()), (Action)(() => { mode.Value = m; Refresh(); refresh(); }))).ToList(),
                        (int)mode.Value), null },
                };
                yield return ListRow("Blacklist", lists, false, kind, label, refresh);
                yield return ListRow("Whitelist", lists, true, kind, label, refresh);
            }
        }

        private static UiRow ListRow(string kind, ToolLists t, bool white, string axe, string tool, Action refresh) => new UiRow
        {
            Label = L.T(kind),
            Cells = new[] { "", white ? t.ActiveWhitelist : t.ActiveBlacklist, "" },
            CellClick = new Action<UiRow, int>[] { null, (r, _) => Manage(tool, kind, t, white, axe, refresh), null },
        };

        // The lists of one kind: click a name to make it active; Edit / Rename / Delete per list; New list below.
        private static void Manage(string tool, string kind, ToolLists t, bool white, string axe, Action refresh)
        {
            UiTable.Window w = null;
            Action reopen = () => { w?.Layer.Close(); refresh(); Manage(tool, kind, t, white, axe, refresh); };
            var lists = white ? t.Whitelists : t.Blacklists;
            string active = white ? t.ActiveWhitelist : t.ActiveBlacklist;
            var rows = new List<UiRow> { new UiRow { Label = L.T("List"), ColumnHeader = true, Cells = new[] { L.T("Materials"), "", "", "" } } };
            foreach (var name in lists.Keys.OrderBy(n => n).ToList())
            {
                var n = name;
                rows.Add(new UiRow
                {
                    Label = n, Checked = n == active,
                    Click = _ => { if (white) t.ActiveWhitelist = n; else t.ActiveBlacklist = n; Save(); reopen(); },
                    Cells = new[] { lists[n].Count == 0 ? L.T("(empty)") : L.F("{0} materials", lists[n].Count), L.T("Edit"), L.T("Rename"), L.T("Delete") },
                    GreyCells = new[] { false, false, false, lists.Count == 1 },
                    CellClick = new Action<UiRow, int>[]
                    {
                        null,
                        (r, _) => TextArea.Open(L.T(tool) + " " + L.T(kind) + ": " + n, string.Join(",", lists[n]),
                            text => { lists[n] = TextArea.Entries(text); Save(); reopen(); }, Hint(white), Options(lists[n], axe)),
                        (r, _) => AskName(L.F("Rename {0}", n), n, lists, nn =>
                        {
                            lists[nn] = lists[n]; lists.Remove(n);
                            if (white && t.ActiveWhitelist == n) t.ActiveWhitelist = nn;
                            if (!white && t.ActiveBlacklist == n) t.ActiveBlacklist = nn;
                            Save(); reopen();
                        }),
                        (r, _) =>
                        {
                            if (lists.Count == 1) return; // greyed out: one list always remains
                            Dialog.YesNo(L.F("Delete the list \"{0}\"?", n), () => { lists.Remove(n); Ensure(t); Save(); reopen(); });
                        },
                    },
                    Disabled = null,
                });
            }
            string example = axe == "axe" ? (white ? "cedar_and_birch_only" : "no_rosewood_and_oak")
                : axe == "pick" ? (white ? "mithril_and_adamantite_only" : "no_granite_or_marble")
                : (white ? "soil_only" : "no_grass_or_sand");
            string note = L.F("Click a list to make it active.\nYou can't delete the last list; keep at least one of each kind.\nName lists for what they do, e.g. {0}.", L.T(example));
            w = UiTable.Open(L.F(white ? "{0} whitelists" : "{0} blacklists", L.T(tool)), rows, new[] { 0f, .36f, .62f, .74f, .88f, 1f }, 640,
                UnityEngine.Mathf.Min(280 + rows.Count * 34, 680), footNote: note, corner: new List<(string, Action)>
                {
                    (L.T("New List"), () => AskName(L.T("New list"), (white ? "whitelist_" : "blacklist_") + (lists.Count + 1), lists, nn =>
                    {
                        lists[nn] = new List<string>();
                        if (white) t.ActiveWhitelist = nn; else t.ActiveBlacklist = nn;
                        Save(); reopen();
                    })),
                });
        }

        private static string Hint(bool white) => white
            ? L.T("Whitelist: only these materials are taken. Comma separated; names (birch) or ids (wood_birch).")
            : L.T("Blacklist: these materials are skipped. Comma separated; names (birch) or ids (wood_birch).");

        private static void AskName(string title, string start, Dictionary<string, List<string>> lists, Action<string> ok)
        {
            Dialog.InputName(title, start, (cancel, text) =>
            {
                if (cancel) return;
                var n = (text ?? "").Trim();
                if (n.Length == 0 || n.Length > 40 || lists.ContainsKey(n)) { Msg.SayRaw(L.T("That name is empty, too long or already used.")); return; }
                ok(n);
            });
        }

        private static TextAreaOptions Options(List<string> current, string tool)
        {
            var all = EClass.sources.materials.rows.Where(r => !string.IsNullOrEmpty(r.alias) && r.id != 0).ToList();
            var known = new HashSet<string>(all.Select(r => r.alias.ToLowerInvariant()));
            string[] first = tool == "axe" ? new[] { "wood" } : tool == "dig" ? new string[0] : new[] { "ore", "rock", "gem", "crystal", "soil" };
            return new TextAreaOptions
            {
                Key = Key,
                Validate = text =>
                {
                    var bad = TextArea.Entries(text).Where(n => !known.Contains(Key(n))).ToList();
                    return bad.Count == 0 ? null : L.F("Unknown material: {0}. Pick from the checklist.", string.Join(", ", bad.Take(6)));
                },
                Choices = () =>
                {
                    var sections = new List<(string, List<string>)> { (L.T("In the list"), current.ToList()) };
                    if (tool == "dig") // suggested floors to leave alone: grass, soil and sand
                        sections.Add((L.T("Common to skip"), all.Where(r => r.category == "grass" || r.category == "soil" || r.alias.Contains("sand")).Select(r => r.GetName()).ToList()));
                    foreach (var cat in first) sections.Add((L.T(Title(cat)), all.Where(r => r.category == cat).Select(r => r.GetName()).ToList()));
                    sections.Add((L.T("Everything else"), all.Select(r => r.GetName()).ToList()));
                    return sections;
                },
            };
        }

        private static string Title(string cat) =>
            cat == "wood" ? "Woods" : cat == "ore" ? "Metals and ores" : cat == "rock" ? "Stones" : cat == "gem" ? "Gems" :
            cat == "crystal" ? "Crystals" : cat == "soil" ? "Soils" : cat;
    }
}
