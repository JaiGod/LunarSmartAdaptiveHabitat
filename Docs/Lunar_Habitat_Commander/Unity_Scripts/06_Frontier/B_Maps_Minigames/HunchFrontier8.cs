// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/06_Frontier/B_Maps_Minigames/HunchFrontier8.cs
//  Section : 06_Frontier/B_Maps_Minigames
//  Folder  : Extra maps and mini-games: solar farm, mining pit, Factory Land, Star Patrol, crew
//            radio + Star Clicker, Science Academy, My Astronaut, model kits, Fun Zone, endless
//            ground, Star Store pages.
//  Purpose : Eighth part of Moon Frontier: FACTORY LAND (map 7). A production chain on its own
//            piece of Moon: drill coal, iron, gold and stone from deposits (by hand with the Mining
//            drill, or build drill rigs)
//  Contains: HunchFrontier, FacDeposit, FacMachine, FacPlot
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

/// Eighth part of Moon Frontier: FACTORY LAND (map 7). A production chain on its own piece of Moon:
///   drill coal, iron, gold and stone from deposits (by hand with the Mining drill, or build drill rigs)
///   -> the POWER TOTEM burns coal and powers everything
///   -> smelters turn ore into iron and gold bars
///   -> factories make gears, engines, tool kits (manufacturing equipment) and base panels for the Moon base
///   -> chests show everything that is in storage; the rocket pad ships finished parts to the base
/// and a garden: soil plots where plants grow over time and ripen, watered by the fish tank's pump.
/// Everything is made in code from simple shapes. The factory keeps running while you are on another map.
public partial class HunchFrontier
{
    public const int FCoal = 0, FIronOre = 1, FGoldOre = 2, FStone = 3, FIron = 4, FGold = 5, FGear = 6, FEngine = 7, FKit = 8, FPanel = 9, FCrop = 10;
    public static readonly string[] FacName = { "Coal", "Iron ore", "Gold ore", "Stone", "Iron bar", "Gold bar", "Gear", "Engine", "Tool kit", "Base panel", "Tomato", "Carrot", "Strawberry", "Lettuce", "Pumpkin" };
    public int[] fac = new int[15];
    public float facFuel; public bool facTotemOn = true; public int facShipped, facMade, facHarvest, facBars, starBoost;

    class FacDeposit { public Transform t, bit; public int item; public float hp = 0.8f, rigT; public bool rig; public TextMeshPro label; public GameObject rigGo; }
    class FacMachine { public string name; public int[] inItem, inCount; public int outItem, outCount, made, cap = 999; public float time, t; public bool on = true, busy; public Transform root, spin; public Renderer lamp; public TextMeshPro label; }
    class FacPlot { public Transform holder, plant; public int crop = -1; public float grow; public TextMeshPro label; public readonly List<GameObject> fruit = new List<GameObject>(); public Renderer leaf; }
    readonly List<FacDeposit> facDeps = new List<FacDeposit>(); readonly List<FacMachine> facMachines = new List<FacMachine>(); readonly List<FacPlot> facPlots = new List<FacPlot>();
    readonly List<Transform> facFish = new List<Transform>(); readonly List<Renderer> facPipes = new List<Renderer>();
    Transform facFlame, facTank; TextMeshPro facTotemLabel, facTankLabel, facPadLabel; readonly TextMeshPro[] facChestLabel = new TextMeshPro[4];
    float facFed = 150f, facUi; int facSheetIndex = -1; Material facOn, facOff, facPipeOn, facPipeOff, facLampOn, facLampOff, facLampWait;
    AudioClip cClank, cHiss, cWarp, cPew, cGrow, cSlice;
    static readonly Color[] CropColor = { new Color(0.9f, 0.15f, 0.12f), new Color(1f, 0.5f, 0.1f), new Color(0.95f, 0.2f, 0.35f), new Color(0.55f, 0.9f, 0.35f), new Color(1f, 0.55f, 0.05f) };
    bool FacPowered { get { return facTotemOn && facFuel > 0f; } }

    void BuildExtraSounds()
    {
        if (cClank) return;
        cClank = Sweep("clank", 340f, 130f, 0.09f, 0.5f); cHiss = Sweep("hiss", 2100f, 900f, 0.45f, 0.95f); cWarp = Sweep("warp", 280f, 2400f, 0.38f, 0.1f);
        cPew = Sweep("pew", 2300f, 520f, 0.12f, 0.05f); cGrow = Sweep("grow", 520f, 980f, 0.22f, 0f); cSlice = Sweep("slice", 1700f, 3300f, 0.1f, 0.3f);
    }

    TextMeshPro FacLabel(Transform parent, float y, float size)
    {
        var l = Label(parent, "", 0f, 0f, size, 12f, 3f, Color.white); l.transform.localPosition = new Vector3(0f, y, 0f); mapLabels.Add(l); return l;
    }
    GameObject FacButton(Transform parent, Vector3 lp, Vector3 size, Material m, int index)
    {
        var g = Prim(PrimitiveType.Cube, parent, lp, size, m, true); var tg = g.AddComponent<HunchTarget>(); tg.kind = 35; tg.index = index; return g;
    }

