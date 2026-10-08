using System.Linq;
using HutongGames.PlayMaker;
using UnityEngine;
using UnityEngine.UI;

namespace PartAdjustment
{
    internal sealed class AdjustmentHint
    {
        private Text useText, title, mode, changeMode, buttons, reset;
        private string suffix, baseText, renderedText, toggleKey;
        private bool stopping, removing, panelDirty = true, rotating;
        private string useKey;
        private int referenceFrame;
        private KeyCode modifier, alternative;
        private GameObject panel;
        private RectTransform nativePanel;
        private Vector2 nativeAnchorMin, nativeAnchorMax, nativePosition;
        private bool nativeMoved;
        private float nextSearch;
        private VerticalWrapMode originalOverflow;
        private static readonly string[] names = { Controls.Mode, Controls.Left, Controls.Right, Controls.Up, Controls.Down, Controls.Forward, Controls.Backward, Controls.Reset };
        private readonly string[] bindings = new string[8];

        internal void Refresh()
        {
            bool playing = AdjustmentRunner.Gameplay && AdjustmentRunner.ToolSelected && AdjustmentRunner.PlayerCamera != null
                && AdjustmentRunner.PlayerCamera.isActiveAndEnabled;
            var session = playing ? AdjustmentRunner.Session : null;
            bool show = playing && (session != null || AdjustmentRunner.Hover != null);
            var source = show ? FsmVariables.GlobalVariables.GetFsmGameObject("UI_ItemUse")?.Value : null;
            if (!show || source == null) ReleaseUseText();
            else
            {
                if (useText == null || useText.gameObject != source)
                {
                    ReleaseUseText(); useText = source.GetComponent<Text>();
                    if (useText != null) originalOverflow = useText.verticalOverflow;
                }
                if (useText != null)
                {
                    // ItemUse's native one-line rectangle otherwise clips our second line.
                    if (useText.verticalOverflow != VerticalWrapMode.Overflow) useText.verticalOverflow = VerticalWrapMode.Overflow;
                    string key = Controls.Keys(Controls.Secondary), use = Controls.Keys(Controls.Use);
                    bool removable = AdjustmentRunner.HoverRemovable;
                    if (suffix == null || key != toggleKey || stopping != (session != null) || removing != removable || (removable && use != useKey))
                    {
                        RemoveSuffix(); toggleKey = key; stopping = session != null; removing = removable; useKey = use;
                        suffix = "\n" + (stopping ? "Stop adjusting: " : "Adjust: ") + key + (removing ? "\nRemove: " + use : "");
                    }
                    // Native FSMs can rewrite the hint; append once and leave unchanged text alone.
                    if (useText.text != renderedText)
                    {
                        baseText = useText.text;
                        if (baseText.EndsWith(suffix, System.StringComparison.Ordinal)) baseText = baseText.Substring(0, baseText.Length - suffix.Length);
                        renderedText = baseText + suffix;
                        if (useText.text != renderedText) useText.text = renderedText;
                    }
                }
            }
            if (session != null && panel == null && Time.unscaledTime >= nextSearch)
            {
                nextSearch = Time.unscaledTime + 1f;
                var original = Resources.FindObjectsOfTypeAll<Transform>().FirstOrDefault(t => t.name == "AdjustUI" && t.gameObject.scene.IsValid());
                if (original != null)
                {
                    MoveNativePanel(false);
                    nativePanel = original as RectTransform;
                    // Clone before moving the native instructions so our detailed controls
                    // retain their existing position instead of overlapping them again.
                    panel = Object.Instantiate(original.gameObject, original.parent, false);
                    panel.name = "PartAdjustment.ItemControls";
                    foreach (var fsm in panel.GetComponentsInChildren<PlayMakerFSM>(true)) { fsm.enabled = false; Object.Destroy(fsm); }
                    var suspensionHint = panel.transform.Find("PartAdjustment.SuspensionHint");
                    if (suspensionHint != null) Object.Destroy(suspensionHint.gameObject);
                    foreach (var text in panel.GetComponentsInChildren<Text>(true))
                    {
                        text.raycastTarget = false;
                        text.horizontalOverflow = HorizontalWrapMode.Overflow;
                        text.verticalOverflow = VerticalWrapMode.Overflow;
                    }
                    title = panel.GetComponent<Text>();
                    mode = Node("mode", -34f, 26f);
                    changeMode = Node("change mode", -60f, 26f);
                    buttons = Node("buttons", -86f, 70f);
                    reset = Node("reset", -164f, 52f);
                    panelDirty = true;
                }
            }
            MoveNativePanel(session != null && panel != null);
            if (panel == null) return;
            if (panel.activeSelf != (session != null)) panel.SetActive(session != null);
            if (session == null) return;
            if (rotating != session.Rotating || referenceFrame != Plugin.ReferenceFrame.Value
                || modifier != Plugin.ModifierKey.Value || alternative != Plugin.ModifierAlternative.Value) panelDirty = true;
            for (int i = 0; i < names.Length; i++)
            {
                string key = Controls.Keys(names[i]);
                if (bindings[i] != key) { bindings[i] = key; panelDirty = true; }
            }
            if (!panelDirty) return;
            panelDirty = false; rotating = session.Rotating; referenceFrame = Plugin.ReferenceFrame.Value;
            modifier = Plugin.ModifierKey.Value; alternative = Plugin.ModifierAlternative.Value;
            Set(title, "Adjust attached item");
            Set(mode, (rotating ? "Rotation" : "Position") + " mode - " + (referenceFrame == 0 ? "camera axes" : "world axes"));
            Set(changeMode, bindings[0] + " - change move / rotate mode");
            Set(buttons, bindings[1] + " / " + bindings[2] + (rotating ? " - rotate left / right\n" : " - move left / right\n")
                + bindings[3] + " / " + bindings[4] + (rotating ? " - rotate forward / backward\n" : " - move up / down\n")
                + bindings[5] + " / " + bindings[6] + (rotating ? " - tilt left / right" : " - move forward / backward"));
            Set(reset, bindings[7] + " - reset starting pose\n" + ModifierHint() + " - faster " + (rotating ? "rotation" : "movement"));
        }

