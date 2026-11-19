using System;
using System.IO;
using BepInEx.Configuration;
using UnityEngine;

namespace AutoExploreExtended
{
    // Run trace (debug, off by default): while AutoExplore runs, writes your position once a second plus notable events
    // (route steps, jobs, fights, AutoExplore's chosen actions) to BepInEx/aex-trace.csv, cheaply (plain file appends,
    // no Unity logging). Used to draw a picture of a run and find where it doubles back.
    internal sealed class Trace : MonoBehaviour
    {
        private static ConfigEntry<bool> on;
        private static string file;
        private static float next;
        private static Zone zone;
        internal static bool On => on != null && on.Value;

        internal static void Register(ConfigFile c, GameObject host)
        {
            on = c.Bind("Debug", "Trace", false, "Record each AutoExplore run (position every second plus events) to BepInEx/aex-trace.csv for review.");
            file = Path.Combine(BepInEx.Paths.BepInExRootPath, "aex-trace.csv");
            host.AddComponent<Trace>();
        }

        internal static void Event(string kind, Point p, string detail = "")
        {
            if (!On || EClass.pc == null) return;
            Write(kind, p ?? EClass.pc.pos, detail);
        }

        private static void Write(string kind, Point p, string detail)
        {
            try
            {
                if (zone != EClass._zone)
                {
                    zone = EClass._zone; var b = EClass._map.bounds;
                    File.AppendAllText(file, $"{Time.time:F1},map,{b.x},{b.z},\"{zone.Name} {b.x}-{b.maxX} {b.z}-{b.maxZ} sweep {Sweep.Describe()}\"\n");
                }
                File.AppendAllText(file, $"{Time.time:F1},{kind},{p.x},{p.z},\"{detail}\"\n");
            }
            catch { }
        }

        private void Update()
        {
            if (!On || !EClass.core.IsGameStarted || EClass.pc == null || Time.time < next) return;
            next = Time.time + 1f;
            var ae = Elin_AutoExplore.Plugin.Instance;
            if (ae == null || !ae.IsRunning) return;
            Write("pos", EClass.pc.pos, (EClass.pc.ai?.GetType().Name ?? "") + " " + Sweep.Describe());
        }
    }
}
