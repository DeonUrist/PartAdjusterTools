using UnityEngine;

namespace PartAdjustment
{
    // 1.2.2: a car standing still rocks itself (seen with truck wheels on a widened, lifted TinyTyrant: the wheel loads swapped left/right
    // twice a second, angular velocity 0.7 rad/s, lateral slip flipping sign, no collision contacts at all). The body rolls, the wheels on the
    // body slide sideways on the ground, their sideways grip pushes the body back, it overshoots - an oscillation the vanilla lifted dampers
    // (half the stock rate) cannot kill under the heavier loads. Below 0.6 m/s the Rigidbody gets extra angular drag; it fades out by 1.2 m/s
    // so driving feel is untouched. The original drag is restored when the car moves. Apocapatrol has the same component for its raider
    // cars; whichever mod attached its StandstillDamper first owns a car (checked by type name).
    [ES3NonSerializable]
    public sealed class StandstillDamper : MonoBehaviour
    {
        internal const float ExtraDrag = 4f, StillSpeed = 0.6f, FreeSpeed = 1.2f;
        private Rigidbody rb; private float baseDrag; private bool boosted;

        internal static bool Owned(GameObject car)
        {
            foreach (var c in car.GetComponents<Component>()) if (c != null && c.GetType().Name == "StandstillDamper") return true;
            return false;
        }

        private void FixedUpdate()
        {
            if (rb == null) { rb = GetComponent<Rigidbody>(); if (rb == null) { Destroy(this); return; } baseDrag = rb.angularDrag; }
            if (!Plugin.StandstillDamping.Value || rb.isKinematic) { Release(); return; }
            float speed = rb.velocity.magnitude;
            float k = 1f - Mathf.InverseLerp(StillSpeed, FreeSpeed, speed);
            if (k <= 0f) { Release(); return; }
            if (!boosted) { baseDrag = rb.angularDrag; boosted = true; }
            rb.angularDrag = baseDrag + ExtraDrag * k;
        }

        private void Release() { if (boosted) { rb.angularDrag = baseDrag; boosted = false; } }
        private void OnDisable() { if (rb != null) Release(); }
    }

    // attaches the damper to the car the player drives (ours or Apocapatrol's, whoever is first)
    [ES3NonSerializable]
    public sealed class StandstillDamperRunner : MonoBehaviour
    {
        private Transform player; private float next;
        private void Update()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + 1f;
            if (player == null) { var go = GameObject.Find("Player"); player = go != null ? go.transform : null; if (player == null) return; }
            for (var a = player.parent; a != null; a = a.parent)
                if (a.parent == null && a.GetComponentInChildren<NWH.WheelController3D.WheelController>(true) != null)
                { if (!StandstillDamper.Owned(a.gameObject)) a.gameObject.AddComponent<StandstillDamper>(); break; }
        }
    }
}
