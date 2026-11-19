using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using Newtonsoft.Json;

namespace AutoExploreLockpicking
{
    // Named lists for AutoExplore's own restriction strings (gathering, mining, pickup), like the material filters:
    // author several, keep one active. The active list's text is what AutoExplore's config holds, so AutoExplore
    // itself still reads its own setting; editing the active list or switching lists rewrites that setting.
    internal static class NamedRestrictions
    {
        private sealed class Set { public Dictionary<string, string> Lists = new Dictionary<string, string>(); public string Active = "list_1"; }

        private static Dictionary<string, Set> data;
        private static string file;

        internal static void Bind(ConfigFile c)
        {
            file = Path.Combine(Path.GetDirectoryName(c.ConfigFilePath), "gharkz.autoexplorelockpicking.restrictions.json");
            try { data = File.Exists(file) ? JsonConvert.DeserializeObject<Dictionary<string, Set>>(File.ReadAllText(file)) : null; } catch { data = null; }
            data = data ?? new Dictionary<string, Set>();
        }

        internal static bool Handles(ConfigEntryBase e) => e.BoxedValue is string && e.Definition.Section == "Restrictions";

        // The set for this setting. AutoExplore's config is the truth for the active list (it may be edited by hand).
        private static Set For(ConfigEntryBase e)
        {
            if (!data.TryGetValue(e.Definition.Key, out var s)) data[e.Definition.Key] = s = new Set();
            if (s.Lists.Count == 0) s.Lists["list_1"] = "";
            if (!s.Lists.ContainsKey(s.Active ?? "")) s.Active = s.Lists.Keys.First();
            s.Lists[s.Active] = (string)e.BoxedValue ?? "";
            return s;
        }

        private static void Save()
        {
            try { File.WriteAllText(file, JsonConvert.SerializeObject(data, Formatting.Indented)); }
            catch (Exception ex) { UnityEngine.Debug.LogException(ex); }
        }

        private static void Apply(ConfigEntryBase e, Set s) { e.BoxedValue = s.Lists[s.Active]; Restrictions.Reload(); Save(); }

        internal static string ActiveName(ConfigEntryBase e) => For(e).Active;

        internal static void Manage(ConfigEntryBase e, string title, string hint, Action refresh)
        {
            var s = For(e);
            UiTable.Window w = null;
            Action reopen = () => { w?.Layer.Close(); refresh(); Manage(e, title, hint, refresh); };
            var rows = new List<UiRow> { new UiRow { Label = L.T("List"), ColumnHeader = true, Cells = new[] { L.T("Entries"), "", "", "" } } };
            foreach (var name in s.Lists.Keys.OrderBy(n => n).ToList())
            {
                var n = name;
                int count = TextArea.Entries(s.Lists[n]).Count;
                rows.Add(new UiRow
                {
                    Label = n, Checked = n == s.Active,
                    Click = _ => { s.Active = n; Apply(e, s); reopen(); },
                    Cells = new[] { count == 0 ? L.T("(empty)") : L.F("{0} entries", count), L.T("Edit"), L.T("Rename"), L.T("Delete") },
                    GreyCells = new[] { false, false, false, s.Lists.Count == 1 },
                    CellClick = new Action<UiRow, int>[]
                    {
                        null,
                        (r, _) => TextArea.Open(title + ": " + n, s.Lists[n], text =>
                        {
                            s.Lists[n] = text;
                            if (n == s.Active) Apply(e, s); else Save();
                            reopen();
                        }, hint, Restrictions.For(e)),
                        (r, _) => AskName(L.F("Rename {0}", n), n, s, nn =>
                        {
                            s.Lists[nn] = s.Lists[n]; s.Lists.Remove(n);
                            if (s.Active == n) s.Active = nn;
                            Save(); reopen();
                        }),
                        (r, _) =>
                        {
                            if (s.Lists.Count == 1) return; // greyed out: one list always remains
                            Dialog.YesNo(L.F("Delete the list \"{0}\"?", n), () =>
                            {
                                bool was = s.Active == n; s.Lists.Remove(n);
                                if (was) { s.Active = s.Lists.Keys.First(); Apply(e, s); } else Save();
                                reopen();
                            });
                        },
                    },
                });
            }
            string note = L.T("Click a list to make it active. AutoExplore uses the active one.\nYou can't delete the last list; keep at least one.\nName lists for what they do, e.g. no_moss_or_pebbles.");
            w = UiTable.Open(L.F("{0} lists", title.EndsWith(" list") ? title.Substring(0, title.Length - 5) : title), rows, new[] { 0f, .36f, .62f, .74f, .88f, 1f }, 640,
                UnityEngine.Mathf.Min(280 + rows.Count * 34, 680), footNote: note, corner: new List<(string, Action)>
                {
                    (L.T("New List"), () => AskName(L.T("New list"), "list_" + (s.Lists.Count + 1), s, nn =>
                    {
                        s.Lists[nn] = ""; s.Active = nn; Apply(e, s); reopen();
                    })),
                });
        }

        private static void AskName(string title, string start, Set s, Action<string> ok)
        {
            Dialog.InputName(title, start, (cancel, text) =>
            {
                if (cancel) return;
                var n = (text ?? "").Trim();
                if (n.Length == 0 || n.Length > 40 || s.Lists.ContainsKey(n)) { Msg.SayRaw(L.T("That name is empty, too long or already used.")); return; }
                ok(n);
            });
        }
    }
}
