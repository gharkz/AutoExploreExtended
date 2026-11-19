using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using UnityEngine;
using Explorer = Elin_AutoExplore.Plugin;

namespace AutoExploreExtended
{
    // System > Mod > AutoExplore Extended: a parchment settings window (Layout/Logistics style) with one tab per
    // AutoExplore config section plus a Tools tab for this mod. Each value is set on its owner's ConfigEntry, so
    // AutoExplore's settings land in AutoExplore's own .cfg; BepInEx saves on set, so changes apply without a restart.
    internal class Settings : MonoBehaviour
    {
        private static ConfigFile own;
        private static UiTable.Window win;
        private static int tab;
        private static ConfigEntryBase capturing;   // key binding waiting for a key press
        private static UiRow capturingRow;
        private static readonly float[] Edges = { 0f, .56f, .66f, .90f, 1f }; // name | − | value | +
        private const string Title = "AutoExplore Extended";

        internal static void Register(ConfigFile ownConfig, GameObject host)
        {
            own = ownConfig;
            host.AddComponent<Settings>();
            ModUtil.AddContextMenuEntry(Open, Title, Title);
        }

        // Tabs: AutoExplore's sections in its own order (Debug folded into Performance, our material filters into
        // Restrictions as "Filters"), then the rest of ours on one "Tools" tab.
        private static List<(string name, List<ConfigEntryBase> entries)> Tabs()
        {
            var tabs = new List<(string, List<ConfigEntryBase>)>();
            var mine = own.Keys.OrderBy(k => k.Section).Select(d => own[d]).ToList();
            List<ConfigEntryBase> Take(string section) { var l = mine.Where(e => e.Definition.Section == section).ToList(); mine.RemoveAll(l.Contains); return l; }
            var ae = Explorer.Instance?.Config;
            if (ae != null)
            {
                var groups = ae.Keys.GroupBy(k => k.Section).ToList();
                var debug = groups.FirstOrDefault(g => g.Key == "Debug")?.Select(d => ae[d]).ToList() ?? new List<ConfigEntryBase>();
                foreach (var g in groups)
                {
                    if (g.Key == "Debug") continue;
                    var list = g.Select(d => ae[d]).ToList();
                    string name = g.Key;
                    if (g.Key == "Performance") { list.AddRange(debug); list.AddRange(Take("Debug")); debug = null; }
                    if (g.Key == "Restrictions") { list.AddRange(Take("Materials")); name = "Filters"; }
                    tabs.Add((name, list));
                }
                if (debug != null && debug.Count > 0) tabs.Add(("Debug", debug));
            }
            tabs.Add(("Tools", mine));
            return tabs;
        }

        internal static void OpenWindow() => Open();
        private static void Open()
        {
            if (win != null && win.Layer != null) win.Layer.Close();
            tab = Mathf.Clamp(tab, 0, Tabs().Count - 1);
            var (rows, h) = Build();
            win = UiTable.Open(Title, rows, Edges, 780, h);
            win.Layer.SetOnKill(() => { win = null; capturing = null; });
        }

        private static void Show(int t)
        {
            tab = t; capturing = null;
            var (rows, h) = Build();
            UiTable.Rebuild(win, Title, rows, Edges, 780, h);
        }

