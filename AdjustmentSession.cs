using UnityEngine;

namespace PartAdjustment
{
    // A session never reparents, unlocks or changes physics. Vanilla saves this local pose.
    internal sealed class AdjustmentSession
    {
        internal readonly Transform Target;
        private readonly Transform parent;
        private readonly Vector3 startPosition;
        private readonly Quaternion startRotation;
        internal bool Rotating;

        internal AdjustmentSession(Transform target)
        {
            Target = target;
            parent = target.parent;
            startPosition = target.localPosition;
            startRotation = target.localRotation;
        }

        internal bool Valid => Target != null && parent != null && Target.parent == parent
            && Target.gameObject.activeInHierarchy && Target.CompareTag("vehPart");

        internal void Reset()
        {
            if (!Valid) return;
            Target.localPosition = startPosition;
            Target.localRotation = startRotation;
        }

        internal void Change(Vector3 direction, float step, Quaternion reference)
        {
            if (!Valid || direction.sqrMagnitude == 0f) return;
            if (!Rotating) Target.position += reference * (direction * step);
            else Target.rotation = PoseMath.Rotate(Target.rotation, reference, direction * step);
        }
    }

    internal static class PoseMath
    {
        internal static Quaternion Reference(int frame, Quaternion camera) => frame == 0 ? camera : Quaternion.identity;

        // Compose axis-angle rotations instead of interpreting a camera direction as Euler angles.
        internal static Quaternion Rotate(Quaternion pose, Quaternion reference, Vector3 degrees) =>
            Quaternion.AngleAxis(degrees.z, reference * Vector3.forward)
            * Quaternion.AngleAxis(degrees.y, reference * Vector3.up)
            * Quaternion.AngleAxis(degrees.x, reference * Vector3.right) * pose;
    }
}
