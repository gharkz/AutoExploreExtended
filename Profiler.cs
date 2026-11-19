using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace AutoExploreExtended
{
    // Dev aid (Debug/Profile, off by default): times AutoExplore's hot methods and our own hooks while
    // AutoExplore runs, and writes a summary every 5 seconds to BepInEx's log and to autoexplore-profile.txt
    // in the BepInEx folder. Patched only while enabled.
    internal class Profiler : MonoBehaviour
    {
        private static ConfigEntry<bool> on;
        private static Harmony harmony;
        private static readonly Dictionary<MethodBase, (long ticks, int calls)> stats = new Dictionary<MethodBase, (long, int)>();
        private static int frames;
        private static float since;
        private static string file;

        private static readonly (string type, string method)[] Targets =
        {
            ("Elin_AutoExplore.Plugin", "Update"),
            ("Elin_AutoExplore.Plugin", "FindVisibleEnemies"),
            ("Elin_AutoExplore.Plugin", "NeedsRest"),
            ("Elin_AutoExplore.AIActionFinder", "FindPotentialActions"),
            ("Elin_AutoExplore.AIActionFinder", "FindTrap"),
            ("Elin_AutoExplore.AIActionFinder", "RefreshFlood"),
            ("Elin_AutoExplore.AIActionFinder", "ClassifyOnTile"),
            ("Elin_AutoExplore.AIActionFinder", "ClassifyApproach"),
            ("Elin_AutoExplore.AIActionFinder", "GetMineTask"),
            ("Elin_AutoExplore.AIActionFinder", "GetHarvestTask"),
            ("Elin_AutoExplore.AIActionFinder", "IsReachableAStar"),
            ("Elin_AutoExplore.AIActionFinder", "FindHarvestables"),
            ("Elin_AutoExplore.AIActionFinder", "FindMineables"),
            // Elin: the frame, the scene, drawing, and what a mined block sets off.
            ("Core", "Update"), ("Game", "OnUpdate"), ("Scene", "OnUpdate"), ("BaseGameScreen", "Draw"), ("BaseTileMap", "Draw"),
            ("Map", "RefreshFOV"), ("Map", "RefreshNeighborTiles"), ("Map", "RefreshSingleTile"), ("Map", "MineBlock"), ("Map", "SetBlock"),
            ("Map", "_ValidateInstalled"), ("Card", "CalculateFOV"), ("Fov", "ListPoints"), ("TaskMine", "OnProgress"),
            ("Player", "RefreshCurrentHotItem"),
        };

        internal static void Register(ConfigFile config, GameObject host)
        {
            on = config.Bind("Debug", "Profile", false,
                "Time AutoExplore's search while it runs; summary every 5 s in the BepInEx log and autoexplore-profile.txt.");
            file = System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "autoexplore-profile.txt");
            host.AddComponent<Profiler>();
            on.SettingChanged += (_, __) => Apply();
            Apply();
        }

        private static void Apply()
        {
            if (on.Value && harmony == null)
            {
                harmony = new Harmony(Plugin.Id + ".profiler");
                var asm = typeof(Elin_AutoExplore.Plugin).Assembly;
                var methods = new HashSet<MethodBase>();
                foreach (var (type, method) in Targets)
                {
                    var t = asm.GetType(type) ?? typeof(EClass).Assembly.GetType(type);
                    if (t == null) continue;
                    foreach (var m in AccessTools.GetDeclaredMethods(t)) if (m.Name == method && !m.IsAbstract && !m.IsGenericMethodDefinition) methods.Add(m);
                }
                // Everything any other mod patches, so per-frame hooks elsewhere show up too.
                foreach (var m in Harmony.GetAllPatchedMethods().ToList())
                    if (!m.IsAbstract && !(m is ConstructorInfo) && !m.IsGenericMethodDefinition) methods.Add(m);
                foreach (var m in methods)
                {
                    try
                    {
                        harmony.Patch(m, new HarmonyMethod(typeof(Profiler), nameof(Pre)) { priority = Priority.First },
                            new HarmonyMethod(typeof(Profiler), nameof(Post)) { priority = Priority.Last });
                    }
                    catch (Exception x) { UnityEngine.Debug.LogWarning("[profile] skip " + m.DeclaringType?.Name + "." + m.Name + ": " + x.Message); }
                }
                stats.Clear(); frames = 0; since = 0;
            }
            else if (!on.Value && harmony != null) { harmony.UnpatchSelf(); harmony = null; }
        }

        private static void Pre(out long __state) => __state = Stopwatch.GetTimestamp();
        private static void Post(long __state, MethodBase __originalMethod)
        {
            long d = Stopwatch.GetTimestamp() - __state;
            stats.TryGetValue(__originalMethod, out var s);
            stats[__originalMethod] = (s.ticks + d, s.calls + 1);
        }

        private void Update()
        {
            if (harmony == null || EClass.core == null || !EClass.core.IsGameStarted) return;
            var ae = Elin_AutoExplore.Plugin.Instance;
            bool auto = ae != null && ae.IsRunning; if (!auto && !(EClass.pc?.ai is TaskMine)) return;
            frames++; since += Time.unscaledDeltaTime;
            if (since < 5f) return;
            var lines = new List<string> { $"[profile] {(auto ? "AUTO" : "BY HAND")} {since:F1}s, {frames} frames ({frames / since:F0} fps), map {EClass._map?.Size}x{EClass._map?.Size}, ai {EClass.pc?.ai?.GetType().Name}" };
            foreach (var kv in stats.OrderByDescending(k => k.Value.ticks).Take(30))
            {
                double ms = kv.Value.ticks * 1000.0 / Stopwatch.Frequency;
                lines.Add($"  {kv.Key.DeclaringType?.Name}.{kv.Key.Name}({kv.Key.GetParameters().Length}): {ms / since:F1} ms/s, {kv.Value.calls / since:F0} calls/s, {ms / Math.Max(1, kv.Value.calls):F3} ms/call");
            }
            var text = string.Join("\n", lines);
            UnityEngine.Debug.Log(text);
            try { System.IO.File.AppendAllText(file, DateTime.Now.ToString("HH:mm:ss ") + text + "\n"); } catch { }
            stats.Clear(); frames = 0; since = 0;
        }
    }
}
