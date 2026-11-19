using HarmonyLib;
using Explorer = Elin_AutoExplore.Plugin;

namespace AutoExploreExtended
{
    // Trace AutoExplore's chosen action (what and where), when the trace is on.
    [HarmonyPatch(typeof(Explorer), "HandleActions")]
    internal static class TraceActions
    {
        private static void Prefix(System.Collections.Generic.List<AIAct> acts)
        {
            if (!Trace.On || acts == null || acts.Count == 0) return;
            var a = acts[0];
            Trace.Event("act", a.GetDestinationPoint(), (a is MultiJob mj ? "multi " + mj.kind : a.GetType().Name + " " + a.Name));
        }
    }
}
