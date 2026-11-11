// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/06_Frontier/B_Maps_Minigames/HunchFrontier12.cs
//  Section : 06_Frontier/B_Maps_Minigames
//  Folder  : Extra maps and mini-games: solar farm, mining pit, Factory Land, Star Patrol, crew
//            radio + Star Clicker, Science Academy, My Astronaut, model kits, Fun Zone, endless
//            ground, Star Store pages.
//  Purpose : Twelfth part of Moon Frontier: MY ASTRONAUT experience points and levels. A level-up
//            shows your astronaut in front of a green screen with a PRESENT
//  Contains: HunchFrontier, Costume, Patient
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

/// Twelfth part of Moon Frontier:
///   MY ASTRONAUT          experience points and levels. A level-up shows your astronaut in front of a green screen with a PRESENT
///                          to open and a NEXT box. Presents hold star coins and small gifts.
///   STAR SHOP             star coins buy an instant level-up, a boss battle ticket, a stardust boost and costumes. Seasonal costumes
///                          can only be bought in their season and are kept for ever. Coins are only ever earned by playing.
///   STAR STORE            four one-time add-ons sold by the Meta Horizon Store (HunchStore): the Golden Pumpkin King, the Cosmic
///                          Suit Pack, the Lumi Pet Outfit Pack and the Supporter Pack. Meta keeps what was bought on the player's
///                          account, so nothing here is saved in the game's own progress and a reset can never remove it.
///   CLINIC AND PET HOUSE  (map 12) scan each patient, give the right care before their patience runs out; and look after Lumi the
///                          moon pup: feed, play, wash and sleep. Lumi grows up and earns care points.
///   BOSS ARENA            (map 13) the Grumpy Moon Golem. Zap its tickle spots for three rounds; it laughs, and hands over a Star Diamond.
///   SEASONS               decorations and drifting leaves, snow, petals or sparkles that follow the real season on Earth (and Halloween).
public partial class HunchFrontier
{
    // ================================================================== a simple astronaut figure
    Transform Figure(Transform parent, Vector3 lp, Material suit, Material trim)
    {
        var f = new GameObject("Astronaut").transform; f.SetParent(parent, false); f.localPosition = lp; var gold = Lit(new Color(0.98f, 0.76f, 0.22f));
        var mdl = KitSource(false, "Astronaut");
        if (mdl)
        {
            var g = Instantiate(mdl, f); g.name = "Model"; g.SetActive(true); g.transform.localPosition = Vector3.zero; g.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * mdl.transform.localRotation; foreach (var c in g.GetComponentsInChildren<Collider>(true)) Destroy(c);
            Color sc = suit.GetColor("_BaseColor"), tc = trim.GetColor("_BaseColor");
            foreach (var r in g.GetComponentsInChildren<Renderer>()) { var ms = r.materials; for (int i = 0; i < ms.Length; i++) { if (ms[i].name.StartsWith("Suit")) ms[i].SetColor("_BaseColor", sc); else if (ms[i].name.StartsWith("Trim")) ms[i].SetColor("_BaseColor", tc); } r.materials = ms; }
            kitPlaced++; return f;
        }
        Prim(PrimitiveType.Capsule, f, new Vector3(0f, 1.02f, 0f), new Vector3(0.46f, 0.36f, 0.34f), suit); Prim(PrimitiveType.Cube, f, new Vector3(0f, 1.08f, -0.22f), new Vector3(0.36f, 0.48f, 0.18f), trim);
        Prim(PrimitiveType.Sphere, f, new Vector3(0f, 1.56f, 0f), Vector3.one * 0.44f, suit); Prim(PrimitiveType.Sphere, f, new Vector3(0f, 1.56f, 0.12f), new Vector3(0.34f, 0.28f, 0.24f), gold);
        Prim(PrimitiveType.Cube, f, new Vector3(0f, 0.8f, 0f), new Vector3(0.44f, 0.07f, 0.34f), trim);
        foreach (float sx in new[] { -1f, 1f })
        {
            Prim(PrimitiveType.Capsule, f, new Vector3(sx * 0.3f, 1.02f, 0f), new Vector3(0.14f, 0.3f, 0.14f), suit); Prim(PrimitiveType.Capsule, f, new Vector3(sx * 0.12f, 0.38f, 0f), new Vector3(0.17f, 0.38f, 0.17f), suit);
            Prim(PrimitiveType.Cube, f, new Vector3(sx * 0.12f, 0.05f, 0.05f), new Vector3(0.19f, 0.1f, 0.3f), trim);
        }
        return f;
    }
    void Hat(Transform parent, Vector3 lp, int hat)
    {
        if (hat <= 0) return; var h = new GameObject("Hat").transform; h.SetParent(parent, false); h.localPosition = lp;
        switch (hat)
        {
            case 1: Prim(PrimitiveType.Sphere, h, new Vector3(0f, 0.12f, 0f), new Vector3(0.4f, 0.3f, 0.4f), Lit(new Color(0.95f, 0.45f, 0.08f))); Prim(PrimitiveType.Cylinder, h, new Vector3(0f, 0.3f, 0f), new Vector3(0.05f, 0.06f, 0.05f), Lit(new Color(0.2f, 0.5f, 0.15f))); break;
            case 2: for (int i = 0; i < 5; i++) Prim(PrimitiveType.Cylinder, h, new Vector3(0f, 0.04f + i * 0.1f, 0f), new Vector3(0.5f - i * 0.1f + (i == 0 ? 0.2f : 0f), i == 0 ? 0.015f : 0.05f, 0.5f - i * 0.1f + (i == 0 ? 0.2f : 0f)), Lit(new Color(0.4f, 0.15f, 0.6f))); Prim(PrimitiveType.Sphere, h, new Vector3(0f, 0.52f, 0f), Vector3.one * 0.08f, Glow(new Color(2.2f, 1.8f, 0.4f))); break;
            case 3: Prim(PrimitiveType.Sphere, h, new Vector3(0f, 0.05f, 0f), new Vector3(0.42f, 0.32f, 0.42f), Lit(new Color(0.85f, 0.12f, 0.12f))); Prim(PrimitiveType.Cylinder, h, new Vector3(0f, -0.02f, 0f), new Vector3(0.46f, 0.03f, 0.46f), Lit(Color.white)); Prim(PrimitiveType.Sphere, h, new Vector3(0f, 0.24f, 0f), Vector3.one * 0.12f, Lit(Color.white)); break;
            case 4: for (int i = 0; i < 8; i++) { float a = i * Mathf.PI / 4f; Prim(PrimitiveType.Sphere, h, new Vector3(Mathf.Cos(a) * 0.2f, 0f, Mathf.Sin(a) * 0.2f), Vector3.one * 0.1f, Lit(i % 2 == 0 ? new Color(0.95f, 0.5f, 0.7f) : new Color(0.98f, 0.9f, 0.3f))); } break;
            case 5: Prim(PrimitiveType.Cylinder, h, new Vector3(0f, 0.02f, 0f), new Vector3(0.75f, 0.012f, 0.75f), Lit(new Color(0.95f, 0.85f, 0.4f))); Prim(PrimitiveType.Sphere, h, new Vector3(0f, 0.05f, 0f), new Vector3(0.4f, 0.26f, 0.4f), Lit(new Color(0.95f, 0.85f, 0.4f))); break;
            case 7:      // Cosmic Nebula: a floating halo of pink, violet and blue lights over a violet cap
                for (int i = 0; i < 12; i++) { float a = i * Mathf.PI / 6f; Color gc = i % 3 == 0 ? new Color(2.4f, 0.6f, 1.6f) : (i % 3 == 1 ? new Color(1.3f, 0.5f, 2.7f) : new Color(0.5f, 1.4f, 2.7f)); Prim(PrimitiveType.Sphere, h, new Vector3(Mathf.Cos(a) * 0.3f, 0.2f + Mathf.Sin(a * 3f) * 0.025f, Mathf.Sin(a) * 0.3f), Vector3.one * 0.075f, Glow(gc)); }
                Prim(PrimitiveType.Sphere, h, new Vector3(0f, 0.03f, 0f), new Vector3(0.38f, 0.12f, 0.38f), Lit(new Color(0.5f, 0.2f, 0.78f))); Prim(PrimitiveType.Sphere, h, new Vector3(0f, 0.1f, 0f), Vector3.one * 0.09f, Glow(new Color(2.6f, 1.2f, 2.4f))); break;
            case 8:      // Comet Racer: a bright comet on the front of the helmet, its tail streaming back
                Prim(PrimitiveType.Sphere, h, new Vector3(0f, 0.07f, 0.13f), Vector3.one * 0.16f, Glow(new Color(2.7f, 2.7f, 2.9f)));
                for (int i = 1; i <= 6; i++) Prim(PrimitiveType.Sphere, h, new Vector3(0f, 0.07f + i * 0.018f, 0.13f - i * 0.075f), Vector3.one * (0.15f - i * 0.018f), Glow(Color.Lerp(new Color(0.6f, 2.3f, 2.8f), new Color(0.5f, 0.6f, 2.6f), i / 6f)));
                Prim(PrimitiveType.Cube, h, new Vector3(0f, 0.01f, -0.04f), new Vector3(0.05f, 0.05f, 0.4f), Lit(new Color(0.92f, 0.95f, 1f))); break;
            case 9:      // Starlight Galaxy: a crown of five glowing stars around a violet galaxy
                for (int i = 0; i < 5; i++)
                {
                    float a = i * Mathf.PI * 0.4f; Vector3 at = new Vector3(Mathf.Cos(a) * 0.19f, 0.15f, Mathf.Sin(a) * 0.19f);
                    Prim(PrimitiveType.Cube, h, at, Vector3.one * 0.085f, Glow(i % 2 == 0 ? new Color(2.8f, 2.3f, 0.8f) : new Color(2.6f, 2.6f, 2.9f))).transform.localRotation = Quaternion.Euler(45f, a * Mathf.Rad2Deg, 45f);
                    Prim(PrimitiveType.Cylinder, h, new Vector3(at.x, 0.07f, at.z), new Vector3(0.025f, 0.05f, 0.025f), Lit(new Color(0.82f, 0.84f, 0.95f)));
                }
                Prim(PrimitiveType.Cylinder, h, new Vector3(0f, 0.02f, 0f), new Vector3(0.44f, 0.015f, 0.44f), Lit(new Color(0.22f, 0.2f, 0.5f)));
                Prim(PrimitiveType.Sphere, h, new Vector3(0f, 0.12f, 0f), new Vector3(0.15f, 0.05f, 0.15f), Glow(new Color(1.4f, 0.8f, 2.7f))); break;
            default: Prim(PrimitiveType.Cylinder, h, new Vector3(0f, 0.06f, 0f), new Vector3(0.36f, 0.06f, 0.36f), Lit(new Color(0.98f, 0.76f, 0.22f))); for (int i = 0; i < 6; i++) { float a = i * Mathf.PI / 3f; Prim(PrimitiveType.Cube, h, new Vector3(Mathf.Cos(a) * 0.16f, 0.17f, Mathf.Sin(a) * 0.16f), new Vector3(0.05f, 0.12f, 0.05f), Lit(new Color(0.98f, 0.76f, 0.22f))); } Prim(PrimitiveType.Sphere, h, new Vector3(0f, 0.16f, 0f), Vector3.one * 0.1f, Glow(new Color(2.4f, 0.5f, 0.2f))); break;
        }
    }

