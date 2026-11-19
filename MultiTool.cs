using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using Elin_AutoExplore;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;
using Explorer = Elin_AutoExplore.Plugin;

namespace AutoExploreExtended
{
    // Multi-tool mode: one pass over the map doing every job a preset allows, swapping to the best carried tool for
    // each target (the same hot-item swap the track digging uses). Presets are named sets of tools; our own hotkey
    // cycles Off -> each preset in the cycle -> Off. Targets are found here from the game's own data (what skill an
    // object or wall needs, growth stages, locks, shearable animals), not from AutoExplore's search.
    internal static class MultiTool
    {
        internal enum Kind { Sickle, Axe, Pickaxe, Shovel, Shears, Lockpick, Crowbar }
        internal static readonly Kind[] All = (Kind[])Enum.GetValues(typeof(Kind));
        private const int Mining = 220, Lumberjack = 225, DigSkill = 230;

        internal sealed class Preset { public List<string> Tools = All.Select(k => k.ToString()).ToList(); public bool InCycle = true; }
        private sealed class Data
        {
            public Dictionary<string, Preset> Presets = new Dictionary<string, Preset>();
            public List<string> Order = new List<string>();   // display and cycle order (the arrows)
            public string Active = "";
        }
        internal enum CycleStyle { ModeKey, OwnKey }

        private static Data data;
        private static string file;
        internal static ConfigEntry<CycleStyle> style;
        internal static ConfigEntry<KeyboardShortcut> ownKey;

        internal static void Bind(ConfigFile c)
        {
            style = c.Bind("Multi-tool", "CycleWith", CycleStyle.ModeKey,
                "AutoExplore Toggle Hotkey: presets come after Combined when you cycle AutoExplore's modes. Multi-Tool Toggle Hotkey: a key of its own cycles Off and the presets, and AutoExplore's mode key goes back to its own modes.");
            ownKey = c.Bind("Multi-tool", "OwnKey", KeyboardShortcut.Empty,
                "The key that cycles Off and the presets, when presets use the Multi-Tool Toggle Hotkey.");
            file = Path.Combine(Path.GetDirectoryName(c.ConfigFilePath), Plugin.Id + ".multitool.json");
            try { data = File.Exists(file) ? JsonConvert.DeserializeObject<Data>(File.ReadAllText(file), new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace }) : null; } catch { data = null; }
            data = data ?? new Data();
            if (data.Presets.Count == 0) data.Presets["all_tools"] = new Preset { InCycle = false }; // off until the player turns it on: no surprise mode in the cycle
            foreach (var p in data.Presets.Values) p.Tools = (p.Tools ?? new List<string>()).Distinct().Where(t => Enum.IsDefined(typeof(Kind), t)).ToList(); // older files doubled the list
            data.Order = (data.Order ?? new List<string>()).Where(data.Presets.ContainsKey).Distinct().ToList();
            data.Order.AddRange(data.Presets.Keys.Where(k => !data.Order.Contains(k)).OrderBy(k => k));
            if (!data.Presets.ContainsKey(data.Active ?? "")) data.Active = "";
            Save();
        }

        internal static void Save()
        {
            try { File.WriteAllText(file, JsonConvert.SerializeObject(data, Formatting.Indented)); }
            catch (Exception e) { Debug.LogException(e); }
        }

        internal static IReadOnlyDictionary<string, Preset> Presets => data.Presets;
        internal static IReadOnlyList<string> Order => data.Order;
        internal static string ActiveName => data.Active;
        internal static Preset Current => data != null && data.Presets.TryGetValue(data.Active ?? "", out var p) ? p : null;
        internal static bool Uses(Kind k) => Current != null && Current.Tools.Contains(k.ToString());

        internal static bool Active => Current != null && Explorer.Instance != null && Explorer.Instance.IsRunning &&
            EClass.pc != null && !EClass.pc.isDead && EClass._zone != null && !EClass._zone.IsRegion;

        // Mode key style: Explore -> Gather -> Mining -> Combined -> each preset -> Explore. Own key style: the separate
        // key cycles Off -> each preset -> Off. Either way, while a preset is active AutoExplore stays in Combined
        // underneath so every search runs, and leaving multi-tool returns it to plain Explore.
        private static readonly System.Reflection.MethodInfo getMode = AccessTools.Method(typeof(AutoExplorerConfig), "GetMode");
        private static readonly System.Reflection.MethodInfo setMode = AccessTools.Method(typeof(AutoExplorerConfig), "SetMode");
        private static object AeMode(string name) => Enum.Parse(getMode.ReturnType, name);
        private static AutoExplorerConfig Cfg => Explorer.Instance.AutoExplorerConfig;
        private static List<string> CycleOrder => data.Order.Where(n => data.Presets[n].InCycle).ToList();