        private static (List<UiRow>, float) Build()
        {
            var tabs = Tabs();
            var rows = new List<UiRow> { new UiRow { Label = "", Tabs = tabs.Select(t => L.T(t.name)).ToArray(), ActiveTab = tab, TabClick = Show } };
            string section = null;
            bool ours = tab == tabs.Count - 1;
            bool materials = false;
            bool headings = ours || tabs[tab].entries.Select(x => x.Definition.Section).Distinct().Count() > 1;
            foreach (var e in tabs[tab].entries)
            {
                // The Materials section has its own rows (filter mode + named lists).
                if (e.Definition.Section == "Materials" && e.ConfigFile == own)
                {
                    if (!materials) { materials = true; rows.Add(new UiRow { Label = L.T("Materials"), Heading = true, Section = true }); rows.AddRange(Materials.Rows(() => Show(tab))); }
                    continue;
                }
                if (headings && e.Definition.Section != section)
                {
                    section = e.Definition.Section;
                    rows.Add(new UiRow { Label = L.T(section == "Locked chests" ? "Locked containers" : section), Heading = true, Section = true }); // section key kept for saved settings
                }
                var row = Row(e); row.Hint = L.T(e.Description?.Description); rows.Add(row);
            }
            // Sliders wander; BepInEx remembers each setting's default, so one click puts them back.
            var numbers = tabs[tab].entries.Where(e => e.BoxedValue is int || e.BoxedValue is float).ToList();
            if (numbers.Count > 0)
                rows.Add(new UiRow
                {
                    Label = L.F("Reset sliders to defaults ({0})", string.Join(", ", numbers.Select(e => Name(e) + " " + e.DefaultValue))),
                    Choice = true,
                    Click = r => { foreach (var e in numbers) e.BoxedValue = e.DefaultValue; Show(tab); },
                });
            rows.Add(new UiRow { Label = L.T(ours ? "This mod's settings." : tabs[tab].entries.Any(x => x.ConfigFile == own)
                ? "AutoExplore's settings save to AutoExplore's config file; this mod's settings save to its own." : "AutoExplore's own settings, saved to its config file."), Note = true, Info = true });
            if (tabs[tab].name != "Filters") // Filters scrolls past it, and its lists explain themselves
                rows.Add(new UiRow { Label = L.T("Point at a setting to see what it does."), Note = true, Info = true, HintRow = true });
            if (tab == 0)
            {
                rows.Add(new UiRow { Label = "", Note = true, Info = true }); // gap
                rows.Add(new UiRow { Label = L.T("Changes take effect immediately. No need to restart the game."), Note = true, Info = true });
            }
            return (rows, Mathf.Min(200 + rows.Count * 34, 700));
        }

        // "UseEarlyExitFlood" -> "Use early exit flood"
        private static string Name(ConfigEntryBase e) => L.T(RawName(e));
        private static string RawName(ConfigEntryBase e)
        {
            if (e.Definition.Key == "PickIgnoresInstantActions") return "Lockpicks ignore instant actions";
            if (e.Definition.Key == "PryIgnoresInstantActions") return "Crowbars ignore instant actions";
            if (e.ConfigFile == own && e.Definition.Key == "PracticeChests") return "Practice chests (no damage, no charges used)";
            if (e.ConfigFile == own && e.Definition.Key == "IncludeKarmaTargets") return "Include karma containers (NPC or lost property)";
            if (e.ConfigFile == own && e.Definition.Key == "Enabled" && e.Definition.Section == "Locked chests") return "Open locked containers (crowbar or lockpick)";
            if (e.ConfigFile == own && e.Definition.Key == "Enabled" && e.Definition.Section == "Digging") return "Dig floors and tracks (when shovel tool selected)";
            if (e.ConfigFile == own && e.Definition.Key == "Enabled" && e.Definition.Section == "Shearing")
                return Plugin.ShearingExtension ? "Shear animals (off: the Shearing Extension handles it)" : "Shear animals (when shears tool selected)";
            if (e.ConfigFile == own && e.Definition.Key == "TracksWhileMining") return "Dig tracks while mining (swaps to your best shovel)";
            if (e.Definition.Key == "SelfMenuEntry") return "Show in your own action menu (middle-click yourself)";
            var k = e.Definition.Key; var s = new System.Text.StringBuilder();
            for (int i = 0; i < k.Length; i++)
            {
                if (i > 0 && char.IsUpper(k[i]) && !char.IsUpper(k[i - 1])) s.Append(' ').Append(i + 1 < k.Length && char.IsUpper(k[i + 1]) ? k[i] : char.ToLower(k[i])); // keep acronyms: MinHP -> Min HP
                else s.Append(k[i]);
            }
            return s.ToString();
        }