    // ================================================================== seasons
    public bool seasonOn = true; Transform seasonRoot; readonly List<Transform> seasonBits = new List<Transform>(); bool seasonSaid; float seasonSayIn = 40f;
    static int SeasonNow() { int m = DateTime.Now.Month; return m == 12 || m <= 2 ? 0 : (m <= 5 ? 1 : (m <= 8 ? 2 : 3)); }      // seasons of the northern half of Earth
    static bool Halloween() { var d = DateTime.Now; return d.Month == 10 || (d.Month == 11 && d.Day <= 2); }
    static readonly string[] SeasonNames = { "Winter", "Spring", "Summer", "Autumn" };
    string SeasonTitle { get { return SeasonNames[SeasonNow()] + (Halloween() ? " - Halloween" : ""); } }
    /// does a costume's season match today? -1 always, 0-3 the season, 4 Halloween
    static bool InSeason(int s) { return s < 0 || (s == 4 ? Halloween() : s == SeasonNow()); }

    void Decor(Transform p, Vector3 lp, float k)
    {
        var d = new GameObject("Season decoration").transform; d.SetParent(p, false); d.localPosition = lp; d.localScale = Vector3.one * k; seasonBits.Add(d);
        if (Halloween())
        {
            if (Kit(false, "Pumpkin", d, Vector3.zero, 0f, 0f, false)) return;
            Prim(PrimitiveType.Sphere, d, new Vector3(0f, 0.42f, 0f), new Vector3(1.1f, 0.85f, 1.1f), Lit(new Color(0.95f, 0.45f, 0.08f))); Prim(PrimitiveType.Cylinder, d, new Vector3(0f, 0.92f, 0f), new Vector3(0.12f, 0.1f, 0.12f), Lit(new Color(0.2f, 0.45f, 0.15f)));
            var face = Glow(new Color(2.6f, 1.9f, 0.4f)); Prim(PrimitiveType.Cube, d, new Vector3(-0.2f, 0.55f, -0.5f), new Vector3(0.16f, 0.16f, 0.1f), face).transform.localRotation = Quaternion.Euler(0f, 0f, 45f); Prim(PrimitiveType.Cube, d, new Vector3(0.2f, 0.55f, -0.5f), new Vector3(0.16f, 0.16f, 0.1f), face).transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Prim(PrimitiveType.Cube, d, new Vector3(0f, 0.3f, -0.52f), new Vector3(0.5f, 0.1f, 0.1f), face); return;
        }
        switch (SeasonNow())
        {
            case 0: Prim(PrimitiveType.Sphere, d, new Vector3(0f, 0.45f, 0f), Vector3.one * 0.9f, Lit(Color.white)); Prim(PrimitiveType.Sphere, d, new Vector3(0f, 1.1f, 0f), Vector3.one * 0.65f, Lit(Color.white)); Prim(PrimitiveType.Sphere, d, new Vector3(0f, 1.6f, 0f), Vector3.one * 0.45f, Lit(Color.white)); Prim(PrimitiveType.Cylinder, d, new Vector3(0f, 1.6f, -0.28f), new Vector3(0.07f, 0.12f, 0.07f), Lit(new Color(0.95f, 0.5f, 0.1f))).transform.localRotation = Quaternion.Euler(90f, 0f, 0f); break;
            case 1: Prim(PrimitiveType.Cylinder, d, new Vector3(0f, 0.2f, 0f), new Vector3(0.6f, 0.2f, 0.6f), Lit(new Color(0.6f, 0.35f, 0.2f))); for (int i = 0; i < 5; i++) { float a = i * 1.26f; Prim(PrimitiveType.Cylinder, d, new Vector3(Mathf.Cos(a) * 0.15f, 0.6f, Mathf.Sin(a) * 0.15f), new Vector3(0.03f, 0.25f, 0.03f), Lit(new Color(0.2f, 0.6f, 0.25f))); Prim(PrimitiveType.Sphere, d, new Vector3(Mathf.Cos(a) * 0.15f, 0.9f, Mathf.Sin(a) * 0.15f), Vector3.one * 0.2f, Lit(i % 2 == 0 ? new Color(0.95f, 0.5f, 0.7f) : new Color(0.98f, 0.9f, 0.3f))); } break;
            case 2: Prim(PrimitiveType.Sphere, d, new Vector3(0f, 0.45f, 0f), Vector3.one * 0.9f, Lit(new Color(0.95f, 0.3f, 0.25f))); Prim(PrimitiveType.Sphere, d, new Vector3(0f, 0.45f, 0f), new Vector3(0.92f, 0.3f, 0.92f), Lit(Color.white)); Prim(PrimitiveType.Cylinder, d, new Vector3(1f, 1.1f, 0f), new Vector3(0.05f, 1.1f, 0.05f), Lit(Color.white)); Prim(PrimitiveType.Sphere, d, new Vector3(1f, 2.2f, 0f), new Vector3(1.8f, 0.4f, 1.8f), Lit(new Color(0.98f, 0.8f, 0.2f))); break;
            default: Prim(PrimitiveType.Sphere, d, new Vector3(0f, 0.42f, 0f), new Vector3(1.1f, 0.85f, 1.1f), Lit(new Color(0.9f, 0.5f, 0.12f))); Prim(PrimitiveType.Cylinder, d, new Vector3(0f, 0.92f, 0f), new Vector3(0.12f, 0.1f, 0.12f), Lit(new Color(0.2f, 0.45f, 0.15f))); break;
        }
    }
    readonly List<Transform> seasonFlakes = new List<Transform>();
    void BuildSeason()
    {
        seasonOn = PlayerPrefs.GetInt("hunch_season", 1) == 1;
        for (int k = 1; k < MapCount; k++)
        {
            if (k == 8 || !mapRoot[k]) continue; float gz = k == 5 ? 12f : -13f;
            Decor(mapRoot[k], new Vector3(-11f, 0f, gz), 1.2f); Decor(mapRoot[k], new Vector3(-12.6f, 0f, gz + 1.2f), 0.7f); Decor(mapRoot[k], new Vector3(11f, 0f, gz), 1f); Decor(mapRoot[k], new Vector3(12.4f, 0f, gz - 1f), 1.5f);
        }
        if (mapGateOk) { var g = new GameObject("Season at the gate").transform; g.SetParent(root, false); g.position = mapGateAt; Decor(g, new Vector3(2.4f, 0f, 0.5f), 1.1f); Decor(g, new Vector3(-2.4f, 0f, 0.5f), 0.8f); Decor(g, new Vector3(3.4f, 0f, -0.6f), 0.6f); }
        seasonRoot = new GameObject("Season drift").transform; seasonRoot.SetParent(root, false);
        int s = SeasonNow(); Color[] cols = s == 0 ? new[] { Color.white, new Color(0.85f, 0.92f, 1f) } : (s == 1 ? new[] { new Color(1f, 0.6f, 0.8f), new Color(1f, 0.85f, 0.9f) } : (s == 2 ? new[] { new Color(2.2f, 1.8f, 0.5f), new Color(2.2f, 1.4f, 0.4f) } : new[] { new Color(0.9f, 0.45f, 0.1f), new Color(0.75f, 0.2f, 0.1f), new Color(0.85f, 0.65f, 0.15f) }));
        for (int i = 0; i < 36; i++)
        {
            var c = cols[i % cols.Length]; var f = Prim(s == 0 ? PrimitiveType.Sphere : PrimitiveType.Cube, seasonRoot, Vector3.zero, s == 0 ? Vector3.one * 0.07f : new Vector3(0.16f, 0.012f, 0.11f), s == 2 ? Glow(c) : Lit(c));
            f.transform.position = new Vector3(0f, -999f, 0f); seasonFlakes.Add(f.transform);
        }
    }
    public void SeasonToggle() { seasonOn = !seasonOn; PlayerPrefs.SetInt("hunch_season", seasonOn ? 1 : 0); Note("Season effects " + (seasonOn ? "ON: " + SeasonTitle : "OFF")); }
    void UpdateSeason(float dt)
    {
        if (!seasonRoot) return;
        bool sOn = seasonOn && !hunchOn, outside = sOn && Playing && (mapNow != 0 ? mapNow != 8 : (fx && fx.Outside));
        if (seasonBits.Count > 0 && seasonBits[0] && seasonBits[0].gameObject.activeSelf != sOn) foreach (var b in seasonBits) if (b) b.gameObject.SetActive(sOn);
        if (seasonRoot.gameObject.activeSelf != outside) seasonRoot.gameObject.SetActive(outside);
        if (!seasonSaid && sOn && Playing) { seasonSayIn -= dt; if (seasonSayIn <= 0f) { seasonSaid = true; sim.Say("ARIA: It is " + SeasonNames[SeasonNow()].ToLower() + " on Earth" + (Halloween() ? ", and Halloween is close. I put pumpkins by the map gate, and the Star shop has Halloween costumes." : ". I put up some decorations by the map gate.")); } }
        if (!outside) return;
        Vector3 f = Feet(); float t = Time.time;
        for (int i = 0; i < seasonFlakes.Count; i++)
        {
            var fl = seasonFlakes[i]; Vector3 p = fl.position;
            if (p.y < f.y + 0.05f || (p - f).sqrMagnitude > 30f * 30f) p = f + new Vector3(R(-14f, 14f), R(3f, 11f), R(-14f, 14f));
            p += new Vector3(Mathf.Sin(t * 0.9f + i) * 0.5f, -(0.5f + (i % 5) * 0.12f), Mathf.Cos(t * 0.7f + i * 1.7f) * 0.5f) * dt;
            fl.position = p; fl.Rotate(dt * 90f, dt * 60f, 0f);
        }
    }

