using HutongGames.PlayMaker;
using UnityEngine;

namespace PartAdjustment
{
    // PutAwayKey (X) puts away the Toolbox (utility), Repairbox (repair) or Part Adjustment tools through the game's own
    // "offToolbox" state, i.e. exactly what pressing Use on the toolbox again does (arm hidden, tool FSMs off, sound).
    [ES3NonSerializable]
    public sealed class ToolPutAway : MonoBehaviour
    {
        private static readonly string[] Names = { "UseToolbox", "UseRepairbox", "UseAdjustToolBox" };
        private readonly PlayMakerFSM[] fsms = new PlayMakerFSM[3];
        private GameObject owner;

        private void Update()
        {
            if (!Plugin.PutAwayWithKey.Value) return;
            var key = Plugin.PutAwayKey.Value;
            if (key == KeyCode.None || !Input.GetKeyDown(key)) return;
            if (Time.timeScale <= 0f || !Application.isFocused || Cursor.lockState != CursorLockMode.Locked) return;
            var camera = FsmVariables.GlobalVariables.GetFsmGameObject("PlayerCamera")?.Value;
            if (camera == null || !camera.activeInHierarchy) return;
            if (camera != owner || fsms[0] == null)
            {
                owner = camera;
                for (int i = 0; i < Names.Length; i++) fsms[i] = AdjustmentRunner.Find(camera, Names[i]);
            }
            foreach (var fsm in fsms)
            {
                if (fsm == null || !fsm.isActiveAndEnabled) continue;
                var state = fsm.ActiveStateName;
                if (state != "off 2" && state != "over 2") continue;   // "off 2"/"over 2" = this tool is in the hands
                fsm.Fsm.SetState("offToolbox");
            }
        }

        private void OnDestroy() { owner = null; }
    }
}
