using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace PartAdjustment
{
    internal sealed class SuspensionHint
    {
        private readonly Dictionary<Graphic, bool> originals = new Dictionary<Graphic, bool>();
        private Text label;
        private bool shown;
        private float nextFind;

        internal void Show(bool value)
        {
            if (!value)
            {
                if (!shown) return;
                foreach (var item in originals) if (item.Key != null) item.Key.enabled = item.Value;
                originals.Clear();
                if (label != null) label.gameObject.SetActive(false);
                shown = false;
                return;
            }
            if (label == null)
            {
                if (shown) Show(false);
                if (Time.unscaledTime < nextFind) return;
                nextFind = Time.unscaledTime + 1f;
                var root = Resources.FindObjectsOfTypeAll<Transform>().FirstOrDefault(t => t.name == "AdjustUI" && t.gameObject.scene.IsValid());
                if (root == null) return;
                var template = root.GetComponentsInChildren<Text>(true).FirstOrDefault();
                if (template == null) return;
                var go = new GameObject("PartAdjustment.SuspensionHint", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                go.transform.SetParent(root, false);
                label = go.GetComponent<Text>();
                label.font = template.font;
                label.fontSize = template.fontSize;
                label.fontStyle = template.fontStyle;
                label.color = template.color;
                label.alignment = TextAnchor.UpperLeft;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.verticalOverflow = VerticalWrapMode.Overflow;
                label.raycastTarget = false;
                var rect = label.rectTransform;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(1000f, Mathf.Max(90f, template.fontSize * 4.5f));
            }
            if (!shown)
            {
                foreach (var graphic in label.transform.parent.GetComponentsInChildren<Graphic>(true))
                    if (graphic != label) originals[graphic] = graphic.enabled;
                label.gameObject.SetActive(true);
                shown = true;
            }
            foreach (var graphic in originals.Keys) if (graphic != null && graphic.enabled) graphic.enabled = false;
            if (!label.gameObject.activeSelf) label.gameObject.SetActive(true);
            var text = SuspensionControls.Hint;
            if (label.text != text) label.text = text;
        }

        internal void Destroy()
        {
            Show(false);
            if (label != null) Object.Destroy(label.gameObject);
        }
    }
}
