// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/06_Frontier/B_Maps_Minigames/HunchFrontier9.cs
//  Section : 06_Frontier/B_Maps_Minigames
//  Folder  : Extra maps and mini-games: solar farm, mining pit, Factory Land, Star Patrol, crew
//            radio + Star Clicker, Science Academy, My Astronaut, model kits, Fun Zone, endless
//            ground, Star Store pages.
//  Purpose : Ninth part of Moon Frontier: STAR PATROL (map 8) fly a star fighter in open space high
//            above the Moon. Alien scout ships circle the station;
//  Contains: HunchFrontier, StarShip, StoryNode
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

/// Ninth part of Moon Frontier:
///   STAR PATROL (map 8)   fly a star fighter in open space high above the Moon. Alien scout ships circle the station;
///                          zap them and they warp home (nobody is hurt). Then the Dust Titan arrives: a giant, quiet creature
///                          wrapped in rock. It cannot be beaten, only turned away, by lighting its five beacons.
///   STORY STUDIO (map 9)  a wall to paint on, a choose-your-path story (Cadet Nova and the Dust Titan), a shelf of alien
///                          stories to read, and a rack of light swords with training orbs to slice.
/// The stories, names and creatures are original to this game.
public partial class HunchFrontier
{
    // ================================================================== STAR PATROL
    class StarShip { public Transform t; public float hp, ang, radius, height, speed, respawn, bob; public bool up; }
    readonly List<StarShip> starShips = new List<StarShip>(); readonly List<Transform> starWing = new List<Transform>(), starRocks = new List<Transform>(), starBeacons = new List<Transform>();
    Transform starCockpit, starHolder, starTitan; bool[] starLit = new bool[5]; int starHome, starPhase, starLitCount; float starTitanZ, starShotT; Material starBeaconOff, starBeaconOn;
    Vector3 StarC { get { return mapC[8] + Vector3.up * 400f; } }
    const int StarWave = 8;

    void BuildStar(Transform r)
    {
        BuildExtraSounds();
        starHolder = new GameObject("Space above the Moon").transform; starHolder.SetParent(r, false); starHolder.localPosition = new Vector3(0f, 400f, 0f);
        var silver = Lit(new Color(0.8f, 0.82f, 0.86f)); var gold = Lit(new Color(0.95f, 0.74f, 0.22f)); var blue = Glow(new Color(0.3f, 0.9f, 2.2f)); var dark = Lit(new Color(0.14f, 0.13f, 0.16f));
        // the station you are guarding
        var st = new GameObject("Relay station").transform; st.SetParent(starHolder, false);
        Prim(PrimitiveType.Sphere, st, Vector3.zero, Vector3.one * 9f, silver); Prim(PrimitiveType.Cylinder, st, Vector3.zero, new Vector3(22f, 0.6f, 22f), gold); Prim(PrimitiveType.Cylinder, st, Vector3.zero, new Vector3(23f, 0.2f, 23f), blue);
        for (int e = -1; e <= 1; e += 2) { Prim(PrimitiveType.Cube, st, new Vector3(e * 19f, 0f, 0f), new Vector3(12f, 0.2f, 6f), Glow(new Color(0.2f, 0.4f, 1.2f))); Prim(PrimitiveType.Cube, st, new Vector3(e * 11f, 0f, 0f), new Vector3(6f, 0.4f, 0.4f), silver); }
        mapLabels.Add(Label(st, "<b>RELAY STATION</b>\n<size=60%>keep the sky clear around it</size>", 0f, 9f, 14f, 60f, 8f, new Color(1f, 0.84f, 0.3f)));
        // drifting rocks give a sense of speed
        for (int i = 0; i < 46; i++)
        {
            Vector3 p = UnityEngine.Random.onUnitSphere * R(70f, 300f); p.y *= 0.45f;
            var rk = Prim(PrimitiveType.Sphere, starHolder, p, new Vector3(R(2f, 9f), R(2f, 7f), R(2f, 9f)), dark).transform; rk.localRotation = UnityEngine.Random.rotation; starRocks.Add(rk);
        }
        // alien scout ships
        for (int i = 0; i < 8; i++)
        {
            var s = new StarShip { radius = 70f + i * 16f, height = -30f + (i * 37) % 70, speed = (i % 2 == 0 ? 1f : -1f) * (16f + i * 2.2f), ang = i * 0.8f, bob = i * 1.7f };
            s.t = new GameObject("Alien scout " + (i + 1)).transform; s.t.SetParent(starHolder, false);
            var model = DriveModel(i % 2 == 0 ? "UfoNeon" : "UfoStars", s.t, 12f, true);
            if (!model)
            {
                Prim(PrimitiveType.Sphere, s.t, Vector3.zero, new Vector3(10f, 2.4f, 10f), Lit(new Color(0.45f, 0.7f, 0.5f))); Prim(PrimitiveType.Sphere, s.t, new Vector3(0f, 1.6f, 0f), new Vector3(4.5f, 3.2f, 4.5f), Clear(new Color(0.5f, 1f, 0.9f, 0.5f)));
            }
            Prim(PrimitiveType.Sphere, s.t, new Vector3(0f, -1.2f, 0f), Vector3.one * 2.2f, Glow(new Color(0.6f, 2.4f, 0.8f)));
            var sc = s.t.gameObject.AddComponent<SphereCollider>(); sc.radius = 6.5f;
            var tg = s.t.gameObject.AddComponent<HunchTarget>(); tg.kind = 36; tg.index = i;
            starShips.Add(s);
        }
        // friendly wingmen: the ships from the 3D model collection fly beside you
        string[] wing = { "RedShip", "FuturisticPlane", "BlueSupersonicJet" };
        for (int i = 0; i < wing.Length; i++) { var h = new GameObject("Wingman " + wing[i]).transform; h.SetParent(starHolder, false); if (DriveModel(wing[i], h, 13f, true)) starWing.Add(h); else Destroy(h.gameObject); }
        // the Dust Titan: a giant shape inside a cloud of circling rock, with five beacons
        starTitan = new GameObject("Dust Titan").transform; starTitan.SetParent(starHolder, false);
        Prim(PrimitiveType.Sphere, starTitan, Vector3.zero, Vector3.one * 70f, Lit(new Color(0.2f, 0.14f, 0.26f)));
        Prim(PrimitiveType.Sphere, starTitan, new Vector3(0f, 6f, -32f), new Vector3(16f, 9f, 6f), Glow(new Color(1.6f, 0.5f, 2.2f)));      // one great eye
        for (int i = 0; i < 26; i++) { float a = i * 0.47f, rr = 44f + (i % 5) * 5f; Prim(PrimitiveType.Sphere, starTitan, new Vector3(Mathf.Sin(a) * rr, Mathf.Sin(i * 1.3f) * 22f, Mathf.Cos(a) * rr), Vector3.one * R(5f, 12f), dark); }
        starBeaconOff = Glow(new Color(2.2f, 0.4f, 0.3f)); starBeaconOn = Glow(new Color(0.4f, 2.2f, 0.7f));
        for (int i = 0; i < 5; i++)
        {
            float a = i * 1.257f; var b = Prim(PrimitiveType.Sphere, starTitan, new Vector3(Mathf.Sin(a) * 40f, Mathf.Cos(a * 2f) * 16f, -30f - Mathf.Abs(Mathf.Cos(a)) * 14f), Vector3.one * 9f, starBeaconOff, true);
            b.name = "Titan beacon"; var tg = b.AddComponent<HunchTarget>(); tg.kind = 36; tg.index = 100 + i; starBeacons.Add(b.transform);
        }
        mapLabels.Add(Label(starTitan, "<b>THE DUST TITAN</b>\n<size=60%>zap its five red beacons green and it will turn away</size>", 0f, 46f, 40f, 160f, 20f, new Color(1f, 0.7f, 1f)));
        starTitan.gameObject.SetActive(false);
        // gates beside the arrival point, up here in space
        Gate(r, new Vector3(-10f, 400f, -72f), 0, "BACK TO BASE", new Color(0.3f, 1.5f, 0.6f)); Gate(r, new Vector3(10f, 400f, -72f), 108, "PLAY AGAIN", new Color(1.6f, 1.1f, 0.25f));
        // the cockpit: the nose, wings and cannons of your star fighter round the camera
        starCockpit = new GameObject("Star fighter cockpit").transform; starCockpit.SetParent(root, false);
        var hull = Lit(new Color(0.75f, 0.77f, 0.82f)); var trim = Lit(new Color(0.85f, 0.2f, 0.15f));
        Prim(PrimitiveType.Cube, starCockpit, new Vector3(0f, -0.62f, 1.7f), new Vector3(0.55f, 0.16f, 2.4f), hull); Prim(PrimitiveType.Cube, starCockpit, new Vector3(0f, -0.55f, 2.95f), new Vector3(0.3f, 0.1f, 0.5f), trim);
        for (int e = -1; e <= 1; e += 2)
        {
            Prim(PrimitiveType.Cube, starCockpit, new Vector3(e * 1.35f, -0.7f, 1.0f), new Vector3(1.9f, 0.06f, 1.0f), hull).transform.localRotation = Quaternion.Euler(0f, e * -14f, e * 5f);
            Prim(PrimitiveType.Cylinder, starCockpit, new Vector3(e * 2.1f, -0.66f, 1.6f), new Vector3(0.12f, 0.7f, 0.12f), trim).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Prim(PrimitiveType.Sphere, starCockpit, new Vector3(e * 2.1f, -0.66f, 2.32f), Vector3.one * 0.16f, Glow(new Color(0.6f, 2.6f, 0.8f)));
        }
        Prim(PrimitiveType.Cube, starCockpit, new Vector3(0f, -0.5f, 0.72f), new Vector3(1.1f, 0.03f, 0.3f), Glow(new Color(0.2f, 0.7f, 1.6f)));
        foreach (var rr in starCockpit.GetComponentsInChildren<Renderer>()) rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        starCockpit.gameObject.SetActive(false);
        A("Star pilot", "Send eight alien scouts home in Star Patrol.", () => starHome >= StarWave);
        A("Titan whisperer", "Turn the Dust Titan away by lighting all five beacons.", () => starPhase == 3);
    }