        internal static void SetActive(string name)
        {
            data.Active = name != null && data.Presets.ContainsKey(name) ? name : "";
            Save();
            if (setMode != null) setMode.Invoke(Cfg, new[] { AeMode(data.Active != "" ? "HarvestAndMine" : "Explore") });
            EClass.pc?.TalkRaw(data.Active == "" ? Translations.GetTranslation("Exploring mode") : L.F("Multi-tool: {0}", data.Active));
        }

        // The separate key: Off -> each preset in the cycle -> Off.
        internal static void CycleOwn()
        {
            var order = new List<string> { "" }; order.AddRange(CycleOrder);
            SetActive(order[(order.IndexOf(data.Active ?? "") + 1) % order.Count]);
        }

        [HarmonyPatch(typeof(AutoExplorerConfig), nameof(AutoExplorerConfig.SetNextMode))]
        private static class ModeCycle
        {
            private static bool Prefix(AutoExplorerConfig __instance)
            {
                if (getMode == null || setMode == null) return true;
                if (style.Value == CycleStyle.OwnKey)
                {
                    if (data.Active == "") return true;  // AutoExplore's own modes, untouched
                    SetActive(""); return false;         // leave multi-tool: back to Explore
                }
                var order = CycleOrder;
                if (data.Active != "")
                {
                    int i = order.IndexOf(data.Active);
                    SetActive(i >= 0 && i + 1 < order.Count ? order[i + 1] : "");
                    return false;
                }
                if (order.Count > 0 && getMode.Invoke(__instance, null).ToString() == "HarvestAndMine") { SetActive(order[0]); return false; }
                return true;
            }
        }

        internal static void Move(string name, int by)
        {
            int i = data.Order.IndexOf(name), j = i + by;
            if (i < 0 || j < 0 || j >= data.Order.Count) return;
            data.Order[i] = data.Order[j]; data.Order[j] = name;
            Save();
        }

        internal static void Rename(string from, string to)
        {
            data.Presets[to] = data.Presets[from]; data.Presets.Remove(from);
            data.Order[data.Order.IndexOf(from)] = to;
            if (data.Active == from) data.Active = to;
            Save();
        }

        internal static void Delete(string name)
        {
            if (data.Presets.Count <= 1) return;
            data.Presets.Remove(name); data.Order.Remove(name);
            if (data.Active == name) data.Active = "";
            Save();
        }

        internal static void Add(string name) { data.Presets[name] = new Preset(); data.Order.Add(name); Save(); }

        // ---- Tools ---------------------------------------------------------------------------------------------

        // The best carried tool of each kind, refreshed once per frame (searches ask for it on every tile).
        private static readonly Dictionary<Kind, Thing> best = new Dictionary<Kind, Thing>();
        private static int bestFrame = -1;

        internal static Thing Best(Kind k)
        {
            if (bestFrame != Time.frameCount)
            {
                bestFrame = Time.frameCount; best.Clear();
                var all = EClass.pc.things.List(t => t.trait is TraitTool || t.trait is TraitLockpick || t.HasElement(Mining) || t.HasElement(Lumberjack) || t.HasElement(DigSkill), true);
                foreach (var kind in All) best[kind] = Pick(kind, all);
            }
            return best.TryGetValue(k, out var t) ? t : null;
        }

        private static Thing Pick(Kind k, List<Thing> all)
        {
            IEnumerable<Thing> c;
            switch (k)
            {
                case Kind.Sickle: c = all.Where(t => t.trait is TraitToolSickle || t.category.IsChildOf("scythe")); break;
                case Kind.Axe: c = all.Where(t => t.HasElement(Lumberjack) && !(t.trait is TraitToolCrowbar)).OrderByDescending(t => t.Evalue(Lumberjack)); break;
                case Kind.Pickaxe: c = all.Where(t => t.HasElement(Mining) && !(t.trait is TraitToolCrowbar)).OrderByDescending(t => t.Evalue(Mining)); break;
                case Kind.Shovel: c = all.Where(t => t.HasElement(DigSkill)).OrderByDescending(t => t.Evalue(DigSkill)); break;
                case Kind.Shears: c = all.Where(t => t.trait is TraitToolShears); break;
                case Kind.Lockpick: c = all.Where(t => t.trait is TraitLockpick && t.c_charges > 0).OrderBy(t => t.c_charges); break;
                case Kind.Crowbar: c = all.Where(t => t.trait is TraitToolCrowbar); break;
                default: return null;
            }
            if (k != Kind.Lockpick && k != Kind.Axe && k != Kind.Pickaxe && k != Kind.Shovel) c = c.OrderByDescending(t => t.material?.hardness ?? 0);
            return c.FirstOrDefault();
        }

