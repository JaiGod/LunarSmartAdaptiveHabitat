// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/05_World_FX/HunchKidFeatures.cs
//  Section : 05_World_FX
//  Folder  : Living-world effects (Sun, earthshine, storms, doors, rockets) and the friendly extras
//            (robot buddy, stars, badges).
//  Purpose : Kid-friendly extras: a robot buddy that shows the way, gold stars with Moon facts,
//            high-fives with the crew, mood faces, a garden that grows, badges and confetti. Works
//            the same with or without a headset.
//  Contains: HunchKidFeatures
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System.Collections.Generic;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;

/// Kid-friendly extras: a robot buddy that shows the way, gold stars with Moon facts, high-fives with the crew,
/// mood faces, a garden that grows, badges and confetti. Works the same with or without a headset.
public class HunchKidFeatures : MonoBehaviour
{
    public Material starMaterial;
    public GameObject confettiPrefab;
    public Transform buddy; public TextMeshPro buddySpeech;
    public Transform[] faces; public Renderer[] faceRenderers; public Transform[] mouthL, mouthM, mouthR; public Transform[] handTargets;
    public Transform[] sprouts;
    public int starsPerZone = 2;

    HabitatGame game; HabitatSim sim; XROrigin origin;
    readonly List<Transform> stars = new List<Transform>();
    readonly List<Transform> hands = new List<Transform>();
    readonly List<string> badges = new List<string>();
    int collected, totalStars, lastTends; float[] fiveReady, squash; float toastUntil, growth; string toast = "";
    InputAction five, kidKey; bool ready;
    GUIStyle sText, sBig; Texture2D white;

    static readonly string[] Facts = {
        "On the Moon you weigh one sixth of what you weigh on Earth.",
        "One day on the Moon lasts about 29 Earth days.",
        "Astronauts on the Space Station exercise about two hours every day.",
        "Moon dust is sharp like tiny bits of glass, so it has to stay outside.",
        "Plants make oxygen, and looking after them cheers people up.",
        "There is no air on the Moon, so it is silent outside.",
        "From the Moon, Earth looks almost four times wider than the Moon looks from Earth.",
        "Moon soil is called regolith. A thick layer of it blocks radiation.",
        "The Moon's south pole has ice hidden in craters that never see the Sun.",
        "A message from the Moon reaches Earth in a little over one second.",
        "Good sleep helps your brain handle stress.",
        "Talking with family helps astronauts feel less lonely.",
        "Footprints on the Moon can last for millions of years because there is no wind.",
        "Astronauts recycle their water, even the water from sweat.",
        "Slow, deep breaths can calm you down when you feel worried.",
        "Sharing a meal with friends is one of the best ways to feel happier." };
    static readonly string[] Thanks = { "High five! Thanks, Commander!", "That made my day!", "You're the best!", "I feel better already!", "Nice one! Let's keep going!", "Thanks for checking on me!" };

    void Awake() { game = GetComponent<HabitatGame>(); sim = game.sim; origin = game.origin; }

    void OnEnable()
    {
        five = new InputAction("highFive", InputActionType.Button); five.AddBinding("<Gamepad>/buttonNorth"); five.Enable();     // on keyboard, T opens the talk panel (HunchMenu) which offers the high-five
        kidKey = new InputAction("kidMode", InputActionType.Button); kidKey.AddBinding("<Keyboard>/k"); kidKey.AddBinding("<Gamepad>/select"); kidKey.Enable();
        five.performed += _ => HighFiveNearest();
        kidKey.performed += _ => { if (game.menuOpen) return; sim.kidMode = !sim.kidMode; Toast("Kid mode " + (sim.kidMode ? "ON" : "OFF"), 3f); };
    }
    void OnDisable() { foreach (var a in new[] { five, kidKey }) if (a != null) { a.Disable(); a.Dispose(); } }

    void Start()
    {
        int n = faces != null ? faces.Length : 0; fiveReady = new float[Mathf.Max(4, n)]; squash = new float[Mathf.Max(4, n)];
        Invoke(nameof(Setup), 1.2f);
    }

