// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/06_Frontier/B_Maps_Minigames/HunchFrontier6.cs
//  Section : 06_Frontier/B_Maps_Minigames
//  Folder  : Extra maps and mini-games: solar farm, mining pit, Factory Land, Star Patrol, crew
//            radio + Star Clicker, Science Academy, My Astronaut, model kits, Fun Zone, endless
//            ground, Star Store pages.
//  Purpose : Sixth part of Moon Frontier: extra maps. The central base is untouched; each map is
//            its own flat piece of Moon far away, reached through the Map Gate at the mining field,
//            the menu, the Y key or the MAPS button on the VR dashboard.
//  Contains: HunchFrontier, SolPanel, PitBlock, ZrTarget, BeatRock, TdFoe, TdTower
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// Sixth part of Moon Frontier: extra maps. The central base is untouched; each map is its own flat piece of Moon far away,
/// reached through the Map Gate at the mining field, the menu, the Y key or the MAPS button on the VR dashboard.
///   1 Solar farm       tilt six solar panels to follow the Sun; score = energy = the integral of power over time
///   2 Moon mining pit  drill through layers of rock; ore gets richer with depth
///   3 Alien zap range  zap hopping aliens; their UFO beams them home (nobody is hurt)
///   4 Play park        dance floor, bounce pads and moon balls, just for fun
///   5 Beat Asteroid    asteroids fly at you on the beat of the music; break them in time
///   6 Tower defense    build beam towers that send the aliens home before they reach the snack table
/// Up to four players take turns on one computer; every player has their own best scores.
public partial class HunchFrontier
{
    public const int MapCount = 14;
    public static readonly string[] MapNames = { "Central base", "Solar farm", "Moon mining pit", "Alien zap range", "Play park", "Beat Asteroid", "Tower defense", "Factory Land", "Star Patrol", "Story Studio", "Star Clicker", "Science Academy", "Clinic and Pet House", "Boss Arena" };
    static readonly string[] MapHint =
    {
        "Back to the habitat.",
        "Tilt the six panels with the - and + buttons on the desk so they face the Sun as it crosses the sky. Score: energy collected.",
        "Drill into the rock mound. Deeper layers hold more ore, helium-3 and crystals. Everything you mine goes in your backpack.",
        "Zap the hopping aliens. Their UFO beams them home, nobody is hurt. Hits in a row score more.",
        "A dance floor that makes you dance, bounce pads that throw you high in Moon gravity, and big moon balls to kick.",
        "Asteroids fly at you on the beat. In VR swing the two light sticks; on a PC aim and click. Blue is left, gold is right.",
        "Aliens walk the path to the snack table. Click a pad to build a beam tower, click a tower to make it stronger.",
        "Drill coal, iron, gold and stone. The power totem burns coal; smelters and factories turn ore into bars, gears, engines and base panels. Grow food in the garden. No timer.",
        "Fly a star fighter in open space. Zap the alien scouts and they warp home. Then turn the giant Dust Titan away by lighting its five beacons.",
        "Paint on the big wall, play the story of Cadet Nova, read alien stories, and train with a light sword. No timer.",
        "Click the star to make stardust, buy helpers that make it by themselves, grow the star and go supernova. It keeps earning while you play other maps. No timer.",
        "Physics Force Lab: choose which way the missing force points on each free-body diagram. Space Biology Lab: how living things change in space. Hall of Science: scientists, Moon walkers and NASA HUNCH. No timer.",
        "Scan each patient, then bring what they need before their patience runs out. Next door, look after Lumi the moon pup: feed, play, wash and sleep.",
        "The Grumpy Moon Golem guards a Star Diamond. Zap its glowing tickle spots for three rounds. Nobody is hurt: it laughs and hands the diamond over. Needs a ticket from the Star shop (the first is free)."
    };
    static readonly float[] MgLen = { 0f, 80f, 90f, 60f, 0f, 64f, 0f, 0f, 150f, 0f, 0f, 0f, 120f, 100f };
    static readonly string[] MgUnitName = { "", "%", " points", " points", "", " points", " points", "", " points", "", "", "", " points", " points" };

    public int mapNow, player, mgPlayed; public bool mgOn; public float mgT, mgScore; float mgCount; string mgResult = "";
    readonly Transform[] mapRoot = new Transform[MapCount]; readonly Vector3[] mapC = new Vector3[MapCount]; readonly TextMeshPro[] mapBoard = new TextMeshPro[MapCount];
    readonly List<TextMeshPro> mapLabels = new List<TextMeshPro>();
    Vector3 mapBack; float mapBackYaw; bool mapBackSet; HunchMenu.ExtraPage pageMaps; HunchMusic musicRef; int musicBefore = -1;
    const float MapSize = 110f; HunchGuide guideRef; bool guideHid, guideWas;

    public string PlayerName { get { return "Player " + (player + 1) + " (" + sim.crew[Mathf.Clamp(player, 0, sim.crew.Count - 1)].name + ")"; } }
    float BestScore(int k) { return PlayerPrefs.GetFloat("hunch_best_" + player + "_" + k, 0f); }
    Vector3 MapSpawn(int k) { return mapC[k] + (k == 5 ? Vector3.zero : (k == 8 ? new Vector3(0f, 400f, -80f) : new Vector3(0f, 0f, -16f))); }
    static bool MapFree(int k) { return k == 4 || k == 7 || k == 9 || k == 10 || k == 11; }      // maps with no timer and no score

