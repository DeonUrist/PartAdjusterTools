using System.Linq;
using HutongGames.PlayMaker;
using UnityEngine;
using UnityEngine.UI;

namespace PartAdjustment
{
    // In-game guide (1.6.1): while the Part Adjustment tool is in hand, a light-red text block on the left of the screen says what the
    // tool can do right now - holding a light, aiming at a wrench / attached part, adjusting - with the player's current key bindings.
    // [General] ShowGuide switches it off. Built from the native AdjustUI text (font, size, outline) as a sibling under the same canvas.
    [ES3NonSerializable]
    [DefaultExecutionOrder(10002)]
    public sealed class ToolGuide : MonoBehaviour
    {
        private static readonly Color TextColor = new Color(1f, 0.6f, 0.6f, 1f);
        private Text label;
        private float nextFind;
        private string lastText;

        private void LateUpdate()
        {
            string text = Plugin.ShowGuide.Value ? Compose() : null;
            if (text == null) { Hide(); return; }
            if (label == null && !Create()) return;
            if (!label.gameObject.activeSelf) label.gameObject.SetActive(true);
            if (text != lastText) { label.text = text; lastText = text; }
        }

        private static bool Playing => Time.timeScale > 0f && Application.isFocused && Cursor.lockState == CursorLockMode.Locked
            && AdjustmentRunner.PlayerCamera != null && AdjustmentRunner.PlayerCamera.isActiveAndEnabled;

        private static PlayMakerFSM grab;
        private static GameObject grabOwner;

        // A headlight / tail light in hand (GrabItem ItemInHand), whether or not a tool is out.
        private static bool HoldingLightAnyTool()
        {
            var camera = AdjustmentRunner.PlayerCamera == null ? null : AdjustmentRunner.PlayerCamera.gameObject;
            if (camera != grabOwner) { grabOwner = camera; grab = camera == null ? null : AdjustmentRunner.Find(camera, "GrabItem"); }
            if (grab == null || !grab.isActiveAndEnabled || grab.ActiveStateName != "ItemInHand") return false;
            var held = HeadlightMount.ItemRoot(grab.FsmVariables.GetFsmGameObject("Item")?.Value);
            return held != null && HeadlightMount.IsHeadlight(held);
        }

        private static string Compose()
        {
            if (!Plugin.Enabled.Value || !Playing) return null;
            var tool = ToolRunner.Tool;
            if (tool == null || !tool.isActiveAndEnabled)
                return Plugin.AttachAnywhere.Value && HoldingLightAnyTool()
                    ? "Take the Part Adjustment tool to attach this light anywhere on a car" : null;
            string use = Controls.Keys(Controls.Use), e = Controls.Keys(Controls.Secondary);
            string putAway = Plugin.PutAwayWithKey.Value && Plugin.PutAwayKey.Value != KeyCode.None ? Controls.Label(Plugin.PutAwayKey.Value) : null;
            var state = tool.ActiveStateName;

            if (HeadlightMount.HoldingLight)
            {
                if (state == "adjust") return "Finish the part adjustment first: " + use;
                return HeadlightMount.Ready
                    ? use + " - attach the light here\nAim at a headlight slot to fit it there instead"
                    : "Aim at a car body to attach the light (" + use + ")\nAim at a headlight slot to fit it there";
            }
            if (AdjustmentRunner.Session != null)
                return e + " - finish adjusting this part\n" + Controls.Keys(Controls.Reset) + " - back to where it was";
            if (ToolRunner.SuspensionShown)
                return ToolRunner.SuspensionNoKit
                    ? "Suspension: fit a suspension lift kit to change width and height"
                    : ToolRunner.Active != null ? "Suspension: change width and height\n" + use + " - finish" : use + " - adjust the suspension (width, height)";
            if (state == "adjust") return "Move / rotate the part with the adjust keys\n" + use + " - finish";
            if (state == "over" || state == "compare Tag") return use + " - adjust this part (position / rotation)";
            if (AdjustmentRunner.Hover != null)
                return e + " - move / rotate this part" + (AdjustmentRunner.HoverRemovable ? "\n" + use + " - remove it" : "");
            return "Part Adjustment tool\n"
                + "- Aim at a wrench: adjust engine, exhaust, radiator, suspension (" + use + ")\n"
                + "- Aim at a plate, crate, gauge or light: move / rotate it (" + e + ")\n"
                + "- Hold a headlight or tail light: attach it anywhere on the car body (" + use + ")"
                + (putAway != null ? "\n" + putAway + " - put the tool away" : "");
        }

        private bool Create()
        {
            if (Time.unscaledTime < nextFind) return false;
            nextFind = Time.unscaledTime + 1f;
            var native = Resources.FindObjectsOfTypeAll<Transform>().FirstOrDefault(t => t.name == "AdjustUI" && t.gameObject.scene.IsValid());
            var template = native == null ? null : native.GetComponent<Text>();
            if (template == null || native.parent == null) return false;
            var go = new GameObject("PartAdjustment.Guide", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(native.parent, false);
            label = go.GetComponent<Text>();
            label.font = template.font;
            label.fontSize = Mathf.Max(12, Mathf.RoundToInt(template.fontSize * 0.8f));
            label.fontStyle = template.fontStyle;
            label.color = TextColor;
            label.alignment = TextAnchor.MiddleLeft;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            var outline = template.GetComponent<Outline>();
            var copy = go.AddComponent<Outline>();
            copy.effectColor = outline != null ? outline.effectColor : new Color(0f, 0f, 0f, 0.8f);
            copy.effectDistance = outline != null ? outline.effectDistance : new Vector2(1f, -1f);
            var rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(24f, 0f);
            rect.sizeDelta = new Vector2(900f, 200f);
            lastText = null;
            return true;
        }

        private void Hide()
        {
            if (label != null && label.gameObject.activeSelf) label.gameObject.SetActive(false);
        }

        private void OnDestroy() { if (label != null) Destroy(label.gameObject); }
    }
}