    // ------------------------------------------------------------------ building the land
    void BuildFactory(Transform r)
    {
        BuildExtraSounds();
        var steel = Lit(new Color(0.42f, 0.45f, 0.5f)); var dark = Lit(new Color(0.16f, 0.17f, 0.2f)); var gold = Lit(new Color(0.95f, 0.74f, 0.22f)); var rock = Lit(new Color(0.34f, 0.33f, 0.34f));
        facOn = Glow(new Color(0.4f, 1.8f, 0.6f)); facOff = Lit(new Color(0.25f, 0.1f, 0.1f)); facPipeOn = Glow(new Color(0.3f, 0.9f, 2.0f)); facPipeOff = Lit(new Color(0.2f, 0.25f, 0.32f));
        facLampOn = Glow(new Color(0.4f, 2.0f, 0.6f)); facLampOff = Glow(new Color(1.8f, 0.25f, 0.2f)); facLampWait = Glow(new Color(1.8f, 1.2f, 0.2f));

        // ---- deposits on the west side
        int[] items = { FCoal, FCoal, FIronOre, FIronOre, FGoldOre, FStone, FStone };
        Vector3[] at = { new Vector3(-30f, 0f, -2f), new Vector3(-38f, 0f, 8f), new Vector3(-30f, 0f, 18f), new Vector3(-40f, 0f, 26f), new Vector3(-30f, 0f, 36f), new Vector3(-20f, 0f, 40f), new Vector3(-42f, 0f, 40f) };
        Color[] glow = { new Color(0.35f, 0.35f, 0.4f), new Color(0.35f, 0.35f, 0.4f), new Color(1.6f, 0.7f, 0.3f), new Color(1.6f, 0.7f, 0.3f), new Color(2.0f, 1.5f, 0.2f), new Color(0.8f, 0.8f, 0.85f), new Color(0.8f, 0.8f, 0.85f) };
        string[] dn = { "COAL SEAM", "COAL SEAM", "IRON DEPOSIT", "IRON DEPOSIT", "GOLD DEPOSIT", "ROCK DEPOSIT", "ROCK DEPOSIT" };
        for (int i = 0; i < items.Length; i++)
        {
            var d = new FacDeposit { item = items[i] };
            d.t = new GameObject(dn[i]).transform; d.t.SetParent(r, false); d.t.localPosition = at[i];
            var body = items[i] == FCoal ? Lit(new Color(0.07f, 0.07f, 0.08f)) : rock; var chunk = Glow(glow[i]);
            for (int k = 0; k < 6; k++) Prim(PrimitiveType.Sphere, d.t, new Vector3(R(-1.1f, 1.1f), R(0.3f, 0.9f), R(-1.1f, 1.1f)), new Vector3(R(1.2f, 1.9f), R(1.0f, 1.5f), R(1.2f, 1.9f)), body);
            for (int k = 0; k < 9; k++) Prim(PrimitiveType.Cube, d.t, new Vector3(R(-1.3f, 1.3f), R(0.6f, 1.7f), R(-1.3f, 1.3f)), Vector3.one * R(0.16f, 0.3f), chunk).transform.localRotation = Quaternion.Euler(R(0f, 90f), R(0f, 90f), 0f);
            var sc = d.t.gameObject.AddComponent<SphereCollider>(); sc.center = new Vector3(0f, 0.9f, 0f); sc.radius = 1.9f;
            var tg = d.t.gameObject.AddComponent<HunchTarget>(); tg.kind = 34; tg.index = i;
            // the drill rig: a tripod with a bit that pumps up and down when it runs
            d.rigGo = new GameObject("Drill rig"); d.rigGo.transform.SetParent(d.t, false); d.rigGo.transform.localPosition = new Vector3(0f, 0f, -2.6f);
            for (int k = 0; k < 3; k++) { var leg = Prim(PrimitiveType.Cube, d.rigGo.transform, new Vector3(Mathf.Sin(k * 2.094f) * 0.6f, 1.5f, Mathf.Cos(k * 2.094f) * 0.6f), new Vector3(0.1f, 3.2f, 0.1f), steel).transform; leg.localRotation = Quaternion.Euler(Mathf.Cos(k * 2.094f) * 20f, 0f, -Mathf.Sin(k * 2.094f) * 20f); }
            Prim(PrimitiveType.Cube, d.rigGo.transform, new Vector3(0f, 3.0f, 0f), new Vector3(0.6f, 0.4f, 0.6f), gold);
            d.bit = Prim(PrimitiveType.Cylinder, d.rigGo.transform, new Vector3(0f, 1.4f, 0f), new Vector3(0.22f, 1.2f, 0.22f), dark).transform;
            d.rigGo.SetActive(false);
            FacButton(d.t, new Vector3(2.4f, 0.8f, -2.2f), new Vector3(0.7f, 0.7f, 0.7f), gold, 600 + i);
            d.label = FacLabel(d.t, 4.2f, 3.2f);
            facDeps.Add(d);
        }

        // ---- the power totem: stacked carved blocks; a flame on top while coal is burning
        var totem = new GameObject("Power totem").transform; totem.SetParent(r, false); totem.localPosition = new Vector3(-14f, 0f, 2f);
        Color[] tc = { new Color(0.75f, 0.2f, 0.15f), new Color(0.95f, 0.74f, 0.22f), new Color(0.15f, 0.5f, 0.6f), new Color(0.8f, 0.82f, 0.86f) };
        for (int k = 0; k < 4; k++)
        {
            Prim(PrimitiveType.Cube, totem, new Vector3(0f, 0.75f + k * 1.5f, 0f), new Vector3(1.5f - k * 0.12f, 1.4f, 1.5f - k * 0.12f), Lit(tc[k]));
            for (int e = -1; e <= 1; e += 2) Prim(PrimitiveType.Sphere, totem, new Vector3(e * 0.33f, 0.95f + k * 1.5f, -0.74f + k * 0.06f), Vector3.one * 0.22f, Glow(new Color(0.3f, 1.6f, 2.2f)));
            Prim(PrimitiveType.Cube, totem, new Vector3(0f, 0.5f + k * 1.5f, -0.74f + k * 0.06f), new Vector3(0.6f, 0.1f, 0.06f), dark);
        }
        Prim(PrimitiveType.Cube, totem, new Vector3(0f, 6.2f, 0f), new Vector3(2.6f, 0.2f, 0.5f), gold);
        facFlame = Prim(PrimitiveType.Sphere, totem, new Vector3(0f, 6.9f, 0f), new Vector3(0.9f, 1.4f, 0.9f), Glow(new Color(2.6f, 1.2f, 0.2f))).transform;
        var tb = totem.gameObject.AddComponent<BoxCollider>(); tb.center = new Vector3(0f, 3f, 0f); tb.size = new Vector3(1.8f, 6f, 1.8f);
        var tt = totem.gameObject.AddComponent<HunchTarget>(); tt.kind = 35; tt.index = 400;
        facTotemLabel = FacLabel(totem, 8.6f, 3.4f);

        // ---- the production line in the middle
        AddMachine(r, "IRON SMELTER", new Vector3(-6f, 0f, 16f), new Color(0.55f, 0.3f, 0.2f), true, new[] { FIronOre }, new[] { 2 }, FIron, 1, 4f);
        AddMachine(r, "GOLD SMELTER", new Vector3(2f, 0f, 16f), new Color(0.6f, 0.5f, 0.15f), true, new[] { FGoldOre }, new[] { 2 }, FGold, 1, 6f);
        AddMachine(r, "GEAR PRESS", new Vector3(-6f, 0f, 26f), new Color(0.3f, 0.4f, 0.55f), false, new[] { FIron }, new[] { 1 }, FGear, 2, 3f);
        AddMachine(r, "ENGINE WORKS", new Vector3(2f, 0f, 26f), new Color(0.25f, 0.5f, 0.45f), false, new[] { FGear, FIron, FGold }, new[] { 2, 1, 1 }, FEngine, 1, 8f);
        AddMachine(r, "EQUIPMENT FACTORY", new Vector3(-6f, 0f, 36f), new Color(0.45f, 0.3f, 0.55f), false, new[] { FEngine, FGear, FStone }, new[] { 1, 2, 2 }, FKit, 1, 9f);
        AddMachine(r, "BASE PANEL PLANT", new Vector3(2f, 0f, 36f), new Color(0.5f, 0.5f, 0.55f), false, new[] { FIron, FStone, FGold }, new[] { 3, 2, 1 }, FPanel, 1, 8f);
        var belt = Glow(new Color(1.3f, 0.9f, 0.2f));
        Prim(PrimitiveType.Cube, r, new Vector3(-2f, 0.02f, 26f), new Vector3(0.5f, 0.03f, 26f), belt); Prim(PrimitiveType.Cube, r, new Vector3(-2f, 0.02f, 16f), new Vector3(12f, 0.03f, 0.5f), belt);
        Prim(PrimitiveType.Cube, r, new Vector3(-2f, 0.02f, 26f), new Vector3(12f, 0.03f, 0.5f), belt); Prim(PrimitiveType.Cube, r, new Vector3(-2f, 0.02f, 36f), new Vector3(12f, 0.03f, 0.5f), belt);

        // ---- chests near the arrival point: look inside to see what has been made
        string[] cn = { "RAW MATERIALS", "METALS", "PARTS", "HARVEST" }; Color[] cc = { new Color(0.35f, 0.3f, 0.26f), new Color(0.6f, 0.45f, 0.2f), new Color(0.25f, 0.4f, 0.6f), new Color(0.25f, 0.55f, 0.25f) };
        for (int i = 0; i < 4; i++)
        {
            var c = new GameObject("Chest: " + cn[i]).transform; c.SetParent(r, false); c.localPosition = new Vector3(-6f + i * 4f, 0f, 2f);
            FacButton(c, new Vector3(0f, 0.55f, 0f), new Vector3(2.0f, 1.1f, 1.3f), Lit(cc[i]), 100 + i);
            Prim(PrimitiveType.Cube, c, new Vector3(0f, 1.2f, 0f), new Vector3(2.1f, 0.25f, 1.4f), Lit(cc[i] * 0.7f)); Prim(PrimitiveType.Cube, c, new Vector3(0f, 0.8f, -0.67f), new Vector3(0.3f, 0.35f, 0.06f), gold);
            facChestLabel[i] = FacLabel(c, 2.6f, 2.4f);
        }

        // ---- the rocket pad that ships finished parts to the Moon base
        var pad = new GameObject("Shipping pad").transform; pad.SetParent(r, false); pad.localPosition = new Vector3(12f, 0f, 36f);
        Prim(PrimitiveType.Cylinder, pad, new Vector3(0f, 0.1f, 0f), new Vector3(6f, 0.1f, 6f), dark); Prim(PrimitiveType.Cylinder, pad, new Vector3(0f, 0.22f, 0f), new Vector3(6.3f, 0.02f, 6.3f), belt);
        Prim(PrimitiveType.Cylinder, pad, new Vector3(0f, 2.6f, 0f), new Vector3(1.4f, 2.4f, 1.4f), Lit(new Color(0.85f, 0.86f, 0.9f))); Prim(PrimitiveType.Sphere, pad, new Vector3(0f, 5.2f, 0f), new Vector3(1.4f, 2.0f, 1.4f), Lit(new Color(0.8f, 0.2f, 0.15f)));
        for (int k = 0; k < 3; k++) Prim(PrimitiveType.Cube, pad, new Vector3(Mathf.Sin(k * 2.094f) * 0.9f, 0.8f, Mathf.Cos(k * 2.094f) * 0.9f), new Vector3(0.15f, 1.4f, 0.8f), gold).transform.localRotation = Quaternion.Euler(0f, k * 120f, 0f);
        FacButton(pad, new Vector3(0f, 0.9f, -3.6f), new Vector3(1.2f, 0.9f, 0.5f), Glow(new Color(0.4f, 1.8f, 0.6f)), 500);
        facPadLabel = FacLabel(pad, 7.6f, 3.2f);

        // ---- the garden on the east side: eight soil plots, watered from the fish tank
        var soilM = Lit(new Color(0.27f, 0.17f, 0.1f)); var wood = Lit(new Color(0.5f, 0.36f, 0.2f));
        for (int i = 0; i < 8; i++)
        {
            var p = new FacPlot(); p.holder = new GameObject("Soil plot " + (i + 1)).transform; p.holder.SetParent(r, false); p.holder.localPosition = new Vector3(16f + (i % 4) * 5f, 0f, 6f + (i / 4) * 6f);
            FacButton(p.holder, new Vector3(0f, 0.2f, 0f), new Vector3(3.2f, 0.4f, 3.2f), soilM, 200 + i);
            Prim(PrimitiveType.Cube, p.holder, new Vector3(0f, 0.22f, -1.65f), new Vector3(3.5f, 0.5f, 0.15f), wood); Prim(PrimitiveType.Cube, p.holder, new Vector3(0f, 0.22f, 1.65f), new Vector3(3.5f, 0.5f, 0.15f), wood);
            Prim(PrimitiveType.Cube, p.holder, new Vector3(-1.65f, 0.22f, 0f), new Vector3(0.15f, 0.5f, 3.2f), wood); Prim(PrimitiveType.Cube, p.holder, new Vector3(1.65f, 0.22f, 0f), new Vector3(0.15f, 0.5f, 3.2f), wood);
            p.plant = new GameObject("Plant").transform; p.plant.SetParent(p.holder, false); p.plant.localPosition = new Vector3(0f, 0.4f, 0f);
            Prim(PrimitiveType.Cylinder, p.plant, new Vector3(0f, 0.7f, 0f), new Vector3(0.12f, 0.7f, 0.12f), Lit(new Color(0.2f, 0.5f, 0.15f)));
            p.leaf = Prim(PrimitiveType.Sphere, p.plant, new Vector3(0f, 1.3f, 0f), new Vector3(1.5f, 0.9f, 1.5f), Lit(new Color(0.2f, 0.6f, 0.2f))).GetComponent<Renderer>();
            for (int k = 0; k < 3; k++) Prim(PrimitiveType.Sphere, p.plant, new Vector3(Mathf.Sin(k * 2.094f) * 0.6f, 0.8f, Mathf.Cos(k * 2.094f) * 0.6f), new Vector3(0.9f, 0.3f, 0.6f), Lit(new Color(0.25f, 0.65f, 0.22f)));
            for (int k = 0; k < 5; k++) { var f = Prim(PrimitiveType.Sphere, p.plant, new Vector3(Mathf.Sin(k * 1.257f) * 0.75f, 1.0f + (k % 2) * 0.4f, Mathf.Cos(k * 1.257f) * 0.75f), Vector3.one * 0.34f, Glow(Color.red)); f.SetActive(false); p.fruit.Add(f); }
            p.plant.gameObject.SetActive(false);
            p.label = FacLabel(p.holder, 3.6f, 2.2f);
            // a water pipe to each plot
            facPipes.Add(Prim(PrimitiveType.Cube, r, new Vector3(16f + (i % 4) * 5f, 0.08f, 20f - (i / 4 == 0 ? 6f : 3f) + (i / 4 == 0 ? -1f : 0f)), new Vector3(0.18f, 0.12f, i / 4 == 0 ? 12f : 6f), facPipeOff).GetComponent<Renderer>());
            facPlots.Add(p);
        }
        facPipes.Add(Prim(PrimitiveType.Cube, r, new Vector3(23.5f, 0.08f, 20f), new Vector3(15.5f, 0.12f, 0.18f), facPipeOff).GetComponent<Renderer>());
        // the fish tank: the fish make the water rich, the pump sends it along the pipes to the soil
        facTank = new GameObject("Fish tank").transform; facTank.SetParent(r, false); facTank.localPosition = new Vector3(23.5f, 0f, 25f);
        Prim(PrimitiveType.Cube, facTank, new Vector3(0f, 0.25f, 0f), new Vector3(6.4f, 0.5f, 3.4f), steel);
        FacButton(facTank, new Vector3(0f, 1.5f, 0f), new Vector3(6f, 2f, 3f), Clear(new Color(0.2f, 0.6f, 1f, 0.32f)), 300);
        Prim(PrimitiveType.Cube, facTank, new Vector3(0f, 2.6f, 0f), new Vector3(6.4f, 0.2f, 3.4f), steel);
        Prim(PrimitiveType.Cube, facTank, new Vector3(0f, 1.0f, -2.2f), new Vector3(1.0f, 2.0f, 0.9f), gold);      // the pump
        Color[] fc = { new Color(1f, 0.5f, 0.1f), new Color(1f, 0.85f, 0.2f), new Color(0.9f, 0.3f, 0.3f), new Color(0.9f, 0.9f, 0.95f), new Color(1f, 0.6f, 0.3f) };
        for (int k = 0; k < 5; k++)
        {
            var f = new GameObject("Fish").transform; f.SetParent(facTank, false);
            Prim(PrimitiveType.Sphere, f, Vector3.zero, new Vector3(0.22f, 0.3f, 0.6f), Lit(fc[k])); Prim(PrimitiveType.Cube, f, new Vector3(0f, 0f, -0.36f), new Vector3(0.04f, 0.3f, 0.22f), Lit(fc[k] * 0.8f));
            facFish.Add(f);
        }
        facTankLabel = FacLabel(facTank, 4.4f, 2.6f);
        var sign = Label(r, "<b>FACTORY LAND</b>\n<size=55%>West: drill coal, iron, gold and stone.   Middle: the totem burns coal for power; smelters and factories do the rest.\nEast: plant the soil, feed the fish, pick what ripens.   Chests show what you have. The rocket pad ships parts to the Moon base.</size>", 0f, 0f, 7f, 60f, 8f, new Color(1f, 0.84f, 0.3f));
        sign.transform.localPosition = new Vector3(0f, 9f, 48f);

        A("Iron age", "Smelt your first iron bar in Factory Land.", () => facBars > 0);
        A("Factory boss", "Make a base panel and ship it from the rocket pad.", () => facShipped > 0);
        try { FacKit(r); } catch (Exception e) { HLog.E("Minigames", "factory kit: " + e); }
        A("Green thumb", "Harvest five crops from the garden.", () => facHarvest >= 5);
    }

