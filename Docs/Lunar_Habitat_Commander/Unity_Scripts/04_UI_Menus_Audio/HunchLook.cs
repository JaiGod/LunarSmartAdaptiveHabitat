// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/04_UI_Menus_Audio/HunchLook.cs
//  Section : 04_UI_Menus_Audio
//  Folder  : Menus, rounded UI, story intro, Star Store, music, voices (TTS/commands) and picture
//            quality.
//  Purpose : Picture quality and fonts. - Fonts: the cleaner technical font that ships with Windows
//            10 and 11 (Bahnschrift, falling back to Segoe UI) for every
//  Contains: HunchLook
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// Picture quality and fonts.
///  - Fonts: the cleaner technical font that ships with Windows 10 and 11 (Bahnschrift, falling back to Segoe UI) for every
///    on-screen panel and for the floating labels in the world. No font file is added to the project.
///  - Quality: sharper textures at glancing angles, an extra edge-smoothing pass on desktop, high-quality bloom,
///    and filmic (ACES) tone mapping so bright sunlit metal rolls off the way a camera sees it.
public class HunchLook : MonoBehaviour
{
    public static Font Body, Title; public static bool Kid;
    /// the kid-friendly letters are wider and taller than the technical ones, so every on-screen size is a little smaller to fit the same panels
    public static float FsF(float size) { return Kid ? size * 0.86f : size; }
    public static int Fs(float size) { return Mathf.Max(1, Mathf.RoundToInt(FsF(size))); }
    public bool cinematic = true;                 // filmic tone mapping on or off (Options page)
    public string FontName { get; private set; } = "default";