    // ------------------------------------------------------------------ building the maps
    void BuildMaps()
    {
        Vector3 o = game.zones[0].position; mapC[0] = o; float x = 2600f;
        var ground = Lit(new Color(0.40f, 0.40f, 0.43f)); if (ground.HasProperty("_BaseMap")) ground.SetTextureScale("_BaseMap", new Vector2(40f, 40f)); var rim = Lit(new Color(0.26f, 0.27f, 0.31f)); var line = Glow(new Color(0.2f, 0.45f, 0.8f));
        for (int k = 1; k < MapCount; k++)
        {
            for (int tries = 0; tries < 8; tries++) { if (!Physics.CheckBox(new Vector3(o.x + x, o.y, o.z), new Vector3(MapSize * 0.6f, 400f, MapSize * 0.6f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) break; x += 300f; }
            mapC[k] = new Vector3(o.x + x, o.y, o.z); x += 400f;
            var r = new GameObject("Map: " + MapNames[k]).transform; r.SetParent(root, false); r.position = mapC[k]; mapRoot[k] = r;
            Prim(PrimitiveType.Cube, r, new Vector3(0f, -0.5f, 0f), new Vector3(MapSize, 1f, MapSize), ground, true);
            for (int s = 0; s < 4; s++)
            {
                bool alongX = s < 2; float sign = s % 2 == 0 ? -1f : 1f;
                Prim(PrimitiveType.Cube, r, alongX ? new Vector3(0f, 1.5f, sign * MapSize * 0.5f) : new Vector3(sign * MapSize * 0.5f, 1.5f, 0f), alongX ? new Vector3(MapSize, 3f, 1f) : new Vector3(1f, 3f, MapSize), rim, true);
            }
            for (int g = -2; g <= 2; g++)
            {
                Prim(PrimitiveType.Cube, r, new Vector3(g * 20f, 0.01f, 0f), new Vector3(0.12f, 0.02f, MapSize - 2f), line);
                Prim(PrimitiveType.Cube, r, new Vector3(0f, 0.01f, g * 20f), new Vector3(MapSize - 2f, 0.02f, 0.12f), line);
            }
            float gx = k == 5 ? 9f : 7f, gz = k == 5 ? 6f : -9f;
            Gate(r, new Vector3(-gx, 0f, gz), 0, "BACK TO BASE", new Color(0.3f, 1.5f, 0.6f));
            if (!MapFree(k)) Gate(r, new Vector3(gx, 0f, gz), 100 + k, "PLAY AGAIN", new Color(1.6f, 1.1f, 0.25f));
            int nextMap = k % (MapCount - 1) + 1, prevMap = (k + MapCount - 3) % (MapCount - 1) + 1;      // portals straight to the neighbouring worlds
            Gate(r, new Vector3(gx + 8f, 0f, gz + 5f), nextMap, "NEXT WORLD\n<size=60%>" + MapNames[nextMap] + "</size>", new Color(0.3f, 0.8f, 2.2f));
            Gate(r, new Vector3(-gx - 8f, 0f, gz + 5f), prevMap, "LAST WORLD\n<size=60%>" + MapNames[prevMap] + "</size>", new Color(2.0f, 0.4f, 0.3f));
            mapBoard[k] = Label(r, "", 0f, 0f, 16f, 70f, 16f, Color.white); mapBoard[k].transform.localPosition = new Vector3(0f, 13f, 50f);
            switch (k)
            {
                case 1: BuildSolar(r); break;
                case 2: BuildPit(r); break;
                case 3: BuildRange(r); break;
                case 4: BuildPark(r); break;
                case 5: BuildBeat(r); break;
                case 6: BuildTd(r); break;
                case 7: BuildFactory(r); break;
                case 8: BuildStar(r); break;
                case 9: BuildStudio(r); break;
                case 10: BuildClicker(r); break;
                case 11: BuildAcademy(r); break;
                case 12: BuildClinic(r); break;
                case 13: BuildBoss(r); break;
            }
            r.gameObject.SetActive(false);
        }
        // the Map Gate at the mining field
        Vector3 at;
        if (fieldOk && FreeSpot(new[] { 20f, 28f, 14f, 36f }, 150f, 4f, out at))
        {
            var h = new GameObject("Map gate").transform; h.SetParent(root, false); h.position = at;
            Vector3 toField = fieldC - at; toField.y = 0f; if (toField.sqrMagnitude > 1f) h.rotation = Quaternion.LookRotation(toField.normalized, Vector3.up);
            Gate(h, Vector3.zero, -1, "MAP GATE\n<size=60%>thirteen more maps</size>", new Color(0.7f, 0.5f, 1.8f)); mapGateAt = at; mapGateOk = true;
        }
        if (menu)
        {
            pageMaps = new HunchMenu.ExtraPage { title = "MAPS AND MINI-GAMES", label = "Maps and mini-games", hint = "Thirteen extra maps: Solar farm, Moon mining pit, Alien zap range, Play park, Beat Asteroid, Tower defense, Factory Land, Star Patrol, the Story Studio, Star Clicker, the Science Academy, the Clinic and Pet House, and the Boss Arena. The central base stays as it is." };
            pageMaps.build = m =>
            {
                m.AddItem("Player: " + PlayerName + "   (switch, up to 4)", "Up to four players take turns on this computer. Each player has their own best scores.", () => { player = (player + 1) % 4; m.Rebuild(); });
                for (int k = 1; k < MapCount; k++)
                {
                    int kk = k; string best = MgLen[k] > 0f || k == 6 ? "   best " + Mathf.RoundToInt(BestScore(k)) + MgUnitName[k] : "";
                    m.AddItem((mapNow == k ? "> " : "") + MapNames[k] + best, MapHint[k], () => { m.Close(); PortalGo(kk, null); });
                }
                m.AddItem("Back to the central base", "Returns you to where you were in the habitat.", () => { m.Close(); GoMap(0); });
            };
            menu.extraPages.Add(pageMaps);
        }
        A("Map traveller", "Go through the Map Gate to another map.", () => mapNow != 0);
        A("Mini-game fan", "Finish three different mini-games.", () => Bits(mgPlayed) >= 3);
    }
    Vector3 mapGateAt; bool mapGateOk;
    static int Bits(int v) { int n = 0; while (v != 0) { n += v & 1; v >>= 1; } return n; }

    void Gate(Transform parent, Vector3 lp, int index, string title, Color c)
    {
        var g = new GameObject("Gate").transform; g.SetParent(parent, false); g.localPosition = lp;
        var glow = Glow(c); var metal = Lit(new Color(0.3f, 0.32f, 0.36f)); bool stone = false;
        try { stone = PortalStone(g, c, index); } catch (Exception e) { HLog.E("Minigames", "portal: " + e); }
        if (!stone)
        {
            Prim(PrimitiveType.Cube, g, new Vector3(-1.1f, 1.4f, 0f), new Vector3(0.2f, 2.8f, 0.2f), metal); Prim(PrimitiveType.Cube, g, new Vector3(1.1f, 1.4f, 0f), new Vector3(0.2f, 2.8f, 0.2f), metal);
            Prim(PrimitiveType.Cube, g, new Vector3(0f, 2.85f, 0f), new Vector3(2.4f, 0.2f, 0.2f), glow);
        }
        Prim(PrimitiveType.Cylinder, g, new Vector3(0f, 0.03f, 0f), new Vector3(stone ? 3.4f : 2.2f, 0.03f, stone ? 3.4f : 2.2f), glow);
        var hit = Prim(PrimitiveType.Cube, g, stone ? new Vector3(0f, 2.15f, 0f) : new Vector3(0f, 1.4f, 0f), stone ? new Vector3(3.0f, 3.5f, 0.3f) : new Vector3(2.0f, 2.6f, 0.15f), Clear(new Color(c.r * 0.35f, c.g * 0.35f, c.b * 0.35f, stone ? 0.03f : 0.35f)), true);
        var tg = hit.AddComponent<HunchTarget>(); tg.kind = 46; tg.index = index;
        mapLabels.Add(Label(g, "<b>" + title + "</b>", 0f, stone ? 4.8f : 3.7f, 5f, 9f, 1.6f, Color.white));
    }

    // ------------------------------------------------------------------ travelling
    public void GoMap(int k)
    {
        if (readOpen >= 0) CloseRead();      // a sheet you were reading does not travel with you
        if (!ready || k < 0 || k >= MapCount || !Playing) return;
        if (hunchOn && k != 0) { Note("NASA HUNCH mode: the other maps are switched off. Turn the mode off in the menu to visit them."); return; }
        if (k != 0 && !mapRoot[k]) return;
        if (k == mapNow && k != 0) { MgPrepare(k); return; }
        if (ride != null) Dismount(null);
        if (thirdPerson) SetThird(false);
        if (mapNow == 0 && k != 0) { mapBack = Feet(); mapBackYaw = cam.eulerAngles.y; mapBackSet = true; }
        mgOn = false; mgCount = 0f; mgResult = "";
        if (mapNow != 0 && mapRoot[mapNow]) mapRoot[mapNow].gameObject.SetActive(false);
        if (saberL) saberL.gameObject.SetActive(false); if (saberR) saberR.gameObject.SetActive(false);
        if (!musicRef) musicRef = FindFirstObjectByType<HunchMusic>();
        if (musicRef && musicBefore >= 0 && k != 4 && k != 5) { musicRef.SetStyle(musicBefore); musicBefore = -1; }
        mapNow = k; game.flyMode = false; tool = Hands;
        if (k == 0)
        {
            if (mapBackSet) TeleportTo(mapBack, mapBackYaw); else game.GoTo(0);
            sim.Say("ARIA: Welcome back to the central base."); return;
        }
        mapRoot[k].gameObject.SetActive(true); Physics.SyncTransforms();
        TeleportTo(MapSpawn(k), 0f);
        HunchFx.Spawn("teleport", MapSpawn(k), 0.6f, 1.2f); Play(cPop, 0.7f);
        if (musicRef && (k == 4 || k == 5) && musicBefore < 0) { musicBefore = musicRef.style; musicRef.SetStyle(k == 5 ? 7 : 1); }      // the park gets the happy track, Beat Asteroid the funky one
        Banner(MapNames[k].ToUpper(), k == 4 ? "Dance, bounce and play!" : (k == 7 ? "Dig, smelt, build and grow" : (k == 9 ? "Paint, read and play" : (k == 10 ? "Click the star!" : (k == 11 ? "Forces, biology and heroes of science" : (k == 12 ? "Care for the patients, and for Lumi" : (k == 13 ? "Tickle the Golem!" : "Get ready...")))))), false);
        sim.Say("ARIA: " + MapNames[k] + ". " + MapHint[k]);
        MgPrepare(k);
    }

    void LeaveMapQuiet()
    {
        mgOn = false; mgCount = 0f; if (mapNow == 8) game.flyMode = false;
        if (mapNow != 0 && mapRoot[mapNow]) mapRoot[mapNow].gameObject.SetActive(false);
        if (saberL) saberL.gameObject.SetActive(false); if (saberR) saberR.gameObject.SetActive(false);
        if (musicRef && musicBefore >= 0) { musicRef.SetStyle(musicBefore); musicBefore = -1; }
        mapNow = 0; tool = Hands;
    }

    public void MgPrepare(int k)
    {
        if (k <= 0 || k >= MapCount) return;
        mgOn = false; mgScore = 0f; mgResult = ""; mgT = MgLen[k]; mgCount = MapFree(k) ? 0f : 4f; tool = Hands;
        switch (k)
        {
            case 1: solEnergy = 0f; solPhi = -70f; for (int i = 0; i < solPanels.Count; i++) solPanels[i].ang = i % 2 == 0 ? -30f : 30f; break;
            case 2: PitReset(); tool = Drill; break;
            case 3: zrHits = zrShots = zrCombo = 0; foreach (var z in zrTargets) { z.up = false; z.wait = R(0.2f, 1.2f); z.t.gameObject.SetActive(false); } tool = Zapper; break;
            case 5: BeatReset(); break;
            case 6: TdReset(); break;
            case 8: StarReset(); break;
            case 12: ClinicReset(); break;
            case 13: BossReset(); break;
        }
    }

    void MgBegin(int k)
    {
        if (k == 5 && musicRef) musicRef.Restart();
        if (k == 6) { tdWave = 0; tdPause = 1f; }
    }

    void MgEnd(string why)
    {
        if (!mgOn) return;
        mgOn = false; int k = mapNow; float score = Mathf.Round(mgScore);
        string key = "hunch_best_" + player + "_" + k; float best = PlayerPrefs.GetFloat(key, 0f); bool record = score > best;
        if (record) { best = score; PlayerPrefs.SetFloat(key, best); }
        mgResult = (why.Length > 0 ? why + "  " : "") + "Score " + score + MgUnitName[k] + (record ? "   NEW BEST for " : "   Best for ") + PlayerName + ": " + best + MgUnitName[k];
        Banner(record ? "NEW BEST!" : "FINISHED!", MapNames[k] + ": " + score + MgUnitName[k], false);
        switch (k)
        {
            case 1: sim.battery = Mathf.Min(100f, sim.battery + score * 0.2f); break;
            case 3: AddItem(Crystal, zrHits / 6); break;
            case 5: if (score >= 1500f && food < 9) food++; break;
            case 6: if (tdSnacks > 0) AddItem(Crystal, 2); break;
        }
        mgPlayed |= 1 << k; AddXp(40); if (k == 12) ClinicEnd();
        if (k == 3 || k == 2) tool = Hands;
        foreach (var z in zrTargets) { z.up = false; z.t.gameObject.SetActive(false); }
        foreach (var b in beatRocks) { b.live = false; b.t.gameObject.SetActive(false); }
        sim.Say("ARIA: " + MapNames[k] + " finished. " + mgResult + ". Aim at PLAY AGAIN or BACK TO BASE.");
    }

    void UpdateMaps(float dt)
    {
        foreach (var l in mapLabels) if (l && l.gameObject.activeInHierarchy) l.transform.rotation = Quaternion.LookRotation(l.transform.position - cam.position, Vector3.up);
        var kb = Keyboard.current;
        if (kb != null && kb.yKey.wasPressedThisFrame && Playing && !game.menuOpen && !game.HeadsetActive && !ControlsLocked && menu && pageMaps != null) menu.OpenExtra(pageMaps);
        // the floor arrows point at jobs in the base, so they rest while you are on another map
        if (!guideRef) guideRef = GetComponent<HunchGuide>();
        if (guideRef) { if (mapNow != 0 && !guideHid) { guideHid = true; guideWas = guideRef.arrowsOn; guideRef.arrowsOn = false; } else if (mapNow == 0 && guideHid) { guideHid = false; guideRef.arrowsOn = guideWas; } }
        if (mapNow == 0) return;
        Vector3 ml = Feet() - mapC[mapNow];
        float edge = mapNow == 8 ? 900f : MapSize;
        if (Mathf.Abs(ml.x) > edge || Mathf.Abs(ml.z) > edge) { LeaveMapQuiet(); return; }      // something else moved the player away (the X button, a job, a respawn)
        if (ml.y < -6f) TeleportTo(MapSpawn(mapNow), 0f);
        if (!Playing) return;
        if (mgCount > 0f)
        {
            mgCount -= dt;
            if (mgCount <= 0f) { mgOn = true; mgT = MgLen[mapNow]; Note("GO!"); Play(cPop, 0.8f); MgBegin(mapNow); }
        }
        switch (mapNow)
        {
            case 1: UpdateSolar(dt); break;
            case 3: UpdateRange(dt); break;
            case 4: UpdatePark(dt, ml); break;
            case 5: UpdateBeat(dt); break;
            case 6: UpdateTd(dt); break;
            case 8: UpdateStar(dt); break;
            case 9: UpdateStudio(dt); break;
            case 10: UpdateClickerMap(dt); break;
            case 11: UpdateAcademy(dt); break;
            case 12: UpdateClinic(dt); break;
            case 13: UpdateBoss(dt); break;
        }
        if (mgOn && MgLen[mapNow] > 0f) { mgT -= dt; if (mgT <= 0f) { mgT = 0f; MgEnd("Time!"); } }
        var board = mapBoard[mapNow];
        if (board)
        {
            string s = "<b>" + MapNames[mapNow].ToUpper() + "</b>\n<size=60%>" + (mgCount > 0f ? "Starting in " + Mathf.CeilToInt(mgCount) + "..." : (mgOn ? MapStatus() : (mgResult.Length > 0 ? mgResult : MapHint[mapNow]))) + "</size>";
            if (board.text != s) board.text = s;
        }
    }

    string MapStatus()
    {
        switch (mapNow)
        {
            case 1: return "Time " + Mathf.CeilToInt(mgT) + " s   Sun angle " + Mathf.RoundToInt(solPhi) + " deg   Power " + Mathf.RoundToInt(solPower * 100f) + "%   Energy (integral of P dt) " + solEnergy.ToString("F1");
            case 2: return "Time " + Mathf.CeilToInt(mgT) + " s   Mined value " + Mathf.RoundToInt(mgScore) + "   Blocks left " + PitLeft();
            case 3: return "Time " + Mathf.CeilToInt(mgT) + " s   Sent home " + zrHits + "   In a row " + zrCombo + "   Score " + Mathf.RoundToInt(mgScore);
            case 5: return "Time " + Mathf.CeilToInt(mgT) + " s   Score " + Mathf.RoundToInt(mgScore) + "   Combo " + beatCombo + "   Hit " + beatHits + "  Missed " + beatMiss;
            case 6: return "Wave " + Mathf.Min(tdWave + 1, TdWaves) + " of " + TdWaves + "   Energy " + tdEnergy + "   Snacks " + tdSnacks + " of 10   Sent home " + tdHome;
            case 7: return FacStatus();
            case 8: return "Time " + Mathf.CeilToInt(mgT) + " s   Scouts sent home " + starHome + (starPhase >= 2 ? "   DUST TITAN: beacons " + starLitCount + " of 5" : " of " + StarWave) + "   Score " + Mathf.RoundToInt(mgScore);
            case 9: return "Paint strokes " + artStrokes + "   Story endings " + Bits(storyEnds) + " of 3   Orbs sliced " + swordHits;
            case 10: return ClickerStatus();
            case 11: return AcademyStatus();
            case 12: return ClinicStatus();
            case 13: return BossStatus();
        }
        return "";
    }
    /// one line for the HUD while on another map
    string MapHud()
    {
        if (mapNow == 0) return "";
        string s = "<b>" + MapNames[mapNow] + "</b>   " + PlayerName + "   ";
        if (mgCount > 0f) return s + "Starting in " + Mathf.CeilToInt(mgCount) + "...";
        if (mgOn) return s + MapStatus();
        return s + (mgResult.Length > 0 ? mgResult : (mapNow == 4 ? "Step on the dance floor, the pink pads and kick the balls." : (mapNow == 7 || mapNow >= 9 && MapFree(mapNow) ? MapStatus() : "Aim at PLAY AGAIN to start.")));
    }

    // ------------------------------------------------------------------ 1. solar farm
    class SolPanel { public Transform pivot; public float ang, shown; public TextMeshPro label; }
    readonly List<SolPanel> solPanels = new List<SolPanel>(); Transform solSun; float solPhi = -70f; public float solEnergy, solPower;

    void BuildSolar(Transform r)
    {
        var metal = Lit(new Color(0.55f, 0.57f, 0.62f)); var blue = Lit(new Color(0.08f, 0.16f, 0.5f)); var gold = Lit(new Color(0.95f, 0.74f, 0.22f)); var dark = Lit(new Color(0.12f, 0.13f, 0.16f));
        var minus = Glow(new Color(0.3f, 0.7f, 1.8f)); var plus = Glow(new Color(1.7f, 1.2f, 0.25f));
        var desk = new GameObject("Solar control desk").transform; desk.SetParent(r, false); desk.localPosition = new Vector3(0f, 0f, 1.5f);
        Prim(PrimitiveType.Cube, desk, new Vector3(0f, 0.45f, 0.3f), new Vector3(4.6f, 0.9f, 0.5f), dark);
        Prim(PrimitiveType.Cube, desk, new Vector3(0f, 1.3f, 0.06f), new Vector3(4.6f, 0.9f, 0.08f), dark);
        var title = Label(desk, "<b>SOLAR CONTROL</b>   - tilts west, + tilts east.   Power = cos(panel angle - Sun angle)", 0f, 1.67f, 0.9f, 4.4f, 0.14f, new Color(1f, 0.84f, 0.3f)); title.transform.localPosition = new Vector3(0f, 1.67f, 0.0f);
        for (int i = 0; i < 6; i++)
        {
            float x = (i - 2.5f) * 5.4f, cx = (i - 2.5f) * 0.72f;
            var b = new GameObject("Solar panel " + (i + 1)).transform; b.SetParent(r, false); b.localPosition = new Vector3(x, 0f, 13f);
            Prim(PrimitiveType.Cylinder, b, new Vector3(0f, 1.45f, 0f), new Vector3(0.24f, 1.45f, 0.24f), metal);
            var p = new SolPanel(); p.pivot = new GameObject("Pivot").transform; p.pivot.SetParent(b, false); p.pivot.localPosition = new Vector3(0f, 3.0f, 0f);
            Prim(PrimitiveType.Cube, p.pivot, Vector3.zero, new Vector3(4.4f, 0.1f, 3.0f), blue); Prim(PrimitiveType.Cube, p.pivot, new Vector3(0f, 0.07f, 0f), new Vector3(4.5f, 0.04f, 0.1f), gold);
            var num = Label(b, "<b>" + (i + 1) + "</b>", 0f, 0.9f, 12f, 3f, 2f, new Color(1f, 0.84f, 0.3f)); num.transform.localPosition = new Vector3(0f, 0.9f, -0.4f);
            p.ang = i % 2 == 0 ? -30f : 30f; p.shown = p.ang;
            for (int d = 0; d < 2; d++)
            {
                var bt = Prim(PrimitiveType.Cube, desk, new Vector3(cx + (d == 0 ? -0.17f : 0.17f), 1.18f, 0f), new Vector3(0.28f, 0.28f, 0.08f), d == 0 ? minus : plus, true);
                bt.GetComponent<BoxCollider>().size = new Vector3(1.1f, 1.2f, 3f);
                var tg = bt.AddComponent<HunchTarget>(); tg.kind = 30; tg.index = i * 2 + d;
                var sign = Label(desk, d == 0 ? "<b>-</b>" : "<b>+</b>", 0f, 0f, 2.2f, 0.3f, 0.3f, Color.black); sign.transform.localPosition = new Vector3(cx + (d == 0 ? -0.17f : 0.17f), 1.18f, -0.05f);
            }
            p.label = Label(desk, "", 0f, 0f, 0.62f, 0.7f, 0.3f, Color.white); p.label.transform.localPosition = new Vector3(cx, 1.5f, 0.0f);
            solPanels.Add(p);
        }
        solSun = Prim(PrimitiveType.Sphere, r, new Vector3(0f, 40f, 13f), Vector3.one * 5f, Glow(new Color(3.2f, 2.5f, 0.9f))).transform;
    }

    void SolPress(int index)
    {
        int i = index / 2; if (i < 0 || i >= solPanels.Count) return;
        solPanels[i].ang = Mathf.Clamp(solPanels[i].ang + (index % 2 == 0 ? -10f : 10f), -80f, 80f);
        Play(cPick, 0.5f); game.Buzz(0.3f, 0.04f);
    }

    void UpdateSolar(float dt)
    {
        float elapsed = mgOn ? MgLen[1] - mgT : 0f;
        solPhi = -70f + 140f * Mathf.Clamp01(elapsed / MgLen[1]);
        float pr = solPhi * Mathf.Deg2Rad; solSun.localPosition = new Vector3(Mathf.Sin(pr) * 46f, Mathf.Cos(pr) * 46f + 2f, 13f);
        float sum = 0f;
        for (int i = 0; i < solPanels.Count; i++)
        {
            var p = solPanels[i]; p.shown = Mathf.MoveTowards(p.shown, p.ang, 60f * dt); p.pivot.localRotation = Quaternion.Euler(0f, 0f, -p.shown);
            float power = Mathf.Max(0f, Mathf.Cos((p.shown - solPhi) * Mathf.Deg2Rad)); sum += power;
            string s = "<b>" + (i + 1) + "</b>  " + Mathf.RoundToInt(p.ang) + " deg\n<color=#" + (power > 0.9f ? "7CFC9A" : (power > 0.6f ? "FFE08A" : "FF8A7A")) + ">" + Mathf.RoundToInt(power * 100f) + "%</color>";
            if (p.label.text != s) p.label.text = s;
        }
        solPower = sum / Mathf.Max(1, solPanels.Count);
        if (mgOn) { solEnergy += solPower * dt; calcEnergy += solPower * dt; mgScore = 100f * solEnergy / MgLen[1]; }
    }

    // ------------------------------------------------------------------ 2. moon mining pit
    class PitBlock { public Transform t; public Renderer r; public int kind, layer; public float hp; public bool gone; }
    readonly List<PitBlock> pit = new List<PitBlock>(); Material[] pitMat;
    static readonly string[] PitName = { "Regolith", "Basalt", "Ilmenite ore", "Water ice", "Helium-3 pocket", "Moon crystal", "Bedrock" };
    static readonly float[] PitHp = { 0.45f, 1.3f, 1.1f, 0.8f, 1.7f, 2.3f, 4.5f };
    static readonly int[] PitValue = { 0, 0, 10, 8, 25, 40, 0 }, PitItem = { -1, -1, Ore, Ice, He3, Crystal, -1 }, PitCount = { 0, 0, 2, 2, 1, 1, 0 };
    const int PitW = 7, PitLayers = 3;

    void BuildPit(Transform r)
    {
        pitMat = new[] { Lit(new Color(0.55f, 0.55f, 0.57f)), Lit(new Color(0.2f, 0.2f, 0.23f)), Glow(new Color(1.3f, 0.6f, 0.15f)), Glow(new Color(0.5f, 1.2f, 1.7f)), Glow(new Color(1.0f, 1.6f, 0.3f)), Glow(new Color(1.3f, 0.5f, 1.8f)), Lit(new Color(0.05f, 0.05f, 0.06f)) };
        var holder = new GameObject("Rock mound").transform; holder.SetParent(r, false); holder.localPosition = new Vector3(0f, 0f, 6f);
        for (int layer = 0; layer < PitLayers; layer++)
            for (int ix = 0; ix < PitW; ix++)
                for (int iz = 0; iz < PitW; iz++)
                {
                    var g = Prim(PrimitiveType.Cube, holder, new Vector3((ix - 3) * 1.5f, 0.75f + layer * 1.5f, (iz - 3) * 1.5f + 6f), Vector3.one * 1.46f, pitMat[0], true);
                    g.name = "Rock block"; var tg = g.AddComponent<HunchTarget>(); tg.kind = 31; tg.index = pit.Count;
                    pit.Add(new PitBlock { t = g.transform, r = g.GetComponent<Renderer>(), layer = layer });
                }
        var sign = Label(r, "<b>MOON MINING PIT</b>\n<size=60%>Hold the drill on a block. Ore grade with depth d:  G(d) = 1 - e^(-0.55 d)\nGrey regolith and dark basalt are empty. Orange ore, blue ice, yellow helium-3, purple crystal.</size>", 0f, 0f, 7f, 40f, 6f, new Color(1f, 0.84f, 0.3f));
        sign.transform.localPosition = new Vector3(0f, 7.5f, 24f);
    }

    void PitReset()
    {
        foreach (var b in pit)
        {
            int d = PitLayers - 1 - b.layer; float grade = 1f - Mathf.Exp(-0.55f * (d + 0.4f)), roll = R(0f, 1f), pick = R(0f, 1f); int kind;
            if (roll < grade)
            {
                if (d == 0) kind = pick < 0.5f ? 2 : 3;
                else if (d == 1) kind = pick < 0.4f ? 2 : (pick < 0.7f ? 3 : (pick < 0.9f ? 4 : 5));
                else kind = pick < 0.3f ? 2 : (pick < 0.5f ? 3 : (pick < 0.8f ? 4 : 5));
            }
            else kind = d == 0 ? 0 : (pick < 0.1f ? 6 : (pick < 0.45f ? 0 : 1));
            b.kind = kind; b.hp = PitHp[kind]; b.gone = false; b.r.sharedMaterial = pitMat[kind]; b.t.gameObject.SetActive(true);
        }
    }
    int PitLeft() { int n = 0; foreach (var b in pit) if (!b.gone) n++; return n; }

    void PitDrill(int i, float dt, Vector3 at, bool spark)
    {
        if (i < 0 || i >= pit.Count) return; var b = pit[i]; if (b.gone) return;
        b.hp -= dt * drillLv;
        if (spark) HunchFx.Spawn("sparks yellow", at, 0.25f, 0.4f);
        if (b.hp > 0f) return;
        b.gone = true; b.t.gameObject.SetActive(false);
        HunchFx.Spawn("stones hit", b.t.position, 0.6f, 0.9f); Play(cPick, 0.6f); game.Buzz(0.5f, 0.1f);
        if (PitItem[b.kind] >= 0)
        {
            int got = AddItem(PitItem[b.kind], PitCount[b.kind]); minedCount++;
            if (mgOn) mgScore += PitValue[b.kind];
            Note("+" + got + " " + ItemNames[PitItem[b.kind]] + "   value " + PitValue[b.kind]);
        }
    }
    string PitInfo(int i)
    {
        if (i < 0 || i >= pit.Count) return ""; var b = pit[i];
        return "drill: " + PitName[b.kind] + ", depth layer " + (PitLayers - b.layer) + (PitValue[b.kind] > 0 ? ", value " + PitValue[b.kind] : ", nothing inside");
    }

    // ------------------------------------------------------------------ 3. alien zap range
    class ZrTarget { public Transform t; public bool up; public float life, wait, phase, speed; public Vector3 home; }
    readonly List<ZrTarget> zrTargets = new List<ZrTarget>(); public int zrHits, zrShots, zrCombo;

    Transform AlienToy(Transform parent, string name, Color c, float size)
    {
        var t = new GameObject(name).transform; t.SetParent(parent, false);
        var skin = new Material(litSrc); skin.SetColor("_BaseColor", c); if (skin.HasProperty("_BaseMap")) skin.SetTexture("_BaseMap", null);
        var white = Lit(new Color(0.97f, 0.97f, 0.97f)); var black = Lit(new Color(0.03f, 0.03f, 0.04f));
        Prim(PrimitiveType.Sphere, t, new Vector3(0f, 0.5f * size, 0f), new Vector3(0.95f, 0.85f, 0.95f) * size, skin);
        for (int e = -1; e <= 1; e += 2)
        {
            Prim(PrimitiveType.Sphere, t, new Vector3(0.2f * e * size, 0.62f * size, -0.38f * size), Vector3.one * 0.26f * size, white);
            Prim(PrimitiveType.Sphere, t, new Vector3(0.2f * e * size, 0.62f * size, -0.48f * size), Vector3.one * 0.12f * size, black);
        }
        Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 1.05f * size, 0f), new Vector3(0.04f, 0.16f, 0.04f) * size, black);
        Prim(PrimitiveType.Sphere, t, new Vector3(0f, 1.24f * size, 0f), Vector3.one * 0.14f * size, Glow(new Color(2.4f, 2.0f, 0.4f)));
        return t;
    }