        private static Thing Tool(Kind k) => Uses(k) ? Best(k) : null;

        // ---- Targets -------------------------------------------------------------------------------------------

        private static readonly Dictionary<long, DateTime> failed = new Dictionary<long, DateTime>();
        private static Zone failedZone;
        private static long FKey(Kind k, Point p) => ((long)k << 40) | ((long)p.x << 20) | (uint)p.z;
        private static bool Failed(Kind k, Point p)
        {
            if (failedZone != EClass._zone) { failed.Clear(); failedZone = EClass._zone; }
            return failed.TryGetValue(FKey(k, p), out var until) && until > DateTime.UtcNow;
        }
        internal static void Fail(Kind k, Point p) => failed[FKey(k, p)] = DateTime.UtcNow.AddSeconds(30);

        private static bool Home(Point p) => EClass._zone.IsPCFaction && p.cell.isModified;
        private static bool Ignored(IgnoreList.NameList list, string name) => !string.IsNullOrEmpty(name) && list.Contains(name);

        // The job for one tile, if any enabled tool has work there. The sickle gets first claim on growing things;
        // what stands on a tile (plants, walls, chests, animals) comes before digging up its floor.
        internal static MultiJob Job(Point p)
        {
            if (p == null || !p.IsInBounds) return null;
            var ignore = Explorer.Instance.IgnoreList;
            Thing t;

            if (p.HasObj && !Home(p))
            {
                var req = p.sourceObj.reqHarvest;
                string skill = req != null && req.Length > 0 ? req[0] : null;
                bool skipObj = Ignored(ignore.Gathering, p.cell.GetObjName());
                if (!skipObj && p.cell.CanReapSeed() && (t = Tool(Kind.Sickle)) != null && !Failed(Kind.Sickle, p))
                {
                    var h = new TaskHarvest { pos = p.Copy() }; h.SetTarget(EClass.pc, t);
                    if (!h.IsTooHard) return new MultiJob(Kind.Sickle, t, p);
                }
                if (!skipObj && skill == "lumberjack" && (t = Tool(Kind.Axe)) != null && !Failed(Kind.Axe, p))
                {
                    var h = new TaskHarvest { pos = p.Copy() }; h.SetTarget(EClass.pc, t);
                    if (h.idEle == Lumberjack && !h.IsTooHard && Materials.Allowed(h)) return new MultiJob(Kind.Axe, t, p);
                }
                if (!skipObj && skill == "mining" && (t = Tool(Kind.Pickaxe)) != null && !Failed(Kind.Pickaxe, p))
                {
                    var h = new TaskHarvest { pos = p.Copy() }; h.SetTarget(EClass.pc, t);
                    if (h.idEle == Mining && !h.IsTooHard && Materials.Allowed(h)) return new MultiJob(Kind.Pickaxe, t, p);
                }
            }
            if (p.HasBlock && !Home(p) && (t = Tool(Kind.Pickaxe)) != null && !Failed(Kind.Pickaxe, p) &&
                TaskMine.CanMine(p, t) && !Ignored(ignore.Mining, p.cell.GetBlockName()))
            {
                var m = new TaskMine { pos = p.Copy() }; m.SetTarget(EClass.pc, t);
                if (!m.IsTooHard && Materials.Allowed(m)) return new MultiJob(Kind.Pickaxe, t, p);
            }
            if (p.HasThing && Plugin.LocksOn)
            {
                Thing pick = Tool(Kind.Lockpick), bar = Tool(Kind.Crowbar);
                if (pick != null || bar != null)
                    foreach (var thing in p.Things)
                    {
                        if (Failed(Kind.Lockpick, p)) break;
                        bool canPick = (pick != null || Plugin.Practice(thing)) && Plugin.Eligible(thing, false);
                        if (canPick && pick != null) return new MultiJob(Kind.Lockpick, pick, p, thing);
                        if (bar != null && Plugin.Eligible(thing, true)) return new MultiJob(Kind.Crowbar, bar, p, thing);
                    }
            }
            if (p.HasChara && Plugin.ShearOn && (t = Tool(Kind.Shears)) != null && !Failed(Kind.Shears, p))
                foreach (var c in p.Charas)
                    if (Shearing.Eligible(c)) return new MultiJob(Kind.Shears, t, p, null, c);
            // The floor itself last: whatever stands on a tile (animals, chests, plants) comes first.
            if ((t = Tool(Kind.Shovel)) != null && Digging.On && !Failed(Kind.Shovel, p) && Digging.Task(p, t) != null)
                return new MultiJob(Kind.Shovel, t, p);
            return null;
        }