    void StarReset()
    {
        starHome = 0; starPhase = 1; starLitCount = 0; starTitanZ = 520f;
        for (int i = 0; i < 5; i++) { starLit[i] = false; if (i < starBeacons.Count) starBeacons[i].GetComponent<Renderer>().sharedMaterial = starBeaconOff; }
        if (starTitan) { starTitan.gameObject.SetActive(false); starTitan.localScale = Vector3.one; }
        for (int i = 0; i < starShips.Count; i++) { var s = starShips[i]; s.up = true; s.hp = 2f; s.respawn = 0f; s.t.gameObject.SetActive(true); s.t.localScale = Vector3.one; }
        game.flyMode = true; tool = Zapper;
    }

    string StarText(int i) { return i >= 100 ? "zap the beacon" : "zap the alien scout: it warps home"; }

    void StarHit(int i)
    {
        if (mapNow != 8) return;
        if (i >= 100)
        {
            int k = i - 100; if (starPhase != 2 || k < 0 || k >= 5 || starLit[k]) return;
            starLit[k] = true; starLitCount++; starBeacons[k].GetComponent<Renderer>().sharedMaterial = starBeaconOn; mgScore += 40f;
            Play(cPop, 0.8f); HunchFx.Spawn("star hit", starBeacons[k].position, 4f, 1f); Note("Beacon " + starLitCount + " of 5 is green.");
            if (starLitCount >= 5) { starPhase = 3; mgScore += 200f; Play(cWarp, 1f); Banner("THE TITAN TURNS AWAY", "It was only passing through. Well flown!", false); sim.Say("ARIA: All five beacons are green. The Dust Titan is turning back toward deep space. Nobody was hurt."); }
            return;
        }
        if (i < 0 || i >= starShips.Count) return; var s = starShips[i]; if (!s.up) return;
        s.hp -= zapLv; HunchFx.Spawn("electro hit", s.t.position, 3f, 0.6f);
        if (s.hp > 0f) return;
        s.up = false; s.respawn = 3.5f; s.t.gameObject.SetActive(false); starHome++; mgScore += 10f;
        HunchFx.Spawn("teleport", s.t.position, 4f, 1.2f); TeleFx(s.t.position, 4f); Play(cWarp, 0.7f); game.Buzz(0.6f, 0.12f);
        if (starPhase == 1 && starHome >= StarWave && mgOn)
        {
            starPhase = 2; starTitan.gameObject.SetActive(true); starTitanZ = 520f;
            Banner("THE DUST TITAN", "Zap its five red beacons to turn it away", true);
            sim.Say("ARIA: Something enormous is drifting toward the station: a Dust Titan. Cannons cannot stop it. Light its five beacons and it will change course.");
        }
    }

