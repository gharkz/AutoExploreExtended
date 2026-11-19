using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Elin_AutoExplore;
using Explorer = Elin_AutoExplore.Plugin;

namespace AutoExploreExtended
{
    // Auto-Explore Tool Extension: AutoExplore (Gather/Combined mode) works with the selected tool across the current map.
    //  - Shears: shears every shearable NPC (the former Auto-Explore Shearing Extension, unchanged behaviour).
    //  - Crowbar / lockpick: opens every legal locked container (prying or picking), mainly for lockpicking experience;
    //    practice chests are always picked by hand (prying would destroy them). Karma/crime targets are excluded by default.
    // With one of these tools selected, AutoExplore's other gathering is set aside.
    [BepInPlugin(Id, DisplayName, "1.2.1")]
    [BepInDependency("yuof.elin.autoExplore.mod", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Id = "gharkz.autoexploreextended";
        internal const string OldId = "gharkz.autoexplorelockpicking"; // the development name; settings are carried over once
        public const string DisplayName = "AutoExplore Extended";
        private static ConfigEntry<bool> on, allowKarma;
        internal static ConfigEntry<bool> shearOn, practiceOn;
        internal static ConfigEntry<bool> pryIgnoresInstant, pickIgnoresInstant;
        private sealed class FailedTarget { public DateTime Until; }
        private static readonly ConditionalWeakTable<Thing, FailedTarget> failures = new ConditionalWeakTable<Thing, FailedTarget>();
        private static Harmony harmony;
        public static bool Installed { get; private set; }

        private void Start()
        {
            try { Migrate(); Install(Config); Materials.Bind(Config); NamedRestrictions.Bind(Config); Digging.Bind(Config); Tracks.Bind(Config); SelfMenu.Bind(Config); MultiTool.Bind(Config); Sweep.Bind(Config); Combat.Bind(Config); OwnLand.Bind(Config); gameObject.AddComponent<MultiToolKey>(); Settings.Register(Config, gameObject); Profiler.Register(Config, gameObject); Trace.Register(Config, gameObject); Logger.LogInfo(DisplayName + " 1.2.1 active."); }
            catch (Exception e) { Logger.LogError(DisplayName + " disabled: " + e); }
        }

        // 1.0.0 shipped under the development ID; copy its settings files to the new names once (old files are kept).
        private void Migrate()
        {
            string dir = System.IO.Path.GetDirectoryName(Config.ConfigFilePath);
            bool copied = false;
            foreach (var (from, to) in new[] { (OldId + ".cfg", Id + ".cfg"), (OldId + ".materials.json", Id + ".materials.json"), (OldId + ".restrictions.json", Id + ".restrictions.json") })
            {
                string a = System.IO.Path.Combine(dir, from), b = System.IO.Path.Combine(dir, to);
                if (System.IO.File.Exists(a) && !System.IO.File.Exists(b)) { System.IO.File.Copy(a, b); copied = true; }
            }
            if (copied) { Config.Reload(); Logger.LogInfo("Carried settings over from " + OldId + "."); }
        }

        private void OnDestroy() => Uninstall();

        public static void Install(ConfigFile configuration)
        {
            if (Installed) return;
            if (Explorer.Instance == null)
                throw new InvalidOperationException("Enable AutoExplore 1.6 or newer before using AutoExplore Extended.");
            foreach (var method in PatchTargets())
                if (method == null) throw new MissingMethodException("Unsupported AutoExplore targeting API");
            on = configuration.Bind("Locked chests", "Enabled", true,
                "Open locked containers you may legally open, with AutoExplore in Gather/Combined mode while a crowbar or lockpick is selected.");
            shearOn = configuration.Bind("Shearing", "Enabled", true,
                "Shear every shearable NPC with AutoExplore in Gather/Combined mode while shears are selected.");
            practiceOn = configuration.Bind("Locked chests", "PracticeChests", true,
                "Also train on locked practice chests while holding a lockpick or crowbar. They are always picked by hand: never pried, never damaged, and no lockpick charges are used.");
            allowKarma = configuration.Bind("Locked chests", "IncludeKarmaTargets", false,
                "Locked containers only: also open ones that cost karma or count as a crime (NPC property, lost property). Gathering and mining are not affected.");
            pryIgnoresInstant = configuration.Bind("Compatibility", "PryIgnoresInstantActions", true,
                "Prying (crowbar) by this mod runs at its normal length even with the Instant Actions mod on; instant prying stops after one tick and never opens anything.");
            pickIgnoresInstant = configuration.Bind("Compatibility", "PickIgnoresInstantActions", true,
                "Picking (lockpick) by this mod runs at its normal length even with the Instant Actions mod on.");
            harmony = new Harmony(Id);
            try { harmony.PatchAll(typeof(Plugin).Assembly); Installed = true; }
            catch { Uninstall(); throw; }
        }

        public static void Uninstall() { harmony?.UnpatchSelf(); Installed = false; on = null; allowKarma = null; }

        public static MethodInfo[] PatchTargets() => new[] {
            AccessTools.Method(typeof(AIActionFinder), "ClassifyApproach", new[] { typeof(Point) }),
            AccessTools.Method(typeof(AIActionFinder), nameof(AIActionFinder.FindHarvestables), Type.EmptyTypes)
        };

        internal static bool Crowbar => EClass.pc?.Tool?.trait is TraitToolCrowbar;
        internal static bool Lockpick => EClass.pc?.Tool?.trait is TraitLockpick;

        internal static bool Shears => EClass.pc?.Tool?.trait is TraitToolShears;
        internal static bool ShearReady => ShearOn && Running && Shears && !MultiTool.Active;
        internal static bool ShearOn => shearOn != null && shearOn.Value && !ShearingExtension;
        internal static bool LocksOn => on != null && on.Value;
        // A job already chosen (by the tool mode or multi-tool mode) may run while AutoExplore runs in any work mode.
        internal static bool Working => Explorer.Instance != null && Explorer.Instance.IsRunning && EClass.pc != null && !EClass.pc.isDead;
        // The standalone Auto-Explore Shearing Extension does the same job; when it's loaded, ours steps aside.
        internal static bool ShearingExtension => shearingExt ?? (bool)(shearingExt = HarmonyLib.AccessTools.TypeByName("AutoExploreShearing.Plugin") != null); // Elin only loads enabled mods' DLLs
        private static bool? shearingExt;
        internal static bool Running => Explorer.Instance != null && Explorer.Instance.IsRunning &&
            Explorer.Instance.AutoExplorerConfig.HandleHarvestables.Value && EClass.pc != null && !EClass.pc.isDead;
        internal static bool Practice(Thing t) => t.trait is TraitChestPractice;

        internal static bool Ready => on != null && on.Value &&
            Explorer.Instance != null && Explorer.Instance.IsRunning &&
            Explorer.Instance.AutoExplorerConfig.HandleHarvestables.Value &&
            EClass.pc != null && !EClass.pc.isDead && (Crowbar || Lockpick) && !MultiTool.Active;

        internal static bool Eligible(Thing t) => Eligible(t, Crowbar);

        internal static bool Eligible(Thing t, bool crowbar)
        {
            var pc = EClass.pc;
            if (t == null || t.isDestroyed || !t.IsContainer || t.c_lockLv <= 0 || t.isMasked || t.isHidden) return false;
            if (t.parent != EClass._zone || t.pos == null || !t.pos.IsInBounds) return false; // on the map, not inside something
            if (RecentlyFailed(t)) return false;
            if (!allowKarma.Value && (t.isNPCProperty || t.isLostProperty)) return false; // karma / crime: excluded by default
            if (Explorer.Instance.IgnoreList.Gathering.Contains(t.Name)) return false;
            // Picking needs enough skill for the lock (native rule); prying always works eventually.
            if (Practice(t)) { if (!practiceOn.Value) return false; }
            else if (!crowbar && !CanPick(t)) return false;
            return true; // stamina: the job rests first through AutoExplore's own routine (bed when you have one)
        }

        // Your picking skill beats the lock (native rule: with a pick, skill + 10; bare hands, skill / 2 + 2).
        internal static bool CanPick(Thing t)
        {
            var pc = EClass.pc;
            bool hasPick = pc.things.FindBest<TraitLockpick>(x => -x.c_charges) != null;
            int skill = hasPick ? pc.Evalue(280) + 10 : pc.Evalue(280) / 2 + 2;
            return skill > t.c_lockLv || t.trait is TraitChestPractice;
        }

        internal const int Reserve = 3;

        // AutoExplore's own rest (sets your bed down and sleeps when you carry one, packs it up after; else its usual
        // resting). Private in AutoExplore, so reached by reflection; false when unavailable.
        static readonly MethodInfo tryRest = AccessTools.Method(typeof(Explorer), "TryRest");
        internal static bool Rest()
        {
            try { return tryRest != null && Explorer.Instance != null && (bool)tryRest.Invoke(Explorer.Instance, null); }
            catch { return false; }
        }

        // While resting: asleep, or a rest act in progress.
        internal static bool Resting(Chara c) => c.HasCondition<ConSleep>() || !(c.ai is NoGoal) && !(c.ai is GoalEndTurn) && c.ai.GetType().Name.Contains("Sleep"); // each pry/pick tick can cost 1 stamina; keep a little back


        internal static bool RecentlyFailed(Thing t) => failures.TryGetValue(t, out var f) && f.Until > DateTime.UtcNow;

        internal static void NoteFailure(Thing t)
        {
            failures.Remove(t);
            failures.Add(t, new FailedTarget { Until = DateTime.UtcNow.AddSeconds(10) });
        }

        // Never call IsPointReachable here: this runs inside AutoExplore's early-exit flood callback.
        [HarmonyPatch(typeof(AIActionFinder), "ClassifyApproach")]
        internal static class EarlySearch
        {
            private static void Postfix(Point t, ref AIAct __result)
            {
                if (ShearReady)
                {
                    __result = null;
                    if (t == null || !t.IsInBounds || !t.HasChara) return;
                    foreach (var c in t.Charas) if (Shearing.Eligible(c)) { __result = new ExploreShear(c); return; }
                    return;
                }
                if (!Ready) return;
                // With a crowbar or lockpick selected, only locks count: ordinary harvesting is set aside.
                __result = null;
                if (t == null || !t.IsInBounds) return;
                foreach (var thing in t.Things)
                    if (Eligible(thing)) { __result = new ExploreOpen(thing); return; }
            }
        }

        [HarmonyPatch(typeof(AIActionFinder), nameof(AIActionFinder.FindHarvestables))]
        internal static class FullSearch
        {
            private static void Postfix(AIActionFinder __instance, List<AIAct> __result)
            {
                if (ShearReady)
                {
                    __result.RemoveAll(a => !(a is ExploreShear));
                    foreach (var c in EClass._map.charas)
                        if (Shearing.Eligible(c) && __instance.IsPointReachable(c.pos, 1)) __result.Add(new ExploreShear(c));
                    return;
                }
                if (!Ready) return;
                __result.RemoveAll(a => !(a is ExploreOpen)); // locks only while a crowbar or lockpick is selected
                foreach (var thing in EClass._map.things.ToList())
                    if (Eligible(thing) && __instance.IsPointReachable(thing.pos, 1))
                        __result.Add(new ExploreOpen(thing));
            }
        }
    }

    // One container, worked until it opens: a crowbar keeps prying while stamina lasts (resting when allowed); a
    // lockpick keeps picking while picks last, and when the held pick breaks the next one in the pack is taken in hand.
    public sealed class ExploreOpen : AIAct
    {
        public Thing Target { get; }
        bool pry;
        public ExploreOpen(Thing target) => Target = target ?? throw new ArgumentNullException(nameof(target));
        public override Point GetDestination() => Target.pos;
        public override bool CanManualCancel() => true;

        static bool Running => Explorer.Instance != null && Explorer.Instance.IsRunning;
        bool Open => Target.isDestroyed || Target.c_lockLv <= 0;

        public override IEnumerable<Status> Run()
        {
            if (!Plugin.LocksOn || !Plugin.Working || !(Plugin.Crowbar || Plugin.Lockpick) || !Plugin.Eligible(Target)) yield break;
            pry = Plugin.Crowbar && !Plugin.Practice(Target); // practice chests: always picked by hand
            isFail = () => !Running;
            for (int round = 0; round < 200 && !Open && Running; round++)
            {
                // Stamina low: AutoExplore's own rest (bed when you carry one), then carry on.
                if (owner.stamina.value <= Plugin.Reserve)
                {
                    if (!Plugin.Rest()) break;
                    yield break; // the rest takes over; the chest is picked up again next time round
                }
                if (pry)
                {
                    if (!Plugin.Crowbar) break; // you switched tools
                    yield return Do(new NativePry { target = Target }, Failed);
                }
                else
                {
                    if (!Plugin.Practice(Target) && !HoldAPick()) break; // all picks used up (practice chests need none)
                    yield return Do(new NativePick { target = Target }, Failed);
                    if (!Open && !Plugin.CanPick(Target)) break; // not enough skill after all
                }
            }
            if (!Open) Plugin.NoteFailure(Target); // set aside a moment; other chests first
        }

        // A lockpick in hand: the held one, or the next one from the pack (charges left) when it broke.
        bool HoldAPick()
        {
            if (Plugin.Lockpick && owner.Tool.c_charges > 0) return true;
            var next = owner.things.Find(t => t.trait is TraitLockpick && t.c_charges > 0);
            if (next == null) return false;
            EClass.player.SetCurrentHotItem(new HotItemHeld(next));
            return Plugin.Lockpick;
        }

        private Status Failed() { Plugin.NoteFailure(Target); return Cancel(); }

        // Native actions, stopping when the tool is gone or stamina reaches the reserve.
        private sealed class NativePry : AI_PryOpen
        {
            public override AIProgress CreateProgress() { var p = base.CreateProgress(); if (Plugin.pryIgnoresInstant.Value) RealLength.Mark(p); return p; }
            public override bool IsValidTC(Card c) => base.IsValidTC(c) && Plugin.Crowbar && owner.stamina.value > Plugin.Reserve;
        }

        private sealed class NativePick : AI_OpenLock
        {
            public override AIProgress CreateProgress() { var p = base.CreateProgress(); if (Plugin.pickIgnoresInstant.Value) RealLength.Mark(p); return p; }
            public override bool IsValidTC(Card c) => base.IsValidTC(c) && (c.trait is TraitChestPractice || owner.things.Find(t => t.trait is TraitLockpick && t.c_charges > 0) != null)
                && owner.stamina.value > Plugin.Reserve;
        }
    }

    // Progress of our own prying/picking keeps its real length: runs after every other patch on MaxProgress (Instant
    // Actions forces it to 1) and puts the action's own value back, for our marked progresses only.
    [HarmonyPatch(typeof(Progress_Custom), nameof(Progress_Custom.MaxProgress), MethodType.Getter)]
    internal static class RealLength
    {
        static readonly ConditionalWeakTable<AIProgress, object> marked = new ConditionalWeakTable<AIProgress, object>();
        internal static void Mark(AIProgress p) { if (p != null && !marked.TryGetValue(p, out _)) marked.Add(p, null); }

        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Progress_Custom __instance, ref int __result)
        {
            if (marked.TryGetValue(__instance, out _)) __result = __instance.maxProgress;
        }
    }
}
