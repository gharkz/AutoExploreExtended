using System.Collections.Generic;
using BepInEx.Configuration;
using Elin_AutoExplore;
using HarmonyLib;

namespace AutoExploreExtended
{
    // Sweep (multi-tool mode, optional): instead of always taking the nearest job anywhere, cover the map in a
    // snake-shaped route through square chunks, finishing each chunk (its jobs, and exploring it) before moving on.
    // Chunks with nothing to do are skipped from a quick look at their tiles. When the route is done, plain
    // nearest-first takes over again for anything left.
    internal static class Sweep
    {
        private static int C = 7;   // chunk size: the width you see as you walk (2 x sight radius + 1), fixed per map when first planned
        private static ConfigEntry<bool> on, onBase;
        private static Zone zone;
        private static string preset;
        private static List<int> route;
        private static int idx, next = -1; // next: the following chunk worth visiting, found ahead of time (-1: not yet)
        private static bool done, rerunning, bypass;
        private static readonly HashSet<int> finished = new HashSet<int>(); // chunks already swept on this map
        private static Zone finishedZone;

        internal static void Bind(ConfigFile c)
        {
            on = c.Bind("Multi-tool", "Sweep", true,
                "Work the map in a sweeping route, chunk by chunk, instead of always the nearest job. Less back and forth; falls back to nearest-first when the route is done.");
            onBase = c.Bind("Exploration", "SweepBaseModes", false,
                "Experimental: AutoExplore's own modes (Explore, Gather, Mining, Combined) cover the map in lawnmower lanes as wide as your sight, chunk by chunk, instead of always heading for the nearest unexplored tile or target. Falls back to nearest-first when the route is done.");
        }

        // Running AutoExplore outside multi-tool mode (its own modes).
        private static bool BaseRunning => !MultiTool.Active && Elin_AutoExplore.Plugin.Instance != null && Elin_AutoExplore.Plugin.Instance.IsRunning &&
            EClass.pc != null && !EClass.pc.isDead && EClass._zone != null && !EClass._zone.IsRegion;

        // The grid covers the map's playable bounds only (many maps sit in a much larger grid of void), cut into
        // equal bands of about C tiles on each axis.
        private static MapBounds B => EClass._map.bounds;
        private static int SpanX => B.maxX - B.x + 1;
        private static int SpanZ => B.maxZ - B.z + 1;
        // For the trace: steering state in a few words.
        internal static string Describe() => route == null ? "none" : done ? "done" : "chunk " + route[idx] + " step " + (idx + 1) + "/" + route.Count + (next >= 0 && next < route.Count ? " next " + route[next] : "");

        private static int Width => System.Math.Max(1, (System.Math.Max(SpanX, SpanZ) + C / 2) / C);   // bands per side
        private static int BandX(int v) => System.Math.Max(0, System.Math.Min(Width - 1, (v - B.x) * Width / SpanX));
        private static int BandZ(int v) => System.Math.Max(0, System.Math.Min(Width - 1, (v - B.z) * Width / SpanZ));
        private static int FromX(int band) => B.x + band * SpanX / Width;   // first tile of a band
        private static int FromZ(int band) => B.z + band * SpanZ / Width;
        private static int Chunk(Point p) => BandX(p.x) + BandZ(p.z) * Width;

        // Active while a multi-tool preset runs with sweep on and the route isn't finished.
        internal static bool Steering
        {
            get
            {
                bool multi = on != null && on.Value && MultiTool.Active, plain = onBase != null && onBase.Value && BaseRunning;
                if (!multi && !plain) return false;
                if (zone != EClass._zone || preset != MultiTool.ActiveName) Plan();
                return !done;
            }
        }

        // A snake route over the chunk rows, starting from the corner nearest the player.
        private static void Plan()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            if (finishedZone != EClass._zone)
            {
                finished.Clear(); finishedZone = EClass._zone;
                C = System.Math.Max(5, System.Math.Min(17, EClass.pc.GetSightRadius() * 2 + 1)); // daylight: wide lines; torchlight: narrow
            }
            zone = EClass._zone; preset = MultiTool.ActiveName; idx = 0; next = -1; done = false;
            // Lawnmower lines starting where you are: lines run parallel to the nearer edge. First the lines from yours
            // toward that edge (the first starting at you, toward its nearer end), then one short walk back for the rest
            // of your line, then the lines on the other side. In a corner or by a side the first part is tiny.
            int w = Width, last = w - 1;
            int px = BandX(EClass.pc.pos.x), pz = BandZ(EClass.pc.pos.z);
            bool columns = System.Math.Min(px, last - px) <= System.Math.Min(pz, last - pz); // nearer a left/right side
            int a0 = columns ? px : pz, b0 = columns ? pz : px;                         // across-line, along-line
            int toEdge = a0 <= last - a0 ? -1 : 1;                                      // toward the nearer side
            bool up = b0 > last - b0;                                                   // first line heads to its nearer end
            route = new List<int>();
            var seen = new HashSet<int>();
            void Add(int across, int along) { int c = columns ? across + along * w : along + across * w; if (seen.Add(c)) route.Add(c); }
            void Line(int across, bool increasing) { for (int j = 0; j < w; j++) Add(across, increasing ? j : last - j); }
            for (int j = b0; j >= 0 && j <= last; j += up ? 1 : -1) Add(a0, j);        // from you to the line's near end
            bool dir = !up;
            for (int i = a0 + toEdge; i >= 0 && i <= last; i += toEdge) { Line(i, dir); dir = !dir; } // toward the near side
            Line(a0, dir); dir = !dir;                                                  // the short walk back: rest of your line
            for (int i = a0 - toEdge; i >= 0 && i <= last; i -= toEdge) { Line(i, dir); dir = !dir; } // the other side
            // No scan up front: the route is just the pattern; chunks are only looked at as the sweep reaches them.
            while (idx < route.Count && finished.Contains(route[idx])) idx++;
            if (idx >= route.Count) done = true;
            MultiTool.Log("sweep planned: " + route.Count + " chunks of " + C + " tiles, starting at chunk " + (done ? -1 : route[idx]) + " (" + sw.ElapsedMilliseconds + " ms)");
        }

