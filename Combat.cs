using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using Explorer = Elin_AutoExplore.Plugin;

namespace AutoExploreExtended
{
    // Fight in place (optional, AutoExplore's own modes and multi-tool alike): never chase. Fights use the game's own
    // auto-combat with its "don't chase" switch forced on for the fight (abilities, ranged, throwing, melee when
    // adjacent), and end when nothing is in reach; the route or the interrupted job then carries on.
    internal static class Combat
    {
        private static ConfigEntry<bool> on;
        internal static bool On => on != null && on.Value;

        internal static void Bind(ConfigFile c) =>
            on = c.Bind("Exploration", "FightInPlace", false,
                "Never chase enemies: fight with what's in reach (abilities, ranged, throwing, melee when adjacent), then carry on with the route or the job that was interrupted.");

        private static bool? saved; // the player's own "don't chase" setting while an in-place fight runs
        internal static void Hold()
        {
            var cfg = EClass.game?.config?.autoCombat; if (cfg == null) return;
            if (saved == null) saved = cfg.bDontChase;
            cfg.bDontChase = true;
        }
        internal static void Release()
        {
            var cfg = EClass.game?.config?.autoCombat; if (cfg == null || saved == null) return;
            if (EClass.pc?.ai is HoldCombat) return; // still fighting
            cfg.bDontChase = saved.Value; saved = null;
        }

        // Hostile creatures you can see.
        internal static Chara Enemy()
        {
            var pc = EClass.pc; int sight = pc.GetSightRadius(); Chara best = null; int bestD = int.MaxValue;
            foreach (var c in EClass._map.charas)
            {
                if (c == pc || c.isDead || !c.IsHostile(pc)) continue;
                int d = c.pos.Distance(pc.pos);
                if (d > sight || d >= bestD || !pc.CanSee(c)) continue;
                best = c; bestD = d;
            }
            return best;
        }

        // AutoExplore's combat step, when Fight in Place is on: fight the nearest enemy in place; an enemy just fought
        // (nothing was in reach) is left alone for a few seconds unless it comes adjacent, and AutoExplore carries on
        // with its route meanwhile, so an unreachable enemy can't stall the run.
        private static readonly Dictionary<Chara, System.DateTime> rested = new Dictionary<Chara, System.DateTime>();
        private static readonly System.Reflection.FieldInfo stateField = AccessTools.Field(typeof(Explorer), "state");
        private static readonly System.Reflection.FieldInfo finderField = AccessTools.Field(typeof(Explorer), "actionFinder");
        private static readonly System.Reflection.MethodInfo handleActions = AccessTools.Method(typeof(Explorer), "HandleActions");

        [HarmonyPatch(typeof(Explorer), "HandleCombat")]
        private static class Takeover
        {
            private static bool Prefix(Explorer __instance, List<Chara> enemies)
            {
                Release();
                if (!On || stateField == null || finderField == null || handleActions == null) return true;
                if (!__instance.AutoExplorerConfig.HandleFighting.Value) return true;
                var pc = EClass.pc;
                var e = enemies.OrderBy(c => c.pos.Distance(pc.pos)).First();
                bool adjacent = e.pos.Distance(pc.pos) <= 1;
                if (adjacent || !rested.TryGetValue(e, out var until) || until < System.DateTime.UtcNow)
                {
                    rested[e] = System.DateTime.UtcNow.AddSeconds(5);
                    MultiTool.Log("fight in place: " + e.Name + (adjacent ? " (adjacent)" : ""));
                    pc.SetAIImmediate(new HoldCombat(e));
                    stateField.SetValue(__instance, System.Enum.Parse(stateField.FieldType, "Combat"));
                    return false;
                }
                // Nothing to fight right now: keep going with the route.
                var finder = (Elin_AutoExplore.AIActionFinder)finderField.GetValue(__instance);
                var acts = finder.FindPotentialActions();
                if (acts.Count > 0) handleActions.Invoke(__instance, new object[] { acts });
                return false;
            }
        }
    }

    // The game's auto-combat, with "don't chase" on while it runs. The player's own setting is saved when the fight
    // starts and put back when it ends; Combat.Release also puts it back whenever no in-place fight is running.
    public sealed class HoldCombat : GoalAutoCombat
    {
        public HoldCombat(Chara e) : base(e) { }
        public override void OnStart() { Combat.Hold(); base.OnStart(); }
        public override void OnCancelOrSuccess() { base.OnCancelOrSuccess(); Combat.Release(); }
    }
}