        // Jobs AutoExplore itself does without a tool (berries, flowers...) stay; tool jobs are ours while active.
        private static bool KeepNative(AIAct a) => a is BaseTaskHarvest h && !(a is TaskMine) && h.idEle != Mining && h.idEle != Lumberjack && h.idEle != DigSkill;

        // Never call IsPointReachable here: this runs inside AutoExplore's early-exit flood callback.
        [HarmonyPatch(typeof(AIActionFinder), "ClassifyApproach"), HarmonyPriority(Priority.Low)]
        private static class EarlySearch
        {
            private static void Postfix(Point t, ref AIAct __result)
            {
                if (!Active) return;
                var job = Job(t);
                if (job != null) __result = job;
                else if (__result != null && !KeepNative(__result)) __result = null;
            }
        }

        private static void Fill(AIActionFinder finder, List<AIAct> result)
        {
            result.RemoveAll(a => !KeepNative(a));
            EClass._map.ForeachCell(c =>
            {
                var p = new Point(c.x, c.z);
                if (!finder.IsPointReachable(p, 1)) return;
                var job = Job(p);
                if (job != null) result.Add(job);
            });
        }

        private static bool Harvest => Explorer.Instance.AutoExplorerConfig.HandleHarvestables.Value;

        [HarmonyPatch(typeof(AIActionFinder), nameof(AIActionFinder.FindHarvestables)), HarmonyPriority(Priority.Low)]
        private static class HarvestSearch
        {
            private static void Postfix(AIActionFinder __instance, List<AIAct> __result) { if (Active) Fill(__instance, __result); }
        }

        [HarmonyPatch(typeof(AIActionFinder), nameof(AIActionFinder.FindMineables)), HarmonyPriority(Priority.Low)]
        private static class MineSearch
        {
            private static void Postfix(AIActionFinder __instance, List<AIAct> __result)
            {
                if (!Active) return;
                if (Harvest) __result.Clear(); // the harvest search already found everything
                else Fill(__instance, __result);
            }
        }
    }

    // One multi-tool job: put the chosen tool in hand (the selected hot item, as the game counts it), then run the
    // game's own action for that target. The next job swaps again as needed.
    public sealed class MultiJob : AIAct
    {
        internal readonly MultiTool.Kind kind;
        private readonly Thing tool, thing;
        private readonly Chara chara;
        private readonly Point pos;
        internal MultiJob(MultiTool.Kind k, Thing tool, Point p, Thing thing = null, Chara chara = null)
        { kind = k; this.tool = tool; pos = p.Copy(); this.thing = thing; this.chara = chara; }

        public override Point GetDestination() => chara != null ? chara.pos : pos;
        public override bool CanManualCancel() => true;

        public override IEnumerable<Status> Run()
        {
            if (tool == null || tool.isDestroyed || tool.GetRootCard() != EClass.pc) yield break;
            if (EClass.player.currentHotItem?.Thing != tool) EClass.player.SetCurrentHotItem(tool.trait.GetHotItem());
            AIAct job = Make();
            if (job == null) { MultiTool.Fail(kind, pos); yield break; }
            yield return Do(job, () => { MultiTool.Fail(kind, pos); return Cancel(); });
        }

        private AIAct Make()
        {
            switch (kind)
            {
                case MultiTool.Kind.Sickle:
                case MultiTool.Kind.Axe:
                    return new TaskHarvest { pos = pos.Copy() };
                case MultiTool.Kind.Pickaxe:
                    if (pos.HasBlock && TaskMine.CanMine(pos, tool)) return new TaskMine { pos = pos.Copy() };
                    return new TaskHarvest { pos = pos.Copy() };
                case MultiTool.Kind.Shovel:
                    return Digging.Task(pos, tool);
                case MultiTool.Kind.Lockpick:
                case MultiTool.Kind.Crowbar:
                    return thing != null && !thing.isDestroyed ? new ExploreOpen(thing) : null;
                case MultiTool.Kind.Shears:
                    return chara != null && chara.IsAliveInCurrentZone ? new ExploreShear(chara) : null;
            }
            return null;
        }
    }

    // The separate key, when presets cycle with their own key (never while typing).
    internal sealed class MultiToolKey : MonoBehaviour
    {
        private void Update()
        {
            if (MultiTool.style == null || MultiTool.style.Value != MultiTool.CycleStyle.OwnKey) return;
            var k = MultiTool.ownKey.Value;
            if (k.MainKey == KeyCode.None || EInput.isInputFieldActive || !EClass.core.IsGameStarted || !k.IsDown()) return;
            Ui.Guard(MultiTool.CycleOwn);
        }
    }
}