    // ================================================================== my astronaut: levels, presents, star coins, costumes
    /// premium costumes come from the STAR STORE (sku); the others are bought with star coins. New costumes go at the END of the list,
    /// because each player's owned costumes are saved as bits by position.
    class Costume { public string name, sku; public Color suit; public int hat, price, season; public bool premium; }
    static readonly Costume[] Costumes =
    {
        new Costume { name = "Classic white", suit = new Color(0.95f, 0.95f, 0.97f), season = -1 },
        new Costume { name = "Midnight blue", suit = new Color(0.12f, 0.2f, 0.5f), price = 4, season = -1 },
        new Costume { name = "Mars red", suit = new Color(0.8f, 0.25f, 0.15f), price = 4, season = -1 },
        new Costume { name = "Gold commander", suit = new Color(0.95f, 0.74f, 0.22f), price = 8, season = -1 },
        new Costume { name = "Pumpkin suit and hat", suit = new Color(0.95f, 0.45f, 0.08f), hat = 1, price = 6, season = 4 },
        new Costume { name = "Wizard of the Moon", suit = new Color(0.4f, 0.15f, 0.6f), hat = 2, price = 6, season = 4 },
        new Costume { name = "Golden Pumpkin King", suit = new Color(0.98f, 0.62f, 0.1f), hat = 6, season = -1, premium = true, sku = HunchStore.GoldenPumpkin },
        new Costume { name = "Snow explorer", suit = new Color(0.7f, 0.86f, 0.98f), hat = 3, price = 6, season = 0 },
        new Costume { name = "Spring gardener", suit = new Color(0.4f, 0.75f, 0.4f), hat = 4, price = 6, season = 1 },
        new Costume { name = "Summer sun", suit = new Color(0.98f, 0.85f, 0.3f), hat = 5, price = 6, season = 2 },
        new Costume { name = "Cosmic Nebula", suit = new Color(0.55f, 0.22f, 0.85f), hat = 7, season = -1, premium = true, sku = HunchStore.CosmicSuits },
        new Costume { name = "Comet Racer", suit = new Color(0.18f, 0.72f, 0.95f), hat = 8, season = -1, premium = true, sku = HunchStore.CosmicSuits },
        new Costume { name = "Starlight Galaxy", suit = new Color(0.13f, 0.12f, 0.38f), hat = 9, season = -1, premium = true, sku = HunchStore.CosmicSuits }
    };
    /// coin costumes are owned by each player; STAR STORE costumes belong to the Meta account, so every player has them
    bool CostumeOwned(int i) { if (i <= 0) return true; if (i >= Costumes.Length) return false; var c = Costumes[i]; return c.premium ? HunchStore.Owns(c.sku) : (lvOwned & (1 << i)) != 0; }
    /// the costume actually worn: a store costume shows only while the account owns it
    int Worn { get { int g = StoreWear; if (g > 0) return g; return lvCostume > 0 && lvCostume < Costumes.Length && CostumeOwned(lvCostume) ? lvCostume : 0; } }
    /// a STAR STORE costume is worn by EVERY crew member you play, so switching player never takes it off
    int StoreWear { get { int k = PlayerPrefs.GetInt("hunch_wear_store", -1); return k > 0 && k < Costumes.Length && Costumes[k].premium && CostumeOwned(k) ? k : -1; } }
    int lvPreview = -1; bool lvPreviewBadge, badgeOn = true;
    bool BadgeShown { get { return badgeOn && HunchStore.Owns(HunchStore.Supporter); } }
    static readonly string[] LvTitle = { "Cadet", "Moon Walker", "Pilot", "Engineer", "Scientist", "Flight Surgeon", "Mission Specialist", "Commander", "Star Captain", "Moon Legend" };
    public int lvLevel = 1, lvXp, lvCoins, lvDiamonds, lvOwned = 1, lvCostume, lvQueue; bool lvWelcome, lvBossFree = true; int lvPlayer = -1;
    Transform lvStage, lvFigure, lvGift, lvLid; TextMeshPro lvHead, lvInfo; bool lvOpen, lvGiftReady, lvGiftOpened; float lvT, lvPlayT, lvLidY = 0.66f; HunchMenu.ExtraPage pageShop; Material avWhite; int avCostume;
    int LvNeed { get { return 100 * lvLevel; } }
    string LvTitleNow { get { return LvTitle[Mathf.Clamp(lvLevel, 1, LvTitle.Length) - 1]; } }

    void LvSave() { if (lvPlayer >= 0) PlayerPrefs.SetString("hunch_lvl_" + lvPlayer, lvLevel + "|" + lvXp + "|" + lvCoins + "|" + lvDiamonds + "|" + lvOwned + "|" + lvCostume + "|" + (lvWelcome ? 1 : 0) + "|" + (lvBossFree ? 1 : 0)); }
    void LvLoad()
    {
        if (lvPlayer >= 0) LvSave();
        lvPlayer = player; lvLevel = 1; lvXp = lvCoins = lvDiamonds = lvCostume = lvQueue = 0; lvOwned = 1; lvWelcome = false; lvBossFree = true;
        try
        {
            var a = PlayerPrefs.GetString("hunch_lvl_" + player, "").Split('|');
            if (a.Length >= 8) { lvLevel = Mathf.Max(1, int.Parse(a[0])); lvXp = int.Parse(a[1]); lvCoins = int.Parse(a[2]); lvDiamonds = int.Parse(a[3]); lvOwned = int.Parse(a[4]) | 1; lvCostume = Mathf.Clamp(int.Parse(a[5]), 0, Costumes.Length - 1); lvWelcome = a[6] == "1"; lvBossFree = a[7] == "1"; }
        }
        catch { }
        if (lvOpen) LvClose(); DressAvatar();
    }
    public void AddXp(int n)
    {
        if (lvPlayer != player) LvLoad();
        lvXp += n; while (lvXp >= LvNeed) { lvXp -= LvNeed; lvLevel++; lvQueue++; }
        LvSave();
    }
    /// an accomplishment gives experience only the first time a player earns it, not again in every new session
    bool AchXpOnce(string title) { string k = "hunch_achxp_" + player, s = PlayerPrefs.GetString(k, ""), tag = "|" + title + "|"; if (s.Contains(tag)) return false; PlayerPrefs.SetString(k, s + tag); return true; }

