// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/04_UI_Menus_Audio/HunchIntro.cs
//  Section : 04_UI_Menus_Audio
//  Folder  : Menus, rounded UI, story intro, Star Store, music, voices (TTS/commands) and picture
//            quality.
//  Purpose : The story introduction: "Once upon a time..." in blue, then gold words drifting away
//            over the stars. Any key, click, trigger or button skips it. It plays once each time
//            the game is started (not again after Restart).
//  Contains: HunchIntro
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// The story introduction: "Once upon a time..." in blue, then gold words drifting away over the stars.
/// Any key, click, trigger or button skips it. It plays once each time the game is started (not again after Restart).
public class HunchIntro : MonoBehaviour
{
    public static bool Active;
    static bool played;

    const string Opening = "Once upon a time,\nnot so long from now...";
    const string Story =
        "<size=170%><b>LUNAR HABITAT\nCOMMANDER</b></size>\n\n" +
        "THE FIRST THIRTY DAYS\n\n\n" +
        "Four brave and curious astronauts set out to build a home on the Moon.\n\n" +
        "You, the Commander, with Doctor Okafor, Engineer Tanaka and Scientist Rivera, did not always agree, " +
        "but they learned that every big dream needs a team.\n\n" +
        "Together they raised a shining habitat at the lunar south pole, where the Sun circles the horizon " +
        "and ice hides in craters that have never seen daylight.\n\n" +
        "For thirty days they must keep the air clean, the water flowing and the lights on - " +
        "and keep each other smiling.\n\n" +
        "ARIA, the habitat's friendly computer, watches over them day and night.\n\n" +
        "Outside, curious little visitors hop across the dust...\n\n" +
        "And now the crew has a new Commander.\n\n" +
        "<b>YOU.</b>\n\n\n" +
        "Look after your crew.\nExplore.  Build.  Make friends.\n\n" +
        "Your mission begins now.";

    HabitatGame game; Transform holder, crawl, front; TextMeshPro crawlText, frontText, hintText; Renderer shade; InputAction skip; float t0, length = 44f; bool skipped;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (played || Application.isBatchMode || System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-hunchSelfTest") >= 0) return;
        var g = FindFirstObjectByType<HabitatGame>();
        if (g && !g.GetComponent<HunchIntro>()) { Active = true; played = true; g.gameObject.AddComponent<HunchIntro>(); }
    }

    void OnEnable()
    {
        skip = new InputAction("skipIntro", InputActionType.Button);
        foreach (var b in new[] { "<Keyboard>/anyKey", "<Mouse>/leftButton", "<Gamepad>/buttonSouth", "<Gamepad>/start", "<XRController>{RightHand}/triggerPressed", "<XRController>{LeftHand}/triggerPressed",
            "<XRController>{RightHand}/primaryButton", "<XRController>{LeftHand}/primaryButton", "<XRController>{RightHand}/gripPressed", "<XRController>{LeftHand}/gripPressed" }) skip.AddBinding(b);
        skip.performed += _ => { if (Active && Time.unscaledTime - t0 > 1.2f) skipped = true; };
        skip.Enable();
    }
    void OnDisable() { if (skip != null) { skip.Disable(); skip.Dispose(); skip = null; } Active = false; }