    void AddMachine(Transform r, string name, Vector3 lp, Color c, bool smelter, int[] inItem, int[] inCount, int outItem, int outCount, float time)
    {
        var m = new FacMachine { name = name, inItem = inItem, inCount = inCount, outItem = outItem, outCount = outCount, time = time };
        m.root = new GameObject(name).transform; m.root.SetParent(r, false); m.root.localPosition = lp;
        var dark = Lit(new Color(0.16f, 0.17f, 0.2f)); var gold = Lit(new Color(0.95f, 0.74f, 0.22f));
        FacButton(m.root, new Vector3(0f, 1.3f, 0f), new Vector3(3.4f, 2.6f, 3.2f), Lit(c), facMachines.Count);
        Prim(PrimitiveType.Cube, m.root, new Vector3(0f, 2.7f, 0f), new Vector3(3.7f, 0.25f, 3.5f), dark);
        if (smelter)
        {
            Prim(PrimitiveType.Cylinder, m.root, new Vector3(0.9f, 4.0f, 0.8f), new Vector3(0.8f, 1.3f, 0.8f), dark);
            Prim(PrimitiveType.Cube, m.root, new Vector3(0f, 0.9f, -1.62f), new Vector3(1.4f, 1.0f, 0.06f), Glow(new Color(2.4f, 0.9f, 0.2f)));      // the glowing furnace mouth
        }
        else
        {
            Prim(PrimitiveType.Cube, m.root, new Vector3(-1.0f, 3.5f, 0f), new Vector3(0.5f, 1.4f, 0.5f), dark);
            Prim(PrimitiveType.Cube, m.root, new Vector3(0f, 0.9f, -1.62f), new Vector3(1.6f, 0.9f, 0.06f), Glow(new Color(0.3f, 0.9f, 1.8f)));      // the control screen
        }
        m.spin = Prim(PrimitiveType.Cylinder, m.root, new Vector3(1.1f, 1.9f, -1.68f), new Vector3(0.9f, 0.06f, 0.9f), gold).transform; m.spin.localRotation = Quaternion.Euler(90f, 0f, 0f);
        for (int k = 0; k < 4; k++) Prim(PrimitiveType.Cube, m.spin, new Vector3(Mathf.Sin(k * 1.571f) * 0.5f, 0f, Mathf.Cos(k * 1.571f) * 0.5f), new Vector3(0.18f, 1.2f, 0.18f), gold);
        m.lamp = Prim(PrimitiveType.Sphere, m.root, new Vector3(-1.3f, 3.0f, -1.5f), Vector3.one * 0.4f, facLampOff).GetComponent<Renderer>();
        m.label = FacLabel(m.root, 5.6f, 2.8f);
        m.cap = outItem == FGear ? 6 : (outItem == FEngine ? 4 : (outItem == FKit ? 3 : 999));      // a part is only made while storage is short of it, so bars are left for the next machine
        facMachines.Add(m);
    }

