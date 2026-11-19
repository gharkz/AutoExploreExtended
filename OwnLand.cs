using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using Elin_AutoExplore;
using HarmonyLib;
using Newtonsoft.Json;

namespace AutoExploreExtended
{
    // Home rules (your own land), when "Own land: natural things too" is on:
    //  - Objects (wild plants, stones, trees, rocks) are fair game even on tiles you've changed; crops on farm fields
    //    stay protected.
    //  - Walls and floors are never mined or dug at home, built or not (the game doesn't record who built a wall),
    //    unless their name is on the base's active whitelist ("Home walls & floors"; named lists, each base remembers
    //    which one it uses).
    // Off: AutoExplore's own rule (nothing on a tile you've changed). Chests, shrines, statues and traps always keep
    // AutoExplore's own home rules.
    internal static class OwnLand
    {
        private static ConfigEntry<bool> on, listOn;
        internal static ConfigEntry<string> List;
        internal static bool On => on != null && on.Value;
        private static string file;
        private static Dictionary<string, string> perBase = new Dictionary<string, string>();

        internal static void Bind(ConfigFile c)
        {
            on = c.Bind("Exploration", "OwnLandNoProtection", false,
                "On your own land, harvest everything natural, even on tiles you've changed: wild plants, stones, trees and rocks. Crops on your farm fields stay protected. Walls and floors at home are only mined or dug when their name is on the home whitelist.");
            listOn = c.Bind("Exploration", "HomeWhitelistOn", false,
                "Use the home whitelist: walls and floors named on it may be mined or dug at home. Off: no walls or floors at home are touched.");
            List = c.Bind("Exploration", "HomeWallsAndFloors", "",
                "Walls and floors that may be mined or dug at home (used when \"Own land\" is on). Names separated by commas, e.g. soil block, grass. Each base remembers which list it uses.");
            file = Path.Combine(Path.GetDirectoryName(c.ConfigFilePath), "gharkz.autoexploreextended.homes.json");
            try { if (File.Exists(file)) perBase = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(file)) ?? perBase; } catch { }
        }

        private static bool AtHome => EClass._zone != null && EClass._zone.IsPCFaction;

        // Objects on this tile may be worked.
        internal static bool ObjOk(Point p) => !AtHome || (On ? !p.cell.IsFarmField : !p.cell.isModified);
        // The wall on this tile may be mined.
        internal static bool WallOk(Point p) => !AtHome || (On ? White(p.cell.GetBlockName(), p.sourceBlock.GetName()) : !p.cell.isModified);
        // The floor on this tile may be dug.
        internal static bool FloorOk(Point p) => !AtHome || (On ? White(p.cell.GetFloorName(), p.sourceFloor.GetName()) : !p.cell.isModified);

        // ---- The base's whitelist ----
        private static string lastText; private static HashSet<string> names = new HashSet<string>();
        private static Zone lastZone;
        private static bool White(string full, string plain)
        {
            if (!listOn.Value) return false;
            Follow();
            string text = List.Value ?? "";
            if (text != lastText) { lastText = text; names = new HashSet<string>(); foreach (var n in TextArea.Entries(text)) names.Add(IgnoreNames.Key(n)); }
            return names.Count > 0 && (names.Contains(IgnoreNames.Key(full ?? "")) || names.Contains(IgnoreNames.Key(plain ?? "")));
        }

        // Each base keeps its own list: arriving switches to it; choosing a list at a base records it.
        private static void Follow()
        {
            var z = EClass._zone; if (z == null) return;
            string key = z.uid.ToString();
            if (z != lastZone)
            {
                lastZone = z;
                if (perBase.TryGetValue(key, out var want)) NamedRestrictions.SetActive(List, want);
            }
            string now = NamedRestrictions.ActiveName(List);
            if (!perBase.TryGetValue(key, out var had) || had != now)
            {
                perBase[key] = now;
                try { File.WriteAllText(file, JsonConvert.SerializeObject(perBase, Formatting.Indented)); } catch { }
            }
        }

        // ---- AutoExplore's own modes ----
        private static Point at; private static bool mining;
        [HarmonyPatch(typeof(AIActionFinder), "GetMineTask")]
        private static class Mine
        {
            static void Prefix(Point p) { at = p; mining = true; }
            static void Postfix(Point p, ref TaskMine __result) { if (__result != null && On && AtHome && !WallOk(p)) __result = null; }
            static void Finalizer() { at = null; }
        }
        [HarmonyPatch(typeof(AIActionFinder), "GetHarvestTask")]
        private static class Harvest { static void Prefix(Point p) { at = p; mining = false; } static void Finalizer() { at = null; } }

        [HarmonyPatch(typeof(AIActionFinder), "IsOwnLand", MethodType.Getter)]
        private static class NotHome
        {
            private static void Postfix(ref bool __result)
            {
                if (!__result || at == null || !On) return;
                if (mining ? WallOk(at) : at.HasObj && ObjOk(at)) __result = false;
            }
        }
    }
}