    void UpdateStar(float dt)
    {
        if (!starHolder) return;
        if (!game.flyMode) game.flyMode = true;      // there is no floor up here
        Vector3 c = StarC, me = origin.transform.position; float t = Time.time;
        Vector3 off = me - c; if (off.magnitude > 330f) { origin.transform.position = c + off.normalized * 328f; Note("Turning back toward the station."); }
        if (mgOn && !game.HeadsetActive && !game.menuOpen && !ControlsLocked) origin.transform.position += cam.forward * (9f + starBoost * 1.2f) * dt;      // the fighter always cruises forward
        foreach (var s in starShips)
        {
            if (!s.up) { if (mgOn && starPhase == 1) { s.respawn -= dt; if (s.respawn <= 0f) { s.up = true; s.hp = 2f; s.ang += 2f; s.t.gameObject.SetActive(true); } } continue; }
            s.ang += s.speed / s.radius * dt;
            s.t.position = c + new Vector3(Mathf.Sin(s.ang) * s.radius, s.height + Mathf.Sin(t * 0.7f + s.bob) * 9f, Mathf.Cos(s.ang) * s.radius);
            s.t.rotation = Quaternion.Euler(Mathf.Sin(t * 1.3f + s.bob) * 10f, t * 40f, 0f);
        }
        for (int i = 0; i < starWing.Count; i++)
        {
            float a = t * 0.25f + i * 2.094f; Vector3 p = c + new Vector3(Mathf.Sin(a) * 46f, 8f + i * 6f, Mathf.Cos(a) * 46f);
            starWing[i].position = p; starWing[i].rotation = Quaternion.LookRotation(new Vector3(Mathf.Cos(a), 0f, -Mathf.Sin(a)), Vector3.up) * Quaternion.Euler(0f, 0f, -18f);
        }
        if (starPhase >= 2 && starTitan.gameObject.activeSelf)
        {
            if (starPhase == 2) { starTitanZ -= 7f * dt; if (starTitanZ < 150f && mgOn) { mgScore += 0f; MgEnd("The Dust Titan drifted past the station."); } }
            else { starTitanZ += 60f * dt; starTitan.localScale = Vector3.one * Mathf.Max(0.05f, 1f - (starTitanZ - 150f) / 900f); if (mgOn && starTitanZ > 420f) MgEnd("The Dust Titan turned away!"); }
            starTitan.position = c + new Vector3(0f, 20f, starTitanZ); starTitan.rotation = Quaternion.Euler(0f, Mathf.Sin(t * 0.2f) * 8f + (starPhase == 3 ? 180f * Mathf.Clamp01((starTitanZ - 150f) / 200f) : 0f), 0f);
        }
    }

    IEnumerator AutoStar()
    {
        AutoSay("Star Patrol. Sending the alien scouts home, then turning the Dust Titan away.");
        while (mgCount > 0f && mapNow == 8) yield return null;
        float guard = Time.time + 150f, aim = 0f;
        while (mgOn && Time.time < guard && mapNow == 8)
        {
            Vector3 tp = Vector3.zero; int ti = -1;
            if (starPhase == 2) { for (int i = 0; i < 5; i++) if (!starLit[i]) { ti = 100 + i; tp = starBeacons[i].position; break; } }
            else if (starPhase == 1) { float bd = 1e9f; for (int i = 0; i < starShips.Count; i++) if (starShips[i].up) { float d = (starShips[i].t.position - cam.position).sqrMagnitude; if (d < bd) { bd = d; ti = i; tp = starShips[i].t.position; } } }
            if (ti >= 0) { Face(tp); aim += Time.deltaTime; if (aim > 0.5f) { aim = 0f; ShowBeam(Muzzle(), tp, beamGreen); Play(cPew, 0.5f); StarHit(ti); } }
            yield return null;
        }
    }

    // ================================================================== STORY STUDIO
    Texture2D artTex; Color32[] artPix; Collider artCol; Color32 artColor = new Color32(30, 60, 200, 255); int artBrush = 3, artIdx = 5; bool artDirty, artDown; Vector2 artLast; public int artSaved, artStrokes;
    const int ArtW = 320, ArtH = 200;
    static readonly Color32[] ArtPalette = { new Color32(20, 20, 30, 255), new Color32(220, 40, 40, 255), new Color32(250, 150, 30, 255), new Color32(250, 220, 40, 255), new Color32(50, 180, 70, 255), new Color32(30, 60, 200, 255), new Color32(150, 60, 200, 255), new Color32(255, 255, 255, 255) };
    static readonly string[] ArtName = { "black", "red", "orange", "yellow", "green", "blue", "purple", "eraser (white)" };
    TextMeshPro artLabel, storyText; readonly TextMeshPro[] storyChoice = new TextMeshPro[3]; readonly GameObject[] storyBtn = new GameObject[3]; int storyAt; public int storyEnds; int storyFirst = -1;
    Transform swordHeld; int swordIndex = -1; readonly Transform[] swordModel = new Transform[3]; readonly List<Transform> swordOrbs = new List<Transform>(); readonly float[] orbBack = new float[6]; public int swordHits; Vector3 swordRackAt;

