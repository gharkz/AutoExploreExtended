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

        internal enum PickaxeWork { Both, Harvest, Mine }
        internal sealed class Preset
        {
            public List<string> Tools = All.Select(k => k.ToString()).ToList();
            public bool InCycle = true;
            public bool UseHeld;                             // Tool in Hand: the held tool also does its usual jobs
            public PickaxeWork Pickaxe = PickaxeWork.Both;   // pickaxe and axe: objects (Harvest), walls (Mine), or both
        }
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
        internal static bool Uses(Kind k) => Current != null && (Current.Tools.Contains(k.ToString()) || Current.UseHeld && HeldKind == k);

        // What the held tool counts as (between jobs the held tool is always the player's own: jobs swap back).
        internal static Kind? KindOf(Thing t)
        {
            if (t == null) return null;
            if (t.trait is TraitToolSickle || t.category.IsChildOf("scythe")) return Kind.Sickle;
            if (t.trait is TraitToolCrowbar) return Kind.Crowbar;
            if (t.trait is TraitLockpick) return Kind.Lockpick;
            if (t.trait is TraitToolShears) return Kind.Shears;
            if (t.HasElement(DigSkill)) return Kind.Shovel;
            if (t.HasElement(Mining)) return Kind.Pickaxe;
            if (t.HasElement(Lumberjack)) return Kind.Axe;
            return null;
        }
        private static Kind? HeldKind => KindOf(EClass.pc?.Tool);

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
                if (!EClass.core.IsGameStarted || EClass.pc == null) return false; // mode key on the title screen
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

        // A copy right below the original, named name_2, name_3, ...
        internal static void Duplicate(string name)
        {
            var p = data.Presets[name]; string n = CopyName.Next(name, data.Presets.ContainsKey);
            data.Presets[n] = new Preset { Tools = new List<string>(p.Tools), InCycle = p.InCycle, UseHeld = p.UseHeld, Pickaxe = p.Pickaxe };
            data.Order.Insert(data.Order.IndexOf(name) + 1, n); Save();
        }

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

        // The preset's tools use the best you carry; a kind added only through Tool in Hand uses the held tool itself.
        private static Thing Tool(Kind k)
        {
            if (Current == null) return null;
            if (Current.Tools.Contains(k.ToString())) return Best(k);
            return Current.UseHeld && HeldKind == k ? EClass.pc.Tool : null;
        }
        private static bool PickHarvest => Current == null || Current.Pickaxe != PickaxeWork.Mine;
        private static bool PickMine => Current == null || Current.Pickaxe != PickaxeWork.Harvest;

        // ---- Targets -------------------------------------------------------------------------------------------

        // A spot whose job failed sits out only after two failures in a row (a single interruption, such as an enemy
        // showing up mid-job, doesn't count), and only for 10 seconds, so the map is never "done" with work left.
        private static readonly Dictionary<long, (int count, DateTime until)> failed = new Dictionary<long, (int, DateTime)>();
        private static Zone failedZone;
        private static long FKey(Kind k, Point p) => ((long)k << 40) | ((long)p.x << 20) | (uint)p.z;
        private static bool Failed(Kind k, Point p)
        {
            if (failedZone != EClass._zone) { failed.Clear(); failedZone = EClass._zone; }
            return failed.TryGetValue(FKey(k, p), out var f) && f.count >= 2 && f.until > DateTime.UtcNow;
        }
        internal static void Fail(Kind k, Point p, string why = "")
        {
            var key = FKey(k, p);
            int n = failed.TryGetValue(key, out var f) && f.until > DateTime.UtcNow ? f.count + 1 : 1;
            failed[key] = (n, DateTime.UtcNow.AddSeconds(10));
            Log("failed " + k + " at " + p + " " + why + (n >= 2 ? " (sitting out 10 s)" : " (will retry)"));
        }
        internal static void Succeeded(Kind k, Point p) => failed.Remove(FKey(k, p));
        internal static void Log(string m) { if (Profiler.On) UnityEngine.Debug.Log("[AEX multi-tool] " + m); if (Trace.On) Trace.Event("note", null, m.Replace("\"", "'")); }

        private static bool Ignored(IgnoreList.NameList list, string name) => !string.IsNullOrEmpty(name) && list.Contains(name);

        // The job for one tile, if any enabled tool has work there. The sickle gets first claim on growing things;
        // what stands on a tile (plants, walls, chests, animals) comes before digging up its floor.
        // The tools to try on a tile: the best of each preset tool, plus the held tool when Tool in Hand is on and its
        // kind isn't already in the preset (no second pass for the same kind).
        private static List<(Thing tool, Kind kind)> Candidates()
        {
            var list = new List<(Thing, Kind)>();
            if (Current == null) return list;
            foreach (var k in All)
                if (Current.Tools.Contains(k.ToString()) && Best(k) is Thing b) list.Add((b, k));
            var held = EClass.pc.Tool; var hk = KindOf(held);
            if (Current.UseHeld && hk != null && !Current.Tools.Contains(hk.Value.ToString())) list.Add((held, hk.Value));
            return list;
        }

        // Run a game check as if this tool were in hand: the game reads the current tool from the selected hot item,
        // so point that at the tool for the check and put it straight back (no visible swap, no refresh).
        private static readonly Dictionary<Thing, HotItem> hots = new Dictionary<Thing, HotItem>();
        private static T As<T>(Thing tool, Func<T> check)
        {
            var player = EClass.player; var keep = player.currentHotItem;
            if (keep?.Thing == tool) return check();
            if (!hots.TryGetValue(tool, out var hot)) hots[tool] = hot = tool.trait.GetHotItem();
            player.currentHotItem = hot;
            try { return check(); } finally { player.currentHotItem = keep; }
        }

        internal static MultiJob Job(Point p)
        {
            if (p == null || !p.IsInBounds || !p.IsSeen) return null; // only what you've seen: exploring reveals, jobs follow
            var ignore = Explorer.Instance.IgnoreList;
            Thing t;

            {
                // Objects and loose things: ask the game what each candidate tool would do there (its own hover and
                // right-click rule), the sickle first, then tools whose skill matches the target before the others.
                if ((p.HasObj || p.HasThing) && OwnLand.ObjOk(p))
                {
                    var req = p.HasObj ? p.sourceObj.reqHarvest : null;
                    int want = req != null && req.Length > 0 && EClass.sources.elements.alias.TryGetValue(req[0], out var er) ? er.id : 0;
                    foreach (var (tool, kind) in Candidates().OrderBy(c => c.kind == Kind.Sickle ? 0 : want != 0 && c.tool.HasElement(want) ? 1 : 2))
                    {
                        if (kind == Kind.Shears || kind == Kind.Lockpick || kind == Kind.Crowbar || Failed(kind, p)) continue;
                        var h = As(tool, () => TaskHarvest.TryGetAct(EClass.pc, p));
                        if (h == null) continue;
                        h.SetTarget(EClass.pc, tool);
                        if (h.IsTooHard) continue;
                        bool reap = kind == Kind.Sickle && p.cell.CanReapSeed();
                        bool toolJob = h.idEle == Mining || h.idEle == Lumberjack || h.idEle == DigSkill;
                        if (!reap && !toolJob) continue;                          // plain gathering: AutoExplore's own harvest does it
                        if ((h.idEle == Mining || h.idEle == Lumberjack) && !reap && !PickHarvest) continue;
                        if (h.idEle == DigSkill && !Digging.On) continue;
                        string name = h.harvestType == BaseTaskHarvest.HarvestType.Thing ? h.target?.Name : p.cell.GetObjName();
                        if (Ignored(ignore.Gathering, name) || !Materials.Allowed(h)) continue;
                        return new MultiJob(kind, tool, p, harvest: true);
                    }
                }
                // Walls: any candidate the game lets mine them (pickaxe; an axe on wood and grass walls).
                if (p.HasBlock && PickMine && OwnLand.WallOk(p) && !Ignored(ignore.Mining, p.cell.GetBlockName()))
                    foreach (var (tool, kind) in Candidates().OrderBy(c => c.tool.HasElement(Mining) ? 0 : 1))
                    {
                        if (Failed(kind, p) || !TaskMine.CanMine(p, tool)) continue;
                        var m = new TaskMine { pos = p.Copy() }; m.SetTarget(EClass.pc, tool);
                        if (!m.IsTooHard && Materials.Allowed(m)) return new MultiJob(kind, tool, p, harvest: true);
                    }
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
            // Seen animals get sheared whenever they're at hand; the sweep never routes for them (they wander).
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
                if (job != null) __result = job; // (not logged: this runs for every tile the search passes)
                else if (__result != null && !KeepNative(__result)) __result = null;
            }
        }

        private static void Fill(AIActionFinder finder, List<AIAct> result)
        {
            result.RemoveAll(a => !KeepNative(a));
            var counts = new Dictionary<Kind, int>(); int unreachable = 0;
            MapScan.Playable(c =>
            {
                var p = new Point(c.x, c.z);
                var job = Job(p);
                if (job == null) return;
                if (!finder.IsPointReachable(p, 1)) { unreachable++; return; }
                result.Add(job); counts[job.kind] = counts.TryGetValue(job.kind, out var n) ? n + 1 : 1;
            });
            Log("full search: " + string.Join(", ", counts.Select(kv => kv.Key + " " + kv.Value)) + "; unreachable " + unreachable + "; kept native " + result.Count(KeepNative));
        }

        private static bool Harvest => Explorer.Instance.AutoExplorerConfig.HandleHarvestables.Value;

        // A shearable animal you can see within a few tiles goes first, whatever else was chosen (route or not).
        private const int NearShear = 4;
        [HarmonyPatch(typeof(AIActionFinder), nameof(AIActionFinder.FindPotentialActions)), HarmonyPriority(Priority.Low)]
        private static class ShearFirst
        {
            private static void Postfix(AIActionFinder __instance, ref List<AIAct> __result)
            {
                if (!Active || !Plugin.ShearOn || Tool(Kind.Shears) is not Thing shears) return;
                var pc = EClass.pc.pos; Chara best = null; int bestD = int.MaxValue;
                foreach (var c in EClass._map.charas)
                {
                    int d = c.pos.Distance(pc);
                    if (d > NearShear || d >= bestD || !c.pos.IsSeen || Failed(Kind.Shears, c.pos) || !Shearing.Eligible(c)) continue;
                    if (!__instance.IsPointReachable(c.pos, 1)) continue;
                    best = c; bestD = d;
                }
                if (best == null) return;
                if (__result != null && __result.Count > 0 && __result[0] is MultiJob mj && mj.kind == Kind.Shears) return;
                Log("shear first: " + best.Name + " at " + best.pos + " (" + bestD + " tiles)");
                __result = new List<AIAct> { new MultiJob(Kind.Shears, shears, best.pos, null, best) };
            }
        }

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
        private readonly bool harvest; // a harvest or mining job found by the game's own rule (not a floor dig)
        internal MultiJob(MultiTool.Kind k, Thing tool, Point p, Thing thing = null, Chara chara = null, bool harvest = false)
        { kind = k; this.tool = tool; pos = p.Copy(); this.thing = thing; this.chara = chara; this.harvest = harvest; }

        public override Point GetDestination() => chara != null ? chara.pos : pos;
        public override bool CanManualCancel() => true;

        public override IEnumerable<Status> Run()
        {
            if (tool == null || tool.isDestroyed || tool.GetRootCard() != EClass.pc) yield break;
            if (EClass.player.currentHotItem?.Thing != tool) { previous = EClass.player.currentHotItem; EClass.player.SetCurrentHotItem(tool.trait.GetHotItem()); }
            // The job is ours until it's done: an enemy interrupting it (with Fight in Place on) means fight where you
            // stand, then take the same job back up. Only a failure with no enemy about ends it.
            for (int round = 0; round < 8; round++)
            {
                AIAct job = Make();
                if (job == null) { MultiTool.Fail(kind, pos, "no action could be made"); Restore(); yield break; }
                MultiTool.Log((round == 0 ? "start " : "resume ") + kind + " at " + pos + " with " + tool.Name + " (" + job.GetType().Name + ")");
                bool failed = false;
                yield return Do(job, () => { failed = true; return Status.Running; });
                if (!failed) { MultiTool.Succeeded(kind, pos); Restore(); yield break; }
                var enemy = Combat.On ? Combat.Enemy() : null;
                if (enemy == null) { MultiTool.Fail(kind, pos, "action failed (" + job.GetType().Name + ")"); Restore(); yield break; }
                MultiTool.Log("fighting " + enemy.Name + " in place, then back to " + kind + " at " + pos);
                yield return Do(new HoldCombat(enemy), () => Status.Running);
                Combat.Release();
                if (tool.isDestroyed || tool.GetRootCard() != EClass.pc) { Restore(); yield break; }
                if (EClass.player.currentHotItem?.Thing != tool) EClass.player.SetCurrentHotItem(tool.trait.GetHotItem());
            }
            MultiTool.Fail(kind, pos, "kept being interrupted");
            Restore();
        }

        // Back to the player's own tool after every job, so "Tool in Hand" always means what you chose to hold.
        private HotItem previous;
        private void Restore()
        {
            if (previous == null) return;
            var back = previous; previous = null;
            if (back.Thing == null || (!back.Thing.isDestroyed && back.Thing.GetRootCard() == EClass.pc)) EClass.player.SetCurrentHotItem(back);
        }

        public override void OnCancel() { Restore(); base.OnCancel(); }

        private AIAct Make()
        {
            if (harvest) // the tool is in hand now: the game's own mining or harvest action for this tile
                return pos.HasBlock && TaskMine.CanMine(pos, tool) ? (AIAct)new TaskMine { pos = pos.Copy() } : TaskHarvest.TryGetAct(owner, pos);
            switch (kind)
            {
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
