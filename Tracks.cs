using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using Elin_AutoExplore;
using HarmonyLib;
using Explorer = Elin_AutoExplore.Plugin;

namespace AutoExploreExtended
{
    // Option (off by default): while AutoExplore mines with a pickaxe in hand and you carry a shovel, it also digs up
    // tracks (wood for the taking) with your hardest shovel, then puts the pickaxe back, so you don't swap by hand.
    internal static class Tracks
    {
        private static ConfigEntry<bool> on;
        private static readonly Dictionary<int, DateTime> failed = new Dictionary<int, DateTime>();
        private static Zone failedZone;

        internal static void Bind(ConfigFile c) =>
            on = c.Bind("Digging", "TracksWhileMining", false,
                "While mining with AutoExplore, also dig up tracks using your hardest carried shovel (swapped in and back automatically).");

        internal static bool Ready => on != null && on.Value && Explorer.Instance != null && Explorer.Instance.IsRunning && EClass.pc != null && !EClass.pc.isDead && !EClass._zone.IsRegion &&
            Explorer.Instance.AutoExplorerConfig.HandleMineables.Value && !Digging.Shovel && Best() != null;

        // Hardest shovel you carry (by material hardness), anywhere in your pack.
        internal static Thing Best() => EClass.pc?.things.List(t => t.HasElement(230), true)
            .OrderByDescending(t => t.material?.hardness ?? 0).FirstOrDefault();

        private static int Key(Point p) => p.x + p.z * EClass._map.Size;

        internal static bool IsTrack(Point p)
        {
            if (p == null || !p.IsInBounds || !p.HasObj) return false;
            var req = p.sourceObj.reqHarvest;
            if (req == null || req.Length == 0 || req[0] != "digging") return false;
            if (EClass._zone.IsPCFaction && p.cell.isModified) return false;
            if (failedZone != EClass._zone) { failed.Clear(); failedZone = EClass._zone; }
            return !(failed.TryGetValue(Key(p), out var until) && until > DateTime.UtcNow);
        }

        internal static void Fail(Point p) => failed[Key(p)] = DateTime.UtcNow.AddSeconds(30);

        // Never call IsPointReachable here: this runs inside AutoExplore's early-exit flood callback.
        [HarmonyPatch(typeof(AIActionFinder), "ClassifyApproach")]
        private static class EarlySearch
        {
            private static void Postfix(Point t, ref AIAct __result)
            {
                if (__result == null && Ready && IsTrack(t)) __result = new ExploreTrack(t);
            }
        }

        [HarmonyPatch(typeof(AIActionFinder), nameof(AIActionFinder.FindMineables))]
        private static class FullSearch
        {
            private static void Postfix(AIActionFinder __instance, List<AIAct> __result)
            {
                if (!Ready) return;
                EClass._map.ForeachCell(c =>
                {
                    var p = new Point(c.x, c.z);
                    if (IsTrack(p) && __instance.IsPointReachable(p, 1)) __result.Add(new ExploreTrack(p));
                });
            }
        }
    }

    // Swap in the hardest shovel, dig the track with the game's own harvest action, then hold the previous tool again.
    public sealed class ExploreTrack : AIAct
    {
        private readonly Point pos;
        private HotItem previous; // the game's "current tool" is the selected hot item, not just what is in hand
        public ExploreTrack(Point p) => pos = p.Copy();
        public override Point GetDestination() => pos;
        public override bool CanManualCancel() => true;

        public override IEnumerable<Status> Run()
        {
            var shovel = Tracks.Best();
            if (shovel == null || !Tracks.IsTrack(pos)) yield break;
            previous = EClass.player.currentHotItem;
            EClass.player.SetCurrentHotItem(shovel.trait.GetHotItem());
            var task = TaskHarvest.TryGetAct(owner, pos);
            if (task == null) { Tracks.Fail(pos); Restore(); yield break; }
            yield return Do(task, () => { Tracks.Fail(pos); Restore(); return Cancel(); });
            Restore();
        }

        private void Restore()
        {
            if (previous == null) return;
            var back = previous; previous = null;
            if (back.Thing == null || (!back.Thing.isDestroyed && back.Thing.GetRootCard() == EClass.pc)) EClass.player.SetCurrentHotItem(back);
        }

        public override void OnCancel() { Restore(); base.OnCancel(); }
    }
}