    class StoryNode { public string text; public string[] choice; public int[] next; public bool ending; }
    static readonly StoryNode[] Story =
    {
        new StoryNode { text = "Night shift at Quiet Sea Base. You are Cadet Nova, the youngest pilot on the Moon. The radio crackles with a signal nobody has heard before: a slow tapping, like a heartbeat, from the dark side of the sky.", choice = new[] { "Wake the Commander", "Ask ARIA what it is", "Run to your star fighter" }, next = new[] { 1, 2, 3 } },
        new StoryNode { text = "The Commander listens, frowning. \"It is not a ship we know. Take the scout fighter and look, Cadet. Do not fire unless you must, and keep talking to us.\"", choice = new[] { "Launch", "Ask ARIA to come along" }, next = new[] { 3, 2 } },
        new StoryNode { text = "ARIA hums. \"The tapping is a pattern: three, five, seven. Prime numbers. Somebody is saying hello with mathematics. I can ride in your fighter's computer if you like.\"", choice = new[] { "Take ARIA and launch", "Answer with eleven taps first" }, next = new[] { 3, 4 } },
        new StoryNode { text = "Your fighter climbs out of the Moon's shadow. Ahead drifts a small silver pod, tumbling. Far behind it something enormous is moving: a cloud of dust and rock as wide as a city.", choice = new[] { "Catch the pod", "Fly toward the giant cloud" }, next = new[] { 5, 6 } },
        new StoryNode { text = "You tap eleven times on the transmitter. Silence. Then thirteen taps come back, faster, almost excited. Whatever it is, it understood you.", choice = new[] { "Launch and meet it" }, next = new[] { 3 } },
        new StoryNode { text = "Inside the pod is a tiny alien no bigger than a helmet, with three bright eyes. It taps the glass: three, five, seven. You name it Pip. Pip points a shaky finger at the giant cloud and makes a sad sound.", choice = new[] { "Ask Pip about the cloud", "Take Pip home to the base" }, next = new[] { 7, 8 } },
        new StoryNode { text = "Up close the cloud is alive. Rocks circle a huge quiet shape: a Dust Titan. It is drifting straight for the base. Your cannons are armed. The Titan has not noticed you yet.", choice = new[] { "Fire the cannons", "Switch off the cannons and listen" }, next = new[] { 9, 10 } },
        new StoryNode { text = "Pip draws in the fog on the glass: a little pod, and a big cloud with a heart in it. The Dust Titan is not hunting. It is Pip's guardian, and it has been looking for the lost pod all night.", choice = new[] { "Bring Pip to the Titan", "Call the base and explain" }, next = new[] { 11, 11 } },
        new StoryNode { text = "Back at the base the crew cheer for Pip, but then the alarms start: the Dust Titan has followed. It stops above the dome, enormous and silent, tapping three, five, seven on the hull.", choice = new[] { "Carry Pip outside to it", "Hide Pip in a locker" }, next = new[] { 11, 12 } },
        new StoryNode { ending = true, text = "The beams splash off the rocks like rain on a mountain. The Titan turns its great face toward you... and simply drifts around your ship, as if you were a pebble. You feel very small, and a little silly.\n\n<b>ENDING 1 of 3: The Pebble.</b>  Maybe listening works better.", choice = new[] { "Start again" }, next = new[] { 0 } },
        new StoryNode { text = "With the engines quiet you hear it: three, five, seven, over and over, slow and worried. It is the same signal as the pod. The Titan is calling for someone.", choice = new[] { "Find the pod it is calling", "Tap the answer: eleven" }, next = new[] { 5, 11 } },
        new StoryNode { ending = true, text = "You open the canopy and Pip floats up into the dust. The rocks part like curtains. The Titan hums so deeply that your whole ship sings, then it turns and carries Pip home toward the stars. On your radio: thirteen happy taps.\n\n<b>ENDING 3 of 3: The Guardian.</b>  You made first contact by listening.", choice = new[] { "Start again" }, next = new[] { 0 } },
        new StoryNode { ending = true, text = "You hide Pip in a locker. The Titan waits above the base for three days, tapping. Nobody sleeps. On the fourth day you open the locker and carry Pip outside yourself.\n\n<b>ENDING 2 of 3: The Long Wait.</b>  Friends should not be kept apart.", choice = new[] { "Start again" }, next = new[] { 0 } },
    };
    static readonly string[] TaleTitle = { "The Alien Who Collected Moon Rocks", "Pip and the Lost Antenna", "The Day the Dust Titan Turned Around", "Star Pilots of the Quiet Sea" };
    static readonly string[] TaleBody =
    {
        "Zib was a small green alien with a very big backpack. Every night Zib hopped across the crater and picked up one moon rock.\n\n\"Why only one?\" asked the astronaut who drove the rover.\n\n\"Because one is enough to remember a place,\" said Zib. \"If I took them all, there would be no place left to remember.\"\n\nThe astronaut thought about that for a long time. The next day she left one Earth rock by the crater, a smooth grey pebble from a beach.\n\nZib put it at the very top of the backpack. It is still there. Sometimes, when Zib is far from home, the pebble smells a little like the sea.",
        "The base antenna fell over in the night. No calls home, no weather, no music.\n\nPip, who was only as tall as a helmet, watched the crew search with big lamps. They looked in the hangar. They looked in the greenhouse. They looked under the rover.\n\nPip tapped the Commander's boot: three, five, seven.\n\n\"Not now, Pip.\"\n\nPip tapped again and pointed up. The antenna had not fallen over at all. A rover had driven off with the cable hooked to its bumper, and the antenna was standing in the next crater, pointing proudly at the wrong planet.\n\nSmall eyes sometimes see the big things first.",
        "People tell this story on the Moon when the lights are low.\n\nA Dust Titan is bigger than a city and older than the craters. It does not chase and it does not hide. It only drifts, looking for something it has lost.\n\nOnce a Titan drifted toward a base full of sleeping people. The pilots flew up with every cannon they had. The youngest pilot switched hers off.\n\nShe had noticed the red lights on its side blinking in a pattern, like a question. So she answered. One by one she turned the lights green.\n\nThe Titan stopped. It hummed, low enough to rattle teeth. Then it turned, slowly, the way a whale turns, and went back the way it came.\n\nNobody ever found out what the question was. But the answer was yes.",
        "At Quiet Sea Base the cadets learn four rules before they are allowed to fly.\n\nOne: check your air before your engine.\n\nTwo: a star fighter is fast, but a good idea is faster.\n\nThree: never leave a wingmate behind, not even the one who snores.\n\nFour: when something strange appears in the sky, say hello before you say anything else.\n\nThe old pilots say rule four has saved more ships than all the shields ever built. The cadets roll their eyes. Then one night something strange appears in the sky, and they are very glad they remembered."
    };

