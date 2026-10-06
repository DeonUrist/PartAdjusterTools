using System.Collections.Generic;
using NWH.WheelController3D;
using UnityEngine;

namespace PartAdjustment
{
    // A parked car is held still (1.2.x, final form in 1.3.0). Why: the vanilla tyre model (NWH) applies its full sideways grip against any
    // sideways velocity, computed one physics step late. On stock cars the impulse is small and dies out; with 0.52 m truck wheels carrying
    // 5000 N on a lifted chassis it reverses the body's sideways velocity every step - the car "shakes" in place, wheel loads swapping sides
    // 50 times a second (measured). Nothing in the tyre or suspension parameters stops it safely, so the body is simply not allowed to move
    // while parked: below 0.3 m/s with no engine torque on the wheels, the Rigidbody's horizontal position and its roll / pitch are frozen
    // (the suspension still works vertically, the car still sits on slopes). Released when the engine drives a wheel, when something hits
    // the car, or when it moves anyway (above 0.5 m/s). Apocapatrol has the same component; whichever mod attached one first owns a car
    // (checked by type name).
    [ES3NonSerializable]
    public sealed class StandstillDamper : MonoBehaviour
    {
        internal const float HoldSpeed = 0.3f, UnholdSpeed = 0.5f, DriveTorque = 30f, BumpImpulse = 50f;
        private Rigidbody rb; private bool held, bumped; private RigidbodyConstraints baseConstraints;
        private WheelController[] wheels = new WheelController[0]; private float nextWheels;

        internal static bool Owned(GameObject car)
        {
            foreach (var c in car.GetComponents<Component>()) if (c != null && c.GetType().Name == "StandstillDamper") return true;
            return false;
        }

        private void FixedUpdate()
        {
            if (rb == null) { rb = GetComponent<Rigidbody>(); if (rb == null) { Destroy(this); return; } }
            if (!Plugin.StandstillDamping.Value || rb.isKinematic) { Unhold(); return; }
            if (Time.time >= nextWheels) Collect();
            float speed = rb.velocity.magnitude;
            bool driving = false;
            for (int i = 0; i < wheels.Length; i++) if (wheels[i] != null && Mathf.Abs(wheels[i].MotorTorque) > DriveTorque) { driving = true; break; }
            if (!held && speed < HoldSpeed && !driving)
            {
                baseConstraints = rb.constraints;
                rb.constraints = baseConstraints | RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
                rb.angularVelocity = Vector3.zero;
                rb.velocity = new Vector3(0f, rb.velocity.y, 0f);
                held = true;
            }
            else if (held && (speed > UnholdSpeed || driving || bumped)) Unhold();
            bumped = false;
        }

        // the wheels (direct hinge_wheel_* children), re-read every 2 s: a wheel put on or taken off changes the set
        private void Collect()
        {
            nextWheels = Time.time + 2f;
            var list = new List<WheelController>();
            foreach (var w in GetComponentsInChildren<WheelController>(true)) if (w != null && w.transform.parent == transform) list.Add(w);
            wheels = list.ToArray();
        }
        private void OnCollisionEnter(Collision col) { if (col.impulse.magnitude > BumpImpulse) bumped = true; }   // rammed, shot, blown...
        private void Unhold() { if (held) { rb.constraints = baseConstraints; held = false; } }
        private void OnDisable() { if (rb != null) Unhold(); }
    }

    // attaches the hold to the car the player drives (ours or Apocapatrol's, whoever is first)
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
                if (a.parent == null && a.GetComponentInChildren<WheelController>(true) != null)
                { if (!StandstillDamper.Owned(a.gameObject)) a.gameObject.AddComponent<StandstillDamper>(); break; }
        }
    }
}