    void BuildRange(Transform r)
    {
        Color[] cols = { new Color(0.35f, 0.85f, 0.3f), new Color(0.65f, 0.4f, 0.9f), new Color(0.2f, 0.8f, 0.75f), new Color(0.95f, 0.55f, 0.75f), new Color(0.5f, 0.9f, 0.25f), new Color(0.4f, 0.55f, 0.95f), new Color(0.95f, 0.75f, 0.3f), new Color(0.9f, 0.4f, 0.4f) };
        var wall = Lit(new Color(0.3f, 0.32f, 0.36f));
        for (int i = 0; i < 5; i++) Prim(PrimitiveType.Cube, r, new Vector3(-20f + i * 10f, 0.6f, 14f + (i % 2) * 10f), new Vector3(4f, 1.2f, 0.5f), wall, true);
        for (int i = 0; i < cols.Length; i++)
        {
            var z = new ZrTarget { speed = 1.2f + 0.25f * i, wait = 0.5f + i * 0.3f };
            z.t = AlienToy(r, "Range alien " + (i + 1), cols[i], 1.15f);
            var sc = z.t.gameObject.AddComponent<SphereCollider>(); sc.center = new Vector3(0f, 0.6f, 0f); sc.radius = 0.75f;
            var tg = z.t.gameObject.AddComponent<HunchTarget>(); tg.kind = 32; tg.index = i;
            z.t.gameObject.SetActive(false); zrTargets.Add(z);
        }
    }