    void BuildStudio(Transform r)
    {
        BuildExtraSounds();
        var steel = Lit(new Color(0.42f, 0.45f, 0.5f)); var dark = Lit(new Color(0.12f, 0.13f, 0.18f)); var gold = Lit(new Color(0.95f, 0.74f, 0.22f)); var wood = Lit(new Color(0.5f, 0.36f, 0.2f));
        // ---- the painting wall
        artTex = new Texture2D(ArtW, ArtH, TextureFormat.RGBA32, false); artTex.wrapMode = TextureWrapMode.Clamp; artTex.filterMode = FilterMode.Bilinear; artPix = new Color32[ArtW * ArtH]; ArtClear();
        var am = new Material(solidSrc); if (am.HasProperty("_BaseMap")) am.SetTexture("_BaseMap", artTex); am.mainTexture = artTex; am.SetColor("_BaseColor", Color.white);
        var easel = new GameObject("Painting wall").transform; easel.SetParent(r, false); easel.localPosition = new Vector3(0f, 0f, 2f);
        Prim(PrimitiveType.Cube, easel, new Vector3(0f, 3.4f, 0.12f), new Vector3(10.6f, 6.8f, 0.2f), wood);
        var canvas = Prim(PrimitiveType.Quad, easel, new Vector3(0f, 3.4f, 0f), new Vector3(10f, 6.25f, 1f), am, true); canvas.name = "Canvas"; artCol = canvas.GetComponent<Collider>();
        for (int i = 0; i < 8; i++) { var c = ArtPalette[i]; FacButton(easel, new Vector3(-4.2f + i * 0.9f, 0.6f, -1.2f), new Vector3(0.7f, 0.7f, 0.7f), Glow(new Color(c.r / 255f, c.g / 255f, c.b / 255f)), 700 + i); }
        FacButton(easel, new Vector3(3.6f, 0.6f, -1.2f), new Vector3(1.0f, 0.7f, 0.7f), steel, 722); FacButton(easel, new Vector3(4.8f, 0.6f, -1.2f), new Vector3(1.0f, 0.7f, 0.7f), Lit(new Color(0.8f, 0.3f, 0.25f)), 720); FacButton(easel, new Vector3(6.0f, 0.6f, -1.2f), new Vector3(1.0f, 0.7f, 0.7f), Lit(new Color(0.25f, 0.6f, 0.3f)), 721);
        var bl = Label(easel, "BRUSH      CLEAR      SAVE", 0f, 0f, 1.5f, 6f, 0.5f, Color.white); bl.transform.localPosition = new Vector3(4.8f, 1.25f, -1.2f);
        artLabel = Label(easel, "", 0f, 0f, 3.2f, 30f, 2f, new Color(1f, 0.84f, 0.3f)); artLabel.transform.localPosition = new Vector3(0f, 7.6f, 0f);

        // ---- the story screen, on the left
        var scr = new GameObject("Story screen").transform; scr.SetParent(r, false); scr.localPosition = new Vector3(-17f, 0f, -2f); scr.localRotation = Quaternion.Euler(0f, -55f, 0f);
        Prim(PrimitiveType.Cube, scr, new Vector3(0f, 3.6f, 0.15f), new Vector3(9.6f, 5.6f, 0.2f), gold); Prim(PrimitiveType.Quad, scr, new Vector3(0f, 3.6f, 0f), new Vector3(9.2f, 5.2f, 1f), Glow(new Color(0.05f, 0.08f, 0.2f)));
        var title = Label(scr, "<b>STORY MODE: Cadet Nova and the Dust Titan</b>", 0f, 0f, 3.6f, 8.8f, 0.6f, new Color(1f, 0.84f, 0.3f)); title.transform.localPosition = new Vector3(0f, 5.75f, -0.02f);
        storyText = Label(scr, "", 0f, 0f, 3.2f, 8.6f, 4.1f, Color.white); storyText.transform.localPosition = new Vector3(0f, 3.3f, -0.02f); storyText.alignment = TextAlignmentOptions.TopLeft; storyText.enableAutoSizing = true; storyText.fontSizeMin = 1.6f; storyText.fontSizeMax = 3.4f;
        for (int i = 0; i < 3; i++)
        {
            storyBtn[i] = FacButton(scr, new Vector3(-3.1f + i * 3.1f, 0.55f, -0.5f), new Vector3(2.9f, 0.9f, 0.5f), Lit(new Color(0.15f, 0.3f, 0.55f)), 740 + i);
            storyChoice[i] = Label(scr, "", 0f, 0f, 1.7f, 2.8f, 0.85f, Color.white); storyChoice[i].transform.localPosition = new Vector3(-3.1f + i * 3.1f, 0.55f, -0.77f); storyChoice[i].enableAutoSizing = true; storyChoice[i].fontSizeMin = 0.9f; storyChoice[i].fontSizeMax = 1.8f;
        }
        StoryShow(0);

        // ---- the bookshelf of alien stories, on the right
        var shelf = new GameObject("Bookshelf").transform; shelf.SetParent(r, false); shelf.localPosition = new Vector3(17f, 0f, -2f); shelf.localRotation = Quaternion.Euler(0f, 55f, 0f);
        Prim(PrimitiveType.Cube, shelf, new Vector3(0f, 1.6f, 0.3f), new Vector3(6.4f, 3.2f, 0.5f), wood); Prim(PrimitiveType.Cube, shelf, new Vector3(0f, 0.9f, 0f), new Vector3(6.4f, 0.12f, 1.0f), wood); Prim(PrimitiveType.Cube, shelf, new Vector3(0f, 2.3f, 0f), new Vector3(6.4f, 0.12f, 1.0f), wood);
        Color[] bc = { new Color(0.25f, 0.6f, 0.3f), new Color(0.3f, 0.45f, 0.85f), new Color(0.6f, 0.3f, 0.7f), new Color(0.85f, 0.35f, 0.25f) };
        for (int i = 0; i < 4; i++)
        {
            FacButton(shelf, new Vector3(-2.25f + i * 1.5f, 1.55f, -0.15f), new Vector3(1.1f, 1.2f, 0.25f), Lit(bc[i]), 760 + i);
            var tl = Label(shelf, "<b>" + TaleTitle[i] + "</b>", 0f, 0f, 0.9f, 1.0f, 1.1f, Color.white); tl.transform.localPosition = new Vector3(-2.25f + i * 1.5f, 1.55f, -0.29f); tl.enableAutoSizing = true; tl.fontSizeMin = 0.5f; tl.fontSizeMax = 1.0f;
        }
        var sl = Label(shelf, "<b>ALIEN STORIES</b>\n<size=60%>click a book to read it</size>", 0f, 0f, 3.2f, 12f, 2f, new Color(1f, 0.84f, 0.3f)); sl.transform.localPosition = new Vector3(0f, 4.2f, 0f);

        // ---- the light-sword rack and its training orbs
        var rack = new GameObject("Light-sword rack").transform; rack.SetParent(r, false); rack.localPosition = new Vector3(13f, 0f, -12f); rack.localRotation = Quaternion.Euler(0f, 35f, 0f); swordRackAt = rack.localPosition;
        Prim(PrimitiveType.Cube, rack, new Vector3(0f, 0.5f, 0f), new Vector3(3.2f, 1.0f, 0.8f), dark); Prim(PrimitiveType.Cube, rack, new Vector3(0f, 1.05f, 0f), new Vector3(3.3f, 0.1f, 0.9f), gold);
        Color[] sc = { new Color(0.3f, 0.9f, 2.6f), new Color(0.4f, 2.4f, 0.6f), new Color(1.8f, 0.5f, 2.6f) };
        for (int i = 0; i < 3; i++)
        {
            var s = new GameObject("Light sword").transform; s.SetParent(rack, false); s.localPosition = new Vector3(-1f + i * 1f, 1.2f, 0f); s.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            SwordShape(s, sc[i]); swordModel[i] = s;
            FacButton(rack, new Vector3(-1f + i * 1f, 1.9f, 0f), new Vector3(0.5f, 1.6f, 0.5f), Clear(new Color(1f, 1f, 1f, 0.02f)), 780 + i);
        }
        var rl = Label(rack, "<b>LIGHT SWORDS</b>\n<size=60%>take one, then slice the glowing training orbs</size>", 0f, 0f, 2.6f, 12f, 2f, new Color(1f, 0.84f, 0.3f)); rl.transform.localPosition = new Vector3(0f, 3.6f, 0f); mapLabels.Add(rl);
        for (int i = 0; i < 6; i++)
        {
            var o = Prim(PrimitiveType.Sphere, r, Vector3.zero, Vector3.one * 0.6f, Glow(new Color(2.2f, 1.4f, 0.3f)), true); o.name = "Training orb";
            var ot = o.AddComponent<HunchTarget>(); ot.kind = 35; ot.index = 790 + i; var sp = o.GetComponent<SphereCollider>(); if (sp) sp.radius = 0.9f;
            swordOrbs.Add(o.transform);
        }
        var sign = Label(r, "<b>STORY STUDIO</b>\n<size=55%>Paint on the big wall.   Left: play the story.   Right: alien stories to read, and light swords.</size>", 0f, 0f, 7f, 60f, 6f, new Color(1f, 0.84f, 0.3f)); sign.transform.localPosition = new Vector3(0f, 11f, 30f);
        A("Space artist", "Paint a picture on the wall in the Story Studio.", () => artStrokes >= 12);
        A("Storyteller", "Reach an ending of Cadet Nova's story.", () => storyEnds != 0);
        A("Sword student", "Slice ten training orbs with a light sword.", () => swordHits >= 10);
    }

