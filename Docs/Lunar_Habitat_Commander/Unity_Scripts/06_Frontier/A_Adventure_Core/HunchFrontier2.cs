// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/06_Frontier/A_Adventure_Core/HunchFrontier2.cs
//  Section : 06_Frontier/A_Adventure_Core
//  Folder  : Moon Frontier adventure layer: mining, aliens, tools, easy mode, survival + repair
//            mini-games, Autopilot, the 3D models from the Drive collection.
//  Purpose : Second half of Moon Frontier: the things that make it easy and friendly to play. -
//            Easy mode: a "smart hand" (the trigger zaps an alien, drills a rock or uses whatever
//            you aim at, without choosing a tool),
//  Contains: HunchFrontier, QuickBtn, Ach
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// Second half of Moon Frontier: the things that make it easy and friendly to play.
///  - Easy mode: a "smart hand" (the trigger zaps an alien, drills a rock or uses whatever you aim at, without choosing a tool),
///    and zone jobs that do themselves when you stand in the ring
///  - VR quick bar: a row of big buttons under the dashboard card - point and pull, nothing to remember
///  - Tool designs: five hand-built models with recoil, glowing rings and a spinning drill
///  - Accomplishments: a "YOU DID IT!" banner for the things the player achieves
///  - Kid-friendly sky: colourful nebula clouds, twinkling stars and a soft fill light so nothing is pitch black
///  - Travel and Accomplishments pages in the menu, and a frame recorder used for test play-throughs
public partial class HunchFrontier
{
    public bool smart = true, kidSky = true;

    // ------------------------------------------------------------------ tool designs
    Transform designRoot, drillBit, plasmaCore, builderCube; readonly Transform[] designs = new Transform[6]; readonly float[] muzzleZ = { 0.3f, 0.27f, 0.235f, 0.25f, 0.34f, 0.21f };
    readonly List<Transform> rings = new List<Transform>(); readonly List<Vector3> ringBase = new List<Vector3>(); int shownDesign = -2; float recoil;

    Transform Part(PrimitiveType type, Transform parent, float x, float y, float z, float sx, float sy, float sz, Material m, float rx = 0f, float ry = 0f, float rz = 0f)
    {
        var t = Prim(type, parent, new Vector3(x, y, z), new Vector3(sx, sy, sz), m).transform; t.localRotation = Quaternion.Euler(rx, ry, rz); return t;
    }
    Transform Ring(Transform parent, float z, float d, Material m, float y = 0f)
    {
        var t = Part(PrimitiveType.Cylinder, parent, 0f, y, z, d, 0.005f, d, m, 90f); rings.Add(t); ringBase.Add(t.localScale); return t;
    }
    Transform Design(int i, string name)
    {
        var t = new GameObject(name).transform; t.SetParent(designRoot, false); designs[i] = t; return t;
    }

