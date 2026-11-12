// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/04_UI_Menus_Audio/HunchMusic.cs
//  Section : 04_UI_Menus_Audio
//  Folder  : Menus, rounded UI, story intro, Star Store, music, voices (TTS/commands) and picture
//            quality.
//  Purpose : Looping background music with a choice of styles. Starts by itself in every scene; M
//            switches it on or off. 0 Space ambient the original recorded track
//  Contains: HunchMusic
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;

/// Looping background music with a choice of styles. Starts by itself in every scene; M switches it on or off.
///   0 Space ambient  the original recorded track
///   1 Happy EDM      made by this script: bouncy dance music at 128 beats a minute (Beat Asteroid plays to it)
///   2 Lo-fi chill    made by this script: slow, soft chords and a gentle beat
///   3 Chiptune       made by this script: old video-game bleeps
///   4 My music       plays the .mp3 / .ogg / .wav files you put in the "Music" folder next to the game
/// Styles 1 to 3 are written as maths (sine, saw and square waves), so they are original and need no audio files.
public class HunchMusic : MonoBehaviour
{
    public static readonly string[] Styles = { "Space ambient", "Happy EDM", "Lo-fi chill", "Chiptune", "My music folder", "Mellow bounce", "Synthwave", "Funky beat" };
    public float volume = 0.35f;
    public int style;
    public string Message = "";
    AudioSource src; float target; HabitatGame game; AudioClip spaceClip; readonly AudioClip[] made = new AudioClip[8]; bool folderMode; int folderIndex; Coroutine folderCo;
    public bool On { get { return target > 0f; } set { target = value ? volume : 0f; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (FindFirstObjectByType<HunchMusic>()) return;
        var clip = Resources.Load<AudioClip>("HUNCH_Music_OutThere");
        if (!clip) HLog.W("UI", "recorded music clip not found; the made-up styles still work");
        var go = new GameObject("HUNCH Music");
        DontDestroyOnLoad(go);
        var m = go.AddComponent<HunchMusic>();
        m.spaceClip = clip;
        m.src = go.AddComponent<AudioSource>();
        m.src.clip = clip; m.src.loop = true; m.src.spatialBlend = 0f; m.src.playOnAwake = false; m.src.volume = 0f; m.src.priority = 0; m.src.bypassListenerEffects = true;   // music is not part of the world, so the "no air outside" filter leaves it alone
        m.target = m.volume;
        if (clip) m.src.Play();
        if (!FindFirstObjectByType<AudioListener>() && Camera.main) Camera.main.gameObject.AddComponent<AudioListener>();
        int saved = PlayerPrefs.GetInt("hunch_music_style", 0);
        if (saved != 0 || !clip) m.SetStyle(saved == 0 ? 1 : saved);
    }

    public void NextStyle() { SetStyle((style + 1) % Styles.Length); }

    public void SetStyle(int s)
    {
        style = Mathf.Clamp(s, 0, Styles.Length - 1); PlayerPrefs.SetInt("hunch_music_style", style); Message = "";
        if (folderCo != null) { StopCoroutine(folderCo); folderCo = null; }
        folderMode = false;
        if (style == 4) { folderCo = StartCoroutine(PlayFolder()); return; }
        AudioClip c = style == 0 ? spaceClip : (made[style] ? made[style] : (made[style] = Synth(style)));
        if (!c) { c = made[1] ? made[1] : (made[1] = Synth(1)); }
        if (src.clip != c || !src.isPlaying) { src.clip = c; src.loop = true; src.time = 0f; src.Play(); }
    }
    /// starts the current track again from its first beat
    public void Restart() { if (src && src.clip) { src.time = 0f; src.Play(); } }

    IEnumerator PlayFolder()
    {
        folderMode = true;
        string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Music");
        var files = new List<string>();
        try
        {
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            foreach (var f in Directory.GetFiles(dir)) { string e = Path.GetExtension(f).ToLowerInvariant(); if (e == ".mp3" || e == ".ogg" || e == ".wav") files.Add(f); }
        }
        catch { }
        if (files.Count == 0)
        {
            Message = "No songs yet. Put .mp3, .ogg or .wav files in: " + dir;
            if (!src.isPlaying && src.clip) src.Play();
            yield break;
        }
        while (folderMode)
        {
            string file = files[folderIndex % files.Count]; folderIndex++;
            string ext = Path.GetExtension(file).ToLowerInvariant();
            var type = ext == ".mp3" ? AudioType.MPEG : (ext == ".ogg" ? AudioType.OGGVORBIS : AudioType.WAV);
            using (var req = UnityWebRequestMultimedia.GetAudioClip("file:///" + file.Replace("\\", "/"), type))
            {
                yield return req.SendWebRequest();
                AudioClip c = null;
                if (req.result == UnityWebRequest.Result.Success) { try { c = DownloadHandlerAudioClip.GetContent(req); } catch { c = null; } }
                if (c && c.length > 0.5f)
                {
                    c.name = Path.GetFileNameWithoutExtension(file); Message = "Playing: " + c.name;
                    src.clip = c; src.loop = false; src.time = 0f; src.Play();
                    float end = Time.realtimeSinceStartup + c.length + 0.5f;
                    while (folderMode && Time.realtimeSinceStartup < end) yield return null;
                }
                else { Message = "Could not play " + Path.GetFileName(file); yield return new WaitForSecondsRealtime(1f); }
            }
        }
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (!game) game = FindFirstObjectByType<HabitatGame>();
        if (kb != null && kb.mKey.wasPressedThisFrame && !(game && game.menuOpen)) target = target > 0f ? 0f : volume;
        if (src) src.volume = Mathf.MoveTowards(src.volume, target, Time.unscaledDeltaTime * 0.15f); // gentle fade
    }

    // ------------------------------------------------------------------ a tiny synthesiser
    static float Hz(int midi) { return 440f * Mathf.Pow(2f, (midi - 69) / 12f); }
    static float Saw(float phase) { return 2f * (phase - Mathf.Floor(phase)) - 1f; }
    static float Sq(float phase, float duty) { return phase - Mathf.Floor(phase) < duty ? 1f : -1f; }
    static float Tri(float phase) { float p = phase - Mathf.Floor(phase); return 4f * Mathf.Abs(p - 0.5f) - 1f; }

    static AudioClip Synth(int style)
    {
        const int sr = 32000;
        float bpm = style == 1 || style == 7 ? 128f : (style == 2 ? 78f : (style == 5 ? 100f : (style == 6 ? 108f : 150f))), beat = 60f / bpm; int beats = 32, n = Mathf.RoundToInt(beats * beat * sr);
        var d = new float[n]; var rnd = new System.Random(style * 31 + 7);
        int[][] chords = style == 2
            ? new[] { new[] { 50, 53, 57, 60 }, new[] { 43, 47, 50, 53 }, new[] { 48, 52, 55, 59 }, new[] { 45, 48, 52, 55 } }      // Dm7 G7 Cmaj7 Am7
            : (style == 5 || style == 6
            ? new[] { new[] { 45, 48, 52, 57 }, new[] { 41, 45, 48, 53 }, new[] { 48, 52, 55, 60 }, new[] { 43, 47, 50, 55 } }      // Am F C G
            : (style == 7
            ? new[] { new[] { 40, 47, 50, 55 }, new[] { 45, 49, 52, 55 }, new[] { 40, 47, 50, 55 }, new[] { 47, 51, 54, 57 } }      // Em7 A7 Em7 B7
            : new[] { new[] { 48, 52, 55, 60 }, new[] { 43, 47, 50, 55 }, new[] { 45, 48, 52, 57 }, new[] { 41, 45, 48, 53 } }));   // C G Am F
        int[] mel = { 12, 16, 19, 16, 14, 12, 7, 9 };
        int[] bassPat = { 0, -99, 0, -99, 12, -99, 0, 10, -99, 0, -99, 7, 10, -99, 12, -99 };
        int[] funkLead = { 0, 3, 5, -99, 7, 5, 3, -99, 0, -99, 10, 7, 5, 3, 0, -99 };
        int[] lead = { 0, 4, 7, 4, 12, 7, 4, 7, 0, 4, 7, 12, 14, 12, 7, 4 };
        float lp = 0f, lp2 = 0f, hatLp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr, bt = t / beat; int b = Mathf.FloorToInt(bt); float tb = (bt - b) * beat;       // tb: seconds since this beat began
            int bar = b / 4; var ch = chords[bar % 4]; float eighth = beat * 0.5f, t8 = tb % eighth, sixteenth = beat * 0.25f, t16 = tb % sixteenth;
            float noise = (float)rnd.NextDouble() * 2f - 1f, s = 0f;
            if (style == 1)
            {
                float side = Mathf.Min(1f, 0.25f + tb / (beat * 0.55f));                                             // the pads duck under each kick
                float pad = 0f; for (int k = 0; k < 4; k++) { float f = Hz(ch[k] + 12); pad += Saw(t * f * 1.004f) + Saw(t * f * 0.996f); }
                lp += (pad * 0.045f - lp) * 0.22f; s += lp * side;
                s += Mathf.Sin(2f * Mathf.PI * Hz(ch[0] - 12) * t) * 0.22f * side;
                float kf = 48f + 110f * Mathf.Exp(-tb * 32f); s += Mathf.Sin(2f * Mathf.PI * kf * tb) * Mathf.Exp(-tb * 8f) * 0.55f;
                if (b % 2 == 1) s += noise * Mathf.Exp(-tb * 26f) * 0.22f;                                             // clap on 2 and 4
                hatLp += (noise - hatLp) * 0.5f; if (tb >= eighth) s += (noise - hatLp) * Mathf.Exp(-t8 * 70f) * 0.12f;  // off-beat hat
                int step = Mathf.FloorToInt(bt * 2f) % lead.Length; float lf = Hz(72 + lead[step] + (ch[0] - 48 == 0 ? 0 : 0));
                if (bar >= 2 || true) s += (Sq(t * lf, 0.5f) * 0.5f + Tri(t * lf)) * Mathf.Exp(-t8 * 9f) * 0.085f;
            }
            else if (style == 2)
            {
                int half = Mathf.FloorToInt(bt / 2f); float th = (bt - half * 2f) * beat;                                // re-struck every two beats
                float keys = 0f; for (int k = 0; k < 4; k++) { float f = Hz(ch[k] + 12); keys += Mathf.Sin(2f * Mathf.PI * f * t) + 0.3f * Tri(t * f * 2f); }
                s += keys * 0.07f * Mathf.Exp(-th * 1.3f) * (0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 5f * t));
                s += Mathf.Sin(2f * Mathf.PI * Hz(ch[0] - 12) * t) * 0.2f * Mathf.Exp(-th * 0.9f);
                if (b % 2 == 0) { float kf = 45f + 70f * Mathf.Exp(-tb * 30f); s += Mathf.Sin(2f * Mathf.PI * kf * tb) * Mathf.Exp(-tb * 9f) * 0.4f; }
                else { lp2 += (noise - lp2) * 0.25f; s += lp2 * Mathf.Exp(-tb * 16f) * 0.3f; }
                float swing = tb >= eighth * 1.25f ? tb - eighth * 1.25f : (tb < eighth ? tb : 9f); s += noise * Mathf.Exp(-swing * 80f) * 0.035f;
                if (rnd.Next(900) == 0) s += 0.25f * noise;                                                            // record crackle
                s += noise * 0.006f;
                lp += (s - lp) * 0.35f; s = lp;
            }
            else if (style == 5)
            {
                // Mellow bounce: bright wobbling chords, a deep sub bass, a bouncy half-time beat and a singing lead
                float wob = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * (tb / eighth));
                float pad = 0f; for (int k = 0; k < 4; k++) { float f = Hz(ch[k] + 12); pad += Saw(t * f * 1.006f) + Saw(t * f * 0.994f) + Saw(t * f * 2f) * 0.4f; }
                lp += (pad * 0.04f - lp) * 0.3f; s += lp * wob * Mathf.Min(1f, 0.3f + tb / (beat * 0.5f));
                s += Mathf.Sin(2f * Mathf.PI * Hz(ch[0] - 12) * t) * 0.3f * Mathf.Exp(-tb * 1.2f);
                if (b % 4 == 0 || b % 4 == 2) { float kf = 46f + 120f * Mathf.Exp(-tb * 30f); s += Mathf.Sin(2f * Mathf.PI * kf * tb) * Mathf.Exp(-tb * 7f) * 0.6f; }
                if (b % 4 == 1 || b % 4 == 3) { lp2 += (noise - lp2) * 0.6f; s += (noise * 0.6f + lp2 * 0.4f) * Mathf.Exp(-tb * 18f) * 0.3f; }
                hatLp += (noise - hatLp) * 0.5f; s += (noise - hatLp) * Mathf.Exp(-t16 * 90f) * (tb >= eighth ? 0.1f : 0.05f);
                int step5 = Mathf.FloorToInt(bt * 2f) % lead.Length; float lf5 = Hz(69 + lead[step5]) * (1f + 0.006f * Mathf.Sin(2f * Mathf.PI * 6f * t));
                s += (Mathf.Sin(2f * Mathf.PI * lf5 * t) + 0.5f * Tri(t * lf5 * 2f)) * Mathf.Exp(-t8 * 5f) * 0.1f;
            }
            else if (style == 6)
            {
                // Synthwave: a rolling bass, wide slow chords, a big snare and a long square lead
                int st16 = Mathf.FloorToInt(bt * 4f); float bf = Hz(ch[0] - 12 + (st16 % 2 == 1 ? 12 : 0));
                lp += (Saw(t * bf) * 0.5f - lp) * 0.12f; s += lp * (0.5f + 0.5f * Mathf.Exp(-t16 * 14f)) * 0.9f;
                float pad = 0f; for (int k = 1; k < 4; k++) { float f = Hz(ch[k] + 12); pad += Saw(t * f * 1.003f) + Saw(t * f * 0.997f); }
                lp2 += (pad * 0.035f - lp2) * 0.08f; s += lp2;
                float kf = 50f + 100f * Mathf.Exp(-tb * 30f); s += Mathf.Sin(2f * Mathf.PI * kf * tb) * Mathf.Exp(-tb * 9f) * 0.5f;
                if (b % 2 == 1) s += noise * Mathf.Exp(-tb * 7f) * 0.2f;
                hatLp += (noise - hatLp) * 0.5f; s += (noise - hatLp) * Mathf.Exp(-t8 * 60f) * 0.06f;
                int ms = Mathf.FloorToInt(bt / 2f) % mel.Length; float mf = Hz(60 + mel[ms]) * (1f + 0.004f * Mathf.Sin(2f * Mathf.PI * 5f * t)), mt = (bt % 2f) * beat;
                s += Sq(t * mf, 0.35f) * 0.07f * Mathf.Min(1f, mt * 20f) * Mathf.Exp(-mt * 0.9f);
            }
            else if (style == 7)
            {
                // Funky beat (128 bpm, in time with Beat Asteroid): a slap bass line, chord stabs between the beats and a beeping call-and-answer lead
                int st16 = Mathf.FloorToInt(bt * 4f) % 16; int bn = bassPat[st16];
                if (bn > -50) { float bf = Hz(ch[0] + bn), env = Mathf.Exp(-t16 * 10f); lp += ((Saw(t * bf) * 0.6f + Sq(t * bf, 0.3f) * 0.4f) - lp) * (0.15f + 0.5f * env); s += lp * env * 0.5f; }
                if (st16 % 4 == 2 || st16 == 7 || st16 == 15) { float stab = 0f; for (int k = 1; k < 4; k++) stab += Sq(t * Hz(ch[k] + 24), 0.25f); s += stab * 0.035f * Mathf.Exp(-t16 * 22f); }
                if (st16 == 0 || st16 == 6 || st16 == 8 || st16 == 11) { float kf = 50f + 110f * Mathf.Exp(-t16 * 34f); s += Mathf.Sin(2f * Mathf.PI * kf * t16) * Mathf.Exp(-t16 * 9f) * 0.55f; }
                if (st16 == 4 || st16 == 12) s += noise * Mathf.Exp(-t16 * 16f) * 0.3f;
                hatLp += (noise - hatLp) * 0.5f; s += (noise - hatLp) * Mathf.Exp(-t16 * 80f) * (st16 % 2 == 0 ? 0.05f : 0.1f);
                int ls = Mathf.FloorToInt(bt * 2f) % funkLead.Length; int ln = funkLead[ls];
                if (ln > -50 && bar % 4 >= 2) { float lf7 = Hz(76 + ln); s += (Sq(t * lf7, 0.5f) * 0.6f + Tri(t * lf7)) * Mathf.Exp(-t8 * 7f) * 0.08f; }
            }
            else
            {
                int step = Mathf.FloorToInt(bt * 4f); int note = ch[step % 4] + 24 + (step % 8 >= 4 ? 12 : 0);
                s += Sq(t * Hz(note), 0.25f) * 0.11f * (0.6f + 0.4f * Mathf.Exp(-t16 * 20f));
                int mstep = Mathf.FloorToInt(bt * 2f) % lead.Length; s += Sq(t * Hz(72 + lead[mstep]), 0.5f) * 0.07f * Mathf.Exp(-t8 * 6f);
                s += Tri(t * Hz(ch[0] - 12 + (Mathf.FloorToInt(bt * 2f) % 2 == 1 ? 12 : 0))) * 0.2f;
                float kf = 60f + 160f * Mathf.Exp(-tb * 40f); s += Sq(kf * tb, 0.5f) * Mathf.Exp(-tb * 14f) * 0.2f;
                if (b % 2 == 1) s += noise * Mathf.Exp(-tb * 30f) * 0.16f;
                s += noise * Mathf.Exp(-t8 * 90f) * 0.03f;
            }
            d[i] = s;
        }
        float peak = 0.001f; for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(d[i]));
        float gain = 0.8f / peak; for (int i = 0; i < n; i++) d[i] *= gain;
        var clip = AudioClip.Create("HUNCH " + Styles[style], n, 1, sr, false); clip.SetData(d, 0);
        return clip;
    }
}
