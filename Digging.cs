using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using Elin_AutoExplore;
using HarmonyLib;

namespace AutoExploreExtended
{
    // Shovel mode: with a shovel selected and AutoExplore in Gather/Combined mode, AutoExplore digs up floor tiles
    // across the map (the game's own shovel action, which removes the floor and drops its material), and digs up
    // tracks.
    //  - Each tile is dug once per visit: soil floors stay soil after digging and would otherwise be dug forever.
    //  - Never on the world map; never floors you placed or changed on your own land; not water, stairs or tiles
    //    with something installed; the game's own rules decide the rest (and the shovel's material filter).
    internal static class Digging
    {
        private const int Dig = 230;
        private static ConfigEntry<bool> on;
        private static readonly HashSet<int> dug = new HashSet<int>();
        private static Zone dugZone;

        internal static void Bind(ConfigFile c) =>
            on = c.Bind("Digging", "Enabled", true, "Dig up floors with AutoExplore in Gather, Mining or Combined mode while a shovel is selected.");

        internal static bool Shovel => EClass.pc?.Tool != null && EClass.pc.Tool.HasElement(Dig);
        internal static bool Ready => on != null && on.Value && Elin_AutoExplore.Plugin.Instance != null && Elin_AutoExplore.Plugin.Instance.IsRunning && EClass.pc != null && !EClass.pc.isDead && Shovel && !EClass._zone.IsRegion &&
            (Harvest || Mine);
        private static bool Harvest => Elin_AutoExplore.Plugin.Instance.AutoExplorerConfig.HandleHarvestables.Value;
        private static bool Mine => Elin_AutoExplore.Plugin.Instance.AutoExplorerConfig.HandleMineables.Value;

        private static int Key(Point p) => p.x + p.z * EClass._map.Size;
        private static HashSet<int> Dug
        {
            get { if (dugZone != EClass._zone) { dug.Clear(); dugZone = EClass._zone; } return dug; }
        }

        internal static BaseTaskHarvest Task(Point p)
        {
            if (p == null || !p.IsInBounds || Dug.Contains(Key(p))) return null;
            if (EClass._zone.IsPCFaction && p.cell.isModified) return null; // your own floors/objects at home
            // Tracks (objects the game harvests with a shovel); loose things are left alone.
            if (p.HasObj && p.sourceObj.reqHarvest?.Length > 0 && p.sourceObj.reqHarvest[0] == "digging")
            {
                var h = TaskHarvest.TryGetAct(EClass.pc, p);
                h?.SetTarget(EClass.pc, EClass.pc.Tool);
                if (h != null && h.idEle == Dig && !h.IsTooHard && Materials.Allowed(h)) return h;
            }
            if (p.IsWater || p.HasObj || p.HasBlock || p.Installed != null) return null; // stairs are installed things
            var task = new TaskDig { pos = p.Copy(), mode = TaskDig.Mode.RemoveFloor, owner = EClass.pc };
            if (task.GetHitResult() != HitResult.Valid) return null;
            task.SetTarget(EClass.pc, EClass.pc.Tool);
            if (task.IsTooHard || !Materials.Allowed(task)) return null;
            return task;
        }

        // Remember every tile a dig touched (wide shovels dig a square).
        [HarmonyPatch(typeof(TaskDig), nameof(TaskDig.OnProgressComplete))]
        private static class Remember
        {
            private static void Prefix(TaskDig __instance)
            {
                if (__instance.owner == null || !__instance.owner.IsPC || __instance.pos == null) return;
                int n = __instance.owner.Tool?.Evalue(770) ?? 0;
                int r = n <= 0 ? 0 : 1 + n / 10;
                foreach (var p in r == 0 ? new List<Point> { __instance.pos } : EClass._map.ListPointsInSquare(__instance.pos, r))
                    if (p.IsInBounds) Dug.Add(Key(p));
            }
        }

        // Never call IsPointReachable here: this runs inside AutoExplore's early-exit flood callback.
        [HarmonyPatch(typeof(AIActionFinder), "ClassifyApproach")]
        private static class EarlySearch
        {
            private static void Postfix(Point t, ref AIAct __result)
            {
                if (!Ready) return;
                __result = Task(t); // with a shovel selected, only floors count
            }
        }

        // A shovel counts for both Gather and Mining mode: dig in whichever search runs (once if both do).
        private static void Fill(AIActionFinder finder, List<AIAct> result)
        {
            result.RemoveAll(a => !(a is BaseTaskHarvest h && h.idEle == Dig));
            EClass._map.ForeachCell(c =>
            {
                var p = new Point(c.x, c.z);
                if (!finder.IsPointReachable(p, 1)) return;
                var task = Task(p);
                if (task != null) result.Add(task);
            });
        }

        [HarmonyPatch(typeof(AIActionFinder), nameof(AIActionFinder.FindHarvestables))]
        private static class HarvestSearch
        {
            private static void Postfix(AIActionFinder __instance, List<AIAct> __result)
            {
                if (Ready && Harvest) Fill(__instance, __result);
            }
        }

        [HarmonyPatch(typeof(AIActionFinder), nameof(AIActionFinder.FindMineables))]
        private static class MineSearch
        {
            private static void Postfix(AIActionFinder __instance, List<AIAct> __result)
            {
                if (!Ready) return;
                if (Harvest) __result.Clear(); // the harvest search already has them
                else Fill(__instance, __result);
            }
        }
    }
}