    void Setup()
    {
        if (!origin) origin = game.origin;
        foreach (var t in origin.GetComponentsInChildren<Transform>(true))
            if (t.name == "Left Controller" || t.name == "Right Controller" || t.name == "Left Hand" || t.name == "Right Hand") hands.Add(t);
        SpawnStars();
        ready = true;
    }

    // gold stars, placed on free floor around every zone so they are always reachable
    void SpawnStars()
    {
        var rng = new System.Random(7);
        foreach (var z in game.zones)
            for (int s = 0; s < starsPerZone; s++)
            {
                Vector3 pos = z.position + Vector3.up * 1.1f; bool found = false; RaycastHit hit;
                for (int attempt = 0; attempt < 24 && !found; attempt++)
                {
                    float ang = (float)rng.NextDouble() * 360f, dist = 3f + (float)rng.NextDouble() * 7f;
                    Vector3 q = z.position + Quaternion.Euler(0f, ang, 0f) * Vector3.forward * dist;
                    if (!Physics.Raycast(q + Vector3.up * 1.6f, Vector3.down, out hit, 2.6f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (hit.normal.y < 0.7f || Mathf.Abs(hit.point.y - z.position.y) > 1.3f) continue;
                    if (Physics.CheckSphere(hit.point + Vector3.up * 1.15f, 0.4f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    pos = hit.point + Vector3.up * 1.15f; found = true;
                }
                if (!found) pos = z.position + Quaternion.Euler(0f, 120f * s + 40f, 0f) * Vector3.forward * 2.2f + Vector3.up * 1.15f;
                var root = new GameObject("Wellness Star"); root.transform.position = pos;
                for (int k = 0; k < 2; k++)
                {
                    var c = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(c.GetComponent<Collider>());
                    c.transform.SetParent(root.transform, false); c.transform.localScale = new Vector3(0.36f, 0.36f, 0.07f); c.transform.localRotation = Quaternion.Euler(0f, 0f, 45f * k);
                    var r = c.GetComponent<Renderer>(); r.sharedMaterial = starMaterial; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                stars.Add(root.transform);
            }
        totalStars = stars.Count;
    }

    public void Toast(string text, float seconds) { toast = text; toastUntil = Time.unscaledTime + seconds; }

    void Confetti(Vector3 pos)
    {
        if (!confettiPrefab || Time.timeScale > 2f) return;
        var g = Instantiate(confettiPrefab, pos, Quaternion.identity); Destroy(g, 5f);
    }

    void Badge(string name)
    {
        if (badges.Contains(name)) return;
        badges.Add(name); Toast("New badge: " + name + "!", 5f); game.Chime(0.7f); game.Buzz(0.8f, 0.3f);
        if (origin && origin.Camera) { Vector3 fp = origin.Camera.transform.position + origin.Camera.transform.forward * 1.5f; Confetti(fp); HunchFx.Spawn("sparks explode yellow", fp + origin.Camera.transform.forward, 0.8f, 0.9f); }
    }

    [System.NonSerialized] public bool hideBuddy, hideFaces, busyHide;      // busyHide: the player is working at a repair panel, keep out of the way
    public int Collected { get { return collected; } }
    public int TotalStars { get { return totalStars; } }

    public void HighFive(int k)
    {
        if (k < 0 || k >= sim.crew.Count) return;
        if (Time.time < fiveReady[k]) return;
        fiveReady[k] = Time.time + 12f; squash[k] = 0.7f;
        sim.HighFive(k);
        Toast(sim.crew[k].name + ": \"" + Thanks[(sim.highFives + k) % Thanks.Length] + "\"", 4f);
        game.Chime(0.6f); game.Buzz(0.9f, 0.2f);
        if (game.crewAvatars != null && k < game.crewAvatars.Length && game.crewAvatars[k])
        {
            Vector3 cp = game.crewAvatars[k].position;
            Confetti(cp + Vector3.up * 2.2f); HunchFx.Spawn("love hit", cp + Vector3.up * 1.8f, 0.4f, 0.9f); HunchFx.Spawn("star aura", cp, 0.3f, 2.5f);
        }
    }

    /// Index of the closest astronaut within range of the player, or -1.
    public int NearestCrew(float range)
    {
        if (game.crewAvatars == null || !origin || !origin.Camera) return -1;
        Vector3 p = origin.Camera.transform.position; int best = -1; float bd = range;
        for (int k = 0; k < game.crewAvatars.Length; k++)
        {
            if (!game.crewAvatars[k]) continue;
            float d = Vector3.Distance(p, game.crewAvatars[k].position + Vector3.up * 1.3f);
            if (d < bd) { bd = d; best = k; }
        }
        return best;
    }

    void HighFiveNearest()
    {
        if (!ready || game.crewAvatars == null || !origin || !origin.Camera) return;
        Vector3 p = origin.Camera.transform.position; int best = -1; float bd = 5f;
        for (int k = 0; k < game.crewAvatars.Length; k++)
        {
            if (!game.crewAvatars[k]) continue;
            float d = Vector3.Distance(p, game.crewAvatars[k].position + Vector3.up * 1.3f);
            if (d < bd) { bd = d; best = k; }
        }
        if (game.menuOpen) return;
        if (best >= 0) HighFive(best); else Toast("Walk up to an astronaut first.", 3f);
    }

    void Update()
    {
        if (!ready || !origin || !origin.Camera) return;
        var cam = origin.Camera.transform; float t = Time.unscaledTime;

        // ---- stars: spin, bob, collect by walking into them
        for (int i = stars.Count - 1; i >= 0; i--)
        {
            var s = stars[i];
            s.Rotate(0f, 90f * Time.unscaledDeltaTime, 0f, Space.World);
            Vector3 d = s.position - cam.position; float dy = d.y; d.y = 0f;
            if (d.magnitude < 1.4f && dy > -2.6f && dy < 1.2f)
            {
                Confetti(s.position); HunchFx.Spawn("star hit", s.position, 0.3f, 0.9f); Destroy(s.gameObject); stars.RemoveAt(i); collected++;
                foreach (var c in sim.crew) { c.mood += 1f; c.Clamp(); }
                Toast("Star " + collected + " of " + totalStars + "!  Did you know? " + Facts[(collected - 1) % Facts.Length], 7f);
                game.Chime(0.5f); game.Buzz(0.5f, 0.15f);
            }
        }

        // ---- VR: a real high-five, touch the astronaut's raised hand with your hand or controller
        if (game.HeadsetActive && handTargets != null)
            for (int k = 0; k < handTargets.Length; k++)
            {
                if (!handTargets[k]) continue;
                foreach (var h in hands) if (h && h.gameObject.activeInHierarchy && Vector3.Distance(h.position, handTargets[k].position) < 0.3f) { HighFive(k); break; }
            }

        // ---- crew: mood faces, squash-and-stretch after a high-five, waving hand
        for (int k = 0; k < sim.crew.Count; k++)
        {
            var c = sim.crew[k]; float smile = Mathf.Clamp((c.Wellbeing - 55f) / 28f, -1f, 1f);
            if (faces != null && k < faces.Length && faces[k])
            {
                if (faces[k].gameObject.activeSelf == hideFaces) faces[k].gameObject.SetActive(!hideFaces);
                faces[k].rotation = Quaternion.LookRotation(faces[k].position - cam.position, Vector3.up);
                if (mouthL[k]) { var p = mouthL[k].localPosition; p.y = -0.075f + 0.035f * smile; mouthL[k].localPosition = p; mouthL[k].localRotation = Quaternion.Euler(0f, 0f, -28f * smile); }
                if (mouthR[k]) { var p = mouthR[k].localPosition; p.y = -0.075f + 0.035f * smile; mouthR[k].localPosition = p; mouthR[k].localRotation = Quaternion.Euler(0f, 0f, 28f * smile); }
                if (mouthM[k]) { var p = mouthM[k].localPosition; p.y = -0.075f - 0.012f * smile; mouthM[k].localPosition = p; }
                if (faceRenderers[k]) faceRenderers[k].material.SetColor("_BaseColor", smile >= 0f ? Color.Lerp(new Color(1f, 0.85f, 0.25f), new Color(0.55f, 1f, 0.35f), smile) : Color.Lerp(new Color(1f, 0.85f, 0.25f), new Color(0.55f, 0.7f, 1f), -smile));
            }
            if (game.crewAvatars != null && k < game.crewAvatars.Length && game.crewAvatars[k])
            {
                float sq = 0f;
                if (squash[k] > 0f) { squash[k] -= Time.unscaledDeltaTime; sq = Mathf.Sin((0.7f - squash[k]) / 0.7f * Mathf.PI * 3f) * 0.18f * (squash[k] / 0.7f); }
                // pop in when the astronaut arrives somewhere new
                if (lastPos == null) { lastPos = new Vector3[8]; pop = new float[8]; }
                { Vector3 now = game.crewAvatars[k].position; if ((now - lastPos[k]).sqrMagnitude > 5f * 5f) pop[k] = 0.45f; lastPos[k] = now; }      // only a real jump to a new place pops; walking never does
                float grow = 1f; if (pop[k] > 0f) { pop[k] -= Time.unscaledDeltaTime; float u01 = 1f - Mathf.Clamp01(pop[k] / 0.45f); grow = Mathf.Sin(u01 * Mathf.PI * 0.5f) * (1f + 0.25f * Mathf.Sin(u01 * Mathf.PI)); }
                float breathe = 1f + 0.012f * Mathf.Sin(t * 1.6f + k);
                game.crewAvatars[k].localScale = new Vector3(1f - sq * 0.5f, (1f + sq) * breathe, 1f - sq * 0.5f) * Mathf.Max(0.05f, grow);
            }
            if (handTargets != null && k < handTargets.Length && handTargets[k])
                handTargets[k].localScale = Vector3.one * (Time.time >= fiveReady[k] ? 0.15f + 0.03f * Mathf.Sin(t * 5f + k) : 0.06f);
        }

        // ---- the greenhouse garden grows every time the plants are tended
        if (sim.tends != lastTends) { lastTends = sim.tends; if (sim.tends > 0) Toast("The garden is growing! Tended " + sim.tends + (sim.tends == 1 ? " time." : " times."), 4f); }
        growth = Mathf.MoveTowards(growth, Mathf.Clamp01(sim.tends / 5f), Time.deltaTime * 0.25f);
        if (sprouts != null)
            for (int i = 0; i < sprouts.Length; i++)
            {
                if (!sprouts[i]) continue;
                float g = Mathf.Clamp01(growth * 1.3f - 0.04f * i);
                sprouts[i].localScale = Vector3.one * Mathf.Lerp(0.22f, 1.25f, g);
                var fruit = sprouts[i].Find("Fruit"); if (fruit && fruit.gameObject.activeSelf != (g > 0.6f)) fruit.gameObject.SetActive(g > 0.6f);
            }

        // ---- badges
        if (totalStars > 0 && collected >= 5) Badge("Star Finder");
        if (totalStars > 0 && collected >= totalStars) Badge("Super Explorer");
        if (sim.highFives >= 4) Badge("Best Friend");
        if (sim.tends >= 3) Badge("Green Thumb");
        if (sim.evaDone >= 1) Badge("Dust Buster");
        if (sim.stormsSafe >= 1) Badge("Storm Hero");
        if (sim.psychHelped >= 3) Badge("Kind Heart");
        if (sim.repairs >= 2) Badge("Fix-It Engineer");
        if (sim.nightHandled >= 1) Badge("Night Light");
        if (sim.tutorialIndex >= 8) Badge("Zone Master");

        game.extraLine = "Stars " + collected + "/" + totalStars + "   High-fives " + sim.highFives + "   Badges " + badges.Count;
        game.extraReport = "<b>Badges earned (" + badges.Count + ")</b>  " + (badges.Count > 0 ? string.Join(", ", badges) : "none yet") + "\nStars " + collected + " of " + totalStars + ", high-fives " + sim.highFives + ", garden tended " + sim.tends + " times";

        UpdateBuddy(cam, t);
    }

    // the robot buddy floats beside you and flies ahead toward wherever you are needed
    Vector3[] lastPos; float[] pop;

    float budAng, budAngVel, budDist = 1.9f, budDistVel, budY, budYVel; Vector3 budAnchor, budAnchorVel; bool budInit; int budSide = 1;

    // The buddy circles the player on a ring: it swings round you instead of cutting through your body, it never comes
    // closer than 1.2 m, and it keeps out of the way of the dashboard card (VR) and the middle of the screen (desktop).
    void UpdateBuddy(Transform cam, float t)
    {
        if (!buddy) return;
        bool hb = hideBuddy || busyHide;
        if (buddy.gameObject.activeSelf == hb) buddy.gameObject.SetActive(!hb);
        if (hb) { budInit = false; return; }
        bool vr = game.HeadsetActive; float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        Vector3 flat = Vector3.ProjectOnPlane(cam.forward, Vector3.up); if (flat.sqrMagnitude < 0.01f) flat = Vector3.forward; flat.Normalize();
        float viewYaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
        float wantAng = viewYaw + (vr ? 34f : 24f), wantDist = 1.9f, wantY = cam.position.y - (vr ? 0.12f : 0.3f);
        string say;
        if (Time.unscaledTime < toastUntil) say = toast;
        else if (sim.phase == HabPhase.Briefing) say = "Hi! I'm ARIA, your robot buddy.\nStart the mission and follow me!";
        else if (sim.phase == HabPhase.Report) say = "Great job, Commander!";
        else
        {
            int zi = game.UrgentZone();
            if (zi >= 0 && zi != game.NearZone)
            {
                Vector3 d = game.zones[zi].position - cam.position; float dist = d.magnitude; d.y = 0f;
                if (d.sqrMagnitude > 1f) { wantAng = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; wantDist = 2.2f; }
                say = "This way! " + game.zones[zi].title + "\n<size=75%>" + Mathf.RoundToInt(dist) + " m - follow me, or press " + (vr ? "X" : "G") + " to go there</size>";
            }
            else if (zi >= 0) say = "We're here! " + (vr ? "Point at the orb and pull the trigger." : "Press E.");
            else say = collected < totalStars ? "All calm. Let's find more stars\nor high-five the crew!" : "All calm. You found every star!";
        }

        // stay clear of the card / the middle of the view; remember which side so it does not flip back and forth
        float avoidYaw = viewYaw, half = 15f;
        if (vr && game.hudRoot && game.hudRoot.gameObject.activeInHierarchy && game.hudRoot.parent == null)
        {
            Vector3 c = game.hudRoot.position - cam.position; c.y = 0f;
            if (c.sqrMagnitude > 0.04f) { avoidYaw = Mathf.Atan2(c.x, c.z) * Mathf.Rad2Deg; half = game.menuOpen ? 48f : 36f; }
        }
        float rel = Mathf.DeltaAngle(avoidYaw, wantAng);
        if (Mathf.Abs(rel) > 8f) budSide = rel >= 0f ? 1 : -1;
        if (Mathf.Abs(rel) < half) wantAng = avoidYaw + budSide * half;

        if (!budInit || (budAnchor - cam.position).sqrMagnitude > 9f)      // first frame, or the player teleported
        { budInit = true; budAnchor = cam.position; budAng = wantAng; budDist = wantDist; budY = wantY; budAngVel = budDistVel = budYVel = 0f; budAnchorVel = Vector3.zero; }
        budAnchor = Vector3.SmoothDamp(budAnchor, cam.position, ref budAnchorVel, 0.12f, Mathf.Infinity, dt);
        budAng = Mathf.SmoothDampAngle(budAng, wantAng, ref budAngVel, 0.45f, 200f, dt);
        budDist = Mathf.SmoothDamp(budDist, wantDist, ref budDistVel, 0.4f, Mathf.Infinity, dt);
        budY = Mathf.SmoothDamp(budY, wantY, ref budYVel, 0.35f, Mathf.Infinity, dt);
        float rad = budAng * Mathf.Deg2Rad;
        Vector3 pos = new Vector3(budAnchor.x + Mathf.Sin(rad) * budDist, budY + 0.05f * Mathf.Sin(t * 2.2f), budAnchor.z + Mathf.Cos(rad) * budDist);
        Vector3 off = pos - cam.position; off.y = 0f; float m = off.magnitude;
        if (m < 1.2f) { off = m > 0.01f ? off / m : new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)); pos.x = cam.position.x + off.x * 1.2f; pos.z = cam.position.z + off.z * 1.2f; }
        buddy.position = pos;

        Vector3 look = cam.position - buddy.position; if (look.sqrMagnitude > 0.001f) buddy.rotation = Quaternion.Slerp(buddy.rotation, Quaternion.LookRotation(look.normalized, Vector3.up), 1f - Mathf.Exp(-6f * dt));
        if (buddySpeech)
        {
            if (vr && game.menuOpen) say = "";      // a menu is open: its text is enough
            float sc = vr ? 0.62f : 0.8f;
            // the text box is 1 unit tall and its words sit on its bottom edge: lift it so the words start above the antenna instead of across the robot's face
            if (Mathf.Abs(buddySpeech.transform.localScale.x - sc) > 0.01f) { buddySpeech.transform.localScale = Vector3.one * sc; buddySpeech.transform.localPosition = new Vector3(0f, 0.46f + 0.5f * sc, 0f); }
            if (buddySpeech.text != say) buddySpeech.text = say;
        }
    }

    // desktop: stars, badges and the latest toast
    void OnGUI()
    {
        if (game.HeadsetActive || !ready || game.hideHud || game.simpleHud || game.menuOpen || game.uiHasMouse || sim.phase == HabPhase.Briefing || sim.phase == HabPhase.Report) return;
        float u = Screen.height / 1080f;
        if (sText == null || Mathf.Abs(sText.fontSize - HunchLook.FsF(19f * u)) > 1f)
        {
            white = Texture2D.whiteTexture;
            sText = new GUIStyle(GUI.skin.label) { fontSize = HunchLook.Fs(19f * u), richText = true, wordWrap = true };
            sBig = new GUIStyle(GUI.skin.label) { fontSize = HunchLook.Fs(24f * u), richText = true, wordWrap = true, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            sText.normal.textColor = Color.white; sBig.normal.textColor = Color.white;
        }
        var r = new Rect(14f * u, Screen.height - 190f * u, 400f * u, 176f * u);
        GUI.color = new Color(0.02f, 0.04f, 0.09f, 0.8f); GUI.DrawTexture(r, white); GUI.color = Color.white;
        GUI.Label(new Rect(r.x + 14f * u, r.y + 8f * u, r.width - 28f * u, r.height - 12f * u),
            "<color=#FFD84A><b>Stars " + collected + " / " + totalStars + "</b></color>    High-fives " + sim.highFives + "\n<b>Badges " + badges.Count + "</b>  <size=" + Mathf.RoundToInt(15f * u) + ">" + (badges.Count > 0 ? string.Join(", ", badges) : "none yet") + "</size>\n<size=" + Mathf.RoundToInt(15f * u) + ">T talk / high-five   K kid mode " + (sim.kidMode ? "ON" : "OFF") + "</size>", sText);
        if (Time.unscaledTime < toastUntil)
        {
            var tr = new Rect(Screen.width * 0.5f - 470f * u, 150f * u, 940f * u, 96f * u);
            GUI.color = new Color(0.09f, 0.07f, 0.02f, 0.85f); GUI.DrawTexture(tr, white); GUI.color = Color.white;
            GUI.Label(new Rect(tr.x + 16f * u, tr.y, tr.width - 32f * u, tr.height), "<color=#FFE08A>" + toast + "</color>", sBig);
        }
    }
}