        private Text Node(string name, float y, float height)
        {
            var node = panel.transform.Find(name);
            var rect = node as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(0f, y);
                rect.sizeDelta = new Vector2(1000f, height);
            }
            return node == null ? null : node.GetComponent<Text>();
        }
        private void MoveNativePanel(bool show)
        {
            if (nativePanel == null) { nativeMoved = false; return; }
            if (show == nativeMoved) return;
            if (show)
            {
                nativeAnchorMin = nativePanel.anchorMin;
                nativeAnchorMax = nativePanel.anchorMax;
                nativePosition = nativePanel.anchoredPosition;
                nativePanel.anchorMin = nativePanel.anchorMax = new Vector2(0f, 1f);
                // Keep the original pivot and child layout; put the title 20 canvas
                // units from the top/left edges, respecting the game's Canvas scaling.
                nativePanel.anchoredPosition = new Vector2(20f + nativePanel.sizeDelta.x * nativePanel.pivot.x,
                    -20f - nativePanel.sizeDelta.y * (1f - nativePanel.pivot.y));
            }
            else
            {
                nativePanel.anchorMin = nativeAnchorMin;
                nativePanel.anchorMax = nativeAnchorMax;
                nativePanel.anchoredPosition = nativePosition;
            }
            nativeMoved = show;
        }
        private string ModifierHint()
        {
            if (modifier == KeyCode.None) return alternative == KeyCode.None ? "Unbound modifier" : Controls.Label(alternative);
            return Controls.Label(modifier) + (alternative == KeyCode.None || alternative == modifier ? "" : " / " + Controls.Label(alternative));
        }
        private static void Set(Text text, string value) { if (text != null && text.text != value) text.text = value; }
        private void RemoveSuffix()
        {
            if (useText != null && suffix != null && useText.text.EndsWith(suffix, System.StringComparison.Ordinal))
                useText.text = useText.text == renderedText ? baseText : useText.text.Substring(0, useText.text.Length - suffix.Length);
            suffix = renderedText = baseText = null;
        }
        private void ReleaseUseText()
        {
            RemoveSuffix();
            if (useText != null && useText.verticalOverflow != originalOverflow) useText.verticalOverflow = originalOverflow;
            useText = null;
        }
        internal void Dispose() { ReleaseUseText(); MoveNativePanel(false); if (panel != null) Object.Destroy(panel); }
    }
}
