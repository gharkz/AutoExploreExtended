using System;
using BepInEx.Configuration;
using HarmonyLib;

namespace AutoExploreLockpicking
{
    // "AutoExplore Extended" in the menu you get on your own tile (next to AutoExplore's own "AutoExplore Settings"):
    // a second way into the settings window, alongside System > Mod.
    [HarmonyPatch]
    internal static class SelfMenu
    {
        private const string Label = "AutoExplore Extended";
        private static ConfigEntry<bool> on;

        internal static void Bind(ConfigFile c) =>
            on = c.Bind("Menu", "SelfMenuEntry", true,
                "Show \"AutoExplore Extended\" in your action menu (the game's \"All Action\", middle-click by default; rebind it under Options > Input) when used on yourself. System > Mod always has it.");

        [HarmonyPatch(typeof(ActPlan), "ShowContextMenu"), HarmonyPrefix, HarmonyPriority(Priority.Low)]
        private static void Add(ActPlan __instance)
        {
            if (on != null && !on.Value) return;
            if (EClass.pc == null || !__instance.pos.Equals(EClass.pc.pos)) return;
            var act = new DynamicAct(Label, () => { Settings.OpenWindow(); return false; }, false);
            __instance.list.Add(new ActPlan.Item { act = act });
        }

        // Open the window directly instead of spending a turn on it.
        [HarmonyPatch(typeof(ActPlan.Item), "Perform"), HarmonyPrefix]
        private static bool Perform(ActPlan.Item __instance)
        {
            if (__instance.act is DynamicAct d && d.id == Label) { Ui.Guard(Settings.OpenWindow); return false; }
            return true;
        }
    }
}
