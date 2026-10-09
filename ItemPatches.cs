using HarmonyLib;
using HutongGames.PlayMaker.Actions;
using InsaneSystems.InputManager;
using UnityEngine;

namespace PartAdjustment
{
    [HarmonyPatch(typeof(InputController))]
    internal static class SecondaryInputPatch
    {
        [HarmonyPatch(nameof(InputController.GetKeyActionIsDown)), HarmonyPrefix]
        private static bool Down(string actionName, ref bool __result) => Filter(actionName, ref __result);
        [HarmonyPatch(nameof(InputController.GetKeyActionIsActive)), HarmonyPrefix]
        private static bool Active(string actionName, ref bool __result) => Filter(actionName, ref __result);
        [HarmonyPatch(nameof(InputController.GetKeyActionIsUp)), HarmonyPrefix]
        private static bool Up(string actionName, ref bool __result) => Filter(actionName, ref __result);
        private static bool Filter(string name, ref bool result)
        {
            if (name != Controls.Secondary || !AdjustmentRunner.Gameplay || !AdjustmentRunner.ToolSelected
                || (AdjustmentRunner.Session == null && AdjustmentRunner.ConsumedSecondaryFrame != Time.frameCount)) return true;
            result = false;
            return false;
        }
    }

    // The frame our Use press mounted or removed a headlight, the game's FSMs must not also act on it.
    [HarmonyPatch(typeof(GetButtonDown), nameof(GetButtonDown.OnUpdate))]
    internal static class UseConsumedPatch
    {
        private static bool Prefix(GetButtonDown __instance)
        {
            if (__instance.buttonName?.Value != Controls.Use) return true;
            // Holding a headlight / tail light: the tool never starts a hinge adjustment (UseAdjustTool "over" -> "adjust").
            bool hingeStart = HeadlightMount.HingePressClaimed && __instance.Fsm?.Name == "UseAdjustTool" && __instance.State?.Name == "over";
            if (HeadlightMount.ConsumedUseFrame != Time.frameCount && !hingeStart) return true;
            if (__instance.storeResult != null) __instance.storeResult.Value = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(Rotate), "DoRotate")]
    internal static class HeldRotationPatch
    {
        private static bool Prefix(Rotate __instance)
        {
            if (!AdjustmentRunner.Gameplay || !AdjustmentRunner.ToolSelected || __instance.Fsm.Name != "GrabItem"
                || __instance.State.Name != "Rotate" || __instance.vector == null) return true;
            bool pitch = __instance.vector.Name == "transformX";
            if (!pitch && __instance.vector.Name != "transformY") return true;
            var target = __instance.Fsm.GetOwnerDefaultTarget(__instance.gameObject);
            if (target == null || __instance.Fsm.Owner == null) return true;
            var reference = PoseMath.Reference(Plugin.ReferenceFrame.Value, __instance.Fsm.Owner.transform.rotation);
            float angle = __instance.Fsm.Variables.GetFsmFloat(pitch ? "mouse_x" : "mouse_y")?.Value ?? 0f;
            if (float.IsNaN(angle) || float.IsInfinity(angle)) return false;
            if (__instance.perSecond) angle *= Time.deltaTime;
            target.transform.rotation = PoseMath.Rotate(target.transform.rotation, reference,
                pitch ? new Vector3(angle, 0f, 0f) : new Vector3(0f, -angle, 0f));
            // GrabItem returns to a state that reapplies this local pose. Capture the final late-update
            // rotation immediately so releasing MMB does not discard the last rotation step.
            var saved = __instance.Fsm.Variables.GetFsmVector3("itemRot");
            if (saved != null) saved.Value = target.transform.localEulerAngles;
            return false;
        }
    }
}
