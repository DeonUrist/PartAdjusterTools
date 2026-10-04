using System.Linq;
using HutongGames.PlayMaker;
using UnityEngine;

namespace PartAdjustment
{
    [ES3NonSerializable]
    [DefaultExecutionOrder(10000)]
    public sealed class ToolRunner : MonoBehaviour
    {
        internal static SuspensionAdjustment Active;
        internal static PlayMakerFSM Tool;
        private float nextScan;
        private readonly SuspensionHint hint = new SuspensionHint();
        private PlayMakerFSM mouseIcon;
        private bool suspensionCursor;

        internal static bool Begin(Fsm fsm, GameObject picked)
        {
            var target = picked == null ? null : picked.GetComponent<SuspensionPickTarget>();
            if (target == null || target.Adjustment == null || !Plugin.Enabled.Value) return false;
            Tool = fsm.Owner.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "UseAdjustTool");
            Active = target.Adjustment;
            return true;
        }

        private void Update()
        {
            if (Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + 1f;
                foreach (var fsm in Object.FindObjectsOfType<PlayMakerFSM>())
                {
                    if (fsm.FsmName == "UseAdjustTool") Tool = fsm;
                    if (fsm.FsmName == "MouseIcon" && fsm.name == "Canvas") mouseIcon = fsm;
                    if (fsm.FsmName == "checkSuspension" && fsm.name == "hinge_suspension")
                        SuspensionAdjustment.Ensure(fsm.transform.parent?.parent);
                }
            }
            bool toolOn = Plugin.Enabled.Value && Tool != null && Tool.isActiveAndEnabled;
            foreach (var adjustment in SuspensionAdjustment.All) if (adjustment != null) adjustment.SetPickable(toolOn);
            if (!toolOn || Tool.ActiveStateName != "adjust") Active = null;
            var picked = toolOn ? Tool.FsmVariables.GetFsmGameObject("Hinge")?.Value : null;
            var hover = picked == null ? null : picked.GetComponent<SuspensionPickTarget>();
            bool suspension = Active != null || (hover != null && (Tool.ActiveStateName == "over" || Tool.ActiveStateName == "compare Tag"));
            hint.Show(suspension);
            if (Active == null || Time.timeScale <= 0f || !Application.isFocused || Cursor.lockState != CursorLockMode.Locked) return;
            float width = (SuspensionControls.Pressed(SuspensionControls.Right) ? Plugin.WidthStep.Value : 0f)
                - (SuspensionControls.Pressed(SuspensionControls.Left) ? Plugin.WidthStep.Value : 0f);
            float height = (SuspensionControls.Pressed(SuspensionControls.Up) ? Plugin.HeightStep.Value : 0f)
                - (SuspensionControls.Pressed(SuspensionControls.Down) ? Plugin.HeightStep.Value : 0f);
            bool reset = SuspensionControls.Pressed(SuspensionControls.Reset);
            if (width != 0f || height != 0f || reset) Active.Change(width, height, reset);
        }

        private void LateUpdate()
        {
            bool toolOn = Plugin.Enabled.Value && Tool != null && Tool.isActiveAndEnabled;
            var picked = toolOn ? Tool.FsmVariables.GetFsmGameObject("Hinge")?.Value : null;
            var target = picked == null ? null : picked.GetComponent<SuspensionPickTarget>();
            bool hovering = target != null && target.Adjustment != null
                && (Tool.ActiveStateName == "over" || Tool.ActiveStateName == "compare Tag");
            bool show = toolOn && (hovering || Active != null) && Time.timeScale > 0f
                && Application.isFocused && Cursor.lockState == CursorLockMode.Locked;
            if (mouseIcon == null || !mouseIcon.isActiveAndEnabled) return;
            if (show)
            {
                // Vanilla sends Cursor_Adjust only once on entering "over". Other interaction
                // FSMs can subsequently replace it; keep its native icon while using our target.
                if (mouseIcon.ActiveStateName != "Adjust") mouseIcon.SendEvent("Cursor_Adjust");
                suspensionCursor = true;
            }
            else if (suspensionCursor)
            {
                suspensionCursor = false;
                // Leave the native icon in charge when moving straight to another adjustable part.
                bool vanillaAdjust = toolOn && (Tool.ActiveStateName == "over" || Tool.ActiveStateName == "adjust");
                if (!vanillaAdjust && mouseIcon.ActiveStateName == "Adjust") mouseIcon.SendEvent("Cursor_Point");
            }
        }

        private void OnDestroy()
        {
            hint.Show(false);
            Active = null;
            if (suspensionCursor && mouseIcon != null && mouseIcon.ActiveStateName == "Adjust")
                mouseIcon.SendEvent("Cursor_Point");
        }
    }
}
