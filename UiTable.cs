using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace AutoExploreExtended
{
    // Parchment table windows in the Logistics/Psion style: ink column dividers, inked value buttons, no key badges,
    // optional checkbox, section headings, column headers, notes, footer buttons. Rows update in place.
    internal sealed class UiRow
    {
        public string Label;
        public string[] Cells;           // extra columns after the label
        public bool Heading, ColumnHeader, Note, Info;
        public bool Section;             // a top-level heading: larger, on an inked band (Heading alone = sub-heading)
        public string[] Tabs;            // a tab bar: one inked button per entry, ActiveTab pressed
        public int ActiveTab;
        public Action<int> TabClick;
        public bool Choice;              // one of a set of exclusive choices: button look, Active = pressed
        public bool Active;
        internal Image Fill;
        public bool? Checked;
        public Action<UiRow> Click;      // whole-row click
        public Action<UiRow, int>[] CellClick; // per-cell inked buttons (null entries = plain text)
        public Func<bool> Disabled;
        public bool[] GreyCells;         // cells shown greyed out with no button (an action that isn't available)
        public float SlideMin, SlideMax, SlideValue; // a slider across the middle columns, value in the last cell
        public bool SlideWhole;
        public Action<UiRow, float> Slide;
        public string Hint;              // shown in the window's hint row while the pointer is over this row
        public bool HintRow;             // this note row shows the hovered row's Hint (its own Label otherwise)
        internal ItemGeneral Bound;
        internal Text LabelText;
        internal Text[] CellTexts;
        internal FilterCheckmark Mark;
        internal Color BaseColor;
    }

    internal static class UiTable
    {
        internal static readonly Color Ink = new Color(.39f, .31f, .22f), Warn = new Color(.62f, .20f, .12f);

        internal sealed class Window
        {
            public LayerList Layer;
            public List<UiRow> Rows;
            public float[] Edges; // column edges for the label + cells, e.g. {0, .5, .75, 1}
            public float Width;
        }

        internal static Window Open(string title, List<UiRow> rows, float[] edges, float width = 760, float height = 560,
            List<(string label, Action click)> footer = null, bool floating = false, string footNote = null,
            List<(string label, Action click)> corner = null, float listFrom = 0f)
        {
            // listFrom > 0: the row list takes only the right part of the window (from that fraction); the left is
            // free for hand-built content (the merged text + checklist editor).
            // Never larger than the screen's UI area (small laptop screens): keep a margin around it.
            var (maxW, maxH) = Room();
            width = Mathf.Min(width, maxW); height = Mathf.Min(height, maxH);
            var w = new Window { Rows = rows, Edges = edges, Width = (width - 60) * (1f - listFrom) - (listFrom > 0 ? 10 : 0) };
            // Floating layers (like the inventory) leave map input alone: camera pan, right-click cancel, clicks.
            var layer = floating ? EClass.ui.layerFloat.AddLayer<LayerList>() : EClass.ui.AddLayer<LayerList>();
            w.Layer = layer;
            layer.SetHeader(title);
            layer.SetSize(width, height);
            layer.SetList2(rows, r => r.Label ?? "", (r, _) => { if (!Greyed(r)) r.Click?.Invoke(r); }, (r, item) => Format(w, r, item), autoClose: false);
            if (layer.list.gridLayout != null)
            {
                layer.list.gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                layer.list.gridLayout.constraintCount = 1;
                layer.list.gridLayout.cellSize = new Vector2(w.Width, 32);
            }
            if (footer != null)
                foreach (var (label, click) in footer)
                {
                    var b = layer.windows[0].AddBottomButton(label, () => Ui.Guard(click));
                    b.mainText.text = label;
                }
            layer.RefreshSize();
            layer.scroll.vertical = true; layer.scroll.horizontal = false;
            if (!string.IsNullOrEmpty(footNote)) FootNote(layer, footNote);
            if (corner != null && corner.Count > 0) Corner(layer, corner);
            if (listFrom > 0) Inset(layer, listFrom);
            layer.gameObject.AddComponent<HideNativeHeading>();
            return w;
        }

        internal static (float w, float h) Room()
        {
            var root = EClass.ui != null ? EClass.ui.transform.root as RectTransform : null; // the UI canvas
            if (root == null || root.rect.width <= 0) return (99999f, 99999f);
            return (root.rect.width * .94f, root.rect.height * .88f);
        }

        // New rows (and title, size) in an open window, without closing it: no flicker, scroll position kept.
        internal static void Rebuild(Window w, string title, List<UiRow> rows, float[] edges, float width, float height)
        {
            var layer = w.Layer;
            var (maxW, maxH) = Room();
            width = Mathf.Min(width, maxW); height = Mathf.Min(height, maxH);
            w.Rows = rows; w.Edges = edges; w.Width = width - 60;
            layer.SetHeader(title);
            layer.SetSize(width, height);
            layer.SetList2(rows, r => r.Label ?? "", (r, _) => { if (!Greyed(r)) r.Click?.Invoke(r); }, (r, item) => Format(w, r, item), autoClose: false);
            if (layer.list.gridLayout != null) layer.list.gridLayout.cellSize = new Vector2(w.Width, 32);
            layer.RefreshSize();
        }

        // A note pinned to the window's bottom edge; the list above gives up the room it needs.
        static void FootNote(LayerList layer, string text)
        {
            const float h = 52f, pad = 28f;
            var go = new GameObject("AEX foot note", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            go.GetComponent<LayoutElement>().ignoreLayout = true; // placed by hand, not by the window's layout group
            var r = (RectTransform)go.transform; r.SetParent(layer.windows[0].transform, false);
            r.anchorMin = new Vector2(0f, 0f); r.anchorMax = new Vector2(1f, 0f); r.pivot = new Vector2(.5f, 0f);
            r.offsetMin = new Vector2(pad - 8f, pad - 6f); r.offsetMax = new Vector2(-pad + 4f, pad - 6f + h);
            var t = go.GetComponent<Text>(); t.raycastTarget = false; t.text = text;
            var any = layer.GetComponentsInChildren<Text>(true).FirstOrDefault(x => x != t && x.font != null);
            if (any != null) { t.font = any.font; t.fontSize = Mathf.Max(12, any.fontSize - 2); }
            t.color = Ink; t.alignment = TextAnchor.LowerLeft; t.horizontalOverflow = HorizontalWrapMode.Wrap;
            var sr = (RectTransform)layer.scroll.transform;
            sr.offsetMin = new Vector2(sr.offsetMin.x, sr.offsetMin.y + h + 16f);
        }

        // Clear hover and press feedback for corner buttons: the face darkens a step on hover and two when pressed.
        internal sealed class Face : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
        {
            private Image img; private Color[] c; private bool over;
            internal void Init(Image image, Color[] colors) { img = image; c = colors; }
            public void OnPointerEnter(PointerEventData _) { over = true; img.color = c[1]; }
            public void OnPointerExit(PointerEventData _) { over = false; img.color = c[0]; }
            public void OnPointerDown(PointerEventData _) => img.color = c[2];
            public void OnPointerUp(PointerEventData _) => img.color = over ? c[1] : c[0];
        }

        // Buttons inside the window's bottom-right corner (instead of the hanging tabs under the window).
        static void Corner(LayerList layer, List<(string label, Action click)> buttons)
        {
            const float h = 34f, wd = 150f, pad = 26f;
            var font = layer.GetComponentsInChildren<Text>(true).FirstOrDefault(x => x.font != null);
            for (int i = 0; i < buttons.Count; i++)
            {
                var (label, click) = buttons[buttons.Count - 1 - i];
                var go = new GameObject("AEX corner button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                go.GetComponent<LayoutElement>().ignoreLayout = true;
                var r = (RectTransform)go.transform; r.SetParent(layer.windows[0].transform, false);
                r.anchorMin = r.anchorMax = r.pivot = new Vector2(1f, 0f);
                r.sizeDelta = new Vector2(wd, h); r.anchoredPosition = new Vector2(-pad - i * (wd + 8f), pad - 4f);
                // Parchment buttons: Cancel is secondary (light wash, regular text); everything else is primary
                // (deeper wash, bold dark ink) so the accept action carries the weight.
                // Opaque parchment tones (not see-through washes), so a button looks the same on any window.
                bool secondary = label == L.T("Cancel");
                var fg = secondary ? new Color(.24f, .16f, .08f) : new Color(.09f, .05f, .02f);
                var face = secondary
                    ? new[] { new Color(.80f, .72f, .58f), new Color(.74f, .65f, .50f), new Color(.68f, .58f, .43f) }  // normal, hover, pressed
                    : new[] { new Color(.60f, .49f, .34f), new Color(.54f, .43f, .29f), new Color(.48f, .38f, .25f) }; // the earlier ink tone, a shade lighter
                var img = go.GetComponent<Image>(); img.color = face[0];
                var o = go.AddComponent<Outline>(); o.effectColor = new Color(.38f, .28f, .16f, secondary ? .70f : .90f); o.effectDistance = new Vector2(1, -1);
                var b = go.GetComponent<Button>(); b.targetGraphic = img; b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => Ui.Guard(click));
                go.AddComponent<Face>().Init(img, face);
                var tg = new GameObject("text", typeof(RectTransform), typeof(Text)); var tr = (RectTransform)tg.transform; tr.SetParent(r, false);
                tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = tr.offsetMax = Vector2.zero;
                // Regular weight (the font has no real bold; Unity's faux bold smears), a size up, and a hair space
                // between letters for a touch more tracking (Unity Text has no letter-spacing setting).
                var t = tg.GetComponent<Text>(); t.raycastTarget = false; t.alignment = TextAnchor.MiddleCenter;
                t.text = string.Join(" ", label.Select(ch => ch.ToString()));
                if (font != null) { t.font = font.font; t.fontSize = 16; }
                t.fontStyle = FontStyle.Normal; t.color = fg;
            }
            var sr = (RectTransform)layer.scroll.transform;
            sr.offsetMin = new Vector2(sr.offsetMin.x, sr.offsetMin.y + h + 12f);
        }

        // A side list as its own inset panel: ink border and wash, a margin above the corner buttons, and the
        // scrollbar inside the panel (always shown) so it reads as scrollable.
        static void Inset(LayerList layer, float from)
        {
            var sr = (RectTransform)layer.scroll.transform;
            sr.anchorMin = new Vector2(from, sr.anchorMin.y);
            sr.offsetMin = new Vector2(14, sr.offsetMin.y + 14);
            sr.offsetMax = new Vector2(sr.offsetMax.x - 28, sr.offsetMax.y - 52); // line up with the left column's text
            var panel = new GameObject("AEX inset", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            panel.GetComponent<LayoutElement>().ignoreLayout = true;
            var pr = (RectTransform)panel.transform; pr.SetParent(sr.parent, false);
            pr.anchorMin = sr.anchorMin; pr.anchorMax = sr.anchorMax; pr.pivot = sr.pivot;
            pr.offsetMin = sr.offsetMin - new Vector2(6, 6); pr.offsetMax = sr.offsetMax + new Vector2(26, 6);
            pr.SetSiblingIndex(sr.GetSiblingIndex());
            var pi = panel.GetComponent<Image>(); pi.color = new Color(.38f, .28f, .16f, .07f); pi.raycastTarget = false;
            var po = panel.AddComponent<Outline>(); po.effectColor = new Color(.38f, .28f, .16f, .55f); po.effectDistance = new Vector2(1, -1);
            // The list's own top padding (room for the native heading) would leave a blank band in the panel.
            var grid = layer.list.gridLayout; if (grid != null) grid.padding.top = 4;
            var vlg = layer.scroll.content != null ? layer.scroll.content.GetComponent<VerticalLayoutGroup>() : null; if (vlg != null) vlg.padding.top = 4;
            if (layer.scroll.viewport != null) { var vp = layer.scroll.viewport; vp.offsetMax = new Vector2(vp.offsetMax.x, 0); }
            var bar = layer.scroll.verticalScrollbar;
            if (bar != null)
            {
                layer.scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
                var bt = (RectTransform)bar.transform; bt.SetParent(pr, false);
                bt.anchorMin = new Vector2(1, 0); bt.anchorMax = new Vector2(1, 1); bt.pivot = new Vector2(1, .5f);
                bt.offsetMin = new Vector2(-20, 6); bt.offsetMax = new Vector2(-4, -6); // 16 wide
                bar.gameObject.SetActive(true);
                var track = bar.GetComponent<Image>(); if (track != null) track.color = new Color(.38f, .28f, .16f, .14f);
                if (bar.handleRect != null) { var h = bar.handleRect.GetComponent<Image>(); if (h != null) h.color = new Color(.38f, .28f, .16f, .75f); }
            }
        }

        static void HideKey(UIButton button)
        {
            if (button.keyText != null) button.keyText.gameObject.SetActive(false);
            foreach (var image in button.GetComponentsInChildren<Image>(true))
                if (image.sprite != null && image.sprite.name == "bg_key") image.gameObject.SetActive(false);
        }

        static void Format(Window w, UiRow row, ItemGeneral item)
        {
            row.Bound = item;
            var button = item.button1;
            var label = button.mainText;
            HideKey(button);
            var layout = item.GetComponent<LayoutElement>() ?? item.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = layout.preferredWidth = w.Width;
            int lines = (row.Label ?? "").Split('\n').Length; // notes grow with their line breaks
            layout.minHeight = layout.preferredHeight = row.HintRow ? 64 : row.Note ? Mathf.Max(44, 16 + lines * 20) : 32;
            var fit = label.GetComponent<ContentSizeFitter>(); if (fit != null) fit.enabled = false;
            var labelLayout = label.GetComponent<LayoutElement>() ?? label.gameObject.AddComponent<LayoutElement>(); labelLayout.ignoreLayout = true;
            bool hasCells = row.Cells != null && row.Cells.Length > 0;
            float split = hasCells ? w.Edges[1] : 1f;
            var lr = label.rectTransform; lr.anchorMin = Vector2.zero; lr.anchorMax = new Vector2(split, 1);
            lr.offsetMin = new Vector2(row.Checked.HasValue ? 44 : row.Heading || row.Note ? 8 : 20, 0); lr.offsetMax = new Vector2(-6, 0);
            // Multi-line notes flow down from the top of their row (rows have a fixed height in the list grid).
            bool multi = row.Note && (row.Label ?? "").IndexOf('\n') >= 0;
            label.alignment = multi ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft; label.fontSize = row.Note ? 13 : 14;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            if (multi) lr.offsetMax = new Vector2(lr.offsetMax.x, -8);
            label.horizontalOverflow = row.Note ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            label.text = row.Label ?? "";
            if (row.Note) { label.color = row.Info ? Ink : Warn; label.fontStyle = FontStyle.Normal; }
            else if (row.Heading || row.ColumnHeader) { label.color = Ink; label.fontStyle = FontStyle.Bold; }
            if (row.Section)
            {
                label.fontSize = 16; label.color = new Color(.20f, .14f, .08f);
                var band = new GameObject("AEX section band", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                band.GetComponent<LayoutElement>().ignoreLayout = true;
                var br = (RectTransform)band.transform; br.SetParent(button.transform, false);
                br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one; br.offsetMin = new Vector2(2, 2); br.offsetMax = new Vector2(-2, -2);
                br.SetAsFirstSibling();
                var bi = band.GetComponent<Image>(); bi.color = new Color(.38f, .28f, .16f, .28f); bi.raycastTarget = false;
            }
            else if (row.Heading) lr.offsetMin = new Vector2(20, 0); // sub-heading: indented under its section
            row.BaseColor = label.color;
            row.LabelText = label;
            if (row.Tabs != null) { Tabs(row, button, label); return; }
            if (row.Checked.HasValue) row.Mark = FilterCheckmark.Create(button.transform, new Vector2(24, 0), row.Checked.Value);
            if (row.Choice)
            {
                // Inked button inside the row: a light wash when inactive (click me), a solid fill when active.
                var fill = new GameObject("AEX choice fill", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                fill.GetComponent<LayoutElement>().ignoreLayout = true;
                var fr = (RectTransform)fill.transform; fr.SetParent(button.transform, false);
                fr.anchorMin = Vector2.zero; fr.anchorMax = new Vector2(row.Cells != null && row.Cells.Length > 0 ? w.Edges[1] : 1f, 1f);
                fr.offsetMin = new Vector2(6, 3); fr.offsetMax = new Vector2(-4, -3);
                fr.SetAsFirstSibling();
                row.Fill = fill.GetComponent<Image>(); row.Fill.raycastTarget = false;
                var o = fill.AddComponent<Outline>(); o.effectColor = new Color(.38f, .28f, .16f, .35f); o.effectDistance = new Vector2(1, -1);
                lr.offsetMin = new Vector2(20, 0);
            }
            if (hasCells)
            {
                row.CellTexts = new Text[row.Cells.Length];
                for (int i = 0; i < row.Cells.Length; i++)
                {
                    int ci = i;
                    if (row.Slide != null && ci < row.Cells.Length - 1) continue;
                    Action click = row.CellClick != null && ci < row.CellClick.Length && row.CellClick[ci] != null ? (() => { if (!Greyed(row)) row.CellClick[ci](row, ci); }) : (Action)null;
                    bool grey = row.GreyCells != null && ci < row.GreyCells.Length && row.GreyCells[ci];
                    row.CellTexts[i] = Cell(button, label, w.Edges[i + 1], w.Edges[i + 2], row.Cells[i], row.ColumnHeader, grey ? null : click);
                    if (grey) { var c = row.CellTexts[i].color; row.CellTexts[i].color = new Color(c.r, c.g, c.b, .35f); }
                }
            }
            if (row.Slide != null) Slider(w, row, button);
            if (!string.IsNullOrEmpty(row.Hint)) item.gameObject.AddComponent<Hover>().Init(w, row);
            ApplyState(row);
            StyleChoice(row);
        }

        // Elin's input module doesn't deliver pointer-enter to list rows, so each row checks the mouse itself.
        internal sealed class Hover : MonoBehaviour
        {
            private Window w; private UiRow row; private bool over;
            private RectTransform rect; private Camera cam;
            internal void Init(Window window, UiRow r)
            {
                w = window; row = r; rect = (RectTransform)transform;
                var canvas = GetComponentInParent<Canvas>();
                cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
            }
            static UiRow Target(Window w) => w.Rows.FirstOrDefault(r => r.HintRow);
            private void Update()
            {
                bool now = RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, cam);
                if (now == over) return;
                over = now;
                var t = Target(w);
                if (t == null || !t.LabelText) return;
                if (now) t.LabelText.text = row.Hint;
                else if (t.LabelText.text == row.Hint) t.LabelText.text = t.Label ?? "";
            }
        }

        // Inked slider: a thin track with a darker fill and a square handle, between the label and the value cell.
        static void Slider(Window w, UiRow row, UIButton button)
        {
            GameObject Make(string name, Transform parent, Color color)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                go.GetComponent<LayoutElement>().ignoreLayout = true;
                go.transform.SetParent(parent, false);
                go.GetComponent<Image>().color = color;
                return go;
            }
            var root = new GameObject("AEX slider", typeof(RectTransform), typeof(Slider), typeof(LayoutElement));
            root.GetComponent<LayoutElement>().ignoreLayout = true;
            var rr = (RectTransform)root.transform; rr.SetParent(button.transform, false);
            rr.anchorMin = new Vector2(w.Edges[1], 0); rr.anchorMax = new Vector2(w.Edges[w.Edges.Length - 2], 1);
            rr.offsetMin = new Vector2(14, 6); rr.offsetMax = new Vector2(-14, -6);
            var track = Make("track", rr, new Color(.38f, .28f, .16f, .18f));
            var tr = (RectTransform)track.transform; tr.anchorMin = new Vector2(0, .5f); tr.anchorMax = new Vector2(1, .5f); tr.offsetMin = new Vector2(0, -3); tr.offsetMax = new Vector2(0, 3);
            var area = new GameObject("fill area", typeof(RectTransform)); var ar = (RectTransform)area.transform; ar.SetParent(rr, false);
            ar.anchorMin = new Vector2(0, .5f); ar.anchorMax = new Vector2(1, .5f); ar.offsetMin = new Vector2(0, -3); ar.offsetMax = new Vector2(0, 3);
            var fill = Make("fill", ar, new Color(.38f, .28f, .16f, .55f)); var fr = (RectTransform)fill.transform; fr.offsetMin = fr.offsetMax = Vector2.zero;
            fill.GetComponent<Image>().raycastTarget = false;
            var harea = new GameObject("handle area", typeof(RectTransform)); var hr = (RectTransform)harea.transform; hr.SetParent(rr, false);
            hr.anchorMin = Vector2.zero; hr.anchorMax = Vector2.one; hr.offsetMin = new Vector2(6, 0); hr.offsetMax = new Vector2(-6, 0);
            var handle = Make("handle", hr, new Color(.30f, .21f, .12f, .95f)); var hdr = (RectTransform)handle.transform; hdr.sizeDelta = new Vector2(12, 0);
            var o = handle.AddComponent<Outline>(); o.effectColor = new Color(.85f, .77f, .60f, .8f); o.effectDistance = new Vector2(1, -1);
            var slider = root.GetComponent<Slider>();
            slider.fillRect = fr; slider.handleRect = hdr; slider.targetGraphic = handle.GetComponent<Image>();
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = row.SlideMin; slider.maxValue = row.SlideMax; slider.wholeNumbers = row.SlideWhole;
            slider.value = row.SlideValue;
            slider.onValueChanged.AddListener(v => Ui.Guard(() => { if (!Greyed(row)) row.Slide(row, v); }));
        }

        static Text Cell(UIButton button, Text template, float from, float to, string text, bool header, Action click)
        {
            var divider = new GameObject("AEX column divider", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            divider.GetComponent<LayoutElement>().ignoreLayout = true;
            var line = (RectTransform)divider.transform; line.SetParent(button.transform, false);
            line.anchorMin = new Vector2(from, 0); line.anchorMax = new Vector2(from, 1);
            line.offsetMin = new Vector2(-.5f, 1); line.offsetMax = new Vector2(.5f, -1);
            var ink = divider.GetComponent<Image>(); ink.color = new Color(.38f, .28f, .16f, .30f); ink.raycastTarget = false;
            if (click != null)
            {
                var cell = new GameObject("AEX value button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                cell.GetComponent<LayoutElement>().ignoreLayout = true;
                var cr = (RectTransform)cell.transform; cr.SetParent(button.transform, false);
                cr.anchorMin = new Vector2(from, 0); cr.anchorMax = new Vector2(to, 1); cr.offsetMin = new Vector2(4, 3); cr.offsetMax = new Vector2(-4, -3);
                var ci = cell.GetComponent<Image>(); ci.color = new Color(.38f, .28f, .16f, .14f);
                var co = cell.AddComponent<Outline>(); co.effectColor = new Color(.38f, .28f, .16f, .35f); co.effectDistance = new Vector2(1, -1);
                cell.GetComponent<Button>().targetGraphic = ci;
                cell.GetComponent<Button>().onClick.AddListener(() => Ui.Guard(click));
            }
            var value = UnityEngine.Object.Instantiate(template, template.transform.parent);
            value.name = "AEX cell";
            var vr = value.rectTransform; vr.anchorMin = new Vector2(from, 0); vr.anchorMax = new Vector2(to, 1);
            vr.offsetMin = new Vector2(10, 0); vr.offsetMax = new Vector2(-8, 0);
            value.text = text ?? ""; value.fontStyle = header ? FontStyle.Bold : FontStyle.Normal; value.raycastTarget = false;
            if (header) value.color = Ink;
            value.transform.SetAsLastSibling();
            return value;
        }

        // Tab bar: equal-width inked buttons across the row; the active one pressed (dark fill, bold).
        static void Tabs(UiRow row, UIButton button, Text template)
        {
            template.text = "";
            button.interactable = false;
            int n = row.Tabs.Length;
            for (int i = 0; i < n; i++)
            {
                int k = i;
                bool on = k == row.ActiveTab;
                var go = new GameObject("AEX tab", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                go.GetComponent<LayoutElement>().ignoreLayout = true;
                var r = (RectTransform)go.transform; r.SetParent(button.transform, false);
                r.anchorMin = new Vector2((float)k / n, 0); r.anchorMax = new Vector2((float)(k + 1) / n, 1);
                r.offsetMin = new Vector2(k == 0 ? 6 : 3, 2); r.offsetMax = new Vector2(k == n - 1 ? -4 : -3, -2);
                var img = go.GetComponent<Image>(); img.color = on ? new Color(.38f, .28f, .16f, .42f) : new Color(.38f, .28f, .16f, .12f);
                var o = go.AddComponent<Outline>(); o.effectColor = new Color(.38f, .28f, .16f, .35f); o.effectDistance = new Vector2(1, -1);
                var b = go.GetComponent<Button>(); b.targetGraphic = img;
                b.onClick.AddListener(() => Ui.Guard(() => row.TabClick?.Invoke(k)));
                var t = UnityEngine.Object.Instantiate(template, go.transform);
                t.name = "AEX tab text";
                var tr = t.rectTransform; tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = tr.offsetMax = Vector2.zero;
                t.alignment = TextAnchor.MiddleCenter; t.text = row.Tabs[k]; t.raycastTarget = false;
                t.fontStyle = on ? FontStyle.Bold : FontStyle.Normal;
                t.color = on ? new Color(.20f, .14f, .08f) : Ink;
            }
        }

        internal static bool Greyed(UiRow row) => row.Disabled?.Invoke() ?? false;
        internal static void ApplyState(UiRow row)
        {
            if (row.Bound == null) return;
            bool grey = Greyed(row);
            row.Bound.button1.interactable = (row.Click != null || row.CellClick != null) && !grey;
            if (row.LabelText) row.LabelText.color = new Color(row.BaseColor.r, row.BaseColor.g, row.BaseColor.b, grey ? .38f : row.BaseColor.a);
            if (row.Mark) row.Mark.transform.parent.GetComponent<Image>().color = new Color(.85f, .77f, .60f, grey ? .30f : .70f);
        }
        internal static void SetCell(UiRow row, int i, string text) { row.Cells[i] = text; if (row.CellTexts != null && row.CellTexts[i]) row.CellTexts[i].text = text; }
        internal static void SetLabel(UiRow row, string text) { row.Label = text; if (row.LabelText) row.LabelText.text = text; }
        internal static void SetChecked(UiRow row, bool on) { row.Checked = on; if (row.Mark) row.Mark.gameObject.SetActive(on); }
        internal static void SetActive(UiRow row, bool on) { row.Active = on; StyleChoice(row); }
        static void StyleChoice(UiRow row)
        {
            if (!row.Choice || row.Fill == null) return;
            row.Fill.color = row.Active ? new Color(.38f, .28f, .16f, .42f) : new Color(.38f, .28f, .16f, .12f);
            if (row.LabelText) { row.LabelText.fontStyle = row.Active ? FontStyle.Bold : FontStyle.Normal; row.LabelText.color = row.Active ? new Color(.20f, .14f, .08f) : row.BaseColor; }
        }

        // Small titled modal listing choices; the current one is checked; a click picks and closes.
        internal static void Choose(string title, IList<(string label, Action pick)> options, int current)
        {
            Window modal = null;
            var rows = options.Select((o, i) => new UiRow { Label = o.label, Checked = i == current, Click = _ => { modal?.Layer.Close(); Ui.Guard(o.pick); } }).ToList();
            modal = Open(title, rows, new[] { 0f, 1f }, 460, Math.Min(140 + rows.Count * 34, 560));
        }
    }

    // The generic list's "Name" heading row is replaced by our own headings.
    internal sealed class HideNativeHeading : MonoBehaviour
    {
        void LateUpdate()
        {
            foreach (var header in GetComponentsInChildren<UIHeader>(true))
                if (header.transform.parent != null && header.transform.parent.name == "HeaderRow")
                { header.transform.parent.gameObject.SetActive(false); enabled = false; }
        }
    }
}

namespace AutoExploreExtended
{
    internal static class Ui
    {
        internal static void Guard(Action action)
        {
            try { action(); }
            catch (Exception e) { UnityEngine.Debug.LogException(e); }
        }
    }
}
