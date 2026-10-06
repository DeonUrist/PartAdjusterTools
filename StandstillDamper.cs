using System.Collections.Generic;
using NWH.WheelController3D;
using UnityEngine;

namespace PartAdjustment
{
    // 1.2.2 / 1.2.3: a car standing still rocks itself (seen with truck wheels on a widened, lifted TinyTyrant: wheel loads swapping left/right
    // twice a second, angular velocity 0.6-0.7 rad/s, lateral slip flipping sign, no collision contacts). The body rolls, the wheels on it
    // slide sideways on the ground, their sideways grip pushes the body back harder than the roll needs and it overshoots: the wheels'
    // lateral friction drives the roll instead of damping it, and the vanilla lifted dampers (half the stock rate) cannot stop it under
    // the heavier loads. Extra angular drag alone (1.2.2) was not enough. Below 0.6 m/s (fading out by 1.2 m/s) the driving force itself
    // is taken away - the wheels' lateral friction stiffness is cut to a quarter - and the body's angular velocity is bled off directly
    // each physics step. Everything is restored as soon as the car moves. Apocapatrol has the same component for its raider cars; whichever
    // mod attached its StandstillDamper first owns a car (checked by type name).
    [ES3NonSerializable]
    public sealed class StandstillDamper : MonoBehaviour
    {
        internal const float StillSpeed = 0.6f, FreeSpeed = 1.2f, StiffnessAtRest = 0.25f, SpinBleed = 0.5f, DamperAtRest = 3f, HoldSpeed = 0.3f, UnholdSpeed = 0.5f;
        private Rigidbody rb; private bool boosted, held, bumped; private RigidbodyConstraints baseConstraints;
        private WheelController[] wheels = new WheelController[0]; private float[] baseStiffness = new float[0], baseBump = new float[0], baseRebound = new float[0]; private float nextWheels;

        internal static bool Owned(GameObject car)
        {
            foreach (var c in car.GetComponents<Component>()) if (c != null && c.GetType().Name == "StandstillDamper") return true;
            return false;
        }

        private void FixedUpdate()
        {
            if (rb == null) { rb = GetComponent<Rigidbody>(); if (rb == null) { Destroy(this); return; } }
            if (!Plugin.StandstillDamping.Value || rb.isKinematic) { Release(); return; }
            float k = 1f - Mathf.InverseLerp(StillSpeed, FreeSpeed, rb.velocity.magnitude);
            if (k <= 0f) { Release(); return; }
            if (!boosted || Time.time >= nextWheels) Collect();
            boosted = true;
            for (int i = 0; i < wheels.Length; i++)
                if (wheels[i] == null) continue;
                else
                {
                    // the roll-driving sideways grip goes down, the dampers that eat roll energy go up (the lift kit halves the vanilla rate)
                    wheels[i].LateralFrictionStiffness = Mathf.Lerp(baseStiffness[i], baseStiffness[i] * StiffnessAtRest, k);
                    wheels[i].DamperBumpRate = Mathf.Lerp(baseBump[i], baseBump[i] * DamperAtRest, k);
                    wheels[i].DamperReboundRate = Mathf.Lerp(baseRebound[i], baseRebound[i] * DamperAtRest, k);
                }
            // the roll / yaw rate is bled off directly: a fraction per step, the full fraction when standing
            rb.angularVelocity *= 1f - SpinBleed * k;
            // parked (under 0.3 m/s): the body cannot roll or pitch at all - the roll <-> sideways-grip loop has nothing to work with. The
            // suspension still carries the car, sideways grip still holds it on a slope; released above 0.5 m/s (a push, the gas)
            // The per-step probe showed the real thing: with the roll frozen the body still jittered 5 mm left-right EVERY physics step, the
            // wheel loads swapping sides each step (5600 N / 0 / 5600 N / 0...). NWH's lateral friction at (near) zero speed is bang-bang: the
            // full load x grip against whatever sideways velocity exists, and with 0.52 m wheels carrying 5000 N that impulse (100 N s on a
            // 600 kg car) reverses the sideways velocity every step instead of killing it. So a parked car is also frozen in place
            // horizontally (X/Z position) - no sideways velocity, no lateral force, nothing to flip - while the suspension keeps working
            // vertically. Released when the engine drives a wheel (motor torque), when something hits the car, or when it moves anyway.
            float speed = rb.velocity.magnitude;
            bool driving = false;
            for (int i = 0; i < wheels.Length; i++) if (wheels[i] != null && Mathf.Abs(wheels[i].MotorTorque) > 30f) { driving = true; break; }
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

        // the wheels (direct hinge_wheel_* children) and their vanilla lateral stiffness - re-read every 2 s while active (a wheel put on or
        // taken off changes the set; the game writes the friction values when a wheel goes on)
        private void Collect()
        {
            nextWheels = Time.time + 2f;
            if (boosted) Restore();
            var list = new List<WheelController>();
            foreach (var w in GetComponentsInChildren<WheelController>(true)) if (w != null && w.transform.parent == transform) list.Add(w);
            wheels = list.ToArray(); baseStiffness = new float[wheels.Length]; baseBump = new float[wheels.Length]; baseRebound = new float[wheels.Length];
            for (int i = 0; i < wheels.Length; i++) { baseStiffness[i] = wheels[i].LateralFrictionStiffness; baseBump[i] = wheels[i].DamperBumpRate; baseRebound[i] = wheels[i].DamperReboundRate; }
        }

        private void Restore() { for (int i = 0; i < wheels.Length; i++) if (wheels[i] != null) { wheels[i].LateralFrictionStiffness = baseStiffness[i]; wheels[i].DamperBumpRate = baseBump[i]; wheels[i].DamperReboundRate = baseRebound[i]; } }
        private void OnCollisionEnter(Collision col) { if (col.impulse.magnitude > 50f) bumped = true; }   // rammed, shot off its wheels...
        private void Unhold() { if (held) { rb.constraints = baseConstraints; held = false; } }
        private void Release() { Unhold(); if (boosted) { Restore(); boosted = false; } }
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
                if (a.parent == null && a.GetComponentInChildren<WheelController>(true) != null)
                { if (!StandstillDamper.Owned(a.gameObject)) a.gameObject.AddComponent<StandstillDamper>(); break; }
        }
    }
}