    void BuildToolDesigns()
    {
        foreach (var r in blockGun) if (r) r.enabled = false;      // the old block gun is replaced by the designs below
        designRoot = new GameObject("Designs").transform; designRoot.SetParent(toolModel, false);
        var silver = Lit(new Color(0.8f, 0.82f, 0.86f)); var gold = Lit(new Color(0.95f, 0.74f, 0.22f)); var dark = Lit(new Color(0.11f, 0.12f, 0.15f)); var white = Lit(new Color(0.93f, 0.94f, 0.96f));
        var gGreen = Glow(new Color(0.6f, 2.8f, 0.5f)); var gCyan = Glow(new Color(0.5f, 1.9f, 2.8f)); var gPink = Glow(new Color(2.7f, 0.6f, 2.5f)); var gOrange = Glow(new Color(2.9f, 1.4f, 0.3f)); var gWhite = Glow(new Color(2.2f, 2.2f, 2.4f));

        // ---- Zapper: a slim ray pistol with three energy rings
        var z = Design(Zapper, "Zapper");
        Part(PrimitiveType.Cube, z, 0f, -0.075f, -0.035f, 0.04f, 0.11f, 0.05f, dark, 15f);
        Part(PrimitiveType.Capsule, z, 0f, 0f, 0.02f, 0.07f, 0.11f, 0.07f, silver, 90f);
        Part(PrimitiveType.Cube, z, 0f, 0.045f, 0f, 0.012f, 0.03f, 0.15f, gold);
        Part(PrimitiveType.Cube, z, 0f, -0.032f, 0.045f, 0.012f, 0.028f, 0.012f, gold);
        Part(PrimitiveType.Cylinder, z, 0f, 0f, 0.18f, 0.03f, 0.075f, 0.03f, dark, 90f);
        for (int k = 0; k < 3; k++) Ring(z, 0.15f + 0.04f * k, 0.066f - 0.012f * k, gGreen);
        Part(PrimitiveType.Sphere, z, 0.037f, 0f, -0.02f, 0.03f, 0.03f, 0.03f, gGreen); Part(PrimitiveType.Sphere, z, -0.037f, 0f, -0.02f, 0.03f, 0.03f, 0.03f, gGreen);

        // ---- Freeze ray: a frosty tank on top and a stepped nozzle
        var f = Design(Freeze, "Freeze ray");
        Part(PrimitiveType.Cube, f, 0f, -0.075f, -0.035f, 0.04f, 0.11f, 0.05f, dark, 15f);
        Part(PrimitiveType.Cube, f, 0f, 0f, 0.02f, 0.06f, 0.065f, 0.2f, Lit(new Color(0.78f, 0.9f, 1f)));
        Part(PrimitiveType.Cube, f, 0f, 0.034f, 0.04f, 0.062f, 0.004f, 0.12f, gold);
        Part(PrimitiveType.Sphere, f, 0f, 0.07f, -0.03f, 0.05f, 0.05f, 0.05f, gCyan);
        Part(PrimitiveType.Sphere, f, 0f, 0.07f, -0.03f, 0.088f, 0.088f, 0.088f, Clear(new Color(0.5f, 0.95f, 1f, 0.35f)));
        Part(PrimitiveType.Cylinder, f, 0f, 0f, 0.13f, 0.078f, 0.012f, 0.078f, silver, 90f);
        Part(PrimitiveType.Cylinder, f, 0f, 0f, 0.155f, 0.06f, 0.012f, 0.06f, silver, 90f);
        Part(PrimitiveType.Cylinder, f, 0f, 0f, 0.18f, 0.044f, 0.012f, 0.044f, silver, 90f);
        Ring(f, 0.2f, 0.052f, gCyan);

        // ---- Plasma launcher: a fat barrel with gold bands and a glowing core
        var p = Design(Plasma, "Plasma launcher");
        Part(PrimitiveType.Cube, p, 0f, -0.08f, -0.04f, 0.04f, 0.11f, 0.05f, dark, 15f);
        Part(PrimitiveType.Cube, p, 0f, -0.07f, 0.1f, 0.035f, 0.08f, 0.04f, dark);
        Part(PrimitiveType.Cylinder, p, 0f, 0.01f, 0.04f, 0.1f, 0.14f, 0.1f, dark, 90f);
        foreach (float bz in new[] { -0.06f, 0.04f, 0.14f }) Part(PrimitiveType.Cylinder, p, 0f, 0.01f, bz, 0.107f, 0.008f, 0.107f, gold, 90f);
        Part(PrimitiveType.Cylinder, p, 0f, 0.01f, 0.185f, 0.122f, 0.01f, 0.122f, silver, 90f);
        Part(PrimitiveType.Sphere, p, 0f, 0.01f, -0.1f, 0.096f, 0.096f, 0.06f, silver);
        Part(PrimitiveType.Cube, p, 0f, 0.072f, 0.02f, 0.015f, 0.02f, 0.1f, gold);
        plasmaCore = Part(PrimitiveType.Sphere, p, 0f, 0.01f, 0.19f, 0.085f, 0.085f, 0.085f, gPink);

        // ---- Mining drill: a chunky orange body and a bit that spins
        var d = Design(Drill, "Mining drill");
        Part(PrimitiveType.Cube, d, 0f, -0.075f, -0.035f, 0.04f, 0.11f, 0.05f, dark, 15f);
        Part(PrimitiveType.Cube, d, 0f, 0f, 0f, 0.07f, 0.075f, 0.16f, Lit(new Color(0.96f, 0.6f, 0.1f)));
        Part(PrimitiveType.Cylinder, d, 0f, 0f, 0.1f, 0.086f, 0.02f, 0.086f, dark, 90f);
        Part(PrimitiveType.Cylinder, d, 0.058f, 0f, 0f, 0.022f, 0.045f, 0.022f, dark, 0f, 0f, 90f);
        Part(PrimitiveType.Sphere, d, 0f, 0.045f, -0.04f, 0.025f, 0.025f, 0.025f, gOrange);
        drillBit = new GameObject("Bit").transform; drillBit.SetParent(d, false); drillBit.localPosition = new Vector3(0f, 0f, 0.13f);
        float[] dia = { 0.062f, 0.048f, 0.034f, 0.02f, 0.009f };
        for (int k = 0; k < dia.Length; k++) Part(PrimitiveType.Cylinder, drillBit, 0f, 0f, 0.02f + 0.04f * k, dia[k], 0.02f, dia[k], silver, 90f);
        Part(PrimitiveType.Cube, drillBit, 0f, 0f, 0.04f, 0.076f, 0.01f, 0.03f, gold); Part(PrimitiveType.Cube, drillBit, 0f, 0f, 0.1f, 0.01f, 0.05f, 0.025f, gold);

        // ---- Block builder: two prongs holding a little turning block
        var b = Design(Builder, "Block builder");
        Part(PrimitiveType.Cube, b, 0f, -0.075f, -0.035f, 0.04f, 0.11f, 0.05f, dark, 15f);
        Part(PrimitiveType.Cube, b, 0f, 0f, 0f, 0.06f, 0.06f, 0.16f, white);
        Part(PrimitiveType.Cube, b, 0f, 0.033f, 0f, 0.062f, 0.004f, 0.1f, gold);
        Part(PrimitiveType.Cube, b, 0.03f, 0f, 0.13f, 0.012f, 0.012f, 0.1f, silver); Part(PrimitiveType.Cube, b, -0.03f, 0f, 0.13f, 0.012f, 0.012f, 0.1f, silver);
        Part(PrimitiveType.Sphere, b, 0f, 0.045f, -0.05f, 0.022f, 0.022f, 0.022f, gWhite);
        builderCube = Part(PrimitiveType.Cube, b, 0f, 0f, 0.16f, 0.036f, 0.036f, 0.036f, Lit(new Color(0.82f, 0.8f, 0.76f)));

        foreach (var r in designRoot.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        for (int i = 1; i < designs.Length; i++) if (designs[i]) designs[i].gameObject.SetActive(false);
    }

    /// The tool that is really in the hand: the chosen one, or the one the smart hand picked for what you are aiming at.
    int EffTool { get { return tool != Hands ? tool : smartTool; } }
    string ToolLabel() { return tool == Hands && smart ? "Smart hand" : ToolNames[tool]; }

    void UpdateToolModel()
    {
        bool vr = game.HeadsetActive; Transform parent = vr ? (rightCtrl ? rightCtrl : cam) : cam; int eff = EffTool;
        bool show = eff != Hands && Playing && !game.menuOpen && !thirdPerson;
        if (toolModel.gameObject.activeSelf != show) toolModel.gameObject.SetActive(show);
        if (!show) return;
        if (toolModel.parent != parent)
        {
            toolModel.SetParent(parent, false);
            toolModel.localPosition = vr && rightCtrl ? new Vector3(0f, -0.01f, 0.07f) : new Vector3(0.21f, -0.17f, 0.46f);
            toolModel.localRotation = vr && rightCtrl ? Quaternion.identity : Quaternion.Euler(-2f, -7f, 0f); toolModel.localScale = Vector3.one * (vr ? 1f : 0.72f);
        }
        if (shownTool != eff) { shownTool = eff; tipR.sharedMaterial = tipMats[Mathf.Clamp(eff, 0, tipMats.Length - 1)]; }
        int gun = gunModels.Count > 0 && !vr && eff == Plasma ? 0 : -1, design = gun >= 0 ? -1 : eff;      // the imported Alien Blaster is the plasma launcher on a monitor
        if (gun != shownGun || design != shownDesign)
        {
            shownGun = gun; shownDesign = design;
            for (int i = 0; i < gunModels.Count; i++) gunModels[i].SetActive(i == gun);
            for (int i = 1; i < designs.Length; i++) if (designs[i]) designs[i].gameObject.SetActive(i == design);
            tipT.localPosition = new Vector3(0f, eff == Plasma && gun < 0 ? 0.01f : 0.003f, gun >= 0 ? 0.33f : muzzleZ[Mathf.Clamp(eff, 0, muzzleZ.Length - 1)]);
            tipT.localScale = Vector3.one * (eff == Zapper || eff == Freeze ? 0.036f : 0.02f);
        }
        ExtraGunShow(eff);
        float dt = Time.deltaTime; recoil = Mathf.MoveTowards(recoil, 0f, dt * 5f);
        designRoot.localPosition = new Vector3(0f, 0f, -0.035f * recoil); designRoot.localRotation = Quaternion.Euler(-7f * recoil, 0f, 0f);
        float pulse = 1f + 0.12f * Mathf.Sin(Time.time * 9f) + 0.5f * recoil;
        for (int i = 0; i < rings.Count; i++) rings[i].localScale = new Vector3(ringBase[i].x * pulse, ringBase[i].y, ringBase[i].z * pulse);
        if (plasmaCore) plasmaCore.localScale = Vector3.one * 0.085f * (ballLife > 0f ? 0.45f : 1f + 0.1f * Mathf.Sin(Time.time * 6f));
        if (drillBit) drillBit.Rotate(0f, 0f, (drillSrc && drillSrc.volume > 0.03f ? 1500f : 50f) * dt, Space.Self);
        if (builderCube) builderCube.Rotate(40f * dt, 70f * dt, 0f, Space.Self);
    }

    // ------------------------------------------------------------------ easy mode: the smart hand and zone jobs that do themselves
    int smartTool; float smartUntil, dwell, autoCd;

    void UpdateEasy(float dt)
    {
        // which tool would help with what the player is aiming at?
        int want = 0;
        if (smart && tool == Hands && Playing && !game.menuOpen && heldRock == null && focus == null)
        {
            RaycastHit h;
            if (Cast(AimRay(), out h, mapNow == 8 ? 700f : 70f))
            {
                var tg = h.collider.GetComponentInParent<HunchTarget>();
                if (tg && tg.kind == 1 && tg.index < aliens.Count && aliens[tg.index].alive && !aliens[tg.index].friend) want = Zapper;
                else if (tg && (tg.kind == 32 || tg.kind == 36)) want = Zapper;
                else if (tg && (tg.kind == 2 || tg.kind == 3 || tg.kind == 31 || tg.kind == 34) && h.distance < 7.5f) want = Drill;
            }
        }
        if (want != 0) { smartTool = want; smartUntil = Time.time + 0.7f; }
        else if (Time.time > smartUntil || tool != Hands || focus != null || heldRock != null) smartTool = 0;

        // stand in the ring of the zone that needs you and the job is done for you
        autoCd -= dt;
        if (!smart || !Playing || game.menuOpen || game.Paused) { dwell = 0f; return; }
        int nz = game.NearZone;
        if (nz >= 0 && nz == game.UrgentZone() && autoCd <= 0f)
        {
            dwell += dt;
            if (dwell > 1.2f) { dwell = 0f; autoCd = 2.5f; game.DoInteract(); Note("Easy mode did the job at " + game.zones[nz].title + " for you."); }
        }
        else dwell = 0f;
    }

    void SmartUse(bool held, float dt)
    {
        if (smartTool == Zapper) { if (held && cool <= 0f) { cool = 0.22f; FireBeam(AimRay(), false); smartUntil = Time.time + 0.7f; } }
        else if (smartTool == Drill) { if (held) { DrillAt(AimRay(), dt); smartUntil = Time.time + 0.7f; } else { digT = 0f; blockT = 0f; } }
    }
    string SmartPrompt(bool vr)
    {
        if (smartTool == Zapper) return (vr ? "Right trigger: " : "Click: ") + "zap the alien home";
        if (smartTool == Drill) return (vr ? "Hold the right trigger: " : "Hold click: ") + "drill";
        return "";
    }

    // ------------------------------------------------------------------ VR quick bar: big buttons under the dashboard card
    class QuickBtn { public GameObject go; public Renderer bg; public TextMeshPro label; public XRSimpleInteractable inter; public Action act; public Func<string> text; }
    readonly List<QuickBtn> quick = new List<QuickBtn>(); Transform quickRoot; Material quickMat, quickHot; float quickAt, quickText; TextMeshPro bannerVr; InputAction fireVrL; bool wasL;

    void AddQuick(Func<string> text, Action act)
    {
        var b = new QuickBtn { act = act, text = text };
        b.go = Prim(PrimitiveType.Quad, quickRoot, Vector3.zero, new Vector3(0.122f, 0.105f, 1f), quickMat); b.go.name = "Quick button";
        var box = b.go.AddComponent<BoxCollider>(); box.size = new Vector3(1f, 1f, 0.02f);
        b.bg = b.go.GetComponent<Renderer>(); b.bg.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var lg = new GameObject("Label"); lg.transform.SetParent(quickRoot, false);
        b.label = lg.AddComponent<TextMeshPro>(); if (game.hud) b.label.font = game.hud.font;
        b.label.alignment = TextAlignmentOptions.Center; b.label.richText = true; b.label.color = Color.white; QuickFont(b.label);
        b.label.rectTransform.sizeDelta = new Vector2(0.115f, 0.095f);
        b.inter = b.go.AddComponent<XRSimpleInteractable>();
        b.inter.selectEntered.AddListener(_ => { if (HunchVr.Claim(true)) QuickClick(b); });
        quick.Add(b);
    }

    // The size has to be given outright: text that is created while the dashboard is still hidden (during the intro) otherwise
    // wakes up with the default size, which is a hundred times too big and fills the whole view with giant letters.
    static void QuickFont(TextMeshPro t) { t.fontSize = 0.2f; t.enableAutoSizing = true; t.fontSizeMin = 0.06f; t.fontSizeMax = 0.2f; t.overflowMode = TextOverflowModes.Truncate; }

    void BuildQuickBar()
    {
        fireVrL = new InputAction("fireL", InputActionType.Value, "<XRController>{LeftHand}/trigger"); fireVrL.Enable();
        if (!game.hudRoot || !clearSrc) return;
        quickRoot = new GameObject("HUNCH Quick Bar").transform; quickRoot.SetParent(game.hudRoot, false); quickRoot.localPosition = new Vector3(0f, -0.395f, 0f);
        quickMat = new Material(clearSrc); quickMat.SetColor("_BaseColor", new Color(0.05f, 0.13f, 0.28f, 0.93f)); quickMat.renderQueue = 2996;
        quickHot = new Material(clearSrc); quickHot.SetColor("_BaseColor", new Color(1f, 0.7f, 0.18f, 0.96f)); quickHot.renderQueue = 2996;
        AddQuick(() => "MENU", () => { if (menu) menu.OpenPause(); });
        AddQuick(() => "ALIENS", GoField);
        AddQuick(() => "TOOL\n<size=70%>" + ToolLabel() + "</size>", NextTool);
        AddQuick(() => "JETPACK\n<size=70%>" + (game.flyMode ? "ON" : "off") + "</size>", ToggleFly);
        AddQuick(() => "TALK", () => { if (menu) menu.ProbeTalkAria(); });
        AddQuick(() => "RUN\n<size=70%>" + (runOn ? "ON" : "off") + "</size>", () => { runOn = !runOn; Note(runOn ? "Run: ON" : "Run: OFF"); });
        AddQuick(() => "URGENT\n<size=70%>job</size>", () => { if (game.UrgentZone() >= 0) game.GoToUrgent(); else Note("Nothing urgent right now."); });
        AddQuick(() => "EAT\n<size=70%>x" + food + "</size>", Eat);
        float pitch = 1.04f / quick.Count;
        for (int i = 0; i < quick.Count; i++)
        {
            float x = -0.52f + pitch * (i + 0.5f);
            quick[i].go.transform.localPosition = new Vector3(x, 0f, 0.004f); quick[i].label.rectTransform.localPosition = new Vector3(x, 0f, 0f);
        }
        // the "YOU DID IT!" banner above the card
        var bg = new GameObject("Accomplishment banner"); bg.transform.SetParent(game.hudRoot, false); bg.transform.localPosition = new Vector3(0f, 0.47f, 0f);
        bannerVr = bg.AddComponent<TextMeshPro>(); if (game.hud) bannerVr.font = game.hud.font;
        bannerVr.alignment = TextAlignmentOptions.Center; bannerVr.richText = true; bannerVr.color = new Color(1f, 0.85f, 0.3f); bannerVr.fontSize = 0.9f; bannerVr.rectTransform.sizeDelta = new Vector2(1.6f, 0.3f);
        bg.SetActive(false); quickRoot.gameObject.SetActive(false);
    }

    QuickBtn QuickHovered()
    {
        if (!quickRoot || !quickRoot.gameObject.activeInHierarchy) return null;
        foreach (var b in quick) if ((b.inter && b.inter.isHovered) || HunchVr.Hovering(b.go.transform)) return b;
        return null;
    }
    void QuickClick(QuickBtn b)
    {
        if (Time.unscaledTime - quickAt < 0.35f || game.menuOpen || !Playing || autoOn) return;      // the autopilot has the controls until A is pressed
        quickAt = Time.unscaledTime; Play(cPick, 0.5f); game.Buzz(0.5f, 0.06f);
        b.act(); quickText = 0f;
    }
    /// Called from the input code: true when a quick button took the trigger pull.
    bool QuickInput(bool pressedRight)
    {
        bool l = (fireVrL != null && fireVrL.ReadValue<float>() > 0.55f) || HunchVr.LeftTrigger > 0.55f, pressedL = l && !wasL; wasL = l;
        var qb = QuickHovered(); if (qb == null) return false;
        if (pressedRight || pressedL) QuickClick(qb);
        return true;
    }
    void UpdateQuickBar()
    {
        if (!quickRoot) return;
        bool show = game.HeadsetActive && Playing && !game.menuOpen;
        if (quickRoot.gameObject.activeSelf != show) quickRoot.gameObject.SetActive(show);
        bool ban = game.HeadsetActive && Time.unscaledTime < bannerUntil;
        if (bannerVr && bannerVr.gameObject.activeSelf != ban) bannerVr.gameObject.SetActive(ban);
        if (ban) { string bt = (bannerWarn ? "<color=#FF7A6A><b>" : "<color=#FFD64D><b>") + bannerHead + "</b></color>\n<size=55%><color=#FFFFFF>" + bannerTitle + "</color></size>"; if (bannerVr.text != bt) bannerVr.text = bt; }
        if (!show) return;
        quickText -= Time.unscaledDeltaTime; bool refresh = quickText <= 0f; if (refresh) quickText = 0.3f;
        foreach (var b in quick)
        {
            var want = b.inter.isHovered || HunchVr.Hovering(b.go.transform) ? quickHot : quickMat; if (b.bg.sharedMaterial != want) b.bg.sharedMaterial = want;
            if (refresh) { if (b.label.fontSizeMax > 0.21f || b.label.fontSize > 0.21f) QuickFont(b.label); string s = b.text(); if (b.label.text != s) b.label.text = s; }
        }
    }

    // ------------------------------------------------------------------ accomplishments
    class Ach { public string title, how; public Func<bool> test; public bool done; }
    readonly List<Ach> achs = new List<Ach>(); string bannerTitle = ""; float bannerUntil = -9f, achCheck; int achDone, itemsUsed, rockHits, switchUses; bool rodeRover, flew; AudioClip cWin; GUIStyle sWin, sWinTitle;

    void A(string title, string how, Func<bool> test) { achs.Add(new Ach { title = title, how = how, test = test }); }
    int UpgradeCount() { return (zapLv > 1 ? 1 : 0) + (plasmaUnlocked ? 1 : 0) + (drillLv > 1 ? 1 : 0) + (packLv > 1 ? 1 : 0) + (jetLv > 1 ? 1 : 0) + rigCount + turrets.Count + (solarUp ? 1 : 0); }

    void BuildAch()
    {
        // a happy four-note fanfare
        float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f }; int rate = 44100, per = Mathf.RoundToInt(rate * 0.12f), n = per * notes.Length + rate / 3; var d = new float[n];
        for (int k = 0; k < notes.Length; k++) for (int i = 0; i < n - k * per; i++)
        { float t = i / (float)rate, env = Mathf.Exp(-t * (k == notes.Length - 1 ? 5f : 14f)); d[k * per + i] += (Mathf.Sin(2f * Mathf.PI * notes[k] * t) + 0.3f * Mathf.Sin(4f * Mathf.PI * notes[k] * t)) * env * 0.28f; }
        cWin = AudioClip.Create("you did it", n, 1, rate, false); cWin.SetData(d, 0);

        A("Mission started", "Start a mission or free explore.", () => Playing);
        A("Habitat switched on", "Finish the walk through all 8 zones at the start of a mission.", () => sim.phase == HabPhase.Mission && !sim.timeFrozen);
        A("First problem fixed", "Fix your first alert.", () => sim.Handled >= 1);
        A("Problem solver", "Fix five alerts.", () => sim.Handled >= 5);
        A("First moonwalk", "Step outside the habitat.", () => Playing && fx && fx.Outside);
        A("Found the mining field", "Reach the green light beam outside.", () => Playing && fieldOk && (Feet() - fieldC).sqrMagnitude < 45f * 45f);
        A("Alien wrangler", "Send an alien home.", () => sentHome >= 1);
        A("Moon defender", "Send ten aliens home.", () => sentHome >= 10);
        A("First contact", "Make friends with an alien.", () => friends >= 1);
        A("Best friends forever", "Have three alien friends at once.", () => friends >= 3);
        A("Moon miner", "Mine your first mineral.", () => minedCount >= 1);
        A("Master miner", "Mine ten minerals.", () => minedCount >= 10);
        A("Living off the land", "Use ice, ore, helium-3 or a power cell from your backpack.", () => itemsUsed >= 1);
        A("Moon builder", "Place a block.", () => placedCount >= 1);
        A("Architect", "Place ten blocks.", () => placedCount >= 10);
        A("Upgraded!", "Buy an upgrade.", () => UpgradeCount() >= 1);
        A("Rover driver", "Ride a rover.", () => rodeRover);
        A("Rock star", "Hit an alien with a thrown moon rock.", () => rockHits >= 1);
        A("Light keeper", "Press a light or fan button on a zone console.", () => switchUses >= 1);
        A("Rocket pack pilot", "Fly with the jetpack.", () => flew);
        A("One week on the Moon", "Reach day 7 of a mission.", () => RealMission && sim.Day >= 7);
        A("Mission complete", "Finish all 30 days.", () => sim.phase == HabPhase.Report && !sim.missionFailed);
    }