    void UpdateRange(float dt)
    {
        foreach (var z in zrTargets)
        {
            if (!z.up)
            {
                if (!mgOn) continue;
                z.wait -= dt; if (z.wait > 0f) continue;
                z.home = new Vector3(R(-24f, 24f), 0f, R(6f, 38f)); z.life = R(3.5f, 6f); z.phase = R(0f, 6f); z.up = true;
                z.t.localPosition = z.home; z.t.localScale = Vector3.one * 0.1f; z.t.gameObject.SetActive(true);
                continue;
            }
            z.life -= dt; z.phase += dt * z.speed;
            z.t.localPosition = z.home + new Vector3(Mathf.Sin(z.phase) * 2.6f, Mathf.Abs(Mathf.Sin(z.phase * 2.2f)) * 0.9f, 0f);
            z.t.localScale = Vector3.one * Mathf.MoveTowards(z.t.localScale.x, 1f, dt * 4f);
            Vector3 look = cam.position - z.t.position; look.y = 0f; if (look.sqrMagnitude > 0.1f) z.t.rotation = Quaternion.LookRotation(-look.normalized, Vector3.up);
            if (z.life <= 0f || !mgOn) { z.up = false; z.wait = R(0.4f, 1.4f); z.t.gameObject.SetActive(false); }
        }
    }