    // ------------------------------------------------------------------ what each thing says and does when you aim and click
    string Recipe(FacMachine m)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < m.inItem.Length; i++) sb.Append(i > 0 ? " + " : "").Append(m.inCount[i]).Append(" ").Append(FacName[m.inItem[i]].ToLower());
        return sb.Append(" -> ").Append(m.outCount).Append(" ").Append(FacName[m.outItem].ToLower()).ToString();
    }
    string FacMissing(FacMachine m) { for (int i = 0; i < m.inItem.Length; i++) if (fac[m.inItem[i]] < m.inCount[i]) return FacName[m.inItem[i]].ToLower(); return ""; }

    string FacDepText(int i)
    {
        if (i < 0 || i >= facDeps.Count) return ""; var d = facDeps[i];
        return "drill: " + FacName[d.item].ToLower() + "   (you have " + fac[d.item] + ")";
    }

    string FacText(int i)
    {
        if (i >= 0 && i < facMachines.Count) { var m = facMachines[i]; return m.name.ToLower() + ": switch " + (m.on ? "OFF" : "ON") + "   (" + Recipe(m) + ")"; }
        if (i >= 100 && i < 104) return "look inside the chest";
        if (i >= 200 && i < 200 + facPlots.Count) { var p = facPlots[i - 200]; return p.crop < 0 ? "plant " + FacName[FCrop + (i - 200) % 5].ToLower() + " seeds" : (p.grow >= 1f ? "pick the ripe " + FacName[FCrop + p.crop].ToLower() : FacName[FCrop + p.crop].ToLower() + " growing: " + Mathf.RoundToInt(p.grow * 100f) + "%"); }
        if (i == 300) return facFed > 30f ? "fish tank: the fish are fed (" + Mathf.CeilToInt(facFed) + " s)" : "feed the fish (uses one crop or one food pack)";
        if (i == 400) return "power totem: switch " + (facTotemOn ? "OFF" : "ON") + "   (coal in storage: " + fac[FCoal] + ")";
        if (i == 500) return "ship " + (fac[FPanel] + fac[FKit] + fac[FEngine]) + " finished parts to the Moon base";
        if (i >= 600 && i < 600 + facDeps.Count) { var d = facDeps[i - 600]; return d.rig ? "drill rig: running when the totem has power" : "build a drill rig here (2 iron bars + 1 gear)"; }
        return i >= 800 ? ExtraText(i) : StudioText(i);
    }

    void FacUse(int i)
    {
        if (i >= 0 && i < facMachines.Count) { var m = facMachines[i]; m.on = !m.on; Play(cClank, 0.7f); Note(m.name + (m.on ? " ON" : " OFF") + "   " + Recipe(m)); return; }
        if (i >= 100 && i < 104) { if (facSheetIndex >= 0) OpenRead(facSheetIndex); return; }
        if (i >= 200 && i < 200 + facPlots.Count) { PlotUse(i - 200); return; }
        if (i == 300)
        {
            if (facFed > 30f) { Note("The fish are fed. Their water is flowing to the soil."); return; }
            int crop = -1; for (int c = 0; c < 5; c++) if (fac[FCrop + c] > 0) { crop = c; break; }
            if (crop >= 0) fac[FCrop + crop]--; else if (food > 0) food--; else { Note("Nothing to feed the fish with. Pick a crop or bring a food pack."); return; }
            facFed = 150f; Play(cGrow, 0.7f); Note("Fish fed! The pump sends their water to the soil for 150 seconds: plants grow 4 times faster."); return;
        }
        if (i == 400) { facTotemOn = !facTotemOn; Play(cClank, 0.7f); Note("Power totem " + (facTotemOn ? "ON: it burns one coal every 15 seconds" : "OFF") + ".  Coal in storage: " + fac[FCoal]); return; }
        if (i == 500) { FacShip(); return; }
        if (i >= 600 && i < 600 + facDeps.Count)
        {
            var d = facDeps[i - 600];
            if (d.rig) { Note("This drill rig mines one " + FacName[d.item].ToLower() + " every 3 seconds while the totem has power."); return; }
            if (fac[FIron] < 2 || fac[FGear] < 1) { Note("A drill rig costs 2 iron bars and 1 gear. Smelt iron ore, then press gears."); return; }
            fac[FIron] -= 2; fac[FGear] -= 1; d.rig = true; d.rigGo.SetActive(true); Play(cClank, 0.8f); HunchFx.Spawn("star hit", d.t.position + Vector3.up * 2f, 0.6f, 0.8f);
            Note("Drill rig built on the " + d.t.name.ToLower() + ". It runs by itself while the totem has power."); return;
        }
        if (i >= 800) ExtraUse(i); else StudioUse(i);
    }

    void PlotUse(int k)
    {
        var p = facPlots[k];
        if (p.crop < 0)
        {
            p.crop = k % 5; p.grow = 0f; p.plant.gameObject.SetActive(true); foreach (var f in p.fruit) { f.SetActive(false); f.GetComponent<Renderer>().sharedMaterial = Glow(CropColor[p.crop] * 1.3f); }
            Play(cGrow, 0.6f); Note(FacName[FCrop + p.crop] + " seeds planted. " + (facFed > 0f ? "The fish-tank water is flowing, so they grow fast." : "Feed the fish to water the soil and grow them faster.")); return;
        }
        if (p.grow < 1f) { Note(FacName[FCrop + p.crop] + ": " + Mathf.RoundToInt(p.grow * 100f) + "% grown. " + (facFed > 0f ? "" : "No water is flowing: feed the fish.")); return; }
        fac[FCrop + p.crop] += 2; facHarvest++; p.grow = 0f; foreach (var f in p.fruit) f.SetActive(false);
        if (food < 12) food++;
        Play(cPick, 0.7f); HunchFx.Spawn("star hit", p.holder.position + Vector3.up * 1.4f, 0.5f, 0.8f);
        Note("+2 " + FacName[FCrop + p.crop].ToLower() + " and +1 food pack. The plant starts growing again.");
    }

    void FacShip()
    {
        int panels = fac[FPanel], kits = fac[FKit], engines = fac[FEngine];
        if (panels + kits + engines == 0) { Note("Nothing to ship yet. Make engines, tool kits or base panels first."); return; }
        fac[FPanel] = 0; fac[FKit] = 0; fac[FEngine] = 0; facShipped += panels + kits + engines;
        sim.battery = Mathf.Min(100f, sim.battery + panels * 3f + kits * 2f); sim.arrays = Mathf.Min(100f, sim.arrays + panels * 2f); sim.dust = Mathf.Max(0f, sim.dust - kits * 2f);
        starBoost = Mathf.Min(10, starBoost + engines);
        Play(cWarp, 0.9f); if (facPadLabel) HunchFx.Spawn("teleport", facPadLabel.transform.position - Vector3.up * 5f, 1.2f, 1.6f);
        Banner("SHIPPED TO THE BASE", panels + " base panels, " + kits + " tool kits, " + engines + " engines", false);
        sim.Say("ARIA: Delivery received. Base panels strengthen the solar arrays and batteries, tool kits clean the dust, and each engine makes your star fighter faster (boost " + starBoost + " of 10).");
    }

    /// the Mining drill held on a deposit
    void FacDrill(int i, float dt, Vector3 at, bool spark)
    {
        if (i < 0 || i >= facDeps.Count) return; var d = facDeps[i];
        d.hp -= dt * drillLv; if (spark) HunchFx.Spawn("sparks yellow", at, 0.25f, 0.4f);
        if (d.hp > 0f) return;
        d.hp = 0.8f; fac[d.item]++; minedCount++; Play(cPick, 0.5f); game.Buzz(0.4f, 0.06f);
        Note("+1 " + FacName[d.item].ToLower() + "  (" + fac[d.item] + ")" + (d.item == FCoal && !FacPowered ? "   The totem will burn it for power." : ""));
    }

    string FacSheet()
    {
        var sb = new StringBuilder("<b>Everything in the factory's storage</b>\n\n");
        string[] group = { "RAW MATERIALS", "METALS", "PARTS", "HARVEST" }; int[][] items = { new[] { 0, 1, 2, 3 }, new[] { 4, 5 }, new[] { 6, 7, 8, 9 }, new[] { 10, 11, 12, 13, 14 } };
        for (int g = 0; g < 4; g++) { sb.Append("<b>").Append(group[g]).Append("</b>\n"); foreach (int it in items[g]) sb.Append("   ").Append(FacName[it]).Append(":  ").Append(fac[it]).Append("\n"); }
        sb.Append("\n<b>HOW IT IS MADE</b>\n"); foreach (var m in facMachines) sb.Append(" ").Append(m.name).Append(": ").Append(Recipe(m)).Append("\n");
        sb.Append("\nPower: ").Append(FacPowered ? "ON, " + Mathf.CeilToInt(facFuel) + " s of fuel burning" : "OFF").Append(", coal ").Append(fac[FCoal]).Append("\nShipped to the base so far: ").Append(facShipped).Append(" parts");
        return sb.ToString();
    }

    // ------------------------------------------------------------------ every frame: the factory and the garden keep working even when you are away
    void UpdateFactory(float dt)
    {
        if (facMachines.Count == 0 || !Playing || game.Paused) return;
        bool here = mapNow == 7; float t = Time.time;
        // the totem turns coal into power
        if (facTotemOn && facFuel <= 0f && fac[FCoal] > 0) { fac[FCoal]--; facFuel = 15f; }
        if (facTotemOn && facFuel > 0f) facFuel -= dt;
        bool power = FacPowered;
        // drill rigs
        foreach (var d in facDeps)
        {
            if (!d.rig) continue;
            if (power) { d.rigT += dt; if (d.rigT >= 3f) { d.rigT = 0f; fac[d.item]++; } }
            if (here && d.bit) d.bit.localPosition = new Vector3(0f, 1.4f + (power ? Mathf.Sin(t * 9f) * 0.35f : 0f), 0f);
        }
        // smelters and factories
        foreach (var m in facMachines)
        {
            bool run = m.on && power;
            if (run && !m.busy && fac[m.outItem] < m.cap && FacMissing(m).Length == 0) { for (int i = 0; i < m.inItem.Length; i++) fac[m.inItem[i]] -= m.inCount[i]; m.busy = true; m.t = m.time; }
            if (run && m.busy)
            {
                m.t -= dt;
                if (m.t <= 0f)
                {
                    m.busy = false; fac[m.outItem] += m.outCount; m.made += m.outCount; facMade += m.outCount; if (m.outItem == FIron) facBars++;
                    if (here && (m.root.position - cam.position).sqrMagnitude < 30f * 30f) Play(m.outItem == FIron || m.outItem == FGold ? cHiss : cClank, 0.35f);
                }
            }
            if (here)
            {
                if (run && m.busy) m.spin.Rotate(0f, 220f * dt, 0f, Space.Self);
                var lamp = !m.on ? facLampOff : (run && m.busy ? facLampOn : facLampWait); if (m.lamp.sharedMaterial != lamp) m.lamp.sharedMaterial = lamp;
            }
        }
        // the garden
        facFed = Mathf.Max(0f, facFed - dt); bool water = facFed > 0f;
        foreach (var p in facPlots)
        {
            if (p.crop < 0) continue;
            if (p.grow < 1f) { p.grow = Mathf.Min(1f, p.grow + dt / (water ? 45f : 180f)); if (p.grow >= 1f) { foreach (var f in p.fruit) f.SetActive(p.crop != 3); if (here) Play(cGrow, 0.4f); } }
            if (here)
            {
                float s = 0.18f + 0.82f * p.grow; if (Mathf.Abs(p.plant.localScale.x - s) > 0.004f) p.plant.localScale = Vector3.one * s;
            }
        }
        if (!here) return;
        // what you see: flame, fish, pipes, labels
        if (facFlame) { bool burn = power; if (facFlame.gameObject.activeSelf != burn) facFlame.gameObject.SetActive(burn); if (burn) facFlame.localScale = new Vector3(0.9f, 1.3f + Mathf.Sin(t * 14f) * 0.25f, 0.9f); }
        for (int k = 0; k < facFish.Count; k++)
        {
            float a = t * (0.5f + k * 0.11f) + k * 1.3f; facFish[k].localPosition = new Vector3(Mathf.Sin(a) * 2.3f, 1.1f + Mathf.Sin(a * 1.7f + k) * 0.45f, Mathf.Cos(a) * 0.9f);
            facFish[k].localRotation = Quaternion.LookRotation(new Vector3(Mathf.Cos(a) * 2.3f, 0f, -Mathf.Sin(a) * 0.9f));
        }
        var pm = water ? facPipeOn : facPipeOff; foreach (var r in facPipes) if (r.sharedMaterial != pm) r.sharedMaterial = pm;
        facUi -= dt; if (facUi > 0f) return; facUi = 0.4f;
        foreach (var m in facMachines)
        {
            string miss = FacMissing(m);
            string s = "<b>" + m.name + "</b>\n<size=62%>" + Recipe(m) + "\n" + (!m.on ? "<color=#FF8A7A>switched OFF</color>" : (!power ? "<color=#FFD070>no power: the totem needs coal</color>" : (m.busy ? "<color=#7CFC9A>working " + Mathf.CeilToInt(m.t) + " s</color>" : (fac[m.outItem] >= m.cap ? "<color=#9AD8FF>storage has enough (" + m.cap + ")</color>" : miss.Length > 0 ? "<color=#FFD070>waiting for " + miss + "</color>" : "ready"))))
                + "   made " + m.made + "</size>";
            if (m.label.text != s) m.label.text = s;
        }
        for (int i = 0; i < facDeps.Count; i++)
        {
            var d = facDeps[i]; string s = "<b>" + d.t.name + "</b>\n<size=62%>hold the Mining drill on it" + (d.rig ? "   rig: " + (power ? "<color=#7CFC9A>running</color>" : "<color=#FFD070>no power</color>") : "   gold box: build a rig") + "</size>";
            if (d.label.text != s) d.label.text = s;
        }
        string ts = "<b>POWER TOTEM</b>\n<size=62%>" + (!facTotemOn ? "<color=#FF8A7A>OFF</color>" : (power ? "<color=#7CFC9A>burning coal: " + Mathf.CeilToInt(facFuel) + " s</color>" : "<color=#FFD070>needs coal: drill a coal seam</color>")) + "   coal " + fac[FCoal] + "</size>";
        if (facTotemLabel.text != ts) facTotemLabel.text = ts;
        string[] cn = { "RAW MATERIALS", "METALS", "PARTS", "HARVEST" }; int[][] items = { new[] { 0, 1, 2, 3 }, new[] { 4, 5 }, new[] { 6, 7, 8, 9 }, new[] { 10, 11, 12, 13, 14 } };
        for (int g = 0; g < 4; g++)
        {
            var sb = new StringBuilder("<b>").Append(cn[g]).Append("</b>\n<size=60%>");
            for (int k = 0; k < items[g].Length; k++) sb.Append(k > 0 ? (k % 3 == 0 ? "\n" : "   ") : "").Append(FacName[items[g][k]]).Append(" ").Append(fac[items[g][k]]);
            string cs = sb.Append("</size>").ToString(); if (facChestLabel[g].text != cs) facChestLabel[g].text = cs;
        }
        for (int i = 0; i < facPlots.Count; i++)
        {
            var p = facPlots[i]; string s = p.crop < 0 ? "<size=70%>empty soil\nclick to plant</size>" : "<b>" + FacName[FCrop + p.crop] + "</b>\n<size=70%>" + (p.grow >= 1f ? "<color=#7CFC9A>RIPE: click to pick</color>" : "growing " + Mathf.RoundToInt(p.grow * 100f) + "%") + "</size>";
            if (p.label.text != s) p.label.text = s;
        }
        string fs = "<b>FISH TANK</b>\n<size=62%>" + (water ? "<color=#7CFC9A>pump ON: water flows to the soil (" + Mathf.CeilToInt(facFed) + " s)</color>" : "<color=#FFD070>the fish are hungry: click to feed them</color>") + "</size>";
        if (facTankLabel.text != fs) facTankLabel.text = fs;
        string ps = "<b>ROCKET PAD</b>\n<size=62%>ready to ship: " + fac[FPanel] + " panels, " + fac[FKit] + " tool kits, " + fac[FEngine] + " engines\nshipped so far: " + facShipped + "</size>";
        if (facPadLabel.text != ps) facPadLabel.text = ps;
    }

    string FacStatus()
    {
        return "Power " + (FacPowered ? Mathf.CeilToInt(facFuel) + " s" : "OFF") + "   Coal " + fac[FCoal] + "  Iron ore " + fac[FIronOre] + "  Gold ore " + fac[FGoldOre] + "  Stone " + fac[FStone]
            + "   Bars " + fac[FIron] + "/" + fac[FGold] + "  Gears " + fac[FGear] + "  Engines " + fac[FEngine] + "  Kits " + fac[FKit] + "  Panels " + fac[FPanel];
    }

    // ------------------------------------------------------------------ the autopilot shows the factory working
    IEnumerator AutoFactory()
    {
        AutoSay("Factory Land. Drilling coal for the power totem.");
        yield return AutoDrillDeposit(0, 4); yield return AutoDrillDeposit(2, 6);
        AutoSay("Iron ore goes into the smelter; the totem burns coal to power it.");
        TeleportTo(mapC[7] + new Vector3(-6f, 0f, 9f), 0f); yield return Turn(facMachines[0].root.position + Vector3.up * 2f, 0.6f); yield return new WaitForSeconds(6f);
        AutoSay("Planting the garden and feeding the fish.");
        TeleportTo(mapC[7] + new Vector3(18f, 0f, 1f), 0f); yield return new WaitForSeconds(0.5f);
        for (int i = 0; i < facPlots.Count; i++) if (facPlots[i].crop < 0) { yield return Turn(facPlots[i].holder.position + Vector3.up, 0.25f); PlotUse(i); yield return new WaitForSeconds(0.35f); }
        for (int i = 0; i < facPlots.Count; i++) if (facPlots[i].crop >= 0 && facPlots[i].grow >= 1f) { PlotUse(i); yield return new WaitForSeconds(0.3f); }
        if (facFed <= 30f) FacUse(300);
        yield return new WaitForSeconds(2f);
        AutoSay("Checking the chests: " + fac[FIron] + " iron bars so far.");
        TeleportTo(mapC[7] + new Vector3(0f, 0f, -3f), 0f); yield return new WaitForSeconds(2.5f);
        if (fac[FPanel] + fac[FKit] + fac[FEngine] > 0) FacShip();
    }
    IEnumerator AutoDrillDeposit(int i, int count)
    {
        if (i >= facDeps.Count) yield break; var d = facDeps[i];
        TeleportTo(d.t.position + new Vector3(4.5f, 0f, 0f), 270f); yield return new WaitForSeconds(0.4f); yield return Turn(d.t.position + Vector3.up, 0.5f);
        tool = Drill; float end = Time.time + count * 1.0f + 0.5f; int before = fac[d.item];
        while (Time.time < end && mapNow == 7 && fac[d.item] < before + count)
        {
            Vector3 bp = d.t.position + Vector3.up; Face(bp); drilling = true; drillBeam.enabled = true; drillBeam.SetPosition(0, Muzzle()); drillBeam.SetPosition(1, bp); FacDrill(i, Time.deltaTime * 1.4f, bp, false);
            yield return null;
        }
        tool = Hands;
    }

    public string ProbeFacGive() { fac[FCoal] += 12; fac[FIronOre] += 30; fac[FGoldOre] += 12; fac[FStone] += 12; return FacStatus(); }
    public string ProbeFacFast() { for (int k = 0; k < 600; k++) UpdateFactory(0.1f); return FacStatus() + " | made " + facMade + " bars " + facBars + " fed " + facFed.ToString("F0"); }
    public string ProbeFacPlots() { int ripe = 0, planted = 0; for (int i = 0; i < facPlots.Count; i++) { if (facPlots[i].crop < 0) PlotUse(i); else if (facPlots[i].grow >= 1f) { PlotUse(i); ripe++; } if (facPlots[i].crop >= 0) planted++; } return "planted " + planted + " picked " + ripe + " harvests " + facHarvest + " food " + food; }
    public string ProbeFacRig() { fac[FIron] += 2; fac[FGear] += 1; FacUse(600); FacUse(500); return "rig " + facDeps[0].rig + " shipped " + facShipped + " boost " + starBoost; }
    public string ProbeFacChest() { FacUse(100); return "sheet " + readOpen; }
}
