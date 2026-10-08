using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace PartAdjustment
{
    // Tail lights = the game's Headlight item, red. No new prefab or save data:
    //  - identity is the object NAME (tail_light(Clone)N); Easy Save keeps names, the item saves as a normal headlight;
    //  - the red look is runtime only (MaterialPropertyBlock + Light colour), re-applied on every load (PlayMakerFSM.Start);
    //  - per headlight prefab (Headlight, Headlight_2, Headlight_3) an inactive root template tail_light / _2 / _3 under DontDestroyOnLoad lets Apocaspawner list and spawn it like any
    //    scene item (ID "headlight" -> Vehicle Parts). Its red shared materials exist only for the spawner preview and
    //    are swapped back to the game's materials on every live copy, so saves only reference the game's own materials;
    //  - world loot: an item spawner creating a Headlight turns it into a tail light at TailLightChance %.
    internal static class TailLights
    {
        internal const string Key = "tail_light";
        private const string Base = "Headlight";
        private static readonly Color Tint = new Color(1f, 0.13f, 0.09f);
        private static readonly Color LightColor = new Color(1f, 0.1f, 0.05f);
        private static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
        private static readonly Dictionary<string, GameObject> templates = new Dictionary<string, GameObject>();
        private static readonly Dictionary<Material, Material> redOf = new Dictionary<Material, Material>();
        private static readonly Dictionary<Material, Material> originalOf = new Dictionary<Material, Material>();
        private static readonly Dictionary<string, Dictionary<string, Vector2>> baseLight = new Dictionary<string, Dictionary<string, Vector2>>();
        private static readonly HashSet<GameObject> spawned = new HashSet<GameObject>();
        private static MaterialPropertyBlock block;
        private static readonly int ColorId = Shader.PropertyToID("_Color"), EmissionId = Shader.PropertyToID("_EmissionColor"), TintId = Shader.PropertyToID("_TintColor");

        internal static bool IsTail(GameObject go) => go != null && go.name.StartsWith(Key, StringComparison.Ordinal);

        // "Headlight" -> "tail_light", "Headlight_2" -> "tail_light_2", "Headlight_3" -> "tail_light_3" (same suffix as the game's prefab).
        internal static string TailName(string headlightName) => Key + headlightName.Substring(Base.Length);
        private static string Variant(string name)
        {
            var s = name.StartsWith(Key, StringComparison.Ordinal) ? name.Substring(Key.Length) : name.StartsWith(Base, StringComparison.Ordinal) ? name.Substring(Base.Length) : "";
            int clone = s.IndexOf("(Clone)", StringComparison.Ordinal);
            return (clone >= 0 ? s.Substring(0, clone) : s).Trim();
        }

        internal static void SceneChanged() { spawned.Clear(); nextScan = 0f; TailLightRunner.SceneLoaded(); }

        // One template per headlight prefab; true once every prefab found has one (and at least one exists).
        internal static bool EnsureTemplates()
        {
            ScanPrefabs();
            if (prefabs.Count == 0) return false;
            foreach (var kv in prefabs)
            {
                if (templates.TryGetValue(kv.Key, out var existing) && existing != null) continue;
                var made = MakeTemplate(kv.Value);
                if (made != null) templates[kv.Key] = made;
            }
            return true;
        }

        private static GameObject MakeTemplate(GameObject source)
        {
            var holder = new GameObject("PartAdjustment.TailLightStaging");
            holder.SetActive(false);
            GameObject template = null;
            try
            {
                // Under an inactive parent: no Awake, no FSM, no Easy Save registration ever runs on the template.
                template = UnityEngine.Object.Instantiate(source, holder.transform, false);
                template.name = TailName(source.name);
                template.SetActive(false);
                template.transform.SetParent(null, false);
                UnityEngine.Object.DontDestroyOnLoad(template);
                foreach (var r in template.GetComponentsInChildren<Renderer>(true))
                {
                    if (InLight(r.transform, template.transform)) continue;
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = Red(mats[i]);
                    r.sharedMaterials = mats;
                }
                ApplyLights(template);
                Plugin.Log.LogInfo("Tail light template ready: " + template.name + " (from " + source.name + ")");
                return template;
            }
            catch (Exception e) { Plugin.Log.LogError("Could not create the tail light template for " + source.name + ": " + e); if (template != null) UnityEngine.Object.Destroy(template); return null; }
            finally { UnityEngine.Object.Destroy(holder); }
        }

        // Every headlight prefab asset: root, not in a scene, ID "headlight", name Headlight / Headlight_N.
        private static float nextScan;
        private static void ScanPrefabs()
        {
            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + 5f;   // FindObjectsOfTypeAll is not cheap: at most every 5 s
            foreach (var fsm in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
            {
                if (fsm == null || fsm.FsmName != "ID") continue;
                var go = fsm.gameObject;
                if (!go.name.StartsWith(Base, StringComparison.Ordinal) || go.name.Contains("(") || go.scene.IsValid() || go.transform.parent != null) continue;
                if (fsm.FsmVariables.GetFsmString("ID")?.Value != "headlight") continue;
                var variant = Variant(go.name);
                if (prefabs.TryGetValue(variant, out var known) && known != null) continue;
                prefabs[variant] = go;
                var lights = new Dictionary<string, Vector2>();
                foreach (var light in go.GetComponentsInChildren<Light>(true)) lights[light.name] = new Vector2(light.intensity, light.range);
                baseLight[variant] = lights;
            }
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
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
                // The lit part (child "Light": light, beam/glow): a renderer driven by a script is left alone - a per-material block
                // would replace the script's own block (1.5.1: the tail lights never looked switched on).
                bool lit = InLight(r.transform, go.transform);
                if (lit && r.GetComponents<MonoBehaviour>().Length > 0) continue;
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
                    // Body: darkened red tint. A plain glow mesh of the lit part: red at the same brightness.
                    if (m.HasProperty(ColorId)) block.SetColor(ColorId, lit ? Reddened(m.GetColor(ColorId)) : Tinted(m.GetColor(ColorId)));
                    if (m.HasProperty(EmissionId)) block.SetColor(EmissionId, lit ? Reddened(m.GetColor(EmissionId)) : Tinted(m.GetColor(EmissionId)));
                    if (lit && m.HasProperty(TintId)) block.SetColor(TintId, Reddened(m.GetColor(TintId)));
                    r.SetPropertyBlock(block, i);
                }
            }
            ApplyLights(go);
        }

        // Under the item's light part: a transform named "Light" or carrying a Light, between t and the item root.
        private static bool InLight(Transform t, Transform root)
        {
            for (var p = t; p != null && p != root; p = p.parent)
                if (p.name == "Light" || p.GetComponent<Light>() != null) return true;
            return false;
        }

        private static readonly HashSet<string> described = new HashSet<string>();

        // The red beam: TailLightBeam x the headlight beam's strength (red carries about a third of white's brightness), TailLightReach
        // metres, TailLightShadows off by default (shadow-casting spots made the ground darker where two tail light beams crossed).
        // Live tail lights are tracked so a settings change applies at once.
        private static readonly HashSet<GameObject> live = new HashSet<GameObject>();
        internal static void BeamChanged()
        {
            live.RemoveWhere(g => g == null);
            foreach (var go in live) ApplyLights(go);
            var any = live.FirstOrDefault(g => g.activeInHierarchy);
            var body = any == null ? null : any.GetComponentInParent<Rigidbody>();
            if (body != null) LightProbe.Request(body.transform);
        }

        internal static bool HasTail(Transform car)
        {
            foreach (var fsm in car.GetComponentsInChildren<PlayMakerFSM>())
                if (fsm.FsmName == "Headlight" && IsTail(fsm.gameObject)) return true;
            return false;
        }

        private static void ApplyLights(GameObject go)
        {
            if (go.scene.IsValid() && go.activeInHierarchy) live.Add(go);
            RecolorBeams(go);
            Describe(go);
            if (!baseLight.ContainsKey(Variant(go.name))) ScanPrefabs();
            baseLight.TryGetValue(Variant(go.name), out var lights);
            foreach (var light in go.GetComponentsInChildren<Light>(true))
            {
                light.color = LightColor;
                // Absolute values from the prefab: Easy Save restores the saved light, so never scale the current one.
                if (lights != null && lights.TryGetValue(light.name, out var b))
                {
                    // Point = the lens glow (child "Light", range 0.1 m: it only lights the lens itself) - full reach, a bit stronger
                    // because red carries less brightness. Spot = the beam - same strength, shorter throw (a tail light, not a lamp).
                    if (light.type == LightType.Point) { light.intensity = b.x * 1.6f; light.range = b.y; }
                    else
                    {
                        light.intensity = b.x * Plugin.TailLightBeam.Value;
                        light.range = Mathf.Min(b.y, Plugin.TailLightReach.Value);
                        light.shadows = Plugin.TailLightShadows.Value ? LightShadows.Soft : LightShadows.None;
                        // Forward rendering lights only the N most important lights per object per pixel (QualitySettings.pixelLightCount);
                        // the rest fall back to per-vertex / spherical harmonics - on a terrain that is a coarse, wrong-looking patch.
                        light.renderMode = Plugin.TailLightPixel.Value ? LightRenderMode.ForcePixel : LightRenderMode.Auto;
                    }
                }
            }
        }

        // Scripts on the lit part that have their own colour (a beam / glow / flare component): make that colour red too.
        private static void RecolorBeams(GameObject go)
        {
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || mb is PlayMakerFSM || !InLight(mb.transform, go.transform)) continue;
                var type = mb.GetType();
                bool changed = false;
                foreach (var name in new[] { "color", "Color", "colorFlat", "m_Color" })
                {
                    var f = type.GetField(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (f != null && f.FieldType == typeof(Color)) { f.SetValue(mb, Reddened((Color)f.GetValue(mb))); changed = true; break; }
                    var p = type.GetProperty(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (p != null && p.PropertyType == typeof(Color) && p.CanRead && p.CanWrite) { p.SetValue(mb, Reddened((Color)p.GetValue(mb, null)), null); changed = true; break; }
                }
                if (!changed) continue;
                var update = type.GetMethod("UpdateAfterManualPropertyChange", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance, null, Type.EmptyTypes, null);
                try { update?.Invoke(mb, null); } catch (Exception e) { Plugin.Log.LogWarning("Tail light: " + type.Name + " update failed: " + e.Message); }
            }
        }

        // Keep the brightness, make it red (a dark multiply would leave an emissive/additive part looking off).
        private static Color Reddened(Color c)
        {
            float v = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            return new Color(v * LightColor.r, v * LightColor.g, v * LightColor.b, c.a);
        }

        // Once per model: what the lit part is made of (for the log, in case a model needs special handling).
        private static void Describe(GameObject go)
        {
            var variant = Variant(go.name);
            if (!described.Add(variant)) return;
            var sb = new System.Text.StringBuilder("Tail light" + variant + " lit part:");
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                if (!InLight(t, go.transform)) continue;
                sb.Append(" [").Append(t.name).Append(t.gameObject.activeSelf ? "" : " (off)").Append(':');
                foreach (var c in t.GetComponents<Component>())
                {
                    if (c == null || c is Transform) continue;
                    sb.Append(' ').Append(c.GetType().Name);
                    if (c is Light l) sb.Append("(").Append(l.type).Append(" i=").Append(l.intensity.ToString("0.##")).Append(" r=").Append(l.range.ToString("0.#")).Append(")");
                    if (c is Renderer r && r.sharedMaterial != null) sb.Append("(").Append(r.sharedMaterial.shader.name).Append(")");
                }
                sb.Append(']');
            }
            Plugin.Log.LogInfo(sb.ToString());
        }

        // The item's ItemName FSM writes the literal "Headlight" into the look-at label; say "Tail Light" for ours.
        // (Patched at the action instead of editing FSM data, which may be shared with every headlight.)
        internal static void LabelShown(UiTextSetText action)
        {
            var value = action.text?.Value;
            if (value == null || !value.StartsWith(Base, StringComparison.Ordinal) || !IsTail(action.Owner)) return;
            var target = action.Fsm.GetOwnerDefaultTarget(action.gameObject);
            var text = target == null ? null : target.GetComponent<UnityEngine.UI.Text>();
            if (text != null) text.text = "Tail Light" + value.Substring(Base.Length);
        }

        // ---- world loot ----
        internal static void Created(CreateObject action)
        {
            int chance = Plugin.TailLightChance.Value;
            if (chance <= 0) return;
            var source = action.gameObject?.Value;
            if (source == null || !source.name.StartsWith(Base, StringComparison.Ordinal)) return;
            var owner = action.Owner;
            bool spawner = (action.Fsm?.Name ?? "").IndexOf("Spawn", StringComparison.OrdinalIgnoreCase) >= 0
                || (owner != null && owner.name.IndexOf("Spawn", StringComparison.OrdinalIgnoreCase) >= 0);
            if (!spawner) return;
            var made = action.storeObject?.Value;
            if (made == null || made == source || !made.name.StartsWith(Base, StringComparison.Ordinal) || !HeadlightMount.IsHeadlight(made)) return;
            if (UnityEngine.Random.value * 100f >= chance) return;
            made.name = TailName(made.name);
            spawned.Add(made);
            Convert(made);
        }

        internal static bool Pending => spawned.Count > 0;

        // The spawner names its clone after CreateObject (GetName + id + SetName); keep the tail_light prefix.
        internal static void Renamed(GameObject go)
        {
            if (go == null || !spawned.Remove(go)) return;
            if (go.name.StartsWith(Base, StringComparison.Ordinal)) go.name = TailName(go.name);
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
            LightProbe.Tick();
            if (due < 0 || Time.frameCount < due || Time.unscaledTime < retryAt) return;
            if (TailLights.EnsureTemplates() || ++tries >= 4) { due = -1; return; }
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
