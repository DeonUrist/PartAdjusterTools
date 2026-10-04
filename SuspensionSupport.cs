using System;
using System.Collections.Generic;
using System.Linq;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using NWH.WheelController3D;
using UnityEngine;

namespace PartAdjustment
{
    internal static class SuspensionSupport
    {
        internal static bool Eligible(bool modelActive, bool hingeActive, bool attachmentEnabled,
            bool checkEnabled, IEnumerable<bool> mountSupported)
        {
            if (!modelActive || !hingeActive || !attachmentEnabled || !checkEnabled) return false;
            var mounts = mountSupported.ToArray();
            return mounts.Length >= 4 && mounts.All(supported => supported);
        }

        // Ignore activity above the vehicle: loaders can instantiate an inactive vehicle root.
        internal static bool BranchActive(Transform branch, Transform car)
        {
            for (var current = branch; current != null && current != car; current = current.parent)
            {
                if (!current.gameObject.activeSelf) return false;
                if (current.parent == car) return true;
            }
            return false;
        }

        internal static PlayMakerFSM EnabledFsm(Transform owner, string name) => owner == null ? null
            : owner.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.enabled && f.FsmName == name);

        internal static bool TryAssembly(Transform car, out Transform model, out Transform hinge,
            out WheelController[] wheels)
        {
            model = car == null ? null : car.Find("suspension_model");
            hinge = car == null ? null : car.Find("hinge_suspension_parent/hinge_suspension");
            wheels = car == null ? new WheelController[0] : car.GetComponentsInChildren<WheelController>(true)
                .Where(w => w.transform.parent == car && w.name.StartsWith("hinge_wheel_", StringComparison.Ordinal)).ToArray();
            return Eligible(BranchActive(model, car), BranchActive(hinge, car),
                EnabledFsm(hinge, "vehPart_Attach") != null, EnabledFsm(hinge, "checkSuspension") != null,
                wheels.Select(w => HasMountState(w.transform, "stock") && HasMountState(w.transform, "lifted")));
        }

        private static bool HasMountState(Transform wheel, string stateName)
        {
            var fsm = EnabledFsm(wheel, "Suspension");
            var state = fsm?.FsmStates.FirstOrDefault(s => s.Name == stateName);
            return state != null && MountAction(Actions(fsm.Fsm, state), true,
                a => fsm.Fsm.GetOwnerDefaultTarget(a.gameObject) == wheel.gameObject) != null;
        }

        internal static FsmStateAction[] Actions(Fsm owner, FsmState state)
        {
            if (state == null) return null;
            // Original prefab assets may never have run Awake; bind the state before loading its actions.
            if (!state.IsInitialized) state.Fsm = owner;
            return state.Actions;
        }

        internal static SetPosition MountAction(IEnumerable<FsmStateAction> actions, bool fsmEnabled,
            Func<SetPosition, bool> targetsWheel) => !fsmEnabled || actions == null ? null
            : actions.OfType<SetPosition>().FirstOrDefault(a => a.Enabled && a.space == Space.Self && targetsWheel(a));

        internal static Vector3 Position(SetPosition action, Vector3 fallback)
        {
            if (action == null) return fallback;
            var position = action.vector != null && !action.vector.IsNone ? action.vector.Value : fallback;
            if (action.x != null && !action.x.IsNone) position.x = action.x.Value;
            if (action.y != null && !action.y.IsNone) position.y = action.y.Value;
            if (action.z != null && !action.z.IsNone) position.z = action.z.Value;
            return position;
        }
    }
}