    void ZrHit(int i)
    {
        if (i < 0 || i >= zrTargets.Count) return; var z = zrTargets[i]; if (!z.up) return;
        Vector3 p = z.t.position; z.up = false; z.wait = R(0.4f, 1.2f); z.t.gameObject.SetActive(false);
        HunchFx.Spawn("teleport", p, 0.6f, 1.2f); HunchFx.Spawn("star hit", p + Vector3.up * 0.6f, 0.5f, 0.9f); TeleFx(p, 1f);
        zrHits++; zrCombo++; sentHome++; if (mgOn) mgScore += 10 + Mathf.Min(zrCombo, 10);
        Play(cPop, 0.7f); game.Buzz(0.6f, 0.1f);
    }

    // ------------------------------------------------------------------ 4. play park
    readonly List<Renderer> parkTiles = new List<Renderer>(); readonly List<Rigidbody> parkBalls = new List<Rigidbody>(); readonly List<Vector3> parkPads = new List<Vector3>();
    Material[] parkCols; float parkT, parkDance, parkBounce; int parkStep; public int parkBounces;

    void BuildPark(Transform r)
    {
        parkCols = new[] { Glow(new Color(1.6f, 0.3f, 0.9f)), Glow(new Color(0.3f, 0.9f, 1.8f)), Glow(new Color(1.7f, 1.3f, 0.25f)), Glow(new Color(0.4f, 1.6f, 0.5f)), Glow(new Color(0.8f, 0.5f, 1.9f)) };
        var floor = new GameObject("Dance floor").transform; floor.SetParent(r, false); floor.localPosition = new Vector3(0f, 0f, 10f);
        for (int ix = 0; ix < 5; ix++) for (int iz = 0; iz < 5; iz++) parkTiles.Add(Prim(PrimitiveType.Cube, floor, new Vector3((ix - 2) * 2.4f, 0.03f, (iz - 2) * 2.4f), new Vector3(2.3f, 0.06f, 2.3f), parkCols[(ix + iz) % 5]).GetComponent<Renderer>());
        mapLabels.Add(Label(floor, "<b>DANCE FLOOR</b>\n<size=60%>step on it!</size>", 0f, 4.2f, 6f, 12f, 2.4f, Color.white));
        var pad = Glow(new Color(1.8f, 0.5f, 1.2f));
        foreach (var p in new[] { new Vector3(-14f, 0f, 2f), new Vector3(14f, 0f, 2f), new Vector3(-14f, 0f, 20f), new Vector3(14f, 0f, 20f) })
        {
            Prim(PrimitiveType.Cylinder, r, p + Vector3.up * 0.05f, new Vector3(3.2f, 0.05f, 3.2f), pad); parkPads.Add(p);
            var lab = Label(r, "<b>BOUNCE</b>", 0f, 0f, 4f, 6f, 1f, Color.white); lab.transform.localPosition = p + Vector3.up * 2.2f; mapLabels.Add(lab);
        }
        Color[] bc = { new Color(0.95f, 0.3f, 0.3f), new Color(0.3f, 0.6f, 0.95f), new Color(0.95f, 0.8f, 0.25f), new Color(0.4f, 0.9f, 0.5f), new Color(0.8f, 0.5f, 0.95f), new Color(0.95f, 0.6f, 0.3f) };
        for (int i = 0; i < bc.Length; i++)
        {
            float s = 1.0f + 0.25f * (i % 3);
            var g = Prim(PrimitiveType.Sphere, r, new Vector3(-10f + i * 4f, s * 0.5f + 0.1f, -4f + (i % 2) * 3f), Vector3.one * s, Lit(bc[i]), true); g.name = "Moon ball";
            var rb = g.AddComponent<Rigidbody>(); rb.mass = 1.5f; rb.linearDamping = 0.25f; rb.angularDamping = 0.3f; parkBalls.Add(rb);
        }
    }