        // Skip chunks with nothing to do: no multi-tool job and nothing unexplored.
        private static AIActionFinder finder; // AutoExplore's search, for its reachability map
        private static void Skip()
        {
            while (idx < route.Count && (finished.Contains(route[idx]) || !Worth(route[idx]))) idx++;
            if (idx >= route.Count) { done = true; MultiTool.Log("sweep done; nearest-first from here"); }
        }

        private static bool Worth(int chunk)
        {
            int w = Width, x0 = FromX(chunk % w), z0 = FromZ(chunk / w), x1 = FromX(chunk % w + 1), z1 = FromZ(chunk / w + 1);
            var p = new Point();
            for (int z = z0; z < z1; z++)        // anything left to explore? (cheap)
                for (int x = x0; x < x1; x++) { p.Set(x, z); if (!p.IsSeen && !p.HasBlock && Reach(p)) return true; }
            for (int z = z0; z < z1; z++)        // then any job for the preset
                for (int x = x0; x < x1; x++)
                {
                    var c = EClass._map.cells[x, z];
                    if (!c.HasObj && !c.HasBlock && c.Things.Count == 0 && (c.detail == null || c.detail.charas.Count == 0) && !MultiTool.Uses(MultiTool.Kind.Shovel)) continue; // nothing there for any tool
                    p.Set(x, z); if (MultiTool.Active && Reach(p) && MultiTool.Job(p.Copy()) is MultiJob j && j.kind != MultiTool.Kind.Shears) return true; // animals don't hold a chunk
                }
            return false;
        }

        // Only what AutoExplore can actually walk to counts (unseen open water, for one, never does).
        private static bool Reach(Point p) { try { return finder == null || finder.IsPointReachable(p, 1); } catch { return true; } }

        // Work within a few tiles of you is always fair game (trees beside the path get done in passing), so the
        // route doesn't leave things behind only to walk back for them.
        private const int Near = 3;
        private static bool Outside(Point p)
        {
            if (bypass || p == null || route == null || done || idx >= route.Count) return false;
            int c = Chunk(p);
            bool inNext = next >= 0 && next < route.Count && c == route[next]; // next == route.Count: nothing further ahead
            return c != route[idx] && !inNext && p.Distance(EClass.pc.pos) > Near;
        }

        // Look ahead: the next chunk worth visiting, found while the current one is still being worked.
        private static void FindNext()
        {
            int i = idx + 1;
            while (i < route.Count && (finished.Contains(route[i]) || !Worth(route[i]))) i++;
            next = i < route.Count ? i : route.Count;
        }

        // Inside AutoExplore's search: only the current chunk counts while steering (jobs, exploring, pick-ups).
        [HarmonyPatch(typeof(AIActionFinder), "ClassifyApproach"), HarmonyPriority(Priority.Last)]
        private static class Approach
        {
            private static void Postfix(Point t, ref AIAct __result) { if (__result != null && Steering && Outside(t)) __result = null; }
        }

        [HarmonyPatch(typeof(AIActionFinder), "ClassifyOnTile"), HarmonyPriority(Priority.Last)]
        private static class OnTile
        {
            private static void Postfix(Point c, ref AIAct __result) { if (__result != null && Steering && Outside(c)) __result = null; }
        }

        // An empty search means the current chunk is finished: move along the route and search again.
        [HarmonyPatch(typeof(AIActionFinder), nameof(AIActionFinder.FindPotentialActions))]
        private static class Advance
        {
            private static void Postfix(AIActionFinder __instance, ref List<AIAct> __result)
            {
                if (rerunning || !Steering) return;
                finder = __instance;
                if (next < 0 && !done) FindNext(); // once per chunk, while there's still work here
                if (__result != null && __result.Count > 0)
                {
                    // Full search mode returns everything: keep the current chunk's actions when there are any.
                    var mine = __result.FindAll(a => !Outside(a.GetDestinationPoint()));
                    if (mine.Count > 0) { __result = mine; return; }
                    if (__result.Count > 1) __result = new List<AIAct>(); // early exit gives one action; full search: none here
                    else return;
                }
                rerunning = true;
                try
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew(); int skipped = idx;
                    int tries = 0;
                    while (!done && (__result == null || __result.Count == 0))
                    {
                        if (++tries > 6) { bypass = true; try { __result = __instance.FindPotentialActions(); } finally { bypass = false; } MultiTool.Log("sweep: several empty chunks in a row; nearest job this once, route continues"); break; }
                        finished.Add(route[idx]);
                        idx = next > idx ? next : idx + 1; next = -1;
                        if (idx < route.Count && Worth(route[idx])) { } else Skip();
                        if (idx >= route.Count) { done = true; MultiTool.Log("sweep done; nearest-first from here"); }
                        if (!done) MultiTool.Log("sweep: next chunk " + route[idx] + " (" + (idx + 1) + "/" + route.Count + ")");
                        __result = __instance.FindPotentialActions();
                    }
                    MultiTool.Log("sweep advance took " + sw.ElapsedMilliseconds + " ms over " + (idx - skipped) + " chunks");
                }
                finally { rerunning = false; }
            }
        }
    }
}
