using System.Linq;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using NWH.WheelController3D;
using UnityEngine;

namespace PartAdjustment
{
    [ES3NonSerializable]
    [DefaultExecutionOrder(10000)]
    public sealed class ToolRunner : MonoBehaviour
    {
        internal static SuspensionAdjustment Active;
        internal static PlayMakerFSM Tool;
        private static readonly HashSet<Transform> pendingVehicles = new HashSet<Transform>();
        private static bool sceneScan = true;
        private float nextScan;
        private ToolVisibilityState visibility;
        private readonly SuspensionHint hint = new SuspensionHint();
        private PlayMakerFSM mouseIcon;
        private bool suspensionCursor;

        internal static void SceneChanged()
        {
            sceneScan = true;
            Tool = null;
            Active = null;
        }

        internal static void QueueVehicle(WheelController wheel)
        {
            if (wheel.name.StartsWith("hinge_wheel_", System.StringComparison.Ordinal) && wheel.transform.parent != null)
                pendingVehicles.Add(wheel.transform.parent);
        }

        private void Discover()
        {
            bool toolOn = Plugin.Enabled.Value && Tool != null && Tool.isActiveAndEnabled;
            if (sceneScan || (Time.unscaledTime >= nextScan && (Tool == null || mouseIcon == null || toolOn)))
            {
                nextScan = Time.unscaledTime + 1f;
                if (sceneScan || Tool == null || mouseIcon == null)
                {
                    foreach (var fsm in Object.FindObjectsOfType<PlayMakerFSM>())
                    {
                        if (fsm.FsmName == "UseAdjustTool") Tool = fsm;
                        if (fsm.FsmName == "MouseIcon" && fsm.name == "Canvas") mouseIcon = fsm;
                    }
                }
                // A small wheel scan also catches vehicles whose suspension was enabled after spawning.
                // Full FSM discovery stops once the tool and cursor have been found.
                if (sceneScan || toolOn)
                    foreach (var wheel in Object.FindObjectsOfType<WheelController>()) QueueVehicle(wheel);
                sceneScan = false;
            }
            foreach (var car in pendingVehicles) if (car != null) SuspensionAdjustment.Ensure(car);
            pendingVehicles.Clear();
        }

        internal static bool Begin(Fsm fsm, GameObject picked)
        {
            var target = picked == null ? null : picked.GetComponent<SuspensionPickTarget>();
            if (target == null || target.Adjustment == null || !Plugin.Enabled.Value) return false;
            AdjustmentRunner.Stop();
            Tool = fsm.Owner.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "UseAdjustTool");
            Active = target.Adjustment;
            return true;
        }

        private void Update()
        {
            Discover();
            bool toolOn = Plugin.Enabled.Value && Tool != null && Tool.isActiveAndEnabled;
            if (visibility.Changed(toolOn, SuspensionAdjustment.Revision))
                foreach (var adjustment in SuspensionAdjustment.All) if (adjustment != null) adjustment.SetPickable(toolOn);
            if (!toolOn)
            {
                Active = null;
                hint.Show(false);
                return;
            }
            if (AdjustmentRunner.Session != null)
            {
                Active = null;
                hint.Show(false);
                return;
            }
            if (Tool.ActiveStateName != "adjust") Active = null;
            var picked = toolOn ? Tool.FsmVariables.GetFsmGameObject("Hinge")?.Value : null;
            var hover = picked == null || HeadlightMount.HoldingLight ? null : picked.GetComponent<SuspensionPickTarget>();
            bool suspension = Active != null || (hover != null && (Tool.ActiveStateName == "over" || Tool.ActiveStateName == "compare Tag"));
            var shown = Active ?? (hover != null ? hover.Adjustment : null);
            hint.Override = shown != null && !shown.KitFitted ? "Fit a suspension lift kit to adjust the suspension" : null;
            hint.Show(suspension);
            if (Active == null || Time.timeScale <= 0f || !Application.isFocused || Cursor.lockState != CursorLockMode.Locked) return;
            if (!Active.KitFitted) return;
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
            if (!toolOn && !suspensionCursor) return;
            if (toolOn && SuspensionAdjustment.All.Count > 0)
            {
                var camera = Camera.main;
                foreach (var adjustment in SuspensionAdjustment.All) if (adjustment != null) adjustment.UpdateIndicators(camera);
            }
            var picked = toolOn ? Tool.FsmVariables.GetFsmGameObject("Hinge")?.Value : null;
            var target = picked == null ? null : picked.GetComponent<SuspensionPickTarget>();
            bool holdingLight = HeadlightMount.HoldingLight;
            bool hovering = !holdingLight && target != null && target.Adjustment != null
                && (Tool.ActiveStateName == "over" || Tool.ActiveStateName == "compare Tag");
            bool show = toolOn && (hovering || Active != null || AdjustmentRunner.Session != null || AdjustmentRunner.Hover != null || HeadlightMount.Ready) && Time.timeScale > 0f
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
                if ((holdingLight || !vanillaAdjust) && mouseIcon.ActiveStateName == "Adjust") mouseIcon.SendEvent("Cursor_Point");
            }
            // With a light in hand a hinge behind the aimed spot must not show the vanilla adjust icon either.
            else if (holdingLight && mouseIcon.ActiveStateName == "Adjust") mouseIcon.SendEvent("Cursor_Point");
        }

        private void OnDestroy()
        {
            AdjustmentRunner.Stop();
            hint.Destroy();
            foreach (var adjustment in SuspensionAdjustment.All) if (adjustment != null) adjustment.SetPickable(false);
            pendingVehicles.Clear();
            sceneScan = true;
            Active = null;
            if (suspensionCursor && mouseIcon != null && mouseIcon.ActiveStateName == "Adjust")
                mouseIcon.SendEvent("Cursor_Point");
        }
    }
}