    void SwordShape(Transform s, Color blade)
    {
        Prim(PrimitiveType.Cylinder, s, new Vector3(0f, 0f, 0.11f), new Vector3(0.05f, 0.11f, 0.05f), Lit(new Color(0.75f, 0.77f, 0.82f))).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        Prim(PrimitiveType.Cube, s, new Vector3(0f, 0f, 0.23f), new Vector3(0.16f, 0.03f, 0.03f), Lit(new Color(0.95f, 0.74f, 0.22f)));
        Prim(PrimitiveType.Cylinder, s, new Vector3(0f, 0f, 0.75f), new Vector3(0.035f, 0.5f, 0.035f), Glow(blade)).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
    }

    void ArtClear() { for (int i = 0; i < artPix.Length; i++) artPix[i] = new Color32(255, 255, 255, 255); artDirty = true; }
    void ArtDot(int cx, int cy)
    {
        int rad = artBrush;
        for (int y = -rad; y <= rad; y++) for (int x = -rad; x <= rad; x++)
        {
            if (x * x + y * y > rad * rad) continue; int px = cx + x, py = cy + y; if (px < 0 || py < 0 || px >= ArtW || py >= ArtH) continue;
            artPix[py * ArtW + px] = artColor;
        }
    }
    void ArtLine(Vector2 a, Vector2 b)
    {
        float dist = Vector2.Distance(new Vector2(a.x * ArtW, a.y * ArtH), new Vector2(b.x * ArtW, b.y * ArtH)); int steps = Mathf.Clamp(Mathf.CeilToInt(dist), 1, 400);
        for (int i = 0; i <= steps; i++) { Vector2 p = Vector2.Lerp(a, b, i / (float)steps); ArtDot(Mathf.RoundToInt(p.x * (ArtW - 1)), Mathf.RoundToInt(p.y * (ArtH - 1))); }
        artDirty = true;
    }
    string ArtSave()
    {
        try
        {
            string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Drawings"); Directory.CreateDirectory(dir);
            artTex.SetPixels32(artPix); artTex.Apply(false);
            string path = Path.Combine(dir, "drawing_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png"); File.WriteAllBytes(path, artTex.EncodeToPNG()); artSaved++;
            return path;
        }
        catch (Exception e) { return "could not save: " + e.Message; }
    }

    void StoryShow(int k)
    {
        storyAt = Mathf.Clamp(k, 0, Story.Length - 1); var n = Story[storyAt];
        if (storyText) storyText.text = n.text;
        for (int i = 0; i < 3; i++)
        {
            bool used = i < n.choice.Length; if (storyBtn[i]) storyBtn[i].SetActive(used); if (storyChoice[i]) { storyChoice[i].gameObject.SetActive(used); if (used) storyChoice[i].text = n.choice[i]; }
        }
        if (n.ending) { int bit = storyAt == 9 ? 1 : (storyAt == 12 ? 2 : 4); if ((storyEnds & bit) == 0) { storyEnds |= bit; Banner("THE END", "Endings found: " + Bits(storyEnds) + " of 3", false); } }
    }

