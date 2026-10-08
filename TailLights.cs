using System;
using System.Collections.Generic;
using HarmonyLib;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace PartAdjustment
{
    // Tail lights = the game's Headlight item, red. No new prefab or save data:
    //  - identity is the object NAME (tail_light(Clone)N); Easy Save keeps names, the item saves as a normal headlight;
    //  - the red look is runtime only (MaterialPropertyBlock + Light colour), re-applied on every load (PlayMakerFSM.Start);
    //  - an inactive root template "tail_light" under DontDestroyOnLoad lets Apocaspawner list and spawn it like any
    //    scene item (ID "headlight" -> Vehicle Parts). Its red shared materials exist only for the spawner preview and
    //    are swapped back to the game's materials on every live copy, so saves only reference the game's own materials;
    //  - world loot: an item spawner creating a Headlight turns it into a tail light at TailLightChance %.
    internal static class TailLights
    {
        internal const string Key = "tail_light";
        private const string Base = "Headlight";
        private static readonly Color Tint = new Color(1f, 0.13f, 0.09f);
        private static readonly Color LightColor = new Color(1f, 0.07f, 0.04f);
        private static GameObject template;
        private static bool prefabSearched;
        private static GameObject prefab;
        private static readonly Dictionary<Material, Material> redOf = new Dictionary<Material, Material>();
        private static readonly Dictionary<Material, Material> originalOf = new Dictionary<Material, Material>();
        private static readonly Dictionary<string, Vector2> baseLight = new Dictionary<string, Vector2>();
        private static readonly HashSet<GameObject> spawned = new HashSet<GameObject>();
        private static MaterialPropertyBlock block;
        private static readonly int ColorId = Shader.PropertyToID("_Color"), EmissionId = Shader.PropertyToID("_EmissionColor");

        internal static bool IsTail(GameObject go) => go != null && go.name.StartsWith(Key, StringComparison.Ordinal);

        internal static void SceneChanged() { spawned.Clear(); prefabSearched = false; TailLightRunner.SceneLoaded(); }

        internal static bool EnsureTemplate()
        {
            if (template != null) return true;
            prefabSearched = false;
            var source = HeadlightPrefab();
            if (source == null) return false;
            var holder = new GameObject("PartAdjustment.TailLightStaging");
            holder.SetActive(false);
            try
            {
                // Under an inactive parent: no Awake, no FSM, no Easy Save registration ever runs on the template.
                template = UnityEngine.Object.Instantiate(source, holder.transform, false);
                template.name = Key;
                template.SetActive(false);
                template.transform.SetParent(null, false);
                UnityEngine.Object.DontDestroyOnLoad(template);
                foreach (var r in template.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = Red(mats[i]);
                    r.sharedMaterials = mats;
                }
                ApplyLights(template);
                Plugin.Log.LogInfo("Tail light template ready (spawnable as \"" + Key + "\").");
            }
            catch (Exception e) { Plugin.Log.LogError("Could not create the tail light template: " + e); if (template != null) UnityEngine.Object.Destroy(template); template = null; }
            finally { UnityEngine.Object.Destroy(holder); }
            return true;
        }

        private static GameObject HeadlightPrefab()
        {
            if (prefab != null) return prefab;
            if (prefabSearched) return null;
            prefabSearched = true;
            foreach (var fsm in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
            {
                if (fsm == null || fsm.FsmName != "ID") continue;
                var go = fsm.gameObject;
                if (go.name != Base || go.scene.IsValid() || go.transform.parent != null) continue;
                if (fsm.FsmVariables.GetFsmString("ID")?.Value != "headlight") continue;
                prefab = go;
                foreach (var light in go.GetComponentsInChildren<Light>(true))
                    baseLight[light.name] = new Vector2(light.intensity, light.range);
                return prefab;
            }
            return null;
        }

        private static Material Red(Material m)
        {
            if (m == null) return null;
            if (originalOf.ContainsKey(m)) return m;
            if (redOf.TryGetValue(m, out var red)) return red;
            red = new Material(m) { name = m.name + " (tail light)", hideFlags = HideFlags.HideAndDontSave };
            if (red.HasProperty(ColorId)) red.SetColor(ColorId, Tinted(m.GetColor(ColorId)));
            if (red.HasProperty(EmissionId)) red.SetColor(EmissionId, Tinted(m.GetColor(EmissionId)));
            redOf[m] = red; originalOf[red] = m;
            return red;
        }

        private static Color Tinted(Color c) => new Color(c.r * Tint.r, c.g * Tint.g, c.b * Tint.b, c.a);

        // Idempotent: safe on every Start, load, spawn and conversion.
        internal static void Convert(GameObject go)
        {
            if (go == null) return;
            if (block == null) block = new MaterialPropertyBlock();
            HeadlightPrefab();
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
                var mats = r.sharedMaterials;
                bool swapped = false;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] != null && originalOf.TryGetValue(mats[i], out var original)) { mats[i] = original; swapped = true; }
                if (swapped) r.sharedMaterials = mats;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;
                    block.Clear();
                    if (m.HasProperty(ColorId)) block.SetColor(ColorId, Tinted(m.GetColor(ColorId)));
                    if (m.HasProperty(EmissionId)) block.SetColor(EmissionId, Tinted(m.GetColor(EmissionId)));
                    r.SetPropertyBlock(block, i);
                }
            }
            ApplyLights(go);
        }

        private static void ApplyLights(GameObject go)
        {
            foreach (var light in go.GetComponentsInChildren<Light>(true))
            {
                light.color = LightColor;
                // Absolute values from the prefab: Easy Save restores the saved light, so never scale the current one.
                if (baseLight.TryGetValue(light.name, out var b)) { light.intensity = b.x * 0.6f; light.range = b.y * 0.5f; }
            }
        }

        // The item's ItemName FSM writes the literal "Headlight" into the look-at label; say "Tail Light" for ours.
        // (Patched at the action instead of editing FSM data, which may be shared with every headlight.)
        internal static void LabelShown(UiTextSetText action)
        {
            if (action.text == null || action.text.Value != Base || !IsTail(action.Owner)) return;
            var target = action.Fsm.GetOwnerDefaultTarget(action.gameObject);
            var text = target == null ? null : target.GetComponent<UnityEngine.UI.Text>();
            if (text != null) text.text = "Tail Light";
        }

        // ---- world loot ----
        internal static void Created(CreateObject action)
        {
            int chance = Plugin.TailLightChance.Value;
            if (chance <= 0) return;
            var source = action.gameObject?.Value;
            if (source == null || source.name != Base) return;
            var owner = action.Owner;
            bool spawner = (action.Fsm?.Name ?? "").IndexOf("Spawn", StringComparison.OrdinalIgnoreCase) >= 0
                || (owner != null && owner.name.IndexOf("Spawn", StringComparison.OrdinalIgnoreCase) >= 0);
            if (!spawner) return;
            var made = action.storeObject?.Value;
            if (made == null || made == source || !made.name.StartsWith(Base, StringComparison.Ordinal) || !HeadlightMount.IsHeadlight(made)) return;
            if (UnityEngine.Random.value * 100f >= chance) return;
            made.name = Key + made.name.Substring(Base.Length);
            spawned.Add(made);
            Convert(made);
        }

        internal static bool Pending => spawned.Count > 0;

        // The spawner names its clone after CreateObject (GetName + id + SetName); keep the tail_light prefix.
        internal static void Renamed(GameObject go)
        {
            if (go == null || !spawned.Remove(go)) return;
            if (go.name.StartsWith(Base, StringComparison.Ordinal)) go.name = Key + go.name.Substring(Base.Length);
            spawned.RemoveWhere(g => g == null);
        }
    }

    // Builds the template a few frames after each scene load (the Headlight prefab is loaded with the game scene).
    [ES3NonSerializable]
    public sealed class TailLightRunner : MonoBehaviour
    {
        private static int due = 3, tries;
        private static float retryAt;
        internal static void SceneLoaded() { due = Time.frameCount + 3; tries = 0; }
        private void Update()
        {
            if (due < 0 || Time.frameCount < due || Time.unscaledTime < retryAt) return;
            if (TailLights.EnsureTemplate() || ++tries >= 4) { due = -1; return; }
            retryAt = Time.unscaledTime + 5f;   // the prefab may load a little later; a few cheap retries per scene
        }
    }

    [HarmonyPatch(typeof(CreateObject), nameof(CreateObject.OnEnter))]
    internal static class TailLightSpawnPatch
    {
        private static void Postfix(CreateObject __instance)
        {
            try { TailLights.Created(__instance); }
            catch (Exception e) { Plugin.Log.LogError("Tail light spawn: " + e); }
        }
    }

    [HarmonyPatch(typeof(SetName), nameof(SetName.OnEnter))]
    internal static class TailLightNamePatch
    {
        private static void Postfix(SetName __instance)
        {
            if (!TailLights.Pending) return;
            try { TailLights.Renamed(__instance.Fsm.GetOwnerDefaultTarget(__instance.gameObject)); }
            catch (Exception e) { Plugin.Log.LogError("Tail light name: " + e); }
        }
    }

    [HarmonyPatch(typeof(UiTextSetText), nameof(UiTextSetText.OnEnter))]
    internal static class TailLightLabelPatch
    {
        private static void Postfix(UiTextSetText __instance)
        {
            try { TailLights.LabelShown(__instance); }
            catch (Exception e) { Plugin.Log.LogError("Tail light label: " + e); }
        }
    }

    // Loaded, spawned (Apocaspawner) and world tail lights all start their Headlight FSM once: give them the red look.
    [HarmonyPatch(typeof(PlayMakerFSM), "Start")]
    internal static class TailLightStartPatch
    {
        private static void Postfix(PlayMakerFSM __instance)
        {
            if (__instance.FsmName != "Headlight" || !TailLights.IsTail(__instance.gameObject)) return;
            try { TailLights.Convert(__instance.gameObject); }
            catch (Exception e) { Plugin.Log.LogError("Tail light look: " + e); }
        }
    }
}
