using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoExploreExtended
{
    // Settings rows and windows for multi-tool presets (same patterns as the named lists).
    internal static class MultiToolUi
    {
        internal static string ToolName(MultiTool.Kind k) => L.T(k.ToString());

        internal static IEnumerable<UiRow> Rows(Action refresh)
        {
            yield return new UiRow { Label = L.T("One pass over the map: every job the active preset allows, swapping to your best tool for each."), Note = true, Info = true };
            yield return new UiRow { Label = L.T("The sickle goes first on anything it can reap. Your filters and lists still apply."), Note = true, Info = true };
            yield return new UiRow
            {
                Label = L.T("Manage presets"), Cells = new[] { "", L.T("Manage..."), "" },
                CellClick = new Action<UiRow, int>[] { null, (r, _) => Manage(refresh), null },
            };
        }

        private static string Summary(MultiTool.Preset p) =>
            (p.Tools.Count == 0 ? L.T("(empty)") : p.Tools.Count == MultiTool.All.Length ? L.T("all tools") : L.F("{0} tools", p.Tools.Count)) + (p.UseHeld ? L.T(" + in hand") : "");

        // Click a preset to put it in or take it out of the hotkey cycle; Edit picks its tools.
        private static void Manage(Action refresh, string moved = null, string displaced = null)
        {
            UiTable.Window w = null;
            Action reopen = () => { w?.Layer.Close(); refresh(); Manage(refresh); };
            Action<string, int> move = (n, by) =>
            {
                var o = MultiTool.Order.ToList(); int j = o.IndexOf(n) + by;
                if (j < 0 || j >= o.Count) return;
                string other = o[j]; MultiTool.Move(n, by);
                w?.Layer.Close(); refresh(); Manage(refresh, n, other);
            };
            var rows = new List<UiRow> { new UiRow { Label = L.T("Preset (in cycle)"), ColumnHeader = true, Cells = new[] { L.T("Tools"), "", "", "", "", "" } } };
            var order = MultiTool.Order.ToList();
            foreach (var name in order)
            {
                var n = name; var p = MultiTool.Presets[n];
                rows.Add(new UiRow
                {
                    Label = n, Checked = p.InCycle,
                    Click = _ => { p.InCycle = !p.InCycle; MultiTool.Save(); reopen(); },
                    Cells = new[] { Summary(p), "\u25B2", "\u25BC", L.T("Edit"), L.T("Rename"), L.T("Delete") },
                    GreyCells = new[] { false, n == order[0], n == order[order.Count - 1], false, false, MultiTool.Presets.Count == 1 },
                    CellClick = new Action<UiRow, int>[]
                    {
                        null,
                        (r, _) => move(n, -1),
                        (r, _) => move(n, 1),
                        (r, _) => EditTools(n, reopen),
                        (r, _) => AskName(L.F("Rename {0}", n), n, nn => { MultiTool.Rename(n, nn); reopen(); }),
                        (r, _) =>
                        {
                            if (MultiTool.Presets.Count == 1) return;
                            Dialog.YesNo(L.F("Delete the preset \"{0}\"?", n), () => { MultiTool.Delete(n); reopen(); });
                        },
                    },
                });
            }
            string note = L.T("Click a preset to add it to or remove it from the cycle. The arrows set the cycle order.\nYou can't delete the last preset.\nName presets for what they do, e.g. trees_and_chests.");
            w = UiTable.Open(L.T("Multi-tool presets"), rows, new[] { 0f, .27f, .50f, .56f, .62f, .73f, .86f, 1f }, 880,
                UnityEngine.Mathf.Min(280 + rows.Count * 34, 680), footNote: note, corner: new List<(string, Action)>
                {
                    (L.T("New Preset"), () => AskName(L.T("New preset"), "multitool_" + (MultiTool.Presets.Count + 1), nn => { MultiTool.Add(nn); reopen(); })),
                });
            if (moved != null) SwapFlash.Flash(rows.FirstOrDefault(r => r.Label == moved)?.Bound, rows.FirstOrDefault(r => r.Label == displaced)?.Bound);
        }

        private static void EditTools(string name, Action done)
        {
            var p = MultiTool.Presets[name];
            var rows = new List<UiRow>();
            foreach (var k in MultiTool.All)
            {
                var kind = k;
                rows.Add(new UiRow
                {
                    Label = ToolName(kind), Checked = p.Tools.Contains(kind.ToString()),
                    Click = r =>
                    {
                        if (!p.Tools.Remove(kind.ToString())) p.Tools.Add(kind.ToString());
                        MultiTool.Save(); UiTable.SetChecked(r, p.Tools.Contains(kind.ToString()));
                    },
                });
            }
            rows.Add(new UiRow
            {
                Label = L.T("Tool in Hand"), Checked = p.UseHeld,
                Click = r => { p.UseHeld = !p.UseHeld; MultiTool.Save(); UiTable.SetChecked(r, p.UseHeld); },
                Hint = L.T("Whatever you're holding also does its usual jobs, on top of the tools above."),
            });
            rows.Add(new UiRow
            {
                Label = L.T("Pickaxe & Axe Work"), Cells = new[] { L.T(p.Pickaxe.ToString()) },
                CellClick = new Action<UiRow, int>[] { (r, _) =>
                {
                    var modes = (MultiTool.PickaxeWork[])Enum.GetValues(typeof(MultiTool.PickaxeWork));
                    UiTable.Choose(L.T("Pickaxe & Axe Work"), modes.Select(m => (L.T(m.ToString()), (Action)(() => { p.Pickaxe = m; MultiTool.Save(); UiTable.SetCell(r, 0, L.T(m.ToString())); }))).ToList(),
                        Array.IndexOf(modes, p.Pickaxe));
                } },
            });
            rows.Add(new UiRow { Label = L.T("Tool in Hand: whatever you're holding also does its usual jobs, on top of the tools above."), Note = true, Info = true });
            rows.Add(new UiRow { Label = L.T("Pickaxe & Axe Work: Harvest takes trees, rocks and the like; Mine takes walls. Held tools follow it too."), Note = true, Info = true });
            rows.Add(new UiRow { Label = L.T("Each job uses the best one you carry. Jobs whose own setting is off are skipped."), Note = true, Info = true });
            UiTable.Window w = null;
            w = UiTable.Open(L.F("Tools: {0}", name), rows, new[] { 0f, .58f, 1f }, 720, 640,
                corner: new List<(string, Action)> { (L.T("OK"), () => { w.Layer.Close(); done(); }) });
        }

        private static void AskName(string title, string start, Action<string> ok)
        {
            Dialog.InputName(title, start, (cancel, text) =>
            {
                if (cancel) return;
                var n = (text ?? "").Trim();
                if (n.Length == 0 || n.Length > 40 || MultiTool.Presets.ContainsKey(n)) { Msg.SayRaw(L.T("That name is empty, too long or already used.")); return; }
                ok(n);
            });
        }
    }
}