    void UpdatePark(float dt, Vector3 ml)
    {
        parkT += dt;
        if (parkT > 0.47f) { parkT = 0f; parkStep++; for (int i = 0; i < parkTiles.Count; i++) parkTiles[i].sharedMaterial = parkCols[(i * 7 + parkStep * 3 + i / 5) % parkCols.Length]; }
        bool onFloor = Mathf.Abs(ml.x) < 6f && Mathf.Abs(ml.z - 10f) < 6f && ml.y < 1f;
        if (onFloor && Time.time > parkDance)
        {
            parkDance = Time.time + 9.5f; Dance();
            Fx2("confetti_blast_multicolor", "star hit", mapC[4] + new Vector3(0f, 2.5f, 10f), 1f, 0.6f, 2f);
        }
        if (Time.time > parkBounce && ml.y < 0.6f)
            foreach (var p in parkPads)
                if (new Vector2(ml.x - p.x, ml.z - p.z).sqrMagnitude < 1.5f * 1.5f) { parkBounce = Time.time + 1.0f; parkBounces++; LaunchPlayer(5.2f); Play(cPop, 0.8f); game.Buzz(0.7f, 0.15f); Note("Boing! Moon gravity is one sixth of Earth's."); break; }
        Vector3 f = Feet();
        foreach (var rb in parkBalls)
        {
            Vector3 d = rb.position - f; float reach = rb.transform.localScale.x * 0.5f + 0.55f;
            if (d.y < 1.6f && new Vector2(d.x, d.z).sqrMagnitude < reach * reach) { d.y = 0f; rb.linearVelocity = d.normalized * 4.5f + Vector3.up * 1.8f; }
            if (rb.position.y < mapC[4].y - 3f) { rb.position = mapC[4] + new Vector3(0f, 2f, 0f); rb.linearVelocity = Vector3.zero; }
        }
    }

    void LaunchPlayer(float v)
    {
        if (desk && desk.active) desk.Launch(v);
        else { var vr = GetComponent<HunchVr>(); if (vr) vr.Launch(v); }
    }

    // ------------------------------------------------------------------ 5. Beat Asteroid
    class BeatRock { public Transform t; public Renderer band; public bool live; public float hitAt; public int lane, colour; }
    readonly List<BeatRock> beatRocks = new List<BeatRock>(); Transform saberL, saberR, leftCtrl; Material beatBlue, beatGold;
    int beatNext, beatCombo, beatHits, beatMiss, beatBestCombo; float beatClock;
    const float BeatSec = 60f / 128f, BeatFly = 2.2f, BeatFar = 46f, BeatHitZ = 0.9f;
    static readonly Vector2[] BeatLane = { new Vector2(-0.65f, 1.05f), new Vector2(-0.65f, 1.6f), new Vector2(0.65f, 1.05f), new Vector2(0.65f, 1.6f) };

    void BuildBeat(Transform r)
    {
        beatBlue = Glow(new Color(0.3f, 0.8f, 2.4f)); beatGold = Glow(new Color(2.2f, 1.5f, 0.3f));
        var rock = Lit(new Color(0.36f, 0.34f, 0.33f));
        Prim(PrimitiveType.Cylinder, r, new Vector3(0f, 0.03f, 0f), new Vector3(2.2f, 0.03f, 2.2f), Glow(new Color(0.4f, 1.5f, 0.7f)));
        for (int s = -1; s <= 1; s += 2) Prim(PrimitiveType.Cube, r, new Vector3(1.6f * s, 0.02f, 23f), new Vector3(0.1f, 0.04f, 46f), s < 0 ? beatBlue : beatGold);
        Prim(PrimitiveType.Cube, r, new Vector3(0f, 0.02f, BeatHitZ), new Vector3(3.4f, 0.04f, 0.12f), Glow(new Color(1.8f, 1.8f, 1.8f)));
        for (int i = 0; i < 28; i++)
        {
            var b = new BeatRock(); b.t = new GameObject("Beat asteroid").transform; b.t.SetParent(r, false);
            Prim(PrimitiveType.Sphere, b.t, Vector3.zero, new Vector3(0.62f, 0.52f, 0.58f), rock);
            Prim(PrimitiveType.Sphere, b.t, new Vector3(0.18f, 0.12f, 0.1f), new Vector3(0.34f, 0.3f, 0.32f), rock);
            b.band = Prim(PrimitiveType.Cube, b.t, Vector3.zero, new Vector3(0.7f, 0.1f, 0.7f), beatBlue).GetComponent<Renderer>();
            b.t.gameObject.SetActive(false); beatRocks.Add(b);
        }
        var sign = Label(r, "<b>BEAT ASTEROID</b>\n<size=55%>Break each asteroid as it reaches the white line.  Blue = left,  gold = right.</size>", 0f, 0f, 5f, 30f, 4f, Color.white);
        sign.transform.localPosition = new Vector3(0f, 6.5f, 30f);
    }

    void BeatReset()
    {
        beatNext = 8; beatClock = 0f; beatCombo = beatHits = beatMiss = beatBestCombo = 0;
        foreach (var b in beatRocks) { b.live = false; b.t.gameObject.SetActive(false); }
        if (game.HeadsetActive)
        {
            if (!leftCtrl) foreach (var t in origin.GetComponentsInChildren<Transform>(true)) if (t.name == "Left Controller") leftCtrl = t;
            if (!saberL && leftCtrl) saberL = Saber(leftCtrl, beatBlue);
            if (!saberR && rightCtrl) saberR = Saber(rightCtrl, beatGold);
            if (saberL) saberL.gameObject.SetActive(true); if (saberR) saberR.gameObject.SetActive(true);
        }
    }
    Transform Saber(Transform hand, Material m)
    {
        var s = new GameObject("Light stick").transform; s.SetParent(hand, false);
        Prim(PrimitiveType.Cylinder, s, new Vector3(0f, 0f, 0.6f), new Vector3(0.035f, 0.55f, 0.035f), m).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        return s;
    }

    void BeatSpawn(float hitAt, int lane)
    {
        foreach (var b in beatRocks)
        {
            if (b.live) continue;
            b.live = true; b.hitAt = hitAt; b.lane = lane; b.colour = lane < 2 ? 0 : 1; b.band.sharedMaterial = b.colour == 0 ? beatBlue : beatGold;
            b.t.localPosition = new Vector3(BeatLane[lane].x, BeatLane[lane].y, BeatFar); b.t.gameObject.SetActive(true); return;
        }
    }

    void BeatBreak(BeatRock b, int hand, float early)
    {
        float err = Mathf.Abs(early); int pts = err < 0.14f ? 100 : (err < 0.35f ? 70 : 40);
        if (hand >= 0 && hand != b.colour) pts /= 2;
        beatCombo++; beatBestCombo = Mathf.Max(beatBestCombo, beatCombo); beatHits++; mgScore += pts + Mathf.Min(beatCombo, 20) * 2;
        Vector3 p = b.t.position; b.live = false; b.t.gameObject.SetActive(false);
        HunchFx.Spawn("stones hit", p, 0.5f, 0.7f); HunchFx.Spawn("star hit", p, 0.4f, 0.6f);
        Play(cPop, 0.6f); game.Buzz(0.7f, 0.06f);
        if (pts >= 100) Note("PERFECT!   combo " + beatCombo);
    }