        private static UiRow Value(ConfigEntryBase e, string shown, Action<UiRow> click) => new UiRow
        {
            Label = Name(e), Cells = new[] { "", shown, "" },
            CellClick = new Action<UiRow, int>[] { null, (r, _) => click(r), null },
        };

        private static UiRow Row(ConfigEntryBase e)
        {
            var v = e.BoxedValue;
            if (v is bool)
                return new UiRow { Label = Name(e), Checked = (bool)v, Click = r => { e.BoxedValue = !(bool)e.BoxedValue; UiTable.SetChecked(r, (bool)e.BoxedValue); } };

            if (v is int || v is float)
            {
                float min = 0, max = 100;
                var range = e.Description.AcceptableValues;
                if (range != null)
                {
                    var t = range.GetType();
                    var lo = t.GetProperty("MinValue")?.GetValue(range); var hi = t.GetProperty("MaxValue")?.GetValue(range);
                    if (lo != null) min = Convert.ToSingle(lo);
                    if (hi != null) max = Convert.ToSingle(hi);
                }
                bool isInt = v is int;
                Func<string> show = () => isInt ? e.BoxedValue.ToString() : Convert.ToSingle(e.BoxedValue).ToString("0.##");
                return new UiRow
                {
                    Label = Name(e), Cells = new[] { "", "", show() },
                    SlideMin = min, SlideMax = max, SlideWhole = isInt, SlideValue = Convert.ToSingle(v),
                    Slide = (r, n) => { e.BoxedValue = isInt ? (object)(int)Mathf.Round(n) : n; UiTable.SetCell(r, 2, show()); },
                };
            }

            if (v is Enum)
                return Value(e, v.ToString(), r =>
                {
                    var values = Enum.GetValues(v.GetType()).Cast<object>().ToList();
                    UiTable.Choose(Name(e), values.Select(x => (x.ToString(), (Action)(() => { e.BoxedValue = x; UiTable.SetCell(r, 1, x.ToString()); }))).ToList(),
                        values.IndexOf(e.BoxedValue));
                });

            if (v is KeyCode || v is KeyboardShortcut)
                return Value(e, v.ToString(), r => { capturing = e; capturingRow = r; UiTable.SetCell(r, 1, L.T("press a key…")); });

            if (NamedRestrictions.Handles(e))
                return Value(e, NamedRestrictions.ActiveName(e), r => NamedRestrictions.Manage(e, Name(e), L.T(e.Description?.Description), () => Show(tab)));

            if (v is string)
                return Value(e, Short((string)v), r =>
                    TextArea.Open(Name(e), (string)e.BoxedValue, text => { e.BoxedValue = text; Restrictions.Reload(); UiTable.SetCell(r, 1, Short(text)); },
                        L.T(e.Description?.Description), Restrictions.For(e)));

            return new UiRow { Label = Name(e), Cells = new[] { "", v?.ToString() ?? "", "" } };
        }

        private static string Short(string s) => string.IsNullOrEmpty(s) ? L.T("(none)") : s.Length > 16 ? s.Substring(0, 16) + "…" : s;

        private void Update()
        {
            if (capturing == null || !Input.anyKeyDown) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { UiTable.SetCell(capturingRow, 1, capturing.BoxedValue.ToString()); capturing = null; return; }
            foreach (KeyCode k in Enum.GetValues(typeof(KeyCode)))
            {
                if (k == KeyCode.None || (k >= KeyCode.Mouse0 && k <= KeyCode.Mouse6) || !Input.GetKeyDown(k)) continue;
                capturing.BoxedValue = capturing.BoxedValue is KeyCode ? (object)k : new KeyboardShortcut(k);
                UiTable.SetCell(capturingRow, 1, capturing.BoxedValue.ToString());
                capturing = null;
                return;
            }
        }
    }
}
