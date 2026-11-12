// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Smart Adaptive Habitat (realistic)
//  File    : Unity3/Scripts/01_Core/RenderFix.cs
//  Section : 01_Core
//  Folder  : Start-up, shared state, menu scene, render settings and automated self-test.
//  Purpose : Neutral, realistic colour for the habitat: overrides any project-wide URP volume
//            profile (the VR template ships a teal-tinted default) with a top-priority global
//            volume that resets every colour-grading effect, and writes a
//  Contains: RenderFix, LightShadowBudget
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Hunch3
{
    /// Neutral, realistic colour for the habitat: overrides any project-wide URP volume profile (the VR template ships a
    /// teal-tinted default) with a top-priority global volume that resets every colour-grading effect, and writes a
    /// render_report.txt listing every light and every runtime-created renderer so stray objects can be tracked down.
    public static class RenderFix
    {
        public static void Apply()
        {
            var sb = new StringBuilder();
            // report what the default volume stack currently does
            try
            {
                var st = VolumeManager.instance.stack;
                var ca = st.GetComponent<ColorAdjustments>(); var wb = st.GetComponent<WhiteBalance>(); var tm = st.GetComponent<Tonemapping>();
                var cm = st.GetComponent<ChannelMixer>(); var lgg = st.GetComponent<LiftGammaGain>(); var smh = st.GetComponent<ShadowsMidtonesHighlights>(); var spl = st.GetComponent<SplitToning>();
                sb.AppendLine($"stack: colorFilter={(ca ? ca.colorFilter.value.ToString() : "-")} sat={(ca ? ca.saturation.value : 0)} exp={(ca ? ca.postExposure.value : 0)} hue={(ca ? ca.hueShift.value : 0)}");
                sb.AppendLine($"       whiteBalance temp={(wb ? wb.temperature.value : 0)} tint={(wb ? wb.tint.value : 0)} tonemap={(tm ? tm.mode.value.ToString() : "-")}");
                sb.AppendLine($"       channelMixer R={(cm ? cm.redOutRedIn.value : 0)},{(cm ? cm.redOutGreenIn.value : 0)},{(cm ? cm.redOutBlueIn.value : 0)} lgg={(lgg ? lgg.gain.value.ToString() : "-")} split={(spl ? spl.shadows.value.ToString() : "-")}");
            }
            catch (System.Exception e) { sb.AppendLine("stack read failed: " + e.Message); }

            // our own neutral global volume, highest priority
            if (GameObject.Find("NeutralGrade_Volume")) goto report;
            var go = new GameObject("NeutralGrade_Volume"); Object.DontDestroyOnLoad(go);
            var vol = go.AddComponent<Volume>(); vol.isGlobal = true; vol.priority = 1000f; vol.weight = 1f;
            var prof = ScriptableObject.CreateInstance<VolumeProfile>(); vol.sharedProfile = prof;
            var c = prof.Add<ColorAdjustments>(true); c.colorFilter.Override(Color.white); c.saturation.Override(0f); c.postExposure.Override(0f); c.hueShift.Override(0f); c.contrast.Override(5f);
            var w = prof.Add<WhiteBalance>(true); w.temperature.Override(0f); w.tint.Override(0f);
            var m = prof.Add<ChannelMixer>(true);
            m.redOutRedIn.Override(100f); m.redOutGreenIn.Override(0f); m.redOutBlueIn.Override(0f);
            m.greenOutRedIn.Override(0f); m.greenOutGreenIn.Override(100f); m.greenOutBlueIn.Override(0f);
            m.blueOutRedIn.Override(0f); m.blueOutGreenIn.Override(0f); m.blueOutBlueIn.Override(100f);
            var g = prof.Add<LiftGammaGain>(true); g.lift.Override(new Vector4(1, 1, 1, 0)); g.gamma.Override(new Vector4(1, 1, 1, 0)); g.gain.Override(new Vector4(1, 1, 1, 0));
            var s = prof.Add<ShadowsMidtonesHighlights>(true); s.shadows.Override(new Vector4(1, 1, 1, 0)); s.midtones.Override(new Vector4(1, 1, 1, 0)); s.highlights.Override(new Vector4(1, 1, 1, 0));
            var sp = prof.Add<SplitToning>(true); sp.shadows.Override(Color.grey); sp.highlights.Override(Color.grey); sp.balance.Override(0f);
            var t = prof.Add<Tonemapping>(true); t.mode.Override(TonemappingMode.Neutral);
            var b = prof.Add<Bloom>(true); b.intensity.Override(0.25f); b.threshold.Override(1.2f); b.tint.Override(Color.white);
            var vg = prof.Add<Vignette>(true); vg.intensity.Override(0f);
            var fg = prof.Add<FilmGrain>(true); fg.intensity.Override(0f);
        report:
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                var ad = cam.GetUniversalAdditionalCameraData(); if (ad) ad.renderPostProcessing = true;
                sb.AppendLine($"camera {cam.name}: clear={cam.clearFlags} bg={cam.backgroundColor} post={(ad ? ad.renderPostProcessing : false)}");
            }
            // lights summary
            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            sb.AppendLine($"lights: {lights.Length}");
            foreach (var grp in lights.GroupBy(l => $"{l.type} col={l.color} I={l.intensity:0.##} range={l.range:0.#}").OrderByDescending(q => q.Count()).Take(40))
                sb.AppendLine($"  {grp.Count(),3} × {grp.Key}  e.g. {grp.First().name}");
            // runtime-created visible objects (anything not from the imported habitat / terrain models)
            sb.AppendLine("runtime renderers:");
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                var root = r.transform.root.name;
                if (root.StartsWith("LunarHabitat_Unity3") || !r.enabled) continue;
                sb.AppendLine($"  {root} / {r.name}  at {r.bounds.center:F1} size {r.bounds.size:F2} mat={(r.sharedMaterial ? r.sharedMaterial.name : "-")}");
            }
            if (!Object.FindFirstObjectByType<LightShadowBudget>()) new GameObject("LightShadowBudget").AddComponent<LightShadowBudget>();
            try { System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "Unity3", "render_report.txt"), sb.ToString()); } catch { }
            HLog.I("Render", "fix applied\n" + sb);
        }
    }

    /// Objects only look "attached" when they cast shadows on what they touch. The habitat has 130+ interior lights, so
    /// shadows are given to the few lights nearest you (re-picked twice a second) — real contact shadows where you are,
    /// at a fixed cost. Desktop: 6 shadowed lights · VR: 3 · Quest performance mode: 0.
    public class LightShadowBudget : MonoBehaviour
    {
        Light[] lights; float next;
        void Update()
        {
            if (Time.time < next) return; next = Time.time + 0.5f;
            var pr = PlayerRig.I; if (!pr || !pr.cam) return;
            if (lights == null || Time.frameCount % 600 == 0) lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l => l.type == LightType.Point && !l.name.Contains("Mood") && !l.name.Contains("Beacon")).ToArray();
            int budget = GameState.questPerformance ? 0 : pr.isXR ? 3 : 6;
            Vector3 p = pr.cam.transform.position;
            var near = lights.Where(l => l && l.enabled && l.intensity > 0.3f).OrderBy(l => (l.transform.position - p).sqrMagnitude).Take(budget).ToList();
            foreach (var l in lights)
            {
                if (!l) continue;
                bool want = near.Contains(l);
                if (want && l.shadows == LightShadows.None) { l.shadows = LightShadows.Soft; l.shadowStrength = 0.85f; l.shadowBias = 0.03f; l.shadowNormalBias = 0.3f; }
                else if (!want && l.shadows != LightShadows.None) l.shadows = LightShadows.None;
            }
        }
    }
}