    void UpdateBeat(float dt)
    {
        if (!mgOn) return;
        beatClock += dt; float speed = (BeatFar - BeatHitZ) / BeatFly;
        // new asteroids, on the beat
        while (beatNext * BeatSec - BeatFly <= beatClock && beatNext * BeatSec < MgLen[5] - 1.5f)
        {
            int n = beatNext; beatNext++;
            bool play = n < 24 ? n % 2 == 0 : true;
            if (!play) continue;
            int lane = rng.Next(4); BeatSpawn(n * BeatSec, lane);
            if (n >= 72 && n % 4 == 0) BeatSpawn(n * BeatSec, lane < 2 ? 2 + rng.Next(2) : rng.Next(2));
        }
        bool vr = game.HeadsetActive; var mouse = Mouse.current;
        int click = -1;
        if (!vr && !autoOn && mouse != null && Cursor.lockState == CursorLockMode.Locked) { if (mouse.leftButton.wasPressedThisFrame) click = 0; else if (mouse.rightButton.wasPressedThisFrame) click = 1; }
        BeatRock aimed = null; float aimedAlong = 1e9f;
        foreach (var b in beatRocks)
        {
            if (!b.live) continue;
            float early = b.hitAt - beatClock; float z = BeatHitZ + early * speed;
            b.t.localPosition = new Vector3(BeatLane[b.lane].x, BeatLane[b.lane].y, z); b.t.Rotate(40f * dt, 70f * dt, 0f, Space.Self);
            if (z < -2.5f) { b.live = false; b.t.gameObject.SetActive(false); beatMiss++; beatCombo = 0; continue; }
            if (autoOn) { if (early < 0.02f) { if (rng.Next(100) < 93) BeatBreak(b, b.colour, early); else b.hitAt -= 10f; } continue; }
            if (vr)
            {
                if (z > 3.2f) continue;
                Vector3 p = b.t.position;
                if (saberL && SegDist(p, leftCtrl.position, leftCtrl.position + leftCtrl.forward * 1.2f) < 0.5f) { BeatBreak(b, 0, early); continue; }
                if (saberR && SegDist(p, rightCtrl.position, rightCtrl.position + rightCtrl.forward * 1.2f) < 0.5f) { BeatBreak(b, 1, early); continue; }
            }
            else if (click >= 0 && z < 16f)
            {
                Vector3 to = b.t.position - cam.position; float along = Vector3.Dot(to, cam.forward), off = (to - cam.forward * along).magnitude;
                if (along > 0f && off < 0.7f + along * 0.05f && along < aimedAlong) { aimed = b; aimedAlong = along; }
            }
        }
        if (aimed != null) BeatBreak(aimed, click, aimed.hitAt - beatClock);
        else if (click >= 0) { ShowBeam(cam.position + cam.forward * 0.5f - cam.up * 0.2f, cam.position + cam.forward * 30f, click == 0 ? beamCyan : beamGreen); }
    }
    static float SegDist(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a; float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-5f, ab.sqrMagnitude)); return Vector3.Distance(p, a + ab * t);
    }

    // ------------------------------------------------------------------ 6. tower defense (the aliens are beamed home, nobody is hurt)
    class TdFoe { public Transform t; public bool live; public float s, hp, speed, hop; }
    class TdTower { public Transform body, head; public int lvl; public float cool, beamT; public LineRenderer beam; public TextMeshPro label; public Vector3 pos; }
    readonly List<TdFoe> tdFoes = new List<TdFoe>(); readonly List<TdTower> tdTowers = new List<TdTower>(); readonly List<Transform> tdSnackBalls = new List<Transform>();
    Vector3[] tdPath; float tdTotal, tdSpawnT, tdPause; int tdWave, tdToSpawn, tdEnergy, tdSnacks, tdHome; const int TdWaves = 5;
    static readonly int[] TdCost = { 40, 60, 90 };

    void BuildTd(Transform r)
    {
        tdPath = new[] { new Vector3(-34f, 0f, 38f), new Vector3(-34f, 0f, 24f), new Vector3(22f, 0f, 24f), new Vector3(22f, 0f, 12f), new Vector3(-22f, 0f, 12f), new Vector3(-22f, 0f, 0f), new Vector3(10f, 0f, 0f) };
        var road = Glow(new Color(0.5f, 0.38f, 0.12f)); tdTotal = 0f;
        for (int i = 0; i + 1 < tdPath.Length; i++)
        {
            Vector3 a = tdPath[i], b = tdPath[i + 1]; float len = Vector3.Distance(a, b); tdTotal += len;
            var seg = Prim(PrimitiveType.Cube, r, (a + b) * 0.5f + Vector3.up * 0.02f, new Vector3(2.4f, 0.04f, len + 2.4f), road).transform; seg.localRotation = Quaternion.LookRotation(b - a, Vector3.up);
        }
        // the snack table at the end of the path
        Prim(PrimitiveType.Cube, r, new Vector3(13f, 0.5f, 0f), new Vector3(3f, 1f, 2f), Lit(new Color(0.55f, 0.57f, 0.62f)));
        for (int i = 0; i < 10; i++) tdSnackBalls.Add(Prim(PrimitiveType.Sphere, r, new Vector3(12f + (i % 5) * 0.5f, 1.2f, -0.4f + (i / 5) * 0.8f), Vector3.one * 0.36f, Glow(new Color(1.8f, 0.9f, 0.3f))).transform);
        mapLabels.Add(Label(r, "<b>SNACK TABLE</b>", 13f, 2.6f, 4f, 8f, 1f, Color.white)); mapLabels[mapLabels.Count - 1].transform.localPosition = new Vector3(13f, 2.6f, 0f);
        var padMat = Glow(new Color(0.25f, 0.9f, 0.55f)); var metal = Lit(new Color(0.55f, 0.57f, 0.62f)); var orb = Glow(new Color(0.5f, 2.2f, 0.9f));
        Vector2[] pads = { new Vector2(-30f, 31f), new Vector2(-14f, 28f), new Vector2(6f, 28f), new Vector2(26f, 18f), new Vector2(4f, 17f), new Vector2(-12f, 8f), new Vector2(-26f, 6f), new Vector2(-6f, 4f) };
        for (int i = 0; i < pads.Length; i++)
        {
            var tw = new TdTower { pos = new Vector3(pads[i].x, 0f, pads[i].y) };
            var p = new GameObject("Tower pad " + (i + 1)).transform; p.SetParent(r, false); p.localPosition = tw.pos;
            Prim(PrimitiveType.Cylinder, p, new Vector3(0f, 0.04f, 0f), new Vector3(2.6f, 0.04f, 2.6f), padMat);
            tw.body = Prim(PrimitiveType.Cylinder, p, new Vector3(0f, 1.0f, 0f), new Vector3(0.9f, 1.0f, 0.9f), metal).transform;
            tw.head = Prim(PrimitiveType.Sphere, p, new Vector3(0f, 2.4f, 0f), Vector3.one * 0.9f, orb).transform;
            tw.beam = Line("Tower beam", beamGreen, 0.12f, r); tw.beam.enabled = false;
            tw.label = Label(p, "", 0f, 3.6f, 3.4f, 8f, 1.4f, Color.white); mapLabels.Add(tw.label);
            var hb = new GameObject("Pad hit box"); hb.transform.SetParent(p, false); var bc = hb.AddComponent<BoxCollider>(); bc.center = new Vector3(0f, 1.4f, 0f); bc.size = new Vector3(2.8f, 2.8f, 2.8f);
            var tg = hb.AddComponent<HunchTarget>(); tg.kind = 33; tg.index = i;
            tdTowers.Add(tw);
        }
        Color[] cols = { new Color(0.35f, 0.85f, 0.3f), new Color(0.65f, 0.4f, 0.9f), new Color(0.2f, 0.8f, 0.75f), new Color(0.95f, 0.55f, 0.75f) };
        for (int i = 0; i < 26; i++) { var f = new TdFoe(); f.t = AlienToy(r, "Path alien", cols[i % cols.Length], 1.0f); f.t.gameObject.SetActive(false); tdFoes.Add(f); }
    }

    void TdReset()
    {
        tdWave = 0; tdToSpawn = 0; tdEnergy = 120; tdSnacks = 10; tdHome = 0; tdPause = 1f; tdSpawnT = 0f;
        foreach (var f in tdFoes) { f.live = false; f.t.gameObject.SetActive(false); }
        foreach (var t in tdTowers) { t.lvl = 0; t.cool = 0f; t.beam.enabled = false; }
        TdShow();
    }
    void TdShow()
    {
        foreach (var t in tdTowers)
        {
            t.body.gameObject.SetActive(t.lvl > 0); t.head.gameObject.SetActive(t.lvl > 0);
            t.head.localScale = Vector3.one * (0.7f + 0.25f * t.lvl); t.body.localScale = new Vector3(0.7f + 0.2f * t.lvl, 1.0f, 0.7f + 0.2f * t.lvl);
            t.label.text = t.lvl == 0 ? "<b>Empty pad</b>\n<size=70%>build: " + TdCost[0] + "</size>" : (t.lvl < 3 ? "<b>Tower level " + t.lvl + "</b>\n<size=70%>stronger: " + TdCost[t.lvl] + "</size>" : "<b>Tower level 3</b>\n<size=70%>best</size>");
        }
        for (int i = 0; i < tdSnackBalls.Count; i++) tdSnackBalls[i].gameObject.SetActive(i < tdSnacks);
    }

    void TdPad(int i)
    {
        if (i < 0 || i >= tdTowers.Count) return; var t = tdTowers[i];
        if (t.lvl >= 3) { Note("This tower is already the best."); return; }
        int cost = TdCost[t.lvl];
        if (tdEnergy < cost) { Note("Not enough energy: " + tdEnergy + " of " + cost + ". Sending aliens home earns energy."); Play(cBoop, 0.5f); return; }
        tdEnergy -= cost; t.lvl++; TdShow(); Play(cPick, 0.8f); game.Buzz(0.6f, 0.1f);
        HunchFx.Spawn("star hit", mapC[6] + t.pos + Vector3.up * 1.5f, 0.6f, 0.8f);
    }
    string TdPadText(int i)
    {
        if (i < 0 || i >= tdTowers.Count) return ""; var t = tdTowers[i];
        return t.lvl == 0 ? "build a beam tower (" + TdCost[0] + " energy, you have " + tdEnergy + ")" : (t.lvl < 3 ? "make this tower stronger (" + TdCost[t.lvl] + " energy, you have " + tdEnergy + ")" : "tower level 3 (best)");
    }
    Vector3 TdPoint(float s)
    {
        for (int i = 0; i + 1 < tdPath.Length; i++)
        {
            float len = Vector3.Distance(tdPath[i], tdPath[i + 1]);
            if (s <= len) return Vector3.Lerp(tdPath[i], tdPath[i + 1], s / len);
            s -= len;
        }
        return tdPath[tdPath.Length - 1];
    }

    void UpdateTd(float dt)
    {
        foreach (var t in tdTowers) if (t.beam.enabled) { t.beamT -= dt; if (t.beamT <= 0f) t.beam.enabled = false; }
        if (!mgOn) return;
        // the autopilot builds and upgrades whenever it can afford to
        if (autoOn) for (int i = 0; i < tdTowers.Count; i++) { int k = (i * 3) % tdTowers.Count; if (tdTowers[k].lvl < 3 && tdEnergy >= TdCost[tdTowers[k].lvl] && (tdTowers[k].lvl == 0 || tdEnergy >= 100)) { TdPad(k); break; } }
        int alive = 0; foreach (var f in tdFoes) if (f.live) alive++;
        if (tdToSpawn <= 0 && alive == 0)
        {
            tdPause -= dt;
            if (tdPause <= 0f)
            {
                if (tdWave >= TdWaves) { mgScore = tdHome * 10 + tdSnacks * 30; MgEnd("Every alien was sent home!"); return; }
                tdToSpawn = 6 + tdWave * 2; tdSpawnT = 0f; tdPause = 5f;
                Note("Wave " + (tdWave + 1) + " of " + TdWaves + " is coming!");
            }
        }
        if (tdToSpawn > 0)
        {
            tdSpawnT -= dt;
            if (tdSpawnT <= 0f)
            {
                tdSpawnT = 1.3f;
                foreach (var f in tdFoes) if (!f.live) { f.live = true; f.s = 0f; f.hp = 2f + tdWave * 1.3f; f.speed = 4f + tdWave * 0.4f; f.hop = R(0f, 6f); f.t.localPosition = tdPath[0]; f.t.gameObject.SetActive(true); break; }
                tdToSpawn--; if (tdToSpawn == 0) tdWave++;
            }
        }
        foreach (var f in tdFoes)
        {
            if (!f.live) continue;
            f.s += f.speed * dt; f.hop += dt * 7f;
            Vector3 p = TdPoint(f.s), ahead = TdPoint(f.s + 0.5f); f.t.localPosition = p + Vector3.up * Mathf.Abs(Mathf.Sin(f.hop)) * 0.45f;
            Vector3 dir = ahead - p; if (dir.sqrMagnitude > 0.001f) f.t.localRotation = Quaternion.LookRotation(-dir.normalized, Vector3.up);
            if (f.s >= tdTotal)
            {
                f.live = false; f.t.gameObject.SetActive(false); tdSnacks = Mathf.Max(0, tdSnacks - 1); TdShow(); Play(cBoop, 0.6f);
                Note("An alien took a snack! Snacks left: " + tdSnacks);
                if (tdSnacks <= 0) { mgScore = tdHome * 10; MgEnd("The aliens got all the snacks. Try again!"); return; }
            }
        }
        foreach (var t in tdTowers)
        {
            if (t.lvl <= 0) continue;
            t.cool -= dt; t.head.Rotate(0f, 90f * dt, 0f);
            if (t.cool > 0f) continue;
            float range = 12f + 2f * t.lvl; TdFoe best = null;
            foreach (var f in tdFoes) if (f.live && (f.t.localPosition - t.pos).sqrMagnitude < range * range && (best == null || f.s > best.s)) best = f;
            if (best == null) continue;
            t.cool = 0.7f; best.hp -= t.lvl;
            t.beam.SetPosition(0, mapC[6] + t.pos + Vector3.up * 2.4f); t.beam.SetPosition(1, best.t.position + Vector3.up * 0.5f); t.beam.enabled = true; t.beamT = 0.12f;
            if ((cam.position - best.t.position).sqrMagnitude < 60f * 60f) Play(cZap, 0.12f);
            if (best.hp <= 0f)
            {
                Vector3 p = best.t.position; best.live = false; best.t.gameObject.SetActive(false);
                HunchFx.Spawn("teleport", p, 0.5f, 1.0f); tdHome++; sentHome++; tdEnergy += 8; mgScore = tdHome * 10;
            }
        }
    }

    // ------------------------------------------------------------------ a richer mining field on the central map: deep veins worked in stages, and crystal geodes
    void BuildRichNodes()
    {
        if (!fieldOk) return;
        var oreLit = Lit(new Color(0.42f, 0.36f, 0.32f)); var oreGlow = Glow(new Color(2.2f, 1.0f, 0.25f)); var cryGlow = Glow(new Color(1.3f, 0.5f, 1.9f)); var cryLit = Lit(new Color(0.5f, 0.3f, 0.7f));
        var holder = new GameObject("Deep veins").transform; holder.SetParent(root, false);
        for (int c = 0; c < 4; c++)
        {
            Vector3 centre; if (!RandomGround(40f, 66f, out centre)) continue;
            for (int k = 0; k < 4; k++)
            {
                Vector3 p; float a = (k * 90f + c * 30f) * Mathf.Deg2Rad;
                if (k == 0) p = centre; else if (!GroundAt(centre.x + Mathf.Sin(a) * 3.2f, centre.z + Mathf.Cos(a) * 3.2f, out p)) continue;
                bool vein = k == 0, geode = k == 2; int item = vein ? Ore : (geode ? Crystal : (k == 1 ? He3 : Ice));
                var n = new Node { item = item, up = true, maxHp = vein ? 4f : (geode ? 5f : 3f), stagesMax = vein ? 3 : (geode ? 2 : 1), yield = vein ? 3 : (geode ? 1 : 0) };
                n.hp = n.maxHp; n.stages = n.stagesMax;
                n.t = new GameObject(vein ? "Deep ore vein" : (geode ? "Crystal geode" : ItemNames[item] + " node")).transform; n.t.SetParent(holder, false); n.t.position = p; n.t.rotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
                if (vein)
                {
                    for (int j = 0; j < 7; j++) Prim(PrimitiveType.Sphere, n.t, new Vector3(R(-0.9f, 0.9f), R(0.2f, 0.9f), R(-0.9f, 0.9f)), new Vector3(R(0.9f, 1.5f), R(0.8f, 1.3f), R(0.9f, 1.5f)), oreLit);
                    for (int j = 0; j < 10; j++) Prim(PrimitiveType.Cube, n.t, new Vector3(R(-1.0f, 1.0f), R(0.5f, 1.5f), R(-1.0f, 1.0f)), Vector3.one * R(0.1f, 0.2f), oreGlow).transform.localRotation = Quaternion.Euler(R(0f, 90f), R(0f, 90f), 0f);
                }
                else if (geode)
                {
                    Prim(PrimitiveType.Sphere, n.t, new Vector3(0f, 0.3f, 0f), new Vector3(1.3f, 0.8f, 1.3f), cryLit);
                    for (int j = 0; j < 7; j++) { var s = Prim(PrimitiveType.Cube, n.t, new Vector3(R(-0.4f, 0.4f), R(0.5f, 0.9f), R(-0.4f, 0.4f)), new Vector3(0.16f, R(0.5f, 1.1f), 0.16f), cryGlow).transform; s.localRotation = Quaternion.Euler(R(-30f, 30f), R(0f, 90f), R(-30f, 30f)); }
                }
                else
                {
                    var m = item == He3 ? Glow(new Color(1.6f, 2.4f, 0.4f)) : Glow(new Color(0.5f, 1.5f, 2.2f));
                    for (int j = 0; j < 5; j++) Prim(PrimitiveType.Cube, n.t, new Vector3(R(-0.4f, 0.4f), R(0.3f, 0.6f), R(-0.4f, 0.4f)), new Vector3(0.24f, R(0.6f, 1.2f), 0.24f), m).transform.localRotation = Quaternion.Euler(R(-22f, 22f), R(0f, 90f), R(-22f, 22f));
                }
                var sc = n.t.gameObject.AddComponent<SphereCollider>(); sc.center = new Vector3(0f, vein ? 0.8f : 0.5f, 0f); sc.radius = vein ? 1.5f : 0.9f; n.col = sc;
                var tg = n.t.gameObject.AddComponent<HunchTarget>(); tg.kind = 2; tg.index = nodes.Count;
                nodes.Add(n);
                if (vein) mapLabels.Add(Label(n.t, "<b>DEEP VEIN</b>\n<size=60%>drill it in 3 stages</size>", 0f, 3.2f, 4f, 8f, 1.6f, new Color(1f, 0.84f, 0.3f)));
            }
        }
    }
}