    string StudioText(int i)
    {
        if (i >= 700 && i < 708) return "paint with " + ArtName[i - 700];
        if (i == 720) return "wipe the wall clean";
        if (i == 721) return "save the picture as a file";
        if (i == 722) return "brush size: " + (artBrush <= 2 ? "small" : (artBrush <= 4 ? "medium" : "large")) + " (change)";
        if (i >= 740 && i < 743) { var n = Story[storyAt]; return i - 740 < n.choice.Length ? "choose: " + n.choice[i - 740] : ""; }
        if (i >= 760 && i < 764) return "read: " + TaleTitle[i - 760];
        if (i >= 780 && i < 783) return swordIndex == i - 780 ? "put the light sword back" : "take the light sword";
        if (i >= 790 && i < 796) return swordIndex >= 0 ? "slice the training orb" : "take a light sword from the rack first";
        return "";
    }

    void StudioUse(int i)
    {
        if (i >= 700 && i < 708) { artColor = ArtPalette[i - 700]; artIdx = i - 700; Play(cPick, 0.5f); Note("Painting with " + ArtName[i - 700] + ". Aim at the wall and hold " + (game.HeadsetActive ? "the trigger." : "the mouse button.")); return; }
        if (i == 720) { ArtClear(); Play(cPop, 0.6f); Note("The wall is clean."); return; }
        if (i == 721) { string p = ArtSave(); Play(cWin, 0.6f); Note("Saved: " + p); return; }
        if (i == 722) { artBrush = artBrush <= 2 ? 4 : (artBrush <= 4 ? 7 : 2); Play(cPick, 0.5f); Note("Brush: " + (artBrush <= 2 ? "small" : (artBrush <= 4 ? "medium" : "large"))); return; }
        if (i >= 740 && i < 743) { var n = Story[storyAt]; int c = i - 740; if (c < n.choice.Length) { Play(cPick, 0.6f); StoryShow(n.next[c]); } return; }
        if (i >= 760 && i < 764) { if (storyFirst >= 0) OpenRead(storyFirst + (i - 760)); return; }
        if (i >= 780 && i < 783) { TakeSword(swordIndex == i - 780 ? -1 : i - 780); return; }
        if (i >= 790 && i < 796) { if (swordIndex < 0) Note("Take a light sword from the rack first."); else SliceOrb(i - 790); return; }
    }

    void TakeSword(int k)
    {
        if (swordHeld) { Destroy(swordHeld.gameObject); swordHeld = null; }
        if (swordIndex >= 0 && swordModel[swordIndex]) swordModel[swordIndex].gameObject.SetActive(true);
        swordIndex = k;
        if (k < 0) { Note("Light sword back on the rack."); return; }
        swordModel[k].gameObject.SetActive(false);
        Color[] sc = { new Color(0.3f, 0.9f, 2.6f), new Color(0.4f, 2.4f, 0.6f), new Color(1.8f, 0.5f, 2.6f) };
        bool vr = game.HeadsetActive && rightCtrl;
        swordHeld = new GameObject("Held light sword").transform; swordHeld.SetParent(vr ? rightCtrl : cam, false);
        swordHeld.localPosition = vr ? Vector3.zero : new Vector3(0.32f, -0.3f, 0.45f); swordHeld.localRotation = vr ? Quaternion.identity : Quaternion.Euler(-28f, -8f, 0f);
        SwordShape(swordHeld, sc[k]); foreach (var rr in swordHeld.GetComponentsInChildren<Renderer>()) rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tool = Hands; Play(cSlice, 0.8f);
        if (musicRef) { if (musicBefore < 0) musicBefore = musicRef.style; musicRef.SetStyle(7); }      // the funky sword-training beat
        Note("Light sword on. " + (vr ? "Swing it through the glowing orbs." : "Aim at a glowing orb and click to slice it."));
    }

    void SliceOrb(int k)
    {
        if (k < 0 || k >= swordOrbs.Count || !swordOrbs[k].gameObject.activeSelf) return;
        Vector3 p = swordOrbs[k].position; swordOrbs[k].gameObject.SetActive(false); orbBack[k] = 1.6f; swordHits++;
        Play(cSlice, 0.8f); game.Buzz(0.6f, 0.08f); HunchFx.Spawn("star hit", p, 0.7f, 0.8f); Fx2("flash_star_ellow_purple", null, p, 1f, 1f, 0.8f);
        Note("Sliced! " + swordHits);
    }

    void UpdateStudio(float dt)
    {
        if (!artTex) return;
        // painting: hold the button while the laser or the crosshair is on the wall
        bool down; var mouse = Mouse.current;
        if (game.HeadsetActive) down = HunchVr.RightTrigger > 0.55f; else down = mouse != null && mouse.leftButton.isPressed && Cursor.lockState == CursorLockMode.Locked;
        if (Time.time < probeHold) down = true;
        bool painted = false;
        if (down && !game.menuOpen && !ControlsLocked && swordIndex < 0)
        {
            RaycastHit h;
            if (Cast(AimRay(), out h, 22f) && h.collider == artCol)
            {
                Vector2 uv = h.textureCoord; if (artDown) ArtLine(artLast, uv); else { ArtLine(uv, uv); artStrokes++; }
                artLast = uv; artDown = true; painted = true;
            }
        }
        if (!painted) artDown = false;
        if (artDirty) { artTex.SetPixels32(artPix); artTex.Apply(false); artDirty = false; }
        if (artLabel) { string s = "<b>PAINTING WALL</b>   <size=70%>colour: " + ArtName[artIdx] + "   aim and hold to paint   pictures saved: " + artSaved + "</size>"; if (artLabel.text != s) artLabel.text = s; }
        // training orbs float round the sword rack
        Vector3 c = mapC[9] + swordRackAt; float t = Time.time;
        for (int i = 0; i < swordOrbs.Count; i++)
        {
            var o = swordOrbs[i];
            if (!o.gameObject.activeSelf) { orbBack[i] -= dt; if (orbBack[i] <= 0f) o.gameObject.SetActive(true); continue; }
            float a = t * 0.5f + i * 1.047f; o.position = c + new Vector3(Mathf.Sin(a) * (3.2f + (i % 2)), 1.5f + Mathf.Sin(t * 1.3f + i) * 0.5f, Mathf.Cos(a) * (3.2f + (i % 2)) - 2.5f);
            if (swordHeld && game.HeadsetActive && (o.position - (swordHeld.position + swordHeld.forward * 0.8f)).sqrMagnitude < 0.55f * 0.55f) SliceOrb(i);      // VR: the blade itself does the slicing
        }
    }