    void Award(Ach a)
    {
        if (hunchOn) { a.done = true; achDone++; return; }      // no pop-ups for game accomplishments in NASA HUNCH mode
        a.done = true; achDone++; bannerTitle = a.title; bannerUntil = Time.unscaledTime + 4.5f; bannerHead = "YOU DID IT!"; bannerWarn = false; bannerAch = true;
        Play(cWin, 0.8f); game.Buzz(0.9f, 0.3f); if (AchXpOnce(a.title)) AddXp(30);
        Fx2("confetti_blast_multicolor", "sparks explode yellow", cam.position + cam.forward * 2.4f + Vector3.up * 0.2f, 0.3f, 0.6f, 1.2f);
        try { var v = GetComponent<HunchVoice>(); if (v && v.speakOn) v.Say("You did it! " + a.title + "."); } catch { }
    }

    bool achSeeded;
    void UpdateAch(float dt)
    {
        if (ride != null) rodeRover = true;
        if (game.flyMode && Playing) flew = true;
        // things already earned in an earlier game (a saved level, saved star clicks) are ticked quietly: no banner the moment the game begins
        if (!achSeeded) { if (!Playing) return; achSeeded = true; foreach (var a in achs) { if (a.done) continue; bool was = false; try { was = a.test(); } catch { } if (was) { a.done = true; achDone++; } } return; }
        achCheck -= Time.unscaledDeltaTime;
        if (achCheck > 0f || Time.unscaledTime < bannerUntil - 0.8f) return;      // one banner at a time
        achCheck = 0.4f;
        foreach (var a in achs)
        {
            if (a.done) continue;
            bool ok = false; try { ok = a.test(); } catch { }
            if (ok) { Award(a); break; }
        }
    }

