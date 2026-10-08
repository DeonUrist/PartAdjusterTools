using HutongGames.PlayMaker;
using UnityEngine;

namespace PartAdjustment
{
    [ES3NonSerializable]
    [DefaultExecutionOrder(-10000)]
    public sealed class AdjustmentRunner : MonoBehaviour
    {
        internal static AdjustmentSession Session;
        internal static Transform Hover;
        // Hover (or the adjusted item) is an attached headlight / tail light that Use removes with the tool.
        internal static bool HoverRemovable;
        internal static Camera PlayerCamera;
        internal static int ConsumedSecondaryFrame = -1;
        private static Vector3 direction;
        private static float step;
        private static bool reset;
        private static GameObject cameraOwner;
        private static PlayMakerFSM adjustTool, drive, grab;
        private static float nextCameraRefresh;

        internal static PlayMakerFSM Find(GameObject go, string name)
        {
            if (go == null) return null;
            foreach (var fsm in go.GetComponents<PlayMakerFSM>()) if (fsm.FsmName == name) return fsm;
            return null;
        }

        internal static bool Gameplay => Plugin.Enabled.Value && Time.timeScale > 0f
            && Application.isFocused && Cursor.lockState == CursorLockMode.Locked;

        internal static Transform FreeAttachment(Transform hit)
        {
            if (!ToolSelected) return null;
            for (var t = hit; t != null; t = t.parent)
            {
                bool freelyPlaced = false, gauge = false, detachable = false;
                foreach (var fsm in t.gameObject.GetComponents<PlayMakerFSM>())
                {
                    if (fsm.FsmName == "Attach") freelyPlaced = true;
                    else if (fsm.FsmName == "de_Attach") detachable = true;
                    else if (fsm.FsmName == "ID")
                    {
                        var id = fsm.FsmVariables.GetFsmString("ID")?.Value;
                        // Headlights and tail lights, in a slot or mounted freely, adjust like gauges.
                        gauge = id == "gauge" || id == "headlight";
                    }
                }
                if (!freelyPlaced && !gauge) continue;
                return t.parent != null && t.CompareTag("vehPart") && detachable ? t : null;
            }
            return null;
        }

        internal static bool ToolSelected => adjustTool != null && adjustTool.isActiveAndEnabled;

        private static void RefreshCamera(GameObject owner)
        {
            if (owner == cameraOwner && PlayerCamera != null && adjustTool != null && drive != null && grab != null) return;
            if (owner == cameraOwner && Time.unscaledTime < nextCameraRefresh) return;
            cameraOwner = owner;
            nextCameraRefresh = Time.unscaledTime + 1f;
            PlayerCamera = owner == null ? null : owner.GetComponent<Camera>();
            adjustTool = drive = grab = null;
            if (owner == null) return;
            foreach (var fsm in owner.GetComponents<PlayMakerFSM>())
            {
                if (fsm.FsmName == "UseAdjustTool") adjustTool = fsm;
                else if (fsm.FsmName == "DriveUse") drive = fsm;
                else if (fsm.FsmName == "GrabItem") grab = fsm;
            }
        }

        private void Update()
        {
            direction = Vector3.zero;
            reset = false;
            Hover = null;
            HoverRemovable = false;
            var globals = FsmVariables.GlobalVariables;
            var cameraObject = globals.GetFsmGameObject("PlayerCamera")?.Value;
            RefreshCamera(cameraObject);
            if (!Plugin.Enabled.Value || PlayerCamera == null || !PlayerCamera.isActiveAndEnabled || !ToolSelected) { Stop(); return; }
            // Keep the session during menus/Alt-Tab, but never accept input there.
            if (!Gameplay) return;
            if (drive != null && drive.isActiveAndEnabled && drive.ActiveStateName != "Idle"
                && drive.ActiveStateName != "over" && drive.ActiveStateName != "checkHand") { Stop(); return; }
            if (grab != null && grab.isActiveAndEnabled && (grab.ActiveStateName == "ItemInHand"
                || grab.ActiveStateName == "Rotate" || grab.ActiveStateName == "Forward" || grab.ActiveStateName == "Backward"))
            {
                Stop();
                // Holding a headlight / tail light: offer the free mount on the aimed car body.
                HeadlightMount.Update(grab, adjustTool, PlayerCamera);
                return;
            }
            if (adjustTool.ActiveStateName == "adjust") { Stop(); return; }
            if (Session != null && !Session.Valid) Stop();

            // Match the game's F interaction ray, including trigger colliders and the actual
            // rendered camera/mouse position. A separate camera-forward ray can pick differently.
            const int mask = (1 << 0) | (1 << 8) | (1 << 9) | (1 << 16);
            var hit = ActionHelpers.MousePick(2f, mask);
            if (hit.collider != null && hit.distance <= 2f) Hover = FreeAttachment(hit.collider.transform);

            // Use (F) with the tool removes the attached headlight / tail light under the cursor (or being adjusted),
            // the same de_Attach the utility wrench sends. The vanilla tool keeps the press while it points at a hinge.
            var removable = Session != null ? Session.Target : Hover;
            HoverRemovable = Plugin.AttachAnywhere.Value && removable != null && !HeadlightMount.ToolBusy(adjustTool)
                && removable.CompareTag("vehPart") && HeadlightMount.IsHeadlight(removable.gameObject);
            if (HoverRemovable && Controls.Pressed(Controls.Use))
            {
                Stop();
                HeadlightMount.Detach(removable.gameObject, adjustTool);
                return;
            }

            if (Controls.Pressed(Controls.Secondary))
            {
                if (Session != null) { ConsumedSecondaryFrame = Time.frameCount; Stop(); return; }
                if (Hover != null)
                {
                    ConsumedSecondaryFrame = Time.frameCount;
                    Session = new AdjustmentSession(Hover);
                    return;
                }
            }
            if (Session == null) return;
            // F retains its usual deliberate detach action, after ending our session.
            if (Controls.Pressed(Controls.Use)) { Stop(); return; }
            if (Controls.Pressed(Controls.Mode)) Session.Rotating = !Session.Rotating;
            reset = Controls.Pressed(Controls.Reset);
            direction = Controls.Direction(Session.Rotating);
            step = Controls.Step(Session.Rotating, Controls.Fast);
        }

        internal static void Apply()
        {
            if (!ToolSelected || (adjustTool != null && adjustTool.ActiveStateName == "adjust")) { Stop(); return; }
            if (Session == null || !Gameplay || PlayerCamera == null) return;
            if (!Session.Valid) { Stop(); return; }
            if (reset) Session.Reset();
            else Session.Change(direction, step, PoseMath.Reference(Plugin.ReferenceFrame.Value, PlayerCamera.transform.rotation));
        }

        internal static void Stop() { Session = null; Hover = null; HoverRemovable = false; direction = Vector3.zero; reset = false; }
        internal static void SceneChanged()
        {
            Stop(); HeadlightMount.SceneChanged(); PlayerCamera = null; cameraOwner = null; adjustTool = drive = grab = null;
            nextCameraRefresh = 0f; ConsumedSecondaryFrame = -1;
        }
        private void OnDestroy() { SceneChanged(); }
    }

    [ES3NonSerializable]
    [DefaultExecutionOrder(10000)]
    public sealed class AdjustmentLateRunner : MonoBehaviour
    {
        private readonly AdjustmentHint hint = new AdjustmentHint();
        private void LateUpdate() { AdjustmentRunner.Apply(); HeadlightMount.LateCheck(); hint.Refresh(); }
        private void OnDestroy() { hint.Dispose(); }
    }
}
