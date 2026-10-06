using System.Collections.Generic;
using UnityEngine;

namespace PartAdjustment
{
    // 1.2.0: the wheels of the car the player drives never touch the player. The game isolates the driver from the car's hull by layer
    // (Drive 7 does not collide with Car 8), but the wheel colliders - NWH's own wheel mesh colliders and the game's wheel_hub sphere
    // (0.7 x the tyre radius) - sit on layer 2, which does collide with the driver. Stock wheels never reach the seats; a raised
    // suspension or big truck wheels push them into the cabin, and PhysX shoving the driver and the car apart every step is the
    // vibration players see. Collisions between the driver's colliders and every layer-2 collider of their car are ignored, re-checked
    // twice a second (NWH rebuilds its colliders when the wheel dimensions change).
    [ES3NonSerializable]
    public sealed class DriverContacts : MonoBehaviour
    {
        private Transform player;
        private float next;
        private long signature;
        private Transform car;
        private static readonly List<Collider> playerCols = new List<Collider>(), carCols = new List<Collider>();

        private void Update()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + 0.5f;
            if (player == null) { var go = GameObject.Find("Player"); player = go != null ? go.transform : null; if (player == null) return; }
            Transform vehicle = null;
            for (var a = player.parent; a != null; a = a.parent)
                if (a.GetComponentInChildren<NWH.WheelController3D.WheelController>(true) != null && a.parent == null) { vehicle = a; break; }
            if (vehicle == null) { car = null; signature = 0; return; }
            playerCols.Clear(); carCols.Clear();
            player.GetComponentsInChildren(true, playerCols);
            vehicle.GetComponentsInChildren(true, carCols);
            long sig = vehicle.GetInstanceID();
            foreach (var c in playerCols) if (c != null) sig = sig * 31 + c.GetInstanceID();
            foreach (var c in carCols) if (c != null && c.gameObject.layer == 2) sig = sig * 31 + c.GetInstanceID();
            if (vehicle == car && sig == signature) return;
            car = vehicle; signature = sig;
            int n = 0;
            foreach (var c in carCols)
            {
                if (c == null || c.gameObject.layer != 2 || c.transform.IsChildOf(player)) continue;
                foreach (var p in playerCols) if (p != null) { Physics.IgnoreCollision(p, c, true); n++; }
            }
            if (n > 0) Plugin.Log.LogDebug("Driver contacts: " + n + " wheel collider pairs of " + vehicle.name + " ignored for the player.");
        }
    }
}