    void DrawBanner(float u, float W, float H)
    {
        float left = bannerUntil - Time.unscaledTime; if (left <= 0f) return;
        if (sWin == null || Mathf.Abs(sWin.fontSize - HunchLook.FsF(58f * u)) > 1.5f)
        {
            sWin = new GUIStyle(GUI.skin.label) { fontSize = HunchLook.Fs(58f * u), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true };
            sWinTitle = new GUIStyle(GUI.skin.label) { fontSize = HunchLook.Fs(28f * u), alignment = TextAnchor.MiddleCenter, richText = true };
            sWin.normal.textColor = new Color(1f, 0.84f, 0.3f); sWinTitle.normal.textColor = Color.white;
            if (HunchLook.Title) { sWin.font = HunchLook.Title; sWin.fontStyle = FontStyle.Normal; }
        }
        float a = Mathf.Clamp01(left / 0.5f) * Mathf.Clamp01((4.5f - left) / 0.25f), grow = 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 6f);
        var r = new Rect(W * 0.5f - 430f * u, H * 0.17f, 860f * u, 150f * u);
        GUI.color = new Color(0.04f, 0.03f, 0.1f, 0.82f * a); GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = bannerWarn ? new Color(1f, 0.35f, 0.25f, a) : new Color(1f, 0.8f, 0.25f, a); GUI.DrawTexture(new Rect(r.x, r.y, r.width, 4f * u), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(r.x, r.yMax - 4f * u, r.width, 4f * u), Texture2D.whiteTexture);
        GUI.color = bannerWarn ? new Color(1f, 0.55f, 0.5f, a) : new Color(1f, 1f, 1f, a);
        var old = GUI.matrix; GUIUtility.ScaleAroundPivot(new Vector2(grow, grow), new Vector2(W * 0.5f, r.y + 50f * u));
        GUI.Label(new Rect(r.x, r.y + 8f * u, r.width, 84f * u), bannerHead, sWin);
        GUI.matrix = old; GUI.color = new Color(1f, 1f, 1f, a);
        GUI.Label(new Rect(r.x, r.y + 92f * u, r.width, 46f * u), (bannerTitle.Length > 44 ? "<size=" + Mathf.RoundToInt(21f * u) + ">" + bannerTitle + "</size>" : bannerTitle) + (bannerAch ? "   <color=#9AD8FF><size=" + Mathf.RoundToInt(19f * u) + ">" + achDone + " of " + achs.Count + "</size></color>" : ""), sWinTitle);
        GUI.color = Color.white;
    }

    // ------------------------------------------------------------------ kid-friendly sky: colour, twinkle and a soft fill light
    Light fill; Transform skyRoot; readonly List<Transform> nebulas = new List<Transform>(), twinkles = new List<Transform>(); readonly List<Vector3> nebDir = new List<Vector3>(), twDir = new List<Vector3>();
    readonly List<float> twSize = new List<float>(); readonly List<Material> nebMats = new List<Material>(); readonly List<Color> nebCol = new List<Color>();

    static Vector3 SkyDir(float azDeg, float elDeg) { float az = azDeg * Mathf.Deg2Rad, el = elDeg * Mathf.Deg2Rad; return new Vector3(Mathf.Cos(el) * Mathf.Sin(az), Mathf.Sin(el), Mathf.Cos(el) * Mathf.Cos(az)); }

    void BuildKidSky()
    {
        skyRoot = new GameObject("Kid-friendly sky").transform; skyRoot.SetParent(root, false);
        var lg = new GameObject("Soft fill light"); lg.transform.SetParent(skyRoot, false); lg.transform.rotation = Quaternion.Euler(58f, 35f, 0f);
        fill = lg.AddComponent<Light>(); fill.type = LightType.Directional; fill.shadows = LightShadows.None; fill.color = new Color(0.78f, 0.86f, 1f); fill.intensity = 0.55f;
        if (!clearSrc) return;
        var tex = Radial(1.5f);
        float[] az = { 20f, 95f, 170f, 240f, 300f, 0f }, el = { 24f, 40f, 17f, 34f, 21f, 76f }, size = { 3000f, 3600f, 3200f, 3400f, 3000f, 4200f };
        Color[] col = { new Color(1f, 0.45f, 0.78f, 0.34f), new Color(0.35f, 0.85f, 1f, 0.3f), new Color(0.68f, 0.45f, 1f, 0.34f), new Color(0.4f, 1f, 0.75f, 0.24f), new Color(1f, 0.66f, 0.35f, 0.24f), new Color(0.42f, 0.56f, 1f, 0.26f) };
        for (int i = 0; i < az.Length; i++)
        {
            var m = new Material(clearSrc); m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", col[i]); m.renderQueue = 2890;
            var q = Prim(PrimitiveType.Quad, skyRoot, Vector3.zero, Vector3.one * size[i], m); q.name = "Nebula";
            var r = q.GetComponent<Renderer>(); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            nebulas.Add(q.transform); nebDir.Add(SkyDir(az[i], el[i])); nebMats.Add(m); nebCol.Add(col[i]);
        }
        Material[] star = { Glow(new Color(2.6f, 2.4f, 1.6f)), Glow(new Color(1.6f, 2.2f, 2.8f)), Glow(new Color(2.8f, 1.7f, 2.4f)), Glow(new Color(1.8f, 2.8f, 2.0f)) };
        for (int i = 0; i < 44; i++)
        {
            var s = Prim(PrimitiveType.Sphere, skyRoot, Vector3.zero, Vector3.one, star[i % star.Length]); s.name = "Twinkle";
            s.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            twinkles.Add(s.transform); twDir.Add(SkyDir(R(0f, 360f), R(8f, 80f))); twSize.Add(R(9f, 20f));
        }
    }

    void LateSky()
    {
        if (!skyRoot) return;
        if (skyRoot.gameObject.activeSelf != kidSky) skyRoot.gameObject.SetActive(kidSky);
        if (!kidSky) return;
        float t = Time.time;
        for (int i = 0; i < nebulas.Count; i++)
        {
            nebulas[i].position = cam.position + nebDir[i] * 3600f; nebulas[i].rotation = Quaternion.LookRotation(nebDir[i]);
            var c = nebCol[i]; c.a *= 0.8f + 0.2f * Mathf.Sin(t * 0.25f + i * 1.7f); nebMats[i].SetColor("_BaseColor", c);
        }
        for (int i = 0; i < twinkles.Count; i++)
        {
            twinkles[i].position = cam.position + twDir[i] * 3000f;
            twinkles[i].localScale = Vector3.one * twSize[i] * (0.55f + 0.45f * Mathf.Sin(t * (1.5f + (i % 5) * 0.6f) + i));
        }        LateSky23(t);
    }

    // ------------------------------------------------------------------ extra menu pages
    void RegisterMenu2()
    {
        if (!menu) return;
        // easy-mode and sky switches join the Explorer page
        var oldRoot = pageRoot.build;
        pageRoot.build = m =>
        {
            oldRoot(m);
            m.AddItem("Easy mode: " + (smart ? "ON" : "OFF"), "ON: the trigger or click zaps an alien, drills a rock or uses whatever you aim at without choosing a tool, and a zone's job is done for you when you stand in its ring. OFF: choose every tool and press the button yourself.", () => { smart = !smart; m.Rebuild(); });
            m.AddItem("Colourful sky: " + (kidSky ? "ON" : "OFF"), "ON: soft coloured clouds, twinkling stars and a gentle fill light so nothing is pitch black. OFF: the plain black lunar sky.", () => { kidSky = !kidSky; m.Rebuild(); });
        };
        var travel = new HunchMenu.ExtraPage { title = "TRAVEL", label = "Travel: go to any zone", hint = "Pick a place and you are taken straight there. No walking needed." };
        travel.build = m =>
        {
            if (fieldOk) m.AddItem("Mining field (aliens, rovers, rocks)", "Outside the base.", () => { m.Close(); GoField(); });
            for (int i = 0; i < game.zones.Count; i++) { int k = i; var zn = game.zones[i]; m.AddItem((i + 1) + "   " + zn.title, zn.description, () => { m.Close(); game.GoTo(k); }); }
        };
        var ach = new HunchMenu.ExtraPage { title = "ACCOMPLISHMENTS", label = "Accomplishments", hint = "Everything you have done so far, and what is still to try." };
        ach.build = m =>
        {
            m.AddItem("<b>" + achDone + " of " + achs.Count + " done</b>", "Each one shows a YOU DID IT! banner the first time.", () => { });
            foreach (var a in achs) m.AddItem((a.done ? "<color=#7CFC9A>DONE</color>   " : "<color=#8A96AA>to do</color>   ") + a.title, a.how, () => { });
        };
        menu.extraPages.Add(travel); menu.extraPages.Add(ach);
    }

    // ------------------------------------------------------------------ test recorder (used by the editor probe for play-through videos)
    bool recording; int recFrames; public float recEvery = 0.5f;

    public string ProbeRecord()
    {
        if (recording) return "already recording";
        recording = true; recFrames = 0; StartCoroutine(RecLoop()); return "recording";
    }
    public string ProbeRecordStop()
    {
        recording = false;
        string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "HunchBridge", "rec"), zip = Path.Combine(Directory.GetParent(dir).FullName, "rec.zip");
        try
        {
            if (File.Exists(zip)) File.Delete(zip);
            using (var fs = new FileStream(zip, FileMode.Create))
            using (var arc = new System.IO.Compression.ZipArchive(fs, System.IO.Compression.ZipArchiveMode.Create))
                foreach (var f in Directory.GetFiles(dir, "f_*.jpg"))
                {
                    var e = arc.CreateEntry(Path.GetFileName(f), System.IO.Compression.CompressionLevel.NoCompression);
                    using (var src = File.OpenRead(f)) using (var dst = e.Open()) src.CopyTo(dst);
                }
            return "frames " + recFrames + " zip " + (new FileInfo(zip).Length / 1024) + " KB";
        }
        catch (Exception ex) { return "frames " + recFrames + " zip failed " + ex.Message; }
    }
    IEnumerator RecLoop()
    {
        string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "HunchBridge", "rec");
        Directory.CreateDirectory(dir); foreach (var f in Directory.GetFiles(dir, "f_*.jpg")) File.Delete(f);
        var eof = new WaitForEndOfFrame(); float next = 0f;
        while (recording)
        {
            yield return eof;
            if (Time.unscaledTime < next) continue;
            next = Time.unscaledTime + recEvery;
            Texture2D full = null, small = null;
            try
            {
                full = ScreenCapture.CaptureScreenshotAsTexture();
                int w = 640, h = Mathf.Max(2, Mathf.RoundToInt(640f * full.height / full.width) / 2 * 2);
                var rt = RenderTexture.GetTemporary(w, h, 0); Graphics.Blit(full, rt);
                var prev = RenderTexture.active; RenderTexture.active = rt;
                small = new Texture2D(w, h, TextureFormat.RGB24, false); small.ReadPixels(new Rect(0, 0, w, h), 0, 0); small.Apply();
                RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
                File.WriteAllBytes(Path.Combine(dir, "f_" + recFrames.ToString("0000") + ".jpg"), small.EncodeToJPG(72)); recFrames++;
            }
            catch (Exception e) { Debug.LogWarning("[HUNCH] recorder: " + e.Message); recording = false; }
            if (full) Destroy(full); if (small) Destroy(small);
        }
    }
    void OnDestroy() { PerfRestore(); if (fireVrL != null) { fireVrL.Disable(); fireVrL.Dispose(); fireVrL = null; } }
    public string ProbeAch() { var sb = new StringBuilder(); sb.Append(achDone).Append("/").Append(achs.Count).Append(": "); foreach (var a in achs) if (a.done) sb.Append(a.title).Append(", "); sb.Append(" smart=").Append(smartTool).Append(" eff=").Append(EffTool).Append(" design=").Append(shownDesign).Append(" quick=").Append(quick.Count).Append(" neb=").Append(nebulas.Count); return sb.ToString(); }
}