    HabitatGame game; Volume vol;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var g = FindFirstObjectByType<HabitatGame>();
        if (g && !g.GetComponent<HunchLook>()) g.gameObject.AddComponent<HunchLook>();
    }

    static void LoadFonts()
    {
        if (Body) return;
        try
        {
            var have = new HashSet<string>(Font.GetOSInstalledFontNames());
            // kid-friendly letters (on unless switched off): the round hand-written style that ships with Windows
            Kid = PlayerPrefs.GetInt("hunch_kidfont", 1) == 1 && have.Contains("Comic Sans MS");
            if (Kid)
            {
                Body = Font.CreateDynamicFontFromOSFont(new[] { "Comic Sans MS", "Segoe UI", "Arial" }, 24);
                Title = Font.CreateDynamicFontFromOSFont(new[] { "Comic Sans MS", "Segoe UI", "Arial" }, 48);      // the bold face is not a family of its own: asking for it by name only fills the Console with warnings
                return;
            }
            string body = have.Contains("Bahnschrift") ? "Bahnschrift" : (have.Contains("Segoe UI") ? "Segoe UI" : null);
            if (body == null) return;
            Body = Font.CreateDynamicFontFromOSFont(new[] { body, "Segoe UI", "Arial" }, 24);
            string title = have.Contains("Bahnschrift SemiBold") ? "Bahnschrift SemiBold" : (have.Contains("Segoe UI Semibold") ? "Segoe UI Semibold" : body);
            Title = Font.CreateDynamicFontFromOSFont(new[] { title, body, "Arial" }, 48);
        }
        catch (System.Exception e) { HLog.I("UI", "system fonts not available: " + e.Message); }
    }

    IEnumerator Start()
    {
        game = GetComponent<HabitatGame>();
        LoadFonts();
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
        QualitySettings.globalTextureMipmapLimit = 0;
        QualitySettings.lodBias = Mathf.Max(QualitySettings.lodBias, 2f);

        var go = new GameObject("HUNCH Look Volume"); vol = go.AddComponent<Volume>(); vol.isGlobal = true; vol.priority = 10f;
        var p = ScriptableObject.CreateInstance<VolumeProfile>(); vol.profile = p;
        bool vr = game.HeadsetActive;
        var bloom = p.Add<Bloom>(false); bloom.highQualityFiltering.Override(true); bloom.intensity.Override(0.55f); bloom.threshold.Override(1.05f); bloom.scatter.Override(0.68f);
        var tone = p.Add<Tonemapping>(false); tone.mode.Override(TonemappingMode.ACES);
        var grade = p.Add<ColorAdjustments>(false); grade.postExposure.Override(0.5f);      // ACES is darker than the neutral curve; this keeps the interiors as bright as before
        grade.contrast.Override(12f); grade.saturation.Override(6f);
        if (!vr) { var vig = p.Add<Vignette>(false); vig.intensity.Override(0.2f); vig.smoothness.Override(0.5f); }
        Realism(vr);

        yield return new WaitForSeconds(0.6f);

        // floating labels in the world (zone names, crew tags, the VR dashboard) get the same font family
        TMP_FontAsset fa = null;
        if (Kid) { try { fa = TMP_FontAsset.CreateFontAsset("Comic Sans MS", "Regular"); if (fa) FontName = "Comic Sans MS"; } catch { fa = null; } }
        if (!fa) { try { fa = TMP_FontAsset.CreateFontAsset("Bahnschrift", "Regular"); if (fa) FontName = "Bahnschrift"; } catch { fa = null; } }
        if (!fa) { try { fa = TMP_FontAsset.CreateFontAsset("Segoe UI", "Semibold"); if (fa) FontName = "Segoe UI Semibold"; } catch { fa = null; } }
        if (!fa) { try { fa = TMP_FontAsset.CreateFontAsset("Segoe UI", "Regular"); if (fa) FontName = "Segoe UI"; } catch { fa = null; } }
        if (fa)
        {
            int n = 0;
            foreach (var t in FindObjectsByType<TextMeshPro>(FindObjectsInactive.Include, FindObjectsSortMode.None)) { t.font = fa; n++; }
            HLog.I("UI", "font " + FontName + " applied to " + n + " world labels");
        }

        // one more edge-smoothing pass on the desktop camera (VR keeps MSAA only, for speed)
        if (!game.HeadsetActive && game.origin && game.origin.Camera)
        {
            var data = game.origin.Camera.GetUniversalAdditionalCameraData();
            if (data) { data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing; data.antialiasingQuality = AntialiasingQuality.High; }
        }
    }

    void Update() { if (vol) vol.weight = cinematic ? 1f : 0f; }

    /// sharper, softer and longer shadows, HDR and 4x edge smoothing; ambient occlusion on a screen, off in the headset
    public string RealismReport = "";
    void Realism(bool vr)
    {
        var sb = new System.Text.StringBuilder();
        try
        {
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset; if (!urp) { RealismReport = "no URP asset"; return; }
            urp.supportsHDR = true; urp.msaaSampleCount = 4; urp.shadowDistance = vr ? 60f : 120f; urp.shadowCascadeCount = vr ? 2 : 4;
            var t = typeof(UniversalRenderPipelineAsset); var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            foreach (var kv in new[] { new KeyValuePair<string, object>("mainLightShadowmapResolution", vr ? 2048 : 4096), new KeyValuePair<string, object>("additionalLightsShadowmapResolution", 2048), new KeyValuePair<string, object>("supportsSoftShadows", true) })
            {
                try { var pr = t.GetProperty(kv.Key, flags); if (pr != null && pr.CanWrite) { pr.SetValue(urp, kv.Value); sb.Append(kv.Key).Append(" set; "); } else sb.Append(kv.Key).Append(" read-only; "); } catch (System.Exception e) { sb.Append(kv.Key).Append(" failed ").Append(e.GetType().Name).Append("; "); }
            }
            foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (l.type == LightType.Directional && l.shadows != LightShadows.None) { l.shadows = LightShadows.Soft; l.shadowStrength = Mathf.Max(l.shadowStrength, 0.9f); }
            var f = t.GetField("m_RendererDataList", flags); var list = f != null ? f.GetValue(urp) as ScriptableRendererData[] : null; int ao = 0;
            if (list != null) foreach (var d in list) if (d) foreach (var feat in d.rendererFeatures) if (feat && feat.GetType().Name == "ScreenSpaceAmbientOcclusion") { feat.SetActive(!vr); ao++; }
            sb.Append("ambient occlusion features ").Append(ao).Append(vr ? " (off in the headset)" : " (on)");
        }
        catch (System.Exception e) { sb.Append("realism: ").Append(e.Message); }
        RealismReport = sb.ToString(); HLog.I("UI", "look: " + RealismReport);
    }
    public string ProbeLook() { return RealismReport; }

    void OnGUI() { if (Body && GUI.skin.font != Body) GUI.skin.font = Body; }
}