    void BuildLevels()
    {
        LvLoad(); avWhite = Lit(new Color(0.95f, 0.95f, 0.97f)); DressAvatar();
        A("Level 5", "Reach level 5 with your astronaut. Everything you do earns experience points.", () => lvLevel >= 5);
        A("Dressed up", "Wear a costume from the Star shop.", () => lvCostume != 0);
        A("Best friend", "Raise Lumi the moon pup to level 3 at the Pet House.", () => petLevel >= 3);
        A("Moon medic", "Care for eight patients in one round at the Clinic.", () => clinBest >= 8);
        A("Golem tickler", "Win a Star Diamond from the Grumpy Moon Golem.", () => lvDiamonds >= 1);
        if (!menu) return;
        pageShop = new HunchMenu.ExtraPage { title = "STAR SHOP", parent = pageBase };
        pageShop.build = m =>
        {
            m.AddItem("Star coins: " + lvCoins + "   Level " + lvLevel + " " + LvTitleNow, "Star coins come from level-up presents, raising Lumi, and the boss. " + PlayerName + " keeps their own coins and costumes.", () => { });
            m.AddItem("Instant level-up   (5 star coins)", "Jump straight to level " + (lvLevel + 1) + " and open the present.", () => { if (!Spend(5)) return; lvXp = 0; lvLevel++; lvQueue++; LvSave(); m.Close(); });
            m.AddItem("Boss battle ticket   (" + (lvBossFree ? "first one FREE" : "3 star coins") + ")", "Face the Grumpy Moon Golem in the Boss Arena. Win and it gives you a Star Diamond. Nobody is hurt: you tickle it with beams.", () => { m.Close(); BossEnter(); });
            m.AddItem("Stardust boost x2 for 2 minutes   (2 star coins)", "Doubles everything the Star Clicker makes for two minutes.", () => { if (!Spend(2)) return; clkRush = 120f; Note("Stardust boost ON for 2 minutes."); m.Rebuild(); });
            for (int i = 0; i < Costumes.Length; i++)
            {
                int ii = i; var c = Costumes[i]; bool own = CostumeOwned(i), season = InSeason(c.season);
                string tag = Worn == i ? "<color=#7CFC9A>wearing</color>" : (own ? "owned: wear it" : (c.premium ? "<color=#FFD24A>STAR STORE " + HunchStore.Price(c.sku) + "</color>" : (!season ? "<color=#9AD8FF>limited: back next " + (c.season == 4 ? "Halloween" : SeasonNames[c.season].ToLower()) + "</color>" : c.price + " star coins")));
                string hint = c.premium
                    ? (own ? "From the STAR STORE: yours for ever on your Meta account. " : "A STAR STORE costume (" + StorePackName(c.sku) + "). Choosing it opens its store page, where you can try it on first. ") + "Changes your astronaut's suit and helmet. You see it in third-person view (V) and on your level screen."
                    : (c.season >= 0 ? "Limited edition for " + (c.season == 4 ? "Halloween" : SeasonNames[c.season].ToLower()) + ". Once you have it, it is yours for ever. " : "") + "Changes your astronaut's suit" + (c.hat > 0 ? " and adds a hat" : "") + ". You see it in third-person view (V) and on your level screen.";
                m.AddItem("Costume: " + c.name + "   (" + tag + ")", hint, () => { CostumePick(ii); m.Rebuild(); }, c.premium ? (own ? 2 : 1) : 0);
            }
            if (storePage != null) m.AddItem("<b>STAR STORE</b>   costumes, Lumi outfits, Supporter Pack", "One-time add-ons from the Meta Horizon Store. Star coins are never sold: they are only earned by playing.", () => m.OpenExtra(storePage), 1);
        };
        try { BuildStore(); } catch (Exception e) { Debug.LogError("[HUNCH] store: " + e); }
    }
    bool Spend(int n) { if (lvCoins < n) { Play(cBoop, 0.4f); Note("That costs " + n + " star coins. You have " + lvCoins + ". Presents, Lumi and the boss give coins."); return false; } lvCoins -= n; LvSave(); Play(cPick, 0.6f); return true; }
    void CostumePick(int i)
    {
        var c = Costumes[i];
        if (!CostumeOwned(i))
        {
            if (c.premium) { if (menu && storePage != null) menu.OpenExtra(StoreDetail(c.sku)); else Note(c.name + " is in the STAR STORE (menu)."); return; }
            if (!InSeason(c.season)) { Note(c.name + " is a limited edition. It comes back next " + (c.season == 4 ? "Halloween" : SeasonNames[c.season].ToLower()) + "."); return; }
            if (!Spend(c.price)) return;
            lvOwned |= 1 << i;
        }
        lvCostume = i; PlayerPrefs.SetInt("hunch_wear_store", c.premium ? i : -1); LvSave(); DressAvatar(); if (lvStage) LvDress(); Play(cWin, 0.5f);
        Note("Wearing: " + c.name + (c.premium ? " (every crew member wears it)" : "") + (game.HeadsetActive ? ".  STAR STORE > See yourself shows your astronaut." : ".  Press V to see yourself."));
    }
    /// the third-person astronaut wears the costume too
    void DressAvatar()
    {
        if (!avBody || !avWhite) return; int w = Worn; var want = Lit(Costumes[w].suit); var old = Lit(Costumes[avCostume].suit);
        foreach (var r in avBody.GetComponentsInChildren<Renderer>(true)) if (r.sharedMaterial == avWhite || r.sharedMaterial == old) r.sharedMaterial = want;
        avCostume = w; var h = avBody.Find("Hat"); if (h) Destroy(h.gameObject); Hat(avBody, new Vector3(0f, 1.78f, 0f), Costumes[w].hat);
        var b = avBody.Find("Supporter badge"); if (b) Destroy(b.gameObject); if (BadgeShown) Badge(avBody, new Vector3(0.12f, 1.24f, 0.2f), 1f);
    }
    /// the gold supporter star, worn on the chest
    void Badge(Transform parent, Vector3 lp, float k)
    {
        var b = new GameObject("Supporter badge").transform; b.SetParent(parent, false); b.localPosition = lp; b.localScale = Vector3.one * k;
        Prim(PrimitiveType.Cylinder, b, Vector3.zero, new Vector3(0.11f, 0.008f, 0.11f), Lit(new Color(1f, 0.8f, 0.2f))).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        for (int i = 0; i < 5; i++) { float a = i * 72f; Prim(PrimitiveType.Cube, b, Quaternion.Euler(0f, 0f, a) * new Vector3(0f, 0.042f, 0.007f), new Vector3(0.028f, 0.05f, 0.012f), Glow(new Color(2.8f, 2.1f, 0.6f))).transform.localRotation = Quaternion.Euler(0f, 0f, a); }
        Prim(PrimitiveType.Sphere, b, new Vector3(0f, 0f, 0.009f), Vector3.one * 0.035f, Glow(new Color(2.9f, 2.7f, 2.3f)));
    }
    void LvDress()
    {
        if (lvFigure) Destroy(lvFigure.gameObject); var c = Costumes[lvPreview >= 0 && lvPreview < Costumes.Length ? lvPreview : Worn]; bool model = KitSource(false, "Astronaut");
        lvFigure = Figure(lvStage, Vector3.zero, Lit(c.suit), Lit(new Color(0.16f, 0.34f, 0.82f))); lvFigure.localRotation = Quaternion.Euler(0f, 180f, 0f); Hat(lvFigure, new Vector3(0f, model ? 1.86f : 1.78f, 0f), c.hat);
        if (BadgeShown || lvPreviewBadge) Badge(lvFigure, new Vector3(0.13f, model ? 1.3f : 1.24f, model ? 0.24f : 0.2f), 1.25f);
    }
    void LvBuildStage()
    {
        lvStage = new GameObject("Level stage").transform; lvStage.SetParent(root, false); var gold = Lit(new Color(0.98f, 0.76f, 0.22f));
        Prim(PrimitiveType.Cube, lvStage, new Vector3(0f, 2.0f, 0.9f), new Vector3(3.8f, 4.0f, 0.08f), Glow(new Color(0.08f, 1.25f, 0.2f)));      // the green screen
        Prim(PrimitiveType.Cylinder, lvStage, new Vector3(0f, 0.02f, 0f), new Vector3(3.6f, 0.02f, 2.4f), Lit(new Color(0.1f, 0.6f, 0.18f)));
        lvHead = Label(lvStage, "", 0f, 0f, 4.2f, 3.6f, 1.2f, Color.white); lvHead.transform.localPosition = new Vector3(0f, 3.4f, 0.84f);
        lvInfo = Label(lvStage, "", 0f, 0f, 1.7f, 3.6f, 0.9f, new Color(0.05f, 0.1f, 0.05f)); lvInfo.transform.localPosition = new Vector3(0f, 2.45f, 0.84f); lvInfo.enableAutoSizing = true; lvInfo.fontSizeMin = 0.9f; lvInfo.fontSizeMax = 1.8f;
        lvGift = new GameObject("Present").transform; lvGift.SetParent(lvStage, false); lvGift.localPosition = new Vector3(-1.25f, 0f, -0.5f);
        bool giftModel = KitSource(false, "GiftBox") && KitSource(false, "GiftLid"); lvLidY = giftModel ? 0.52f : 0.66f;
        var box = Prim(PrimitiveType.Cube, lvGift, new Vector3(0f, 0.3f, 0f), Vector3.one * (giftModel ? 0.64f : 0.6f), giftModel ? Clear(new Color(1f, 1f, 1f, 0.02f)) : Lit(new Color(0.85f, 0.12f, 0.15f)), true); var tg = box.AddComponent<HunchTarget>(); tg.kind = 35; tg.index = 900;
        lvLid = new GameObject("Lid").transform; lvLid.SetParent(lvGift, false); lvLid.localPosition = new Vector3(0f, lvLidY, 0f);
        if (giftModel) { Kit(false, "GiftBox", lvGift, Vector3.zero, 0f, 0f, false); Kit(false, "GiftLid", lvLid, Vector3.zero, 0f, 0f, false); }
        else
        {
            Prim(PrimitiveType.Cube, lvGift, new Vector3(0f, 0.3f, 0f), new Vector3(0.62f, 0.62f, 0.12f), gold); Prim(PrimitiveType.Cube, lvGift, new Vector3(0f, 0.3f, 0f), new Vector3(0.12f, 0.62f, 0.62f), gold);
            Prim(PrimitiveType.Cube, lvLid, Vector3.zero, new Vector3(0.68f, 0.12f, 0.68f), Lit(new Color(0.85f, 0.12f, 0.15f))); Prim(PrimitiveType.Sphere, lvLid, new Vector3(-0.09f, 0.12f, 0f), Vector3.one * 0.18f, gold); Prim(PrimitiveType.Sphere, lvLid, new Vector3(0.09f, 0.12f, 0f), Vector3.one * 0.18f, gold);
        }
        var pl = Label(lvGift, "<b>PRESENT</b>", 0f, 0f, 1.5f, 3f, 0.5f, Color.white); pl.transform.localPosition = new Vector3(0f, 1.15f, 0f);
        FacButton(lvStage, new Vector3(1.25f, 0.4f, -0.5f), new Vector3(0.95f, 0.6f, 0.5f), Glow(new Color(0.25f, 0.7f, 2.2f)), 901);
        var nl = Label(lvStage, "<b>NEXT</b>", 0f, 0f, 2.2f, 2f, 0.6f, Color.white); nl.transform.localPosition = new Vector3(1.25f, 0.4f, -0.77f);
        LvDress();
    }
    bool LvCanShow { get { return Playing && !game.menuOpen && !ControlsLocked && !mgOn && mgCount <= 0f && mapNow != 8 && ride == null && readOpen < 0; } }
    void LvShow(bool present, int level)
    {
        if (!lvStage) LvBuildStage();
        Vector3 f = cam.forward; f.y = 0f; if (f.sqrMagnitude < 0.01f) f = Vector3.forward; f.Normalize();
        lvStage.position = Feet() + f * 3.6f; lvStage.rotation = Quaternion.LookRotation(f, Vector3.up); lvStage.gameObject.SetActive(true);
        lvOpen = true; lvGiftReady = present; lvGiftOpened = false; lvT = 0f; lvGift.gameObject.SetActive(present); lvLid.localPosition = new Vector3(0f, lvLidY, 0f); lvLid.localRotation = Quaternion.identity;
        lvHead.text = "<b>LEVEL " + level + "</b>"; lvInfo.text = (BadgeShown ? "<color=#6A1FB0><b>SUPPORTER</b></color>   " : "") + "<b>" + LvTitle[Mathf.Clamp(level, 1, LvTitle.Length) - 1] + "</b>   " + PlayerName + (present ? "\nOpen your present!" : "\nXP " + lvXp + " of " + LvNeed + "   Star coins " + lvCoins + "   Diamonds " + lvDiamonds);
        if (present) { Play(cWin, 0.8f); HunchFx.Spawn("star hit", lvStage.position + Vector3.up * 2.4f, 1f, 1f); }
    }
    void LvClose() { lvOpen = false; if (lvStage) lvStage.gameObject.SetActive(false); if (lvPreview >= 0 || lvPreviewBadge) { lvPreview = -1; lvPreviewBadge = false; if (lvStage) LvDress(); } }
    void LvOpenGift()
    {
        lvGiftOpened = true; int coins = 2 + rng.Next(3) + lvLevel / 5; lvCoins += coins; string extra = "";
        switch (rng.Next(4))
        {
            case 0: AddItem(Crystal, 2); extra = " and 2 crystals"; break;
            case 1: if (food < 9) { food++; extra = " and a food pack"; } break;
            case 2: if (clkStar) { double d = 400.0 * lvLevel; clkDust += d; clkTotal += d; clkLife += d; extra = " and " + Big(d) + " stardust"; } break;
        }
        LvSave(); lvInfo.text = "<b>You got " + coins + " star coins" + extra + "!</b>\nAim at NEXT to carry on."; Play(cPop, 0.8f); Play(cWin, 0.6f); game.Buzz(0.8f, 0.2f);
        HunchFx.Spawn("star hit", lvGift.position + Vector3.up * 0.8f, 0.7f, 0.8f);
    }
    string LevelText(int i)
    {
        if (i == 900) return lvGiftOpened ? "" : "open the present";
        if (i == 901) return lvGiftReady && !lvGiftOpened ? "open the present first" : "next";
        return "";
    }
    void LevelUse(int i)
    {
        if (i == 900) { if (lvGiftReady && !lvGiftOpened) LvOpenGift(); return; }
        if (i == 901) { if (lvGiftReady && !lvGiftOpened) { Note("Open your present first: aim at the gift box."); return; } Play(cPick, 0.5f); LvClose(); }
    }
    void UpdateLevels(float dt)
    {
        if (lvPlayer != player) LvLoad();
        UpdateKiosk(dt);
        if (storeMsgT > 0f) { storeMsgT -= dt; if (storeMsgT <= 0f) { storeMsg = ""; if (menu && menu.IsOpen) menu.RefreshOpen(); } }
        if (hunchOn) { if (lvOpen) LvClose(); return; }
        if (!Playing) return; lvPlayT += dt;
        if (lvOpen)
        {
            lvT += dt; if (lvFigure) { lvFigure.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(lvT * 3f)) * 0.12f, 0f); lvFigure.localRotation = Quaternion.Euler(0f, 180f + Mathf.Sin(lvT * 1.5f) * 12f, 0f); }
            if (lvGiftReady && !lvGiftOpened) lvGift.localScale = Vector3.one * (1f + Mathf.Sin(lvT * 6f) * 0.06f);
            if (lvGiftOpened && lvLid.localPosition.y < 2.2f) { lvLid.localPosition += new Vector3(-0.4f, 1.6f, 0f) * dt; lvLid.Rotate(0f, 0f, dt * 220f); }
            bool far = (Feet() - lvStage.position).sqrMagnitude > 14f * 14f || mapNow == 8;
            if (autoOn || far) { if (lvGiftReady && !lvGiftOpened && (lvT > 2f || far)) LvOpenGift(); if (lvT > 5f || far) LvClose(); }
            return;
        }
        if (!LvCanShow) return;
        if (lvQueue > 0) { lvQueue--; LvShow(true, lvLevel - lvQueue); }
        else if (!lvWelcome && lvPlayT > 25f) { lvWelcome = true; LvSave(); LvShow(true, lvLevel); }
    }
    string LevelHud() { return lvOpen && lvGiftReady && !lvGiftOpened ? "<color=#7CFC9A><b>LEVEL " + lvLevel + "!</b>  A present for you: aim at the gift box and " + (game.HeadsetActive ? "pull the trigger" : "click") + "</color>" : ""; }
    /// rows for the Base systems menu
    void ExtraMenu(HunchMenu m)
    {
        bool vr = game.HeadsetActive;
        m.AddItem("Physics view: " + (physOn ? "<color=#7CFC9A>ON</color>" : "off"), "Draws the physics of what is happening: your velocity and weight as vectors, the gravity field, the path of a jump, a live graph of height, speed and acceleration, and the sensor network." + (vr ? "" : " F8 does the same."), () => { PhysToggle(); m.Rebuild(); });
        m.AddItem("Read: Maths and physics you can see", "Where calculus, vector fields, differential equations, linear algebra, abstract algebra and networking show up in the base.", () => { m.Close(); OpenRead(physSheet); });
        if (!vr) m.AddItem("View: " + (thirdPerson ? "third person" : "first person"), "Third person shows your astronaut from behind. The V key does the same.", () => { SetThird(!thirdPerson); m.Rebuild(); });
        m.AddItem("Sleep mode: " + (sleepOn ? "<color=#9AD0FF>ON</color>   (wake the crew)" : "off"), "The crew go to their sleep pods in Crew Quarters and the lights dim. They also sleep by themselves from 22:00 to 06:00. ARIA wakes them if the habitat needs them.", () => { SleepToggle(); m.Rebuild(); });
        m.AddItem(sleepMe ? "Get up from your sleep pod" : "Sleep in your own pod until morning", "Stand in Crew Quarters first. The clock runs faster while you sleep. Move or press a key to get up. You can also point at your pod and click.", () => { m.Close(); SleepMe(); });
        HubRows(m);
        if (!hunchOn && funRoot)
        {
            m.AddItem("Fun Zone: emote stage", "A stage with one button for every dance move and emote: arm wave, spin, side sway, star jump, hello wave, robot, floss, cheer, moon shuffle, twist. By the mining field.", () => { m.Close(); GoFun(0); });
            m.AddItem("Fun Zone: zero-G dome", "The anti-gravity module. Step inside and you float; loose shapes drift and bump, and there are eight floating stars to catch. A switch outside turns gravity back on.", () => { m.Close(); GoFun(1); });
            m.AddItem("Fun Zone: Space Slicer", "Asteroids, ice chunks and comets are tossed up in front of you for 45 seconds. Slice them with the Comet sword: hold the button and sweep. Leave the satellites alone.", () => { m.Close(); GoFun(2); });
        }
        if (vr) m.AddItem("Mirror astronaut: " + (mirrorMe ? "<color=#7CFC9A>ON</color>" : "off"), "In the headset an astronaut stands in front of you like a mirror and copies your arms: wave, and it waves back. It follows your two controllers, so fingers and legs are not copied.", () => { mirrorMe = !mirrorMe; m.Rebuild(); });
        m.AddItem("Live tracking board: " + (trackOn ? "<color=#7CFC9A>ON</color>" : "off"), "A board in the centre dome, behind the meeting table, shows what the headset measures about you: your height, where you stand, which way your head is turned, how fast you move and what you are looking at. Eye direction is shown on a headset that has eye tracking. The same numbers are saved once a second in the Reports folder (tracking_*.csv).", () => { trackOn = !trackOn; m.Rebuild(); });
        m.AddItem("Play as: " + PlayAsName + "   (F6 switches)", "Step into the next crew member: Commander, Dr. Okafor, Eng. Tanaka, Sci. Rivera. You appear where they are standing. The one you leave gets their body back and carries on with their own day by themselves, like the other two.", () => { m.Close(); SwitchCrew(); });
        m.AddItem("Letters: " + (PlayerPrefs.GetInt("hunch_kidfont", 1) == 1 ? "kid-friendly" : "technical"), "Kid-friendly uses a round, hand-written style of letter (Comic Sans, which comes with Windows). Technical uses the narrow engineering letters. The change shows the next time the game starts.", () => { PlayerPrefs.SetInt("hunch_kidfont", PlayerPrefs.GetInt("hunch_kidfont", 1) == 1 ? 0 : 1); m.Rebuild(); });
        if (!vr && HunchTouch.I) m.AddItem("Mobile controls: " + (HunchTouch.I.on ? "<color=#7CFC9A>ON</color>" : "off"), "For a touch screen: a walking stick under the left thumb, drag to look under the right thumb, and only three buttons (ACT, JUMP, MENU). On a phone or tablet it is on by itself. On a computer the mouse plays the part of one finger.", () => { HunchTouch.I.Set(!HunchTouch.I.on); m.Rebuild(); });
        if (hunchOn) { HunchCoverRow(m); HunchRoverRows(m); }
        if (hunchOn) { m.AddItem("Crew radio: " + (radioOn ? "ON" : "off") + "   (" + RadioState + ")", "Talk into your microphone and a crew member or ARIA answers. Only loudness is measured: nothing is recorded, saved or sent.", () => { RadioToggle(); m.Rebuild(); }); return; }
        m.AddItem("My astronaut: Level " + lvLevel + " " + LvTitleNow + "   XP " + lvXp + "/" + LvNeed, "Star coins " + lvCoins + ", Star Diamonds " + lvDiamonds + ". Everything you do earns experience. Each new level brings a present. Choosing this shows your astronaut.", () => { m.Close(); if (lvOpen) LvClose(); LvShow(false, lvLevel); });
        if (pageShop != null) m.AddItem("Star shop and costumes   (" + lvCoins + " star coins)", "Instant level-up, boss battle ticket, stardust boost and costumes, including limited editions for " + SeasonTitle + ".", () => m.OpenExtra(pageShop));
        m.AddItem("Crew radio: " + (radioOn ? "ON" : "off") + "   (" + RadioState + ")", "Talk into your microphone" + (vr ? " (the headset's)" : "") + ". The game shows TRANSMITTING and a crew member or ARIA answers when you stop. Only loudness is measured: nothing is recorded, saved or sent." + (vr ? "" : " F7 does the same."), () => { RadioToggle(); m.Rebuild(); });
        m.AddItem("Season effects: " + SeasonTitle + "   (" + (seasonOn ? "ON" : "off") + ")", "Decorations and drifting " + (SeasonNow() == 0 ? "snow" : (SeasonNow() == 1 ? "petals" : (SeasonNow() == 2 ? "sparkles" : "leaves"))) + " that follow the real season on Earth, read from this computer's calendar.", () => { SeasonToggle(); m.Rebuild(); });
    }

    // ================================================================== clinic (map 12)
    class Patient { public Transform fig; public TextMeshPro tag; public int[] needs; public int step, who; public bool scanned, here; public float patience, wait; }
    static readonly string[] ClinTool = { "Water pack", "Bandage", "Oxygen mask", "Cool pack", "Warm blanket", "Kind talk" };
    static readonly string[] ClinSign = { "thirsty and dizzy", "nursing a scraped hand", "short of breath after a moonwalk", "too hot after exercise", "shivering after the airlock", "homesick and worried" };
    static readonly string[] ClinWhy = { "Water first: the body loses water fast inside a suit.", "Clean it and cover it so it can heal.", "Slow breaths of oxygen, and a rest.", "Cool the skin and rest.", "Warm up slowly under a blanket.", "Listening is medicine too: talking with a friend lowers stress." };
    static readonly string[] ClinNames = { "Ana", "Ben", "Chen", "Dara", "Eli", "Fay", "Gus", "Hana", "Ivo", "Jun", "Kai", "Lea" };
    readonly Patient[] beds = new Patient[4]; int clinHeld = -1, clinCured, clinWrong, clinSeen; public int clinBest; float clinTagT; TextMeshPro clinHoldL;

    void BuildClinic(Transform r)
    {
        BuildExtraSounds();
        var white = Lit(new Color(0.93f, 0.94f, 0.96f)); var steel = Lit(new Color(0.6f, 0.63f, 0.68f)); var dark = Lit(new Color(0.12f, 0.13f, 0.18f)); var teal = Lit(new Color(0.2f, 0.65f, 0.7f));
        Prim(PrimitiveType.Cube, r, new Vector3(0f, 2.5f, 14f), new Vector3(30f, 5f, 0.3f), white, true); Prim(PrimitiveType.Cube, r, new Vector3(0f, 4.2f, 13.8f), new Vector3(30f, 0.5f, 0.1f), teal);
        Prim(PrimitiveType.Cube, r, new Vector3(-1.2f, 3f, 13.8f), new Vector3(0.5f, 1.6f, 0.1f), Glow(new Color(1.8f, 0.25f, 0.25f))); Prim(PrimitiveType.Cube, r, new Vector3(-1.2f, 3f, 13.78f), new Vector3(1.6f, 0.5f, 0.1f), Glow(new Color(1.8f, 0.25f, 0.25f)));
        for (int b = 0; b < 4; b++)
        {
            var bed = new GameObject("Bed " + (b + 1)).transform; bed.SetParent(r, false); bed.localPosition = new Vector3(-9f + b * 6f, 0f, 9.5f); var p = new Patient(); beds[b] = p;
            bool bedModel = Kit(false, "HospitalBed", bed, Vector3.zero, 180f, 0f, false);
            if (!bedModel)
            {
                Prim(PrimitiveType.Cube, bed, new Vector3(0f, 0.45f, 0f), new Vector3(1.4f, 0.3f, 2.5f), white); Prim(PrimitiveType.Cube, bed, new Vector3(0f, 0.2f, 0f), new Vector3(1.5f, 0.2f, 2.6f), steel); Prim(PrimitiveType.Cube, bed, new Vector3(0f, 0.68f, -0.95f), new Vector3(0.9f, 0.16f, 0.5f), teal);
                Prim(PrimitiveType.Cylinder, bed, new Vector3(1.0f, 0.9f, 1.0f), new Vector3(0.05f, 0.9f, 0.05f), steel);
            }
            Prim(PrimitiveType.Cube, bed, new Vector3(1.0f, 1.9f, 1.0f), new Vector3(0.7f, 0.5f, 0.1f), dark); Prim(PrimitiveType.Cube, bed, new Vector3(1.0f, 1.9f, 0.94f), new Vector3(0.6f, 0.04f, 0.02f), Glow(new Color(0.4f, 2f, 0.6f))); Prim(PrimitiveType.Cylinder, bed, new Vector3(1.0f, 0.85f, 1.05f), new Vector3(0.04f, 0.85f, 0.04f), steel);
            p.fig = Figure(bed, new Vector3(0f, bedModel && KitSource(false, "Astronaut") ? 0.98f : 0.8f, 0.9f), Lit(new Color(0.9f, 0.9f, 0.93f)), b % 2 == 0 ? teal : Lit(new Color(0.85f, 0.45f, 0.2f))); p.fig.localRotation = Quaternion.Euler(-90f, 0f, 0f); p.fig.gameObject.SetActive(false);
            FacButton(bed, new Vector3(0f, 1.0f, 0f), new Vector3(1.6f, 1.2f, 2.6f), Clear(new Color(1f, 1f, 1f, 0.02f)), 1100 + b);
            p.tag = TagLabel(bed, new Vector3(0f, 3.0f, 0f), 1.5f, "", Color.white);
        }
        var cart = new GameObject("Care cart").transform; cart.SetParent(r, false); cart.localPosition = new Vector3(0f, 0f, 1f);
        Prim(PrimitiveType.Cube, cart, new Vector3(0f, 0.5f, 0f), new Vector3(16.4f, 1f, 1.3f), steel); Prim(PrimitiveType.Cube, cart, new Vector3(0f, 1.03f, 0f), new Vector3(16.5f, 0.06f, 1.4f), white);
        Color[] tc = { new Color(0.3f, 0.6f, 1.6f), new Color(0.95f, 0.9f, 0.8f), new Color(0.4f, 1.6f, 1.4f), new Color(0.5f, 0.8f, 1.9f), new Color(1.6f, 0.7f, 0.3f), new Color(1.7f, 0.5f, 1.2f) };
        FacButton(cart, new Vector3(-6.9f, 1.4f, 0f), new Vector3(1.5f, 0.7f, 0.8f), Glow(new Color(0.4f, 2f, 0.6f)), 1120); TagLabel(cart, new Vector3(-6.9f, 2.3f, 0f), 1.4f, "<b>SCANNER</b>\n<size=70%>use first</size>", Color.white);
        for (int i = 0; i < 6; i++) { float x = -4.6f + i * 2.3f; FacButton(cart, new Vector3(x, 1.4f, 0f), new Vector3(1.5f, 0.7f, 0.8f), Glow(tc[i] * 0.8f), 1110 + i); TagLabel(cart, new Vector3(x, 2.3f, 0f), 1.4f, "<b>" + ClinTool[i] + "</b>", Color.white); }
        clinHoldL = TagLabel(cart, new Vector3(0f, 3.5f, 0f), 2.2f, "", new Color(1f, 0.84f, 0.3f));
        var sign = Label(r, "<b>MOON CLINIC</b>\n<size=55%>1. Pick up the SCANNER and click a patient.   2. Pick up what they need and click them again.\nLumi's Pet House is on the right.</size>", 0f, 0f, 6f, 60f, 6f, new Color(1f, 0.84f, 0.3f)); sign.transform.localPosition = new Vector3(0f, 8.5f, 14f);
        BuildPet(r);
    }
    void ClinicReset()
    {
        clinHeld = -1; clinCured = clinWrong = 0;
        for (int b = 0; b < 4; b++) { var p = beds[b]; p.here = false; p.wait = 0.4f + b * 2.5f; if (p.fig) p.fig.gameObject.SetActive(false); p.tag.text = ""; }
    }
    void NewPatient(Patient p)
    {
        int n = clinCured >= 4 ? 2 : 1; p.needs = new int[n]; p.needs[0] = rng.Next(6); if (n > 1) p.needs[1] = (p.needs[0] + 1 + rng.Next(5)) % 6;
        p.step = 0; p.scanned = false; p.here = true; p.patience = 50f; p.who = clinSeen++ % ClinNames.Length; p.fig.gameObject.SetActive(true); Play(cPop, 0.4f);
    }
    void UpdateClinic(float dt)
    {
        clinTagT -= dt; bool tags = clinTagT <= 0f; if (tags) clinTagT = 0.25f;
        if (tags && clinHoldL) clinHoldL.text = clinHeld < 0 ? "" : "Holding: <b>" + (clinHeld == 6 ? "Scanner" : ClinTool[clinHeld]) + "</b>";
        if (!mgOn) return;
        for (int b = 0; b < 4; b++)
        {
            var p = beds[b];
            if (!p.here) { p.wait -= dt; if (p.wait <= 0f) NewPatient(p); else if (tags) p.tag.text = ""; continue; }
            p.patience -= dt;
            if (p.patience <= 0f) { p.here = false; p.wait = 3f; p.fig.gameObject.SetActive(false); Note(ClinNames[p.who] + " was taken to Doctor ARIA. Next time, be a little quicker."); continue; }
            if (!tags) continue; int k = Mathf.Clamp(Mathf.CeilToInt(p.patience / 5f), 0, 10); var sb = new StringBuilder();
            sb.Append("<b>").Append(ClinNames[p.who]).Append("</b>\n<size=80%>");
            if (!p.scanned) sb.Append("<color=#FFD070>? use the SCANNER</color>");
            else for (int i = 0; i < p.needs.Length; i++) sb.Append(i > 0 ? " + " : "Needs: ").Append(i < p.step ? "<color=#7CFC9A>" : "<color=#FFFFFF>").Append(ClinTool[p.needs[i]]).Append("</color>");
            sb.Append("\n<color=").Append(k > 3 ? "#7CFC9A" : "#FF9A8A").Append(">").Append(new string('|', k)).Append(new string('.', 10 - k)).Append("</color></size>"); p.tag.text = sb.ToString();
        }
    }
    string ClinicText(int i)
    {
        if (i == 1120) return "pick up the scanner";
        if (i >= 1110 && i < 1116) return "pick up: " + ClinTool[i - 1110].ToLower();
        if (i >= 1100 && i < 1104) { var p = beds[i - 1100]; if (p == null || !p.here) return mgOn ? "empty bed" : "aim at PLAY AGAIN to start a round"; return !p.scanned ? "scan " + ClinNames[p.who] : (clinHeld >= 0 && clinHeld < 6 ? "give " + ClinNames[p.who] + " the " + ClinTool[clinHeld].ToLower() : ClinNames[p.who] + " needs: " + ClinTool[p.needs[p.step]].ToLower()); }
        return "";
    }
    void ClinicUse(int i)
    {
        if (i == 1120) { clinHeld = 6; clinTagT = 0f; Play(cPick, 0.5f); Note("Scanner in hand. Click a patient to find out what is wrong."); return; }
        if (i >= 1110 && i < 1116) { clinHeld = i - 1110; clinTagT = 0f; Play(cPick, 0.5f); Note(ClinTool[clinHeld] + " in hand. Click the patient who needs it."); return; }
        if (i < 1100 || i >= 1104) return; var p = beds[i - 1100];
        if (!mgOn) { Note("Aim at PLAY AGAIN to start a round."); return; }
        if (!p.here) { Note("This bed is empty. The next patient is on the way."); return; }
        if (clinHeld < 0) { Note("Pick up the scanner or a care item from the cart first."); return; }
        if (clinHeld == 6) { p.scanned = true; clinTagT = 0f; Play(cGrow, 0.6f); Note("Scan: " + ClinNames[p.who] + " is " + ClinSign[p.needs[p.step]] + ". Bring: " + ClinTool[p.needs[p.step]] + "."); return; }
        if (!p.scanned) { Play(cBoop, 0.4f); Note("Scan " + ClinNames[p.who] + " first to find out what is wrong."); return; }
        if (clinHeld != p.needs[p.step]) { clinWrong++; mgScore = Mathf.Max(0f, mgScore - 10f); Play(cBoop, 0.5f); Note("Not what " + ClinNames[p.who] + " needs. They are " + ClinSign[p.needs[p.step]] + "."); return; }
        Note(ClinWhy[p.needs[p.step]]); p.step++; clinTagT = 0f; Play(cGrow, 0.7f); game.Buzz(0.5f, 0.1f);
        if (p.step < p.needs.Length) return;
        clinCured++; clinBest = Mathf.Max(clinBest, clinCured); mgScore += 100f + Mathf.Round(p.patience); AddXp(25); Play(cWin, 0.6f);
        HunchFx.Spawn("star hit", p.fig.position + Vector3.up * 0.6f, 0.8f, 0.8f); p.here = false; p.wait = 2.5f; p.fig.gameObject.SetActive(false); p.tag.text = "<color=#7CFC9A><b>Feeling better!</b></color>";
    }
    string ClinicStatus() { return "Time " + Mathf.CeilToInt(mgT) + " s   Patients helped " + clinCured + "   Score " + Mathf.RoundToInt(mgScore) + "   Holding: " + (clinHeld < 0 ? "nothing" : (clinHeld == 6 ? "scanner" : ClinTool[clinHeld].ToLower())) + "   Lumi: " + PetMood(); }
    void ClinicEnd() { foreach (var c in sim.crew) c.health = Mathf.Min(100f, c.health + clinCured * 0.5f); }
    IEnumerator AutoClinic()
    {
        AutoSay("Moon Clinic. Scanning each patient, then bringing what they need.");
        TeleportTo(mapC[12] + new Vector3(0f, 0f, 4.5f), 0f); float until = Time.time + 26f;
        while (mapNow == 12 && autoOn && Time.time < until)
        {
            if (!mgOn) { yield return new WaitForSeconds(0.5f); continue; }
            for (int b = 0; b < 4 && mapNow == 12 && autoOn; b++)
            {
                var p = beds[b]; if (!p.here) continue; yield return Turn(p.fig.position, 0.4f);
                if (!p.scanned) { ClinicUse(1120); ClinicUse(1100 + b); yield return new WaitForSeconds(0.9f); }
                while (p.here && p.scanned) { ClinicUse(1110 + p.needs[p.step]); ClinicUse(1100 + b); yield return new WaitForSeconds(0.9f); }
            }
            yield return new WaitForSeconds(0.4f);
        }
        AutoSay("Now a visit to Lumi the moon pup."); TeleportTo(mapC[12] + PetAt + new Vector3(0f, 0f, -5f), 0f); yield return new WaitForSeconds(0.8f);
        PetUse(1300); yield return new WaitForSeconds(1.5f); PetUse(1301); yield return new WaitForSeconds(2f); PetUse(1302); yield return new WaitForSeconds(1.5f);
    }
    public string ProbeClinic() { int n = 0; for (int b = 0; b < 4; b++) { var p = beds[b]; if (!p.here) continue; ClinicUse(1120); ClinicUse(1100 + b); int guard = 0; while (p.here && guard++ < 4) { ClinicUse(1110 + p.needs[p.step]); ClinicUse(1100 + b); } n++; } return "treated " + n + " | " + ClinicStatus(); }
    public string ProbeClinicScan() { ClinicUse(1120); for (int b = 0; b < 4; b++) if (beds[b].here) ClinicUse(1100 + b); clinHeld = 0; return ClinicStatus(); }

    // ================================================================== Lumi the moon pup (map 12)
    static readonly Vector3 PetAt = new Vector3(30f, 0f, 4f);
    public float petFood = 70f, petFun = 60f, petClean = 80f, petRest = 75f; public int petLevel = 1, petXp, petPoints; bool petSleep; int petPlayer = -1; float petJump, petPat, petSaveT, petTagT, petNag = 240f, petWash;
    Transform petRoot, petBody, petBall; TextMeshPro petTag;
    string PetStage { get { return petLevel < 3 ? "Baby" : (petLevel < 6 ? "Kid" : (petLevel < 10 ? "Teen" : "Grown-up")); } }
    string PetMood() { float h = (petFood + petFun + petClean + petRest) * 0.25f; return petSleep ? "asleep" : (h > 70f ? "happy" : (h > 40f ? "okay" : "needs you")); }
    void PetSave() { if (petPlayer >= 0) PlayerPrefs.SetString("hunch_pet_" + petPlayer, Mathf.RoundToInt(petFood) + "|" + Mathf.RoundToInt(petFun) + "|" + Mathf.RoundToInt(petClean) + "|" + Mathf.RoundToInt(petRest) + "|" + petLevel + "|" + petXp + "|" + petPoints); }
    void PetLoad()
    {
        if (petPlayer >= 0) PetSave(); petPlayer = player; petFood = 70f; petFun = 60f; petClean = 80f; petRest = 75f; petLevel = 1; petXp = petPoints = 0; petSleep = false;
        try { var a = PlayerPrefs.GetString("hunch_pet_" + player, "").Split('|'); if (a.Length >= 7) { petFood = int.Parse(a[0]); petFun = int.Parse(a[1]); petClean = int.Parse(a[2]); petRest = int.Parse(a[3]); petLevel = Mathf.Max(1, int.Parse(a[4])); petXp = int.Parse(a[5]); petPoints = int.Parse(a[6]); } } catch { }
        petOutfit = Mathf.Clamp(PlayerPrefs.GetInt("hunch_pet_outfit_" + player, 0), 0, PetOutfitNames.Length - 1); PetDress();
    }
    void BuildPet(Transform r)
    {
        var home = new GameObject("Pet House").transform; home.SetParent(r, false); home.localPosition = PetAt;
        var fur = Lit(new Color(0.45f, 0.85f, 0.82f)); var belly = Lit(new Color(0.9f, 0.97f, 0.95f)); var dark = Lit(new Color(0.05f, 0.05f, 0.07f)); var wood = Lit(new Color(0.55f, 0.38f, 0.22f));
        Prim(PrimitiveType.Cylinder, home, new Vector3(0f, 0.03f, 0f), new Vector3(9f, 0.03f, 9f), Lit(new Color(0.85f, 0.55f, 0.6f)));
        Prim(PrimitiveType.Cube, home, new Vector3(0f, 1.5f, 4.6f), new Vector3(5f, 3f, 0.3f), wood); Prim(PrimitiveType.Cube, home, new Vector3(0f, 3.4f, 4.2f), new Vector3(5.6f, 0.3f, 1.6f), Lit(new Color(0.8f, 0.25f, 0.2f))).transform.localRotation = Quaternion.Euler(-20f, 0f, 0f);
        Prim(PrimitiveType.Cylinder, home, new Vector3(-3f, 0.15f, 2.5f), new Vector3(1.6f, 0.15f, 1.6f), Lit(new Color(0.3f, 0.4f, 0.8f)));      // the basket
        petRoot = new GameObject("Lumi").transform; petRoot.SetParent(home, false); petBody = new GameObject("Body").transform; petBody.SetParent(petRoot, false);
        var lumi = KitSource(false, "Lumi");
        if (lumi)
        {
            var g = Instantiate(lumi, petBody); g.name = "Model"; g.SetActive(true); g.transform.localPosition = Vector3.zero; g.transform.localRotation = lumi.transform.localRotation; g.transform.localScale = lumi.transform.localScale * 0.72f; foreach (var c in g.GetComponentsInChildren<Collider>(true)) Destroy(c);
            var sc = petBody.gameObject.AddComponent<SphereCollider>(); sc.center = new Vector3(0f, 0.7f, 0f); sc.radius = 0.65f; var pt = petBody.gameObject.AddComponent<HunchTarget>(); pt.kind = 35; pt.index = 1310; kitPlaced++;
        }
        else
        {
        var body = Prim(PrimitiveType.Sphere, petBody, new Vector3(0f, 0.55f, 0f), new Vector3(1f, 0.95f, 1.05f), fur, true); var tg = body.AddComponent<HunchTarget>(); tg.kind = 35; tg.index = 1310;
        Prim(PrimitiveType.Sphere, petBody, new Vector3(0f, 0.5f, -0.2f), new Vector3(0.7f, 0.7f, 0.75f), belly); Prim(PrimitiveType.Sphere, petBody, new Vector3(0f, 1.2f, -0.15f), Vector3.one * 0.8f, fur);
        foreach (float sx in new[] { -1f, 1f })
        {
            Prim(PrimitiveType.Sphere, petBody, new Vector3(sx * 0.3f, 1.65f, -0.1f), new Vector3(0.22f, 0.5f, 0.16f), fur).transform.localRotation = Quaternion.Euler(0f, 0f, -sx * 18f);
            Prim(PrimitiveType.Sphere, petBody, new Vector3(sx * 0.18f, 1.28f, -0.5f), Vector3.one * 0.2f, Lit(Color.white)); Prim(PrimitiveType.Sphere, petBody, new Vector3(sx * 0.18f, 1.28f, -0.58f), Vector3.one * 0.11f, dark);
            Prim(PrimitiveType.Sphere, petBody, new Vector3(sx * 0.3f, 0.12f, -0.2f), new Vector3(0.3f, 0.22f, 0.4f), fur);
        }
        Prim(PrimitiveType.Sphere, petBody, new Vector3(0f, 1.12f, -0.55f), new Vector3(0.12f, 0.09f, 0.1f), Lit(new Color(0.95f, 0.5f, 0.6f))); Prim(PrimitiveType.Sphere, petBody, new Vector3(0f, 0.6f, 0.55f), Vector3.one * 0.3f, belly);
        Prim(PrimitiveType.Sphere, petBody, new Vector3(0f, 1.75f, -0.15f), Vector3.one * 0.14f, Glow(new Color(2.2f, 1.8f, 0.5f)));      // a little glowing antenna
        }
        petShape = LocalBounds(petBody); petShapeOk = petShape.size.y > 0.05f;
        petBall = Prim(PrimitiveType.Sphere, home, new Vector3(1.6f, 0.3f, -0.5f), Vector3.one * 0.6f, Lit(new Color(0.95f, 0.3f, 0.3f))).transform;
        petTag = TagLabel(home, new Vector3(0f, 3.2f, 0f), 1.5f, "", Color.white);
        var desk = new GameObject("Pet care desk").transform; desk.SetParent(home, false); desk.localPosition = new Vector3(0f, 0f, -3.4f);
        Prim(PrimitiveType.Cube, desk, new Vector3(0f, 0.45f, 0f), new Vector3(7.4f, 0.9f, 0.8f), wood);
        string[] names = { "FEED", "PLAY", "WASH", "SLEEP", "OUTFITS" }; Color[] cols = { new Color(1.7f, 0.9f, 0.3f), new Color(0.4f, 1.7f, 0.6f), new Color(0.4f, 0.9f, 2f), new Color(1.1f, 0.5f, 1.8f), new Color(2.2f, 1.5f, 0.3f) };
        for (int i = 0; i < 5; i++) { float x = -3.0f + i * 1.5f; FacButton(desk, new Vector3(x, 1.15f, 0f), new Vector3(1.2f, 0.5f, 0.6f), Glow(cols[i]), 1300 + i); TagLabel(desk, new Vector3(x, 1.9f, 0f), 1.3f, "<b>" + names[i] + "</b>", i == 4 ? new Color(1f, 0.86f, 0.35f) : Color.white); }
        var sl = Label(home, "<b>LUMI'S PET HOUSE</b>\n<size=60%>Feed, play, wash and let Lumi sleep. Lumi grows up and you earn care points.</size>", 0f, 0f, 3.4f, 30f, 3f, new Color(1f, 0.84f, 0.3f)); sl.transform.localPosition = new Vector3(0f, 5.6f, 4.4f); mapLabels.Add(sl);
    }
    void PetCare(int points)
    {
        petPoints += points; petXp += points; AddXp(Mathf.Max(1, points / 2)); petTagT = 0f;
        while (petXp >= petLevel * 60) { petXp -= petLevel * 60; petLevel++; lvCoins++; LvSave(); AddXp(30); Banner("LUMI GREW!", "Level " + petLevel + ": " + PetStage + "   +1 star coin", false); Play(cWin, 0.7f); }
        PetSave();
    }
    string PetText(int i)
    {
        if (i == 1300) return "feed Lumi"; if (i == 1301) return "play ball with Lumi"; if (i == 1302) return "wash Lumi"; if (i == 1303) return petSleep ? "wake Lumi up" : "put Lumi to bed";
        if (i == 1310) return petSleep ? "Lumi is asleep" : "pat Lumi";
        if (i == 1304) return HunchStore.Owns(HunchStore.LumiOutfits) ? "change Lumi's outfit (now: " + PetOutfitNames[petOutfit] + ")" : "Lumi's outfits (STAR STORE)";
        return "";
    }
    void PetUse(int i)
    {
        if (!petRoot) return;
        if (i == 1304)
        {
            if (HunchStore.Owns(HunchStore.LumiOutfits)) { PetOutfitNext(); Note("Lumi is wearing: " + PetOutfitNames[petOutfit] + "."); }
            else if (menu && storePage != null) menu.OpenExtra(StoreDetail(HunchStore.LumiOutfits));
            else Note("Lumi's outfits are in the STAR STORE, in the menu.");
            return;
        }
        if (i == 1303) { petSleep = !petSleep; Play(cPick, 0.5f); Note(petSleep ? "Shh... Lumi is sleeping. Rest comes back while Lumi sleeps." : "Lumi wakes up and stretches."); petTagT = 0f; return; }
        if (petSleep) { Note("Lumi is asleep. Aim at SLEEP to wake Lumi up."); return; }
        if (i == 1300) { if (petFood > 92f) { Note("Lumi is full."); return; } int pts = petFood < 70f ? 10 : 3; petFood = Mathf.Min(100f, petFood + 30f); Play(cGrow, 0.6f); Note("Yum! Lumi munches a moon berry.  +" + pts + " care points"); petJump = 0.6f; PetCare(pts); return; }
        if (i == 1301) { if (petRest < 15f) { Note("Lumi is too tired to play. Time for bed."); return; } int pts = petFun < 70f ? 10 : 3; petFun = Mathf.Min(100f, petFun + 30f); petRest = Mathf.Max(0f, petRest - 8f); petClean = Mathf.Max(0f, petClean - 5f); Play(cPop, 0.7f); Note("Lumi chases the ball!  +" + pts + " care points"); petJump = 2f; PetCare(pts); return; }
        if (i == 1302) { if (petClean > 92f) { Note("Lumi is already sparkling clean."); return; } int pts = petClean < 70f ? 10 : 3; petClean = Mathf.Min(100f, petClean + 40f); Play(cHiss, 0.4f); Note("Splash! Bubbles everywhere.  +" + pts + " care points"); petWash = 1.5f; HunchFx.Spawn("teleport", petRoot.position + Vector3.up * 0.8f, 0.5f, 0.8f); PetCare(pts); return; }
        if (i == 1310) { if (Time.time < petPat) return; petPat = Time.time + 1f; petFun = Mathf.Min(100f, petFun + 4f); petJump = 0.4f; Play(cPick, 0.5f); HunchFx.Spawn("star hit", petRoot.position + Vector3.up * 1.8f, 0.3f, 0.5f); PetCare(1); }
    }
    /// Lumi's needs run down slowly on every map while you play; the pictures only move on the clinic map
    void UpdatePet(float dt)
    {
        if (!petRoot) return;
        if (petPlayer != player) PetLoad();
        if (!Playing) return;
        petFood = Mathf.Max(0f, petFood - dt * 0.33f); petClean = Mathf.Max(0f, petClean - dt * 0.22f);
        if (petSleep) { petRest = Mathf.Min(100f, petRest + dt * 5f); if (petRest >= 100f) { petSleep = false; if (mapNow == 12) Note("Lumi woke up, full of energy."); } }
        else { petRest = Mathf.Max(0f, petRest - dt * 0.28f); petFun = Mathf.Max(0f, petFun - dt * 0.42f); }
        petSaveT -= dt; if (petSaveT <= 0f) { petSaveT = 15f; PetSave(); }
        if (petPreview >= 0) { petPreviewT -= dt; if (petPreviewT <= 0f) { petPreview = -1; PetDress(); } }
        if (mapNow != 12)
        {
            petNag -= dt; if (petNag <= 0f && Mathf.Min(petFood, petFun) < 20f && !mgOn && !autoOn && !hunchOn) { petNag = 300f; sim.Say("ARIA: Lumi the moon pup misses you. Visit the Clinic and Pet House map to feed and play."); }
            return;
        }
        float t = Time.time, happy = (petFood + petFun + petClean + petRest) * 0.0025f; petJump = Mathf.Max(0f, petJump - dt); petWash = Mathf.Max(0f, petWash - dt);
        float grow = 0.75f + Mathf.Min(petLevel, 12) * 0.06f; petRoot.localScale = Vector3.one * grow;
        if (petSleep) { petRoot.localPosition = new Vector3(-3f, 0.2f, 2.5f); petBody.localScale = new Vector3(1.1f, 0.7f + Mathf.Sin(t * 1.5f) * 0.03f, 1.1f); petBody.localRotation = Quaternion.identity; }
        else
        {
            float hop = petJump > 0f ? Mathf.Abs(Mathf.Sin(t * 9f)) * 0.7f : Mathf.Abs(Mathf.Sin(t * (1.5f + happy * 3f))) * 0.12f * (0.3f + happy);
            Vector3 home = petJump > 1f ? new Vector3(Mathf.Sin(t * 3f) * 2.2f, 0f, Mathf.Cos(t * 2.2f) * 1.2f) : Vector3.zero;
            Vector3 pp = Vector3.Lerp(petRoot.localPosition, home, dt * 4f); pp.y = hop; petRoot.localPosition = pp;
            petBody.localScale = Vector3.one; petBody.localRotation = Quaternion.Euler(0f, Mathf.Sin(t * 1.2f) * 15f + (petWash > 0f ? t * 500f : 0f), Mathf.Sin(t * 2f) * 4f * happy);
            if (petBall) petBall.localPosition = petJump > 1f ? new Vector3(Mathf.Sin(t * 3f + 0.5f) * 2.6f, 0.3f + Mathf.Abs(Mathf.Sin(t * 7f)) * 0.8f, Mathf.Cos(t * 2.2f + 0.5f) * 1.5f) : Vector3.Lerp(petBall.localPosition, new Vector3(1.6f, 0.3f, -0.5f), dt * 3f);
        }
        petTagT -= dt; if (petTagT > 0f) return; petTagT = 0.3f;
        petTag.text = "<b>Lumi</b> the moon pup   " + PetStage + ", level " + petLevel + (petSleep ? "   <color=#C8A8FF>z z z</color>" : "") + "\n<size=80%>" + PetBar("Food", petFood) + "   " + PetBar("Fun", petFun) + "\n" + PetBar("Clean", petClean) + "   " + PetBar("Rest", petRest) + "\nCare points " + petPoints + "   next level in " + (petLevel * 60 - petXp) + "</size>";
    }
    static string PetBar(string name, float v) { int k = Mathf.Clamp(Mathf.RoundToInt(v / 12.5f), 0, 8); return name + " <color=" + (v > 40f ? "#7CFC9A" : (v > 20f ? "#FFD070" : "#FF9A8A")) + ">" + new string('|', k) + new string('.', 8 - k) + "</color>"; }
    public string ProbePet() { petFood = 30f; petFun = 30f; petClean = 30f; PetUse(1300); PetUse(1301); PetUse(1302); PetUse(1310); return "Lumi " + PetStage + " lv " + petLevel + " pts " + petPoints + " food " + petFood.ToString("F0") + " fun " + petFun.ToString("F0") + " clean " + petClean.ToString("F0") + " rest " + petRest.ToString("F0") + " mood " + PetMood(); }

    // ================================================================== boss arena (map 13)
    Transform bossBody, bossHead, bossGem; readonly List<Transform> bossOrbs = new List<Transform>(); Renderer[] bossEyes = new Renderer[2]; Material bossEyeOn, bossEyeCalm; int bossRound, bossLeft, bossZaps; float bossLaugh; bool bossWon;
    static readonly Vector3 BossAt = new Vector3(0f, 0f, 30f);
    void BuildBoss(Transform r)
    {
        BuildExtraSounds();
        var rock = Lit(new Color(0.36f, 0.35f, 0.38f)); var rock2 = Lit(new Color(0.28f, 0.27f, 0.31f)); bossEyeOn = Glow(new Color(2.6f, 1.6f, 0.3f)); bossEyeCalm = Glow(new Color(0.5f, 2.2f, 1.0f));
        var g = new GameObject("Grumpy Moon Golem").transform; g.SetParent(r, false); g.localPosition = BossAt; bossBody = g;
        Prim(PrimitiveType.Sphere, g, new Vector3(0f, 5f, 0f), new Vector3(9f, 9.5f, 8f), rock, true); Prim(PrimitiveType.Sphere, g, new Vector3(0f, 3.5f, -2.5f), new Vector3(6f, 5f, 4f), rock2);
        bossHead = new GameObject("Head").transform; bossHead.SetParent(g, false); bossHead.localPosition = new Vector3(0f, 11.3f, -0.5f);
        Prim(PrimitiveType.Sphere, bossHead, Vector3.zero, new Vector3(5f, 4.4f, 4.6f), rock, true); Prim(PrimitiveType.Cube, bossHead, new Vector3(0f, 1.0f, -2.0f), new Vector3(3.6f, 0.5f, 0.6f), rock2).transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
        for (int e = 0; e < 2; e++) bossEyes[e] = Prim(PrimitiveType.Sphere, bossHead, new Vector3(e == 0 ? -1f : 1f, 0.3f, -2.05f), Vector3.one * 0.8f, bossEyeOn).GetComponent<Renderer>();
        Prim(PrimitiveType.Cube, bossHead, new Vector3(0f, -1.0f, -2.1f), new Vector3(2f, 0.3f, 0.4f), Lit(new Color(0.08f, 0.08f, 0.1f)));
        foreach (float sx in new[] { -1f, 1f }) for (int j = 0; j < 4; j++) Prim(PrimitiveType.Sphere, g, new Vector3(sx * (5f + j * 1.1f), 7.5f - j * 1.6f, -0.6f - j * 0.5f), Vector3.one * (3.2f - j * 0.35f), j % 2 == 0 ? rock2 : rock, true);
        var gem = Prim(PrimitiveType.Cube, g, new Vector3(0f, 15.2f, -0.5f), Vector3.one * 1.3f, Glow(new Color(0.5f, 2.2f, 2.6f))); gem.transform.localRotation = Quaternion.Euler(45f, 0f, 45f); bossGem = gem.transform;
        TagLabel(g, new Vector3(0f, 17.6f, -0.5f), 3f, "<b>STAR DIAMOND</b>", new Color(0.6f, 1f, 1f));
        for (int i = 0; i < 12; i++)
        {
            var o = Prim(PrimitiveType.Sphere, r, Vector3.zero, Vector3.one * 1.3f, Glow(new Color(2.4f, 0.6f, 1.8f)), true); o.name = "Tickle spot"; var ot = o.AddComponent<HunchTarget>(); ot.kind = 36; ot.index = 200 + i;
            var sp = o.GetComponent<SphereCollider>(); if (sp) sp.radius = 0.8f; o.SetActive(false); bossOrbs.Add(o.transform);
        }
        var sign = Label(r, "<b>BOSS ARENA: THE GRUMPY MOON GOLEM</b>\n<size=55%>Zap every glowing tickle spot. Three rounds. Nobody is hurt: it laughs, and gives you the Star Diamond.</size>", 0f, 0f, 6f, 64f, 6f, new Color(1f, 0.84f, 0.3f)); sign.transform.localPosition = new Vector3(0f, 22f, 44f); mapLabels.Add(sign);
    }
    void BossRound(int n) { bossRound = n; bossLeft = Mathf.Min(bossOrbs.Count, 4 + n * 3); for (int i = 0; i < bossOrbs.Count; i++) bossOrbs[i].gameObject.SetActive(i < bossLeft); }
    void BossReset() { bossWon = false; bossZaps = 0; bossLaugh = 0f; foreach (var e in bossEyes) if (e) e.sharedMaterial = bossEyeOn; if (bossGem) bossGem.gameObject.SetActive(true); BossRound(0); tool = Zapper; }
    public void BossEnter()
    {
        if (lvPlayer != player) LvLoad();
        if (lvBossFree) lvBossFree = false; else if (!Spend(3)) return;
        LvSave(); GoMap(13);
    }
    void UpdateBoss(float dt)
    {
        float t = Time.time; bossLaugh = Mathf.Max(0f, bossLaugh - dt);
        if (bossBody) { bossBody.localPosition = BossAt + new Vector3(Mathf.Sin(t * 40f) * 0.15f * bossLaugh, Mathf.Abs(Mathf.Sin(t * 12f)) * 0.5f * bossLaugh, 0f); bossHead.localRotation = Quaternion.Euler(Mathf.Sin(t * 0.8f) * 4f - bossLaugh * 10f, Mathf.Sin(t * 0.5f) * 12f, 0f); bossGem.Rotate(0f, dt * 60f, 0f, Space.World); }
        float sp = 0.45f + bossRound * 0.3f;
        for (int i = 0; i < bossOrbs.Count; i++)
        {
            var o = bossOrbs[i]; if (!o.gameObject.activeSelf) continue; float a = t * sp * (i % 2 == 0 ? 1f : -1f) + i * 0.9f, rad = 8.5f + Mathf.Sin(t * 0.7f + i) * 1.5f;
            o.localPosition = BossAt + new Vector3(Mathf.Cos(a) * rad, 5.5f + Mathf.Sin(t * 1.1f + i * 1.3f) * 4f, Mathf.Sin(a) * rad * 0.6f - 2f);
        }
    }
    void BossHit(int i)
    {
        if (!mgOn || i < 0 || i >= bossOrbs.Count || !bossOrbs[i].gameObject.activeSelf) return;
        bossOrbs[i].gameObject.SetActive(false); HunchFx.Spawn("star hit", bossOrbs[i].position, 1f, 0.8f); Play(cPop, 0.7f); mgScore += 50f; bossZaps++; bossLeft--; bossLaugh = Mathf.Max(bossLaugh, 0.5f);
        if (bossLeft > 0) return;
        bossLaugh = 2f; Play(cWin, 0.7f);
        if (bossRound < 2) { Banner("ROUND " + (bossRound + 2), "The Golem is giggling. More tickle spots, and faster!", false); BossRound(bossRound + 1); return; }
        bossWon = true; mgScore += Mathf.Round(mgT) * 10f; lvDiamonds++; lvCoins += 2; AddItem(Crystal, 3); AddXp(120); LvSave();
        foreach (var e in bossEyes) if (e) e.sharedMaterial = bossEyeCalm; if (bossGem) bossGem.gameObject.SetActive(false);
        MgEnd("The Golem laughed so hard it gave you the STAR DIAMOND, 2 star coins and 3 crystals!");
    }
    string BossStatus() { return "Time " + Mathf.CeilToInt(mgT) + " s   Round " + (bossRound + 1) + " of 3   Tickle spots left " + bossLeft + "   Score " + Mathf.RoundToInt(mgScore); }
    IEnumerator AutoBoss()
    {
        AutoSay("Boss Arena. The Grumpy Moon Golem guards a Star Diamond. The AI tickles it with beams until it laughs.");
        float until = Time.time + 40f;
        while (mapNow == 13 && autoOn && Time.time < until && !bossWon)
        {
            if (!mgOn) { yield return new WaitForSeconds(0.4f); if (mgResult.Length > 0) break; continue; }
            int k = -1; for (int i = 0; i < bossOrbs.Count; i++) if (bossOrbs[i].gameObject.activeSelf) { k = i; break; }
            if (k >= 0) { Face(bossOrbs[k].position); yield return new WaitForSeconds(0.35f); ShowBeam(Muzzle(), bossOrbs[k].position, beamGreen); Play(cPew, 0.5f); BossHit(k); }
            yield return new WaitForSeconds(0.25f);
        }
    }
    public string ProbeBoss() { int n = 0; for (int i = 0; i < bossOrbs.Count; i++) if (bossOrbs[i].gameObject.activeSelf) { BossHit(i); n++; } return "zapped " + n + " | " + BossStatus() + " won " + bossWon + " diamonds " + lvDiamonds + " mg " + mgOn; }
    public string ProbeBossEnter() { BossEnter(); return "map " + mapNow + " coins " + lvCoins + " free " + lvBossFree; }
    public string ProbeLevel() { AddXp(probeInt > 0 ? probeInt : 150); return "level " + lvLevel + " xp " + lvXp + "/" + LvNeed + " queue " + lvQueue + " coins " + lvCoins + " open " + lvOpen + " welcome " + lvWelcome; }
    public string ProbeGift() { LevelUse(900); return "opened " + lvGiftOpened + " coins " + lvCoins + " | " + (lvInfo ? lvInfo.text : ""); }
    public string ProbeNext() { LevelUse(901); return "open " + lvOpen + " queue " + lvQueue; }
    public string ProbeCostume() { lvCoins += 20; CostumePick(Mathf.Clamp(probeInt, 0, Costumes.Length - 1)); if (!lvOpen) LvShow(false, lvLevel); else LvDress(); return "costume " + lvCostume + " " + Costumes[lvCostume].name + " owned " + lvOwned + " coins " + lvCoins + " season " + SeasonTitle; }
    public string ProbeSeason() { return "season " + SeasonTitle + " on " + seasonOn + " bits " + seasonBits.Count + " flakes " + seasonFlakes.Count + " active " + (seasonRoot && seasonRoot.gameObject.activeSelf); }
}
