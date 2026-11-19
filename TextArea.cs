using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace AutoExploreLockpicking
{
    // Optional add-ons for a TextArea: validation and a checklist editor for comma-separated lists.
    internal sealed class TextAreaOptions
    {
        public Func<string, string> Validate;   // error text blocks saving (window stays open); null = fine
        public Func<string, string> Warn;       // shown but does not block
        public Func<List<(string section, List<string> names)>> Choices; // checklist sections; enables "Checklist"
        public Func<string, string> Key = n => n.Trim().ToLowerInvariant(); // when two names count as the same entry
    }

    // A parchment window with a large wrapping text box, for long values (comma lists) the native one-line
    // name prompt cuts off. Enter or OK saves; Esc, Cancel or the close button leave the value unchanged.
    internal static class TextArea
    {
        internal static void Open(string title, string text, Action<string> onSave, string hint = null, TextAreaOptions opt = null)
        {
            opt = opt ?? new TextAreaOptions();
            UiTable.Window win = null;
            List<(UiRow row, string key)> checks = null;
            InputField field = null;
            bool done = false;
            var problem = new UiRow { Label = "", Note = true };
            Text problemLine = null;

            Func<bool> check = () =>
            {
                var err = opt.Validate?.Invoke(field.text);
                var warn = err == null ? opt.Warn?.Invoke(field.text) : null;
                UiTable.SetLabel(problem, err ?? warn ?? "");
                if (problemLine != null) problemLine.text = err ?? warn ?? "";
                return err == null;
            };
            Action save = () =>
            {
                if (done) return;
                var v = field.text.Replace("\r", "").Replace("\n", "");
                field.text = v;
                if (!check()) { field.ActivateInputField(); return; } // rejected: say why, stay open
                done = true; win.Layer.Close(); onSave(v);
            };
            Action cancel = () => { done = true; win.Layer.Close(); };

            bool merged = opt.Choices != null;
            var footer = new List<(string, Action)> { (L.T("Cancel"), cancel), (L.T("OK"), save) }; // primary action rightmost
            List<UiRow> rows;
            if (merged) rows = ChecklistRows(() => field, opt, c => checks = c);
            else
            {
                rows = new List<UiRow>();
                if (!string.IsNullOrEmpty(hint)) rows.Add(new UiRow { Label = hint, Note = true, Info = true });
                rows.Add(problem);
            }
            // Merged: one wide window, the text on the left and the checklist on the right.
            const float split = .46f;
            win = UiTable.Open(title, rows, new[] { 0f, 1f }, merged ? 1180 : 720, merged ? Fit(720) : 500, corner: footer, listFrom: merged ? split : 0f);
            win.Layer.SetOnKill(() => { done = true; });

            var parent = win.Layer.windows[0].transform;
            var font = win.Layer.GetComponentsInChildren<Text>(true).FirstOrDefault(x => x.font != null);
            Func<string, float, float, Color, Text> line = (name, top, h, color) =>
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(LayoutElement));
                go.GetComponent<LayoutElement>().ignoreLayout = true;
                var r = (RectTransform)go.transform; r.SetParent(parent, false);
                r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(split, 1); r.pivot = new Vector2(0, 1);
                r.offsetMin = new Vector2(34, -top - h); r.offsetMax = new Vector2(-10, -top);
                var x = go.GetComponent<Text>(); if (font != null) x.font = font.font;
                x.fontSize = 13; x.color = color; x.alignment = TextAnchor.UpperLeft; x.horizontalOverflow = HorizontalWrapMode.Wrap; x.raycastTarget = false;
                return x;
            };
            float boxTop = string.IsNullOrEmpty(hint) ? 112 : 156;
            if (merged)
            {
                float y = 96;
                if (!string.IsNullOrEmpty(hint)) { line("AEX hint", y, 60, UiTable.Ink).text = hint; y += 64; }
                problemLine = line("AEX problem", y, 22, UiTable.Warn); y += 26;
                var label = line("AEX box label", y, 40, UiTable.Ink); y += 44; // room to wrap in longer languages
                label.text = L.T("Raw list, as saved in the config file. Type here or select items on the right.");
                label.fontStyle = FontStyle.Bold;
                boxTop = y;
            }

            var box = new GameObject("AEX text area", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            box.GetComponent<LayoutElement>().ignoreLayout = true;
            var br = (RectTransform)box.transform; br.SetParent(parent, false);
            br.anchorMin = new Vector2(0, 0); br.anchorMax = new Vector2(merged ? split : 1, 1);
            br.offsetMin = new Vector2(34, 96); br.offsetMax = new Vector2(merged ? -10 : -34, -boxTop);
            var bg = box.GetComponent<Image>(); bg.color = new Color(.38f, .28f, .16f, .12f);
            var outline = box.AddComponent<Outline>(); outline.effectColor = new Color(.38f, .28f, .16f, .45f); outline.effectDistance = new Vector2(1, -1);

            var tgo = new GameObject("AEX text", typeof(RectTransform), typeof(Text));
            var tr = (RectTransform)tgo.transform; tr.SetParent(box.transform, false);
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = new Vector2(10, 8); tr.offsetMax = new Vector2(-10, -8);
            var t = tgo.GetComponent<Text>();
            if (font != null) t.font = font.font;
            t.fontSize = 16; t.color = new Color(.20f, .14f, .08f);
            t.alignment = TextAnchor.UpperLeft; t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = false;

            field = box.AddComponent<InputField>();
            field.textComponent = t;
            field.targetGraphic = bg;
            field.lineType = InputField.LineType.MultiLineSubmit; // wraps on screen; Enter submits
            field.caretColor = new Color(.20f, .14f, .08f);
            field.selectionColor = new Color(.38f, .28f, .16f, .35f);
            field.text = text ?? "";
            field.onEndEdit.AddListener(_ => { if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Ui.Guard(save); });
            // Typing keeps the checklist (when open) and the problem line in step.
            field.onValueChanged.AddListener(v => Ui.Guard(() =>
            {
                check();
                if (checks == null) return;
                var keys = new HashSet<string>(Entries(v).Select(opt.Key));
                foreach (var (row, key) in checks) UiTable.SetChecked(row, keys.Contains(key));
            }));
            check();
            if (checks != null) { var keys0 = new HashSet<string>(Entries(field.text).Select(opt.Key)); foreach (var (row, key) in checks) UiTable.SetChecked(row, keys0.Contains(key)); }
            field.ActivateInputField();
            field.Select();
            field.StartCoroutine(CaretToEnd(field));
        }

        // Cap a window height to the UI's own height (small laptop screens), keeping room around it.
        private static float Fit(float h)
        {
            var root = EClass.ui != null ? EClass.ui.transform.root as RectTransform : null; // the UI canvas
            float avail = root != null && root.rect.height > 0 ? root.rect.height * .88f : h;
            return Mathf.Max(420, Mathf.Min(h, avail));
        }

        // Unity selects everything on focus; put the caret at the end instead, so typing adds rather than replaces.
        private static System.Collections.IEnumerator CaretToEnd(InputField f)
        {
            yield return null;
            if (f == null) yield break;
            f.caretPosition = f.selectionAnchorPosition = f.selectionFocusPosition = f.text.Length;
            f.ForceLabelUpdate();
        }

        internal static List<string> Entries(string text) =>
            (text ?? "").Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();

        // Checklist rows: every choice as a checkbox; ticking adds the name to the text, unticking removes it.
        private static List<UiRow> ChecklistRows(Func<InputField> field, TextAreaOptions opt, Action<List<(UiRow, string)>> bind)
        {
            var checks = new List<(UiRow, string)>();
            var seen = new HashSet<string>();
            var rows = new List<UiRow>();
            foreach (var (section, names) in opt.Choices())
            {
                var fresh = names.Where(n => !string.IsNullOrWhiteSpace(n) && seen.Add(opt.Key(n))).OrderBy(n => n).ToList();
                if (fresh.Count == 0) continue;
                rows.Add(new UiRow { Label = section, Heading = true, Section = true });
                foreach (var name in fresh)
                {
                    var key = opt.Key(name);
                    var row = new UiRow { Label = name, Checked = false };
                    row.Click = r =>
                    {
                        var entries = Entries(field().text);
                        if (entries.Any(e => opt.Key(e) == key)) entries.RemoveAll(e => opt.Key(e) == key);
                        else entries.Add(name);
                        field().text = string.Join(",", entries); // onValueChanged re-ticks every row
                    };
                    rows.Add(row); checks.Add((row, key));
                }
            }
            bind(checks);
            return rows;
        }
    }
}
