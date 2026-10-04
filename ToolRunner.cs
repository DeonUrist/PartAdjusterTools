using System.Linq;
using HutongGames.PlayMaker;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PartAdjustment
{
    [ES3NonSerializable]
    public sealed class ToolRunner : MonoBehaviour
    {
        internal static SuspensionAdjustment Active;
        internal static PlayMakerFSM Tool;
        private float nextScan;
        private readonly SuspensionHint hint = new SuspensionHint();

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
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            float width = (keyboard.numpad6Key.wasPressedThisFrame ? Plugin.WidthStep.Value : 0f)
                - (keyboard.numpad4Key.wasPressedThisFrame ? Plugin.WidthStep.Value : 0f);
            float height = (keyboard.numpad8Key.wasPressedThisFrame ? Plugin.HeightStep.Value : 0f)
                - (keyboard.numpad2Key.wasPressedThisFrame ? Plugin.HeightStep.Value : 0f);
            bool reset = keyboard.numpad0Key.wasPressedThisFrame;
            if (width != 0f || height != 0f || reset) Active.Change(width, height, reset);
        }

        private void OnDestroy() { hint.Show(false); Active = null; }
    }
}