    // ================================================================== set-up, frame and autopilot for parts 8 and 9
    void BuildBatch8()
    {
        // stories and the factory's storage list are read on the same sheet as the tablets; they are added after the pick-ups so no tablet is made for them
        storyFirst = reads.Count;
        for (int i = 0; i < TaleTitle.Length; i++) { int ii = i; reads.Add(new ReadItem { title = TaleTitle[i], zone = "Story Studio", got = true, body = () => TaleBody[ii] }); }
        facSheetIndex = reads.Count; reads.Add(new ReadItem { title = "Factory Storage", zone = "Factory Land", got = true, body = FacSheet });
        BuildBatch10();
    }

    void UpdateBatch8(float dt)
    {
        UpdateFactory(dt); UpdateBatch10(dt);
        if (starCockpit)
        {
            bool want = mapNow == 8 && Playing;
            if (starCockpit.gameObject.activeSelf != want)
            {
                starCockpit.gameObject.SetActive(want);
                if (want && !game.HeadsetActive) { starCockpit.SetParent(cam, false); starCockpit.localPosition = Vector3.zero; starCockpit.localRotation = Quaternion.identity; }
                else starCockpit.SetParent(root, false);
            }
            if (want && game.HeadsetActive) starCockpit.SetPositionAndRotation(cam.position - Vector3.up * 0.25f, Quaternion.Euler(0f, origin.transform.eulerAngles.y, 0f));      // in the headset the cockpit turns with your body, not your head
        }
        if (mapNow != 9 && swordIndex >= 0) TakeSword(-1);
    }

    IEnumerator AutoMapExtra(int k)
    {
        if (k >= 10) { yield return AutoMapExtra2(k); yield break; }
        if (k == 7) yield return AutoFactory();
        else if (k == 8) yield return AutoStar();
        else if (k == 9)
        {
            AutoSay("Story Studio. Painting a rocket on the wall.");
            TeleportTo(mapC[9] + new Vector3(0f, 0f, -9f), 0f); yield return new WaitForSeconds(0.6f);
            Vector2[] pts = { new Vector2(0.5f, 0.2f), new Vector2(0.5f, 0.75f), new Vector2(0.44f, 0.6f), new Vector2(0.5f, 0.75f), new Vector2(0.56f, 0.6f), new Vector2(0.5f, 0.2f), new Vector2(0.42f, 0.12f), new Vector2(0.5f, 0.2f), new Vector2(0.58f, 0.12f) };
            artColor = ArtPalette[1]; artIdx = 1; artBrush = 4;
            for (int i = 1; i < pts.Length && mapNow == 9; i++) { for (float u = 0f; u < 1f; u += Time.deltaTime * 3f) { ArtLine(Vector2.Lerp(pts[i - 1], pts[i], u), Vector2.Lerp(pts[i - 1], pts[i], Mathf.Min(1f, u + 0.05f))); yield return null; } artStrokes++; }
            AutoSay("Playing the story: Cadet Nova hears a strange signal. The AI chooses to listen.");
            TeleportTo(mapC[9] + new Vector3(-8f, 0f, -10f), -55f); yield return new WaitForSeconds(0.8f);
            int[] path = { 1, 0, 0, 1, 1, 1 };      // ask ARIA -> launch -> catch... each step picks a choice that exists
            StoryShow(0);
            for (int i = 0; i < 8 && mapNow == 9 && !Story[storyAt].ending; i++) { yield return new WaitForSeconds(3.2f); var n = Story[storyAt]; int c = Mathf.Min(path[i % path.Length], n.choice.Length - 1); if (storyAt == 6) c = 1; if (storyAt == 8) c = 0; StoryShow(n.next[c]); }
            yield return new WaitForSeconds(4f);
        }
    }

    public string ProbeStar() { return "phase " + starPhase + " home " + starHome + " lit " + starLitCount + " score " + mgScore.ToString("F0") + " mg " + mgOn + " fly " + game.flyMode + " ships " + starShips.Count + " wing " + starWing.Count + " at " + (origin.transform.position - mapC[8]).ToString("F0"); }
    public string ProbeStarZap() { int n = 0; for (int i = 0; i < starShips.Count; i++) if (starShips[i].up) { StarHit(i); StarHit(i); n++; } return "zapped " + n + " | " + ProbeStar(); }
    public string ProbeStarBeacons() { for (int i = 0; i < 5; i++) StarHit(100 + i); return ProbeStar(); }
    public string ProbeStarFaceTitan() { if (starTitan) Face(starTitan.position); return "titan z " + starTitanZ.ToString("F0") + " active " + starTitan.gameObject.activeSelf; }
    public string ProbePaint() { artColor = ArtPalette[5]; artIdx = 5; artBrush = 4; ArtLine(new Vector2(0.1f, 0.2f), new Vector2(0.9f, 0.8f)); artColor = ArtPalette[1]; ArtLine(new Vector2(0.1f, 0.8f), new Vector2(0.9f, 0.2f)); artStrokes += 2; return "painted, strokes " + artStrokes; }
    public string ProbeArtSave() { return ArtSave(); }
    public string ProbeStory() { var n = Story[storyAt]; StudioUse(740 + Mathf.Min(probeInt, n.choice.Length - 1)); return "story node " + storyAt + " ending " + Story[storyAt].ending + " ends " + storyEnds; }
    public string ProbeTale() { StudioUse(760 + probeInt % 4); return "reading " + readOpen + " of " + reads.Count; }
    public string ProbeSword() { StudioUse(780 + probeInt % 3); for (int i = 0; i < 3; i++) SliceOrb(i); return "sword " + swordIndex + " hits " + swordHits + " music " + (musicRef ? HunchMusic.Styles[musicRef.style] : "none"); }
    public string ProbeStudioView() { if (mapNow != 9) return "not in the studio"; TeleportTo(mapC[9] + new Vector3(probeX, 0f, probeZ), probeYaw); if (desk) desk.SetLook(probeYaw, -6f); return "view " + probeX + "," + probeZ + " yaw " + probeYaw; }
}
