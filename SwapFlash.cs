using UnityEngine;
using UnityEngine.UI;

namespace AutoExploreExtended
{
    // Reorder feedback (as in the Logistics Computer): the moved row glows green, the row it swapped with glows brown,
    // fading in and back out.
    internal sealed class SwapFlash : MonoBehaviour
    {
        const float Duration = .4f;
        Image fill;
        Color tint;
        float started;

        internal static void Flash(ItemGeneral moved, ItemGeneral displaced)
        {
            Paint(moved, new Color(.35f, .48f, .22f, .32f));
            Paint(displaced, new Color(.58f, .36f, .15f, .28f));
        }

        static void Paint(ItemGeneral row, Color color)
        {
            if (row == null) return;
            var f = row.GetComponent<SwapFlash>() ?? row.gameObject.AddComponent<SwapFlash>();
            if (f.fill == null)
            {
                var go = new GameObject("AEX swap highlight", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                go.GetComponent<LayoutElement>().ignoreLayout = true;
                var rect = (RectTransform)go.transform; rect.SetParent(row.button1.transform, false); rect.SetAsFirstSibling();
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(1, 1); rect.offsetMax = new Vector2(-1, -1);
                f.fill = go.GetComponent<Image>(); f.fill.raycastTarget = false;
            }
            f.tint = color; f.started = Time.unscaledTime;
            f.fill.color = new Color(color.r, color.g, color.b, 0);
            f.fill.gameObject.SetActive(true); f.enabled = true;
        }

        void Update()
        {
            float elapsed = Time.unscaledTime - started;
            float phase = elapsed < Duration ? elapsed / Duration : 1 - (elapsed - Duration) / Duration;
            fill.color = new Color(tint.r, tint.g, tint.b, tint.a * Mathf.SmoothStep(0, 1, Mathf.Clamp01(phase)));
            if (elapsed >= Duration * 2) { fill.gameObject.SetActive(false); enabled = false; }
        }

        void OnDisable() { if (fill != null) fill.gameObject.SetActive(false); }
    }
}