    TextMeshPro Text(string name, Transform parent, string text, float size, Color colour, Vector2 box, TextAlignmentOptions align)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshPro>(); t.text = text; t.fontSize = size; t.color = colour; t.alignment = align; t.richText = true;
        t.rectTransform.sizeDelta = box;
        return t;
    }

    IEnumerator Start()
    {
        game = GetComponent<HabitatGame>(); t0 = Time.unscaledTime;
        yield return new WaitForSecondsRealtime(0.6f);      // let the camera settle (headset or keyboard-and-mouse)
        var cam = game && game.origin && game.origin.Camera ? game.origin.Camera.transform : (Camera.main ? Camera.main.transform : null);
        if (!cam) { Finish(); yield break; }
        bool vr = game.HeadsetActive;

        holder = new GameObject("HUNCH Intro").transform;
        if (vr)
        {
            // in the headset the words stay put in the room, in front of where you were looking
            Vector3 f = Vector3.ProjectOnPlane(cam.forward, Vector3.up); if (f.sqrMagnitude < 0.01f) f = Vector3.forward; f.Normalize();
            holder.SetPositionAndRotation(cam.position, Quaternion.LookRotation(f, Vector3.up));
        }
        else holder.SetParent(cam, false);

        // a dark veil so the words read clearly over the Moon base
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(q.GetComponent<Collider>());
        q.name = "Veil"; q.transform.SetParent(holder, false); q.transform.localPosition = new Vector3(0f, 0f, 9f); q.transform.localScale = new Vector3(60f, 40f, 1f);
        shade = q.GetComponent<Renderer>();
        // the veil uses the same see-through material as the zone rings, which is known to draw correctly in both eyes
        var ringSrc = game.zones.Count > 0 && game.zones[0].ring ? game.zones[0].ring.sharedMaterial : null;
        if (ringSrc) { shade.material = new Material(ringSrc); shade.material.color = new Color(0f, 0f, 0.02f, 0.82f); if (shade.material.HasProperty("_BaseColor")) shade.material.SetColor("_BaseColor", new Color(0f, 0f, 0.02f, 0.82f)); shade.material.renderQueue = 2980; }
        else q.SetActive(false);
        shade.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        front = new GameObject("Front").transform; front.SetParent(holder, false); front.localPosition = new Vector3(0f, 0f, 3f);
        frontText = Text("Opening", front, Opening, 3.2f, new Color(0.45f, 0.8f, 1f), new Vector2(4.4f, 2f), TextAlignmentOptions.Center);
        hintText = Text("Hint", front, vr ? "Pull the trigger or press A to skip" : "Press any key or click to skip", 1.1f, new Color(0.8f, 0.85f, 0.95f, 0.8f), new Vector2(4.4f, 0.4f), TextAlignmentOptions.Center);
        hintText.transform.localPosition = new Vector3(0f, vr ? 1.05f : 1.45f, 0f);

        crawl = new GameObject("Crawl").transform; crawl.SetParent(holder, false);
        crawl.localPosition = new Vector3(0f, vr ? -1.3f : -1.15f, vr ? 2.2f : 1.7f); crawl.localRotation = Quaternion.Euler(64f, 0f, 0f);
        crawlText = Text("Story", crawl, Story, 2.3f, new Color(1f, 0.84f, 0.3f), new Vector2(3.4f, 40f), TextAlignmentOptions.Top);
        crawlText.rectTransform.pivot = new Vector2(0.5f, 1f); crawlText.gameObject.SetActive(false);
        crawlText.ForceMeshUpdate(); float h = Mathf.Max(8f, crawlText.preferredHeight);
        foreach (var r in holder.GetComponentsInChildren<Renderer>()) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }

        // 1. the opening line
        float t = 0f;
        while (t < 4.2f && !skipped) { t += Time.unscaledDeltaTime; frontText.alpha = Mathf.Clamp01(t / 0.8f) * Mathf.Clamp01((4.2f - t) / 0.8f); yield return null; }
        frontText.gameObject.SetActive(false);

        // 2. the story drifts away into the distance
        crawlText.gameObject.SetActive(true);
        float speed = 0.5f, travel = h + 7f; length = travel / speed; t = 0f;
        while (t < length && !skipped)
        {
            t += Time.unscaledDeltaTime;
            crawlText.transform.localPosition = new Vector3(0f, -0.4f + t * speed, 0f);
            crawlText.alpha = Mathf.Clamp01(t / 1.5f) * Mathf.Clamp01((length - t) / 2.5f);
            yield return null;
        }

        // 3. fade out
        float a0 = shade && shade.gameObject.activeSelf ? shade.material.color.a : 0f; t = 0f;
        while (t < 0.6f)
        {
            t += Time.unscaledDeltaTime; float k = 1f - Mathf.Clamp01(t / 0.6f);
            if (shade && shade.gameObject.activeSelf) { var c = shade.material.color; c.a = a0 * k; shade.material.color = c; }
            crawlText.alpha = Mathf.Min(crawlText.alpha, k); hintText.alpha = 0.8f * k;
            yield return null;
        }
        Finish();
    }

    void Finish()
    {
        Active = false;
        if (holder) Destroy(holder.gameObject);
        Destroy(this);
    }

    public static void Skip() { var i = FindFirstObjectByType<HunchIntro>(); if (i) i.skipped = true; }
}
