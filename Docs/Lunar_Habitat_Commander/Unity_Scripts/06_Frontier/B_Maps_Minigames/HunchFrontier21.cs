// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/06_Frontier/B_Maps_Minigames/HunchFrontier21.cs
//  Section : 06_Frontier/B_Maps_Minigames
//  Folder  : Extra maps and mini-games: solar farm, mining pit, Factory Land, Star Patrol, crew
//            radio + Star Clicker, Science Academy, My Astronaut, model kits, Fun Zone, endless
//            ground, Star Store pages.
//  Purpose : Twenty-first part of Moon Frontier: - Moon ground that never ends: past the edge of
//            the model the game lays its own terrain in tiles around you,
//  Contains: HunchFrontier, NinjaStar, BlastRock
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// Twenty-first part of Moon Frontier:
///   - Moon ground that never ends: past the edge of the model the game lays its own terrain in tiles around you,
///     with hills and craters, and a glowing border ring says how far you may go. You can never fall off the map.
///   - Ninja stars (full game): a throwing tool with real flight, spin, a glowing trail and one ricochet.
///   - The Asteroid Blaster (full game): a gunner's seat with a giant twin laser. Asteroids cross the sky; burn them down.
public partial class HunchFrontier
{
    // ================================================================== endless ground and the border
    const float TileSize = 240f, MapEdge = 780f; public const float BorderR = 1500f; const int TileRes = 24; int TileRing = 3;
    Transform terrRoot, borderRing; readonly Dictionary<long, GameObject> tiles = new Dictionary<long, GameObject>(); Material terrMat; float terrBase, terrNext; bool terrOk; int terrMade; float borderNote;

    float terrOut, terrDrop = 1.2f; bool terrOld; string terrLog = ""; Material rockMat; Mesh[] rockShapes; readonly HashSet<long> coreTiles = new HashSet<long>();
    void BuildEndlessGround()
    {
        terrRoot = new GameObject("Endless Moon ground").transform; terrRoot.SetParent(root, false);
        Vector3 g; terrBase = GroundAt(fieldC.x, fieldC.z, out g) ? g.y : 0f; terrOut = terrBase;
        FindMoonbases();
        // the old flat Moon plane around the base is taken away; this ground replaces it. The paved pad under the main base stays: the buildings and roads are part of it.
        try
        {
            Physics.SyncTransforms(); var old = new List<Collider>(); float sum = 0f; int hits = 0;
            foreach (var pt in new[] { new Vector2(400f, 0f), new Vector2(-400f, 0f), new Vector2(0f, 400f), new Vector2(0f, -400f), new Vector2(330f, 330f), new Vector2(-330f, 330f), new Vector2(330f, -330f), new Vector2(-330f, -330f), new Vector2(690f, 20f), new Vector2(-690f, -20f) })
            {
                RaycastHit h; if (!Physics.Raycast(new Vector3(pt.x, terrBase + 600f, pt.y), Vector3.down, out h, 900f, GroundMask, QueryTriggerInteraction.Ignore)) continue;
                Bounds bb = h.collider.bounds; if (bb.size.x < 600f || bb.size.z < 600f || bb.size.y > 2f || h.normal.y < 0.98f || h.collider.name.StartsWith("BAKED")) continue;
                sum += h.point.y; hits++; if (!old.Contains(h.collider)) old.Add(h.collider);
            }
            if (hits >= 4 && old.Count > 0)
            {
                terrOut = sum / hits; terrDrop = 0f; terrOld = true;
                foreach (var c in old) { var r = c.GetComponent<Renderer>(); if (r) r.enabled = false; c.enabled = false; terrLog += "replaced " + c.name + "; "; }
            }
            else if (hits == 0) { terrOld = true; terrDrop = 0f; terrOut = terrBase - 0.06f; terrLog = "there was no ground outside the paved pad: new ground laid level with it; "; }
            else terrLog = "the old ground was kept (" + hits + " flat hits); ";
        }
        catch (Exception e) { terrLog = "old ground kept: " + e.Message; }
        Texture2D bump; var tex = MoonTexture(out bump);
        terrMat = new Material(Lit(Color.white)); terrMat.mainTexture = tex; if (terrMat.HasProperty("_BaseMap")) terrMat.SetTexture("_BaseMap", tex);
        if (terrMat.HasProperty("_BumpMap")) { terrMat.SetTexture("_BumpMap", bump); terrMat.EnableKeyword("_NORMALMAP"); if (terrMat.HasProperty("_BumpScale")) terrMat.SetFloat("_BumpScale", 1.3f); }
        if (terrMat.HasProperty("_Smoothness")) terrMat.SetFloat("_Smoothness", 0.04f);
        // one Moon floor everywhere: the paved pad from Blender gets the same dust and bumps as the ground around it
        try
        {
            var model = GameObject.Find("HUNCH_Lunar_Base"); var pt = model ? model.transform.Find("BAKED_KINGDOM_Ground") : null; var pr = pt ? pt.GetComponent<Renderer>() : null;
            if (pr)
            {
                var pm = new Material(terrMat); Vector2 tile = new Vector2(pr.bounds.size.x / 22f, pr.bounds.size.z / 22f);
                pm.mainTextureScale = tile; if (pm.HasProperty("_BaseMap")) pm.SetTextureScale("_BaseMap", tile); if (pm.HasProperty("_BumpMap")) pm.SetTextureScale("_BumpMap", tile);
                var ms = pr.sharedMaterials; for (int i = 0; i < ms.Length; i++) ms[i] = pm; pr.sharedMaterials = ms; terrLog += "pad re-surfaced; ";
            }
        }
        catch (Exception e) { terrLog += "pad kept: " + e.Message + "; "; }
        rockMat = new Material(terrMat); rockMat.color = new Color(0.62f, 0.6f, 0.58f); if (rockMat.HasProperty("_BaseColor")) rockMat.SetColor("_BaseColor", new Color(0.62f, 0.6f, 0.58f));
        rockShapes = new[] { RockShape(1), RockShape(2), RockShape(3), RockShape(4) };
        terrOk = true;
        // the whole map is laid at once, so everything built afterwards stands on it; further out it grows as you walk
        for (int tx = -4; tx <= 3; tx++) for (int tz = -4; tz <= 3; tz++) { long key = ((long)tx << 32) ^ (uint)tz; tiles[key] = MakeTile(tx, tz); coreTiles.Add(key); }
        Physics.SyncTransforms();
        // the border: a ring of soft light posts, so the limit can be seen from far away
        borderRing = new GameObject("Border").transform; borderRing.SetParent(root, false);
        var glow = Glow(new Color(0.5f, 1.8f, 2.8f));
        for (int i = 0; i < 72; i++) { float a = i * 5f * Mathf.Deg2Rad; var p = Prim(PrimitiveType.Cylinder, borderRing, new Vector3(Mathf.Sin(a) * BorderR, terrBase + 30f, Mathf.Cos(a) * BorderR), new Vector3(3f, 30f, 3f), glow); p.name = "Border light"; }
    }
    /// Moon dust made in code: fine grain, soft patches and many small dimples. It repeats without a seam. Also gives its bump map.
    Texture2D MoonTexture(out Texture2D bump)
    {
        const int n = 256; var h = new float[n * n];
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            float u = x / (float)n, v = y / (float)n, t = 0f, amp = 0.5f, f = 5f;
            for (int o = 0; o < 4; o++)
            {
                float a = Mathf.PerlinNoise(u * f + 3.1f, v * f + 7.7f), b2 = Mathf.PerlinNoise((u - 1f) * f + 3.1f, v * f + 7.7f), c = Mathf.PerlinNoise(u * f + 3.1f, (v - 1f) * f + 7.7f), d = Mathf.PerlinNoise((u - 1f) * f + 3.1f, (v - 1f) * f + 7.7f);
                t += amp * Mathf.Lerp(Mathf.Lerp(a, b2, u), Mathf.Lerp(c, d, u), v); amp *= 0.5f; f *= 2.3f;
            }
            h[y * n + x] = t;
        }
        var rnd = new System.Random(77);
        for (int k = 0; k < 90; k++)      // dimples and pebble bumps, wrapped at the edges
        {
            int cx = rnd.Next(n), cy = rnd.Next(n); float r = 2f + (float)rnd.NextDouble() * (k < 12 ? 16f : 6f), deep = (k % 3 == 0 ? 0.10f : -0.13f) * (0.5f + (float)rnd.NextDouble());
            int ir = Mathf.CeilToInt(r * 1.3f);
            for (int dy = -ir; dy <= ir; dy++) for (int dx = -ir; dx <= ir; dx++)
            {
                float q = Mathf.Sqrt(dx * dx + dy * dy) / r; if (q > 1.3f) continue; int px = ((cx + dx) % n + n) % n, py = ((cy + dy) % n + n) % n;
                h[py * n + px] += q < 1f ? deep * (1f - q * q) : -deep * 0.35f * (1f - (q - 1f) / 0.3f);
            }
        }
        var col = new Color32[n * n]; var nor = new Color32[n * n];
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            float c = Mathf.Clamp01(0.34f + h[y * n + x] * 0.36f); byte gch = (byte)(c * 255f); col[y * n + x] = new Color32((byte)(c * 255f * 1.02f > 255f ? 255 : (int)(c * 255f * 1.02f)), gch, (byte)(c * 245f), 255);
            float hx = h[y * n + (x + 1) % n] - h[y * n + (x + n - 1) % n], hy = h[((y + 1) % n) * n + x] - h[((y + n - 1) % n) * n + x];
            Vector3 nv = new Vector3(-hx * 9f, -hy * 9f, 1f).normalized; nor[y * n + x] = new Color32((byte)(nv.x * 127f + 128f), (byte)(nv.y * 127f + 128f), (byte)(nv.z * 127f + 128f), 255);
        }
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, true, false) { wrapMode = TextureWrapMode.Repeat, anisoLevel = 8, name = "Moon dust" }; tex.SetPixels32(col); tex.Apply(true);
        bump = new Texture2D(n, n, TextureFormat.RGBA32, true, true) { wrapMode = TextureWrapMode.Repeat, anisoLevel = 8, name = "Moon dust bumps" }; bump.SetPixels32(nor); bump.Apply(true);
        return tex;
    }
    /// a lumpy rock: a ball of 66 points pushed in and out
    Mesh RockShape(int seed)
    {
        var v = new List<Vector3> { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
        var t = new List<int> { 0, 4, 3, 0, 3, 5, 0, 5, 2, 0, 2, 4, 1, 3, 4, 1, 5, 3, 1, 2, 5, 1, 4, 2 };
        for (int pass = 0; pass < 2; pass++)
        {
            var nt = new List<int>(); var mid = new Dictionary<long, int>();
            Func<int, int, int> M = (a, b) => { long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a; int i; if (mid.TryGetValue(key, out i)) return i; v.Add(((v[a] + v[b]) * 0.5f).normalized); mid[key] = v.Count - 1; return v.Count - 1; };
            for (int i = 0; i < t.Count; i += 3) { int a = t[i], b = t[i + 1], c = t[i + 2], ab = M(a, b), bc = M(b, c), ca = M(c, a); nt.AddRange(new[] { a, ab, ca, ab, b, bc, ca, bc, c, ab, bc, ca }); }
            t = nt;
        }
        for (int i = 0; i < v.Count; i++) { Vector3 p = v[i]; float k = 0.72f + 0.5f * Mathf.PerlinNoise(p.x * 1.3f + seed * 9.1f, p.y * 1.3f + p.z * 1.7f + seed * 3.3f); v[i] = new Vector3(p.x * k, p.y * k * 0.72f, p.z * k); }
        var m = new Mesh { name = "Rock " + seed }; m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds(); return m;
    }
    /// rocks and pebbles for one tile, joined into one mesh. Always the same for the same tile. Nothing lies on the paved pad, under a roof, or where the portals and games stand.
    void ScatterRocks(GameObject tile, int tx, int tz)
    {
        var rnd = new System.Random(tx * 73856093 ^ tz * 19349663); var v = new List<Vector3>(); var nrm = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
        for (int i = 0; i < 90; i++)
        {
            float x = (tx + (float)rnd.NextDouble()) * TileSize, z = (tz + (float)rnd.NextDouble()) * TileSize, pick = (float)rnd.NextDouble(), r01 = (float)rnd.NextDouble(), yaw = (float)rnd.NextDouble() * 360f; int shape = rnd.Next(rockShapes.Length);
            float r = pick < 0.76f ? Mathf.Lerp(0.07f, 0.3f, r01) : pick < 0.96f ? Mathf.Lerp(0.3f, 0.95f, r01) : Mathf.Lerp(1.2f, 2.8f, r01);
            if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) < 300f) continue;
            Vector3 at = new Vector3(x, TerrHeight(x, z), z); if (UnderRoof(at, 5f)) continue;
            if (r > 0.3f) { bool near = x * x + z * z < 430f * 430f; for (int k = 0; k < 5 && !near; k++) if (baseOk[k] && (new Vector2(x - baseCentre[k].x, z - baseCentre[k].z)).sqrMagnitude < 340f * 340f) near = true; if (near) continue; }
            var src = rockShapes[shape]; var sv = src.vertices; var sn = src.normals; var st = src.triangles; int b0 = v.Count; Quaternion q = Quaternion.Euler((float)rnd.NextDouble() * 20f - 10f, yaw, (float)rnd.NextDouble() * 20f - 10f);
            Vector3 local = at - tile.transform.position + Vector3.up * r * 0.28f;
            for (int k = 0; k < sv.Length; k++) { v.Add(local + q * (sv[k] * r)); nrm.Add(q * sn[k]); uv.Add(new Vector2(sv[k].x + sv[k].z, sv[k].y + sv[k].z) * r * 0.25f); }
            for (int k = 0; k < st.Length; k++) tri.Add(b0 + st[k]);
            if (r >= 1.2f) { var c = new GameObject("Boulder"); c.transform.SetParent(tile.transform, false); c.transform.localPosition = local; c.AddComponent<SphereCollider>().radius = r * 0.78f; }
        }
        if (v.Count == 0) return;
        var go = new GameObject("Rocks"); go.transform.SetParent(tile.transform, false);
        var m = new Mesh { name = "Rocks" }; m.SetVertices(v); m.SetNormals(nrm); m.SetUVs(0, uv); m.SetTriangles(tri, 0); m.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh = m; go.AddComponent<MeshRenderer>().sharedMaterial = rockMat;
    }
    /// the height of the Moon ground. Around the bases it is level; past the edge of the old map it rises into hills and craters.
    float TerrHeight(float x, float z)
    {
        float d = Mathf.Sqrt(x * x + z * z), k = Mathf.Clamp01((d - MapEdge) / 260f); k = k * k * (3f - 2f * k);
        // level ground all round every moonbase, so the portals and the games there stand on a flat floor
        if (k > 0f) for (int b = 0; b < 5; b++) if (baseOk[b]) { float bx = x - baseCentre[b].x, bz = z - baseCentre[b].z, m = Mathf.Clamp01((Mathf.Sqrt(bx * bx + bz * bz) - 330f) / 220f); m = m * m * (3f - 2f * m); if (m < 1f) k *= m; }
        float hills = (Mathf.PerlinNoise(x * 0.004f + 31.7f, z * 0.004f + 12.3f) - 0.5f) * 46f + (Mathf.PerlinNoise(x * 0.017f + 5.1f, z * 0.017f + 9.9f) - 0.5f) * 9f;
        // craters: one possible crater in every 300 m cell
        float cx = Mathf.Floor(x / 300f), cz = Mathf.Floor(z / 300f), crater = 0f;
        for (int ix = -1; ix <= 1; ix++) for (int iz = -1; iz <= 1; iz++)
        {
            float gx = cx + ix, gz = cz + iz, h = Mathf.Abs(Mathf.Sin(gx * 127.1f + gz * 311.7f) * 43758.5f) % 1f; if (h < 0.45f) continue;
            float px = (gx + 0.2f + 0.6f * (Mathf.Abs(Mathf.Sin(gx * 269.5f + gz * 183.3f) * 9631.7f) % 1f)) * 300f, pz = (gz + 0.2f + 0.6f * (Mathf.Abs(Mathf.Sin(gx * 419.2f + gz * 71.9f) * 5137.3f) % 1f)) * 300f, r = 40f + 70f * h;
            float t = Mathf.Sqrt((x - px) * (x - px) + (z - pz) * (z - pz)) / r; if (t > 1.35f) continue;
            crater += t < 1f ? -r * 0.22f * (1f - t * t) + r * 0.05f * t * t : r * 0.05f * (1f - (t - 1f) / 0.35f);
        }
        // under the paved pad of the main base the ground lies a little lower, out of sight
        float pad = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)), drop = terrOld ? (pad < 280f ? Mathf.Lerp(1.2f, 0.42f, Mathf.InverseLerp(270f, 280f, pad)) : Mathf.Lerp(0.42f, 0f, Mathf.InverseLerp(280f, 290f, pad))) : terrDrop;      // under the Blender pad this ground stays below it everywhere (the pad reaches 0.3 m down), and comes up to its level just outside the edge
        return terrOut - drop + (hills + crater) * k;
    }
    GameObject MakeTile(int tx, int tz)
    {
        var go = new GameObject("Ground " + tx + " " + tz); go.transform.SetParent(terrRoot, false); go.transform.position = new Vector3(tx * TileSize, 0f, tz * TileSize);
        int n = TileRes + 1; var v = new Vector3[n * n]; var uv = new Vector2[n * n]; var tri = new int[TileRes * TileRes * 6]; float step = TileSize / TileRes;
        for (int j = 0; j < n; j++) for (int i = 0; i < n; i++) { float lx = i * step, lz = j * step; v[j * n + i] = new Vector3(lx, TerrHeight(tx * TileSize + lx, tz * TileSize + lz), lz); uv[j * n + i] = new Vector2(tx * TileSize + lx, tz * TileSize + lz) / 22f; }
        int q = 0; for (int j = 0; j < TileRes; j++) for (int i = 0; i < TileRes; i++) { int a = j * n + i; tri[q++] = a; tri[q++] = a + n; tri[q++] = a + 1; tri[q++] = a + 1; tri[q++] = a + n; tri[q++] = a + n + 1; }
        var m = new Mesh { name = "Ground tile" }; m.vertices = v; m.uv = uv; m.triangles = tri; m.RecalculateNormals(); m.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh = m; var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = terrMat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        go.AddComponent<MeshCollider>().sharedMesh = m; terrMade++; try { ScatterRocks(go, tx, tz); } catch { } return go;
    }
    /// makes sure the ground tile under this point exists right now. False only past the border.
    bool EnsureGround(float x, float z)
    {
        if (!terrOk || x * x + z * z > (BorderR + 300f) * (BorderR + 300f)) return false;
        int tx = Mathf.FloorToInt(x / TileSize), tz = Mathf.FloorToInt(z / TileSize); long key = ((long)tx << 32) ^ (uint)tz;
        if (!tiles.ContainsKey(key)) { tiles[key] = MakeTile(tx, tz); Physics.SyncTransforms(); }
        return true;
    }
    void UpdateEndlessGround()
    {
        if (!terrOk || !cam) return;
        // in the mini-game maps (their own arenas far out) the Moon ground and the border are put away, so they never cut through a game
        bool here = mapNow == 0; if (terrRoot.gameObject.activeSelf != here) terrRoot.gameObject.SetActive(here); if (borderRing && borderRing.gameObject.activeSelf != here) borderRing.gameObject.SetActive(here);
        if (!here) return;
        Vector3 p = cam.position;
        if (Time.time >= terrNext)
        {
            terrNext = Time.time + 0.5f; int cx = Mathf.FloorToInt(p.x / TileSize), cz = Mathf.FloorToInt(p.z / TileSize), made = 0;
            for (int dx = -TileRing; dx <= TileRing && made < 2; dx++) for (int dz = -TileRing; dz <= TileRing && made < 2; dz++)      // at most two new tiles each half second, so it never stutters
            {
                long key = ((long)(cx + dx) << 32) ^ (uint)(cz + dz); if (tiles.ContainsKey(key)) continue;
                tiles[key] = MakeTile(cx + dx, cz + dz); made++;
            }
            if (tiles.Count > 140)
            {
                var far = new List<long>(); foreach (var kv in tiles) { if (coreTiles.Contains(kv.Key)) continue; Vector3 tp = kv.Value.transform.position; if (Mathf.Abs(tp.x + TileSize * 0.5f - p.x) > TileSize * (TileRing + 2.5f) || Mathf.Abs(tp.z + TileSize * 0.5f - p.z) > TileSize * (TileRing + 2.5f)) far.Add(kv.Key); }
                foreach (long k in far) { var g = tiles[k]; tiles.Remove(k); foreach (var mf in g.GetComponentsInChildren<MeshFilter>()) if (mf.sharedMesh) Destroy(mf.sharedMesh); Destroy(g); }
            }
        }
        // the border: a gentle push back, never a hard stop that could trap you
        if (!Playing || mapNow != 0) return;
        Vector3 f = Feet(); float d = new Vector2(f.x, f.z).magnitude;
        if (d > BorderR - 40f && Time.time > borderNote) { borderNote = Time.time + 6f; Note("This is the edge of the map. Turn back towards the base."); }
        if (d > BorderR)
        {
            Vector3 inward = -new Vector3(f.x, 0f, f.z).normalized; Vector3 to = new Vector3(f.x, 0f, f.z) + inward * (d - BorderR + 6f); Vector3 g; float y = GroundAt(to.x, to.z, out g) ? g.y : TerrHeight(to.x, to.z);
            bool fly = game.flyMode; TeleportTo(new Vector3(to.x, Mathf.Max(y, fly ? f.y : y) + 0.1f, to.z), Mathf.Atan2(inward.x, inward.z) * Mathf.Rad2Deg); game.flyMode = fly;
        }
    }
    public string ProbeTerrain() { return terrLog + " out " + terrOut.ToString("F2") + " tiles " + tiles.Count + " made " + terrMade + " base " + terrBase.ToString("F1") + " here " + TerrHeight(Feet().x, Feet().z).ToString("F1") + " feet " + Feet().ToString("F0") + " border " + BorderR; }

    // ================================================================== ninja stars
    public const int NinjaTool = 10;
    class NinjaStar { public Transform t; public TrailRenderer trail; public Vector3 vel; public float life, stuck; public bool live, bounced; }
    readonly List<NinjaStar> ninjaStars = new List<NinjaStar>(); GameObject ninjaView; bool ninjaBuilt; int ninjaThrown;

    void BuildNinja()
    {
        ninjaBuilt = true; var trailMat = Glow(new Color(1.6f, 0.6f, 3f));
        for (int i = 0; i < 10; i++)
        {
            GameObject g = null; try { var src = KitSource(false, "NinjaStar"); if (src) { g = Instantiate(src); foreach (var c in g.GetComponentsInChildren<Collider>(true)) Destroy(c); var b = BoundsOf(g); float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)); if (longest > 0.0001f) g.transform.localScale *= 0.42f / longest; } } catch { g = null; }
            var holder = new GameObject("Ninja star").transform; holder.SetParent(root, false);
            if (g) { g.transform.SetParent(holder, false); g.transform.localPosition = Vector3.zero; g.SetActive(true); }
            else { var gold = Lit(new Color(0.9f, 0.7f, 0.25f)); for (int k = 0; k < 2; k++) Prim(PrimitiveType.Cube, holder, Vector3.zero, new Vector3(0.4f, 0.03f, 0.1f), gold).transform.localRotation = Quaternion.Euler(0f, k * 90f, 0f); }
            var tr = holder.gameObject.AddComponent<TrailRenderer>(); tr.sharedMaterial = trailMat; tr.time = 0.35f; tr.startWidth = 0.16f; tr.endWidth = 0f; tr.minVertexDistance = 0.08f; tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            holder.gameObject.SetActive(false); ninjaStars.Add(new NinjaStar { t = holder, trail = tr });
        }
    }
    void NinjaThrow(Ray ray)
    {
        if (!ninjaBuilt) BuildNinja();
        foreach (var s in ninjaStars)
        {
            if (s.live) continue;
            s.live = true; s.bounced = false; s.stuck = 0f; s.life = 4f; s.t.position = cam.position + cam.right * 0.25f - cam.up * 0.12f + ray.direction * 0.5f; s.t.rotation = Quaternion.LookRotation(ray.direction, cam.up);
            s.vel = ray.direction * 30f + Vector3.up * 1.2f; s.t.gameObject.SetActive(true); s.trail.Clear(); ninjaThrown++;
            recoil = 1.4f; Play(cPop, 0.45f); game.Buzz(0.4f, 0.05f); return;
        }
    }
    void UpdateNinja(float dt)
    {
        if (!ninjaBuilt) return;
        foreach (var s in ninjaStars)
        {
            if (!s.live) continue;
            if (s.stuck > 0f) { s.stuck -= dt; if (s.stuck <= 0f) { s.live = false; s.t.gameObject.SetActive(false); } continue; }
            s.life -= dt; if (s.life <= 0f) { s.live = false; s.t.gameObject.SetActive(false); continue; }
            s.vel += Vector3.down * 1.62f * dt;                                   // the Moon's own gravity
            Vector3 step = s.vel * dt; float dist = step.magnitude; RaycastHit h;
            s.t.Rotate(0f, 1400f * dt, 0f, Space.Self);
            if (dist > 0.0001f && Cast(new Ray(s.t.position, step / dist), out h, dist + 0.12f))
            {
                var tg = h.collider.GetComponentInParent<HunchTarget>(); bool hitThing = false;
                if (tg && tg.kind == 1 && tg.index < aliens.Count) { HitAlien(aliens[tg.index], zapLv * 3f, false); hitThing = true; }
                else if (tg && tg.kind == 32) { ZrHit(tg.index); hitThing = true; }
                else if (tg && tg.kind == 36) { if (tg.index >= 200) BossHit(tg.index - 200); else StarHit(tg.index); hitThing = true; }
                else if (tg && tg.kind == 37) { SliceHit(tg.index); hitThing = true; }
                try { Fx2("sparks explode yellow", "electro hit", h.point, 0.35f, 0.4f, 0.8f); } catch { }
                if (!hitThing && !s.bounced && Vector3.Angle(-s.vel, h.normal) > 35f) { s.bounced = true; s.vel = Vector3.Reflect(s.vel, h.normal) * 0.72f; s.t.position = h.point + h.normal * 0.06f; Play(cThud, 0.3f); }      // one ricochet off a glancing hit
                else { s.t.position = h.point + h.normal * 0.03f; s.stuck = hitThing ? 0.05f : 1.6f; s.t.rotation = Quaternion.LookRotation(Vector3.Cross(h.normal, Vector3.up).sqrMagnitude > 0.01f ? Vector3.Cross(h.normal, Vector3.up) : Vector3.forward, h.normal); if (!hitThing) Play(cThud, 0.4f); }
            }
            else s.t.position += step;
        }
    }

    // ================================================================== the Asteroid Blaster
    Transform blastRoot, blastGun; TextMeshPro blastLabel; LineRenderer blastBeamA, blastBeamB; bool blastOk, blastOn; Vector3 blastAt; float blastLeft, blastNext; int blastScore, blastBest;
    class BlastRock { public Transform t; public Vector3 vel, spin; public float hp, r; public bool live; public Renderer rend; }
    readonly List<BlastRock> blastRocks = new List<BlastRock>(); Material rockCold, rockHot;

    void BuildBlaster()
    {
        Vector3 g; if (!FunSpot(9f, out g)) { funLog += "no room for the Asteroid Blaster; "; return; }
        blastOk = true; blastAt = g; var s = new GameObject("Asteroid Blaster").transform; s.SetParent(funRoot, false); s.position = g; blastRoot = s;
        var deck = Lit(new Color(0.22f, 0.24f, 0.3f)); var post = Lit(new Color(0.75f, 0.77f, 0.82f)); var gold = Lit(new Color(0.83f, 0.62f, 0.2f)); var seat = Lit(new Color(0.6f, 0.15f, 0.15f));
        Prim(PrimitiveType.Cylinder, s, new Vector3(0f, 0.3f, 0f), new Vector3(8f, 0.3f, 8f), deck); Solid(s, new Vector3(0f, 0.3f, 0f), new Vector3(5.6f, 0.6f, 5.6f));
        Prim(PrimitiveType.Cylinder, s, new Vector3(0f, 0.61f, 0f), new Vector3(8.3f, 0.02f, 8.3f), Glow(new Color(3f, 0.6f, 0.4f))); Prim(PrimitiveType.Cylinder, s, new Vector3(0f, 0.62f, 0f), new Vector3(7.6f, 0.021f, 7.6f), deck);
        // the gunner's seat
        Prim(PrimitiveType.Cube, s, new Vector3(0f, 1.0f, -0.2f), new Vector3(0.7f, 0.12f, 0.7f), seat); Prim(PrimitiveType.Cube, s, new Vector3(0f, 1.55f, -0.55f), new Vector3(0.7f, 1.1f, 0.12f), seat); Prim(PrimitiveType.Cylinder, s, new Vector3(0f, 0.8f, -0.2f), new Vector3(0.18f, 0.2f, 0.18f), post);
        // the gun: a Blender model on a yoke above the seat, or simple barrels when the model is missing
        blastGun = new GameObject("Blaster gun").transform; blastGun.SetParent(s, false); blastGun.localPosition = new Vector3(0f, 5.4f, 0f);
        GameObject model = null; try { var src = KitSource(false, "AsteroidBlaster"); if (src) { model = Instantiate(src); foreach (var c in model.GetComponentsInChildren<Collider>(true)) Destroy(c); model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 180f, 0f) * src.transform.localRotation); var b = BoundsOf(model); float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)); var h = new GameObject("Model").transform; h.position = b.center; model.transform.SetParent(h, true); h.localScale = Vector3.one * (7f / Mathf.Max(0.001f, longest)); h.SetParent(blastGun, false); h.localPosition = new Vector3(0f, 0f, 3.2f); h.localRotation = Quaternion.identity; model.SetActive(true); } } catch { model = null; }
        if (!model) foreach (float sx in new[] { -0.9f, 0.9f }) { var b = Prim(PrimitiveType.Cylinder, blastGun, new Vector3(sx, 0f, 2.5f), new Vector3(0.5f, 2.6f, 0.5f), post); b.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); Prim(PrimitiveType.Sphere, blastGun, new Vector3(sx, 0f, 5.1f), Vector3.one * 0.7f, Glow(new Color(3f, 0.5f, 0.3f))); }
        foreach (float sx in new[] { -1.9f, 1.9f }) Prim(PrimitiveType.Cylinder, s, new Vector3(sx, 2.7f, 0f), new Vector3(0.3f, 2.7f, 0.3f), gold);
        var red = Glow(new Color(4f, 0.5f, 0.25f));
        blastBeamA = Line("Blaster beam", red, 0.7f, root); blastBeamB = Line("Blaster beam", red, 0.7f, root); blastBeamA.enabled = false; blastBeamB.enabled = false;
        var sign = Label(s, "<b>ASTEROID BLASTER</b>\n<size=55%>Take the gunner's seat and burn down the asteroids before they pass. Hold the trigger: two giant red lasers.</size>", 0f, 0f, 5f, 46f, 5f, new Color(1f, 0.84f, 0.3f)); sign.transform.localPosition = new Vector3(0f, 10.5f, 0f); mapLabels.Add(sign);
        blastLabel = Label(s, "", 0f, 0f, 1.8f, 12f, 1.6f, Color.white); blastLabel.transform.localPosition = new Vector3(0f, 2.3f, 4.6f); mapLabels.Add(blastLabel);
        var ped = new GameObject("Blaster start").transform; ped.SetParent(s, false); ped.localPosition = new Vector3(1.5f, 0.6f, 0.9f);
        Prim(PrimitiveType.Cylinder, ped, new Vector3(0f, 0.45f, 0f), new Vector3(0.24f, 0.45f, 0.24f), post, true);
        FacButton(ped, new Vector3(0f, 0.98f, 0f), new Vector3(0.46f, 0.14f, 0.46f), Glow(new Color(3f, 0.7f, 0.5f)), 1670);
        rockCold = Lit(new Color(0.4f, 0.38f, 0.36f)); rockHot = Glow(new Color(3f, 0.9f, 0.3f));
        for (int k = 0; k < 9; k++)
        {
            var t = new GameObject("Sky asteroid").transform; t.SetParent(funRoot, false); var body = Prim(PrimitiveType.Sphere, t, Vector3.zero, new Vector3(1f, 0.82f, 0.9f), rockCold);
            Prim(PrimitiveType.Sphere, t, new Vector3(0.32f, 0.2f, 0.1f), Vector3.one * 0.5f, rockCold); Prim(PrimitiveType.Sphere, t, new Vector3(-0.28f, -0.18f, 0.2f), Vector3.one * 0.45f, rockCold);
            t.gameObject.SetActive(false); blastRocks.Add(new BlastRock { t = t, rend = body.GetComponent<Renderer>() });
        }
        blastBest = PlayerPrefs.GetInt("blast_best", 0);
    }
    string BlastText() { return blastOn ? "leave the gunner's seat (or press BACKSPACE)" : "take the gunner's seat (60 seconds)   best " + blastBest; }
    void BlastToggle()
    {
        if (!blastOk) return;
        if (blastOn) { BlastEnd(); return; }
        blastOn = true; blastLeft = 60f; blastNext = 0.5f; blastScore = 0; game.flyMode = false; TeleportTo(blastRoot.TransformPoint(new Vector3(0f, 0.65f, -0.2f)), blastRoot.eulerAngles.y);
        Note("Asteroid Blaster! Aim at the sky and hold " + (game.HeadsetActive ? "the trigger." : "the mouse button.") + " BACKSPACE gets you out."); Play(cWin, 0.5f);
    }
    void BlastEnd()
    {
        blastOn = false; blastBeamA.enabled = false; blastBeamB.enabled = false; foreach (var r in blastRocks) { r.live = false; r.t.gameObject.SetActive(false); }
        bool best = blastScore > blastBest; if (best) { blastBest = blastScore; PlayerPrefs.SetInt("blast_best", blastBest); }
        Note("Asteroid Blaster: " + blastScore + " asteroids" + (best ? "  -  a new best!" : "   (best " + blastBest + ")")); Play(cWin, 0.7f);
    }
    void UpdateBlaster(float dt)
    {
        if (!blastOk) return;
        if (blastLabel) blastLabel.text = blastOn ? "<b>" + blastScore + "</b>   <size=70%>" + Mathf.CeilToInt(blastLeft) + " s</size>" : "<size=70%>press the red button to play\nbest " + blastBest + "</size>";
        if (!blastOn) return;
        if (hunchOn || mapNow != 0 || !Playing || Flat(Feet() - blastAt).sqrMagnitude > 4.5f * 4.5f) { BlastEnd(); return; }
        blastLeft -= dt; if (blastLeft <= 0f) { BlastEnd(); return; }
        Ray aim = AimRay(); blastGun.rotation = Quaternion.Slerp(blastGun.rotation, Quaternion.LookRotation(aim.direction, Vector3.up), 1f - Mathf.Exp(-10f * dt));
        blastNext -= dt;
        if (blastNext <= 0f)
        {
            blastNext = Mathf.Lerp(2.2f, 1.0f, 1f - blastLeft / 60f);
            foreach (var r in blastRocks) if (!r.live)
            {
                r.live = true; r.r = R(5f, 12f); r.hp = r.r * 0.09f; r.t.localScale = Vector3.one * r.r * 2f; r.t.gameObject.SetActive(true); if (r.rend) r.rend.sharedMaterial = rockCold;
                float a = R(0f, 360f) * Mathf.Deg2Rad, el = R(22f, 60f) * Mathf.Deg2Rad, d = R(170f, 260f);
                r.t.position = blastAt + new Vector3(Mathf.Sin(a) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(a) * Mathf.Cos(el)) * d;
                Vector3 across = Vector3.Cross(Vector3.up, r.t.position - blastAt).normalized * (R(0f, 1f) > 0.5f ? 1f : -1f); r.vel = across * R(12f, 26f) + Vector3.down * R(0f, 3f); r.spin = new Vector3(R(-30f, 30f), R(-30f, 30f), R(-30f, 30f)); break;
            }
        }
        bool held = game.HeadsetActive ? HunchVr.RightTrigger > 0.55f : (Mouse.current != null && Mouse.current.leftButton.isPressed && Cursor.lockState == CursorLockMode.Locked);
        held = held || Time.time < probeHold;
        Vector3 end = aim.origin + aim.direction * 600f; BlastRock hit = null; float hd = float.MaxValue;
        foreach (var r in blastRocks)
        {
            if (!r.live) continue; r.t.position += r.vel * dt; r.t.Rotate(r.spin * dt, Space.Self);
            if ((r.t.position - blastAt).sqrMagnitude > 420f * 420f || r.t.position.y < blastAt.y + 8f) { r.live = false; r.t.gameObject.SetActive(false); continue; }
            Vector3 to = r.t.position - aim.origin; float along = Vector3.Dot(to, aim.direction); if (along < 0f) continue;
            float off = (to - aim.direction * along).magnitude; if (off < r.r + 2.5f && along < hd) { hd = along; hit = r; }
        }
        blastBeamA.enabled = held; blastBeamB.enabled = held;
        if (!held) return;
        if (hit != null)
        {
            end = aim.origin + aim.direction * hd; hit.hp -= dt; if (hit.rend) hit.rend.sharedMaterial = rockHot;
            if (hit.hp <= 0f) { hit.live = false; hit.t.gameObject.SetActive(false); blastScore++; Play(cBoom, 0.6f); game.Buzz(0.8f, 0.12f); try { Fx2("explosion big", "sparks explode yellow", hit.t.position, hit.r * 0.5f, hit.r * 0.6f, 2.5f); } catch { } }
        }
        Vector3 ma = blastGun.TransformPoint(new Vector3(-0.9f, 0f, 5.2f)), mb = blastGun.TransformPoint(new Vector3(0.9f, 0f, 5.2f));
        blastBeamA.SetPosition(0, ma); blastBeamA.SetPosition(1, end); blastBeamB.SetPosition(0, mb); blastBeamB.SetPosition(1, end);
        float w = 0.6f + 0.25f * Mathf.Sin(Time.time * 40f); blastBeamA.widthMultiplier = w; blastBeamB.widthMultiplier = w; game.Buzz(0.25f, 0.03f);
    }

    // one way out of every seat (rover, gunner's seat): BACKSPACE on the keyboard, both grips squeezed in the headset
    float seatHint;
    void UpdateSeatExit()
    {
        if (ride == null && !blastOn) { seatHint = 0f; return; }
        var kb = Keyboard.current; bool leave = kb != null && !game.menuOpen && kb.backspaceKey.wasPressedThisFrame;
        if (game.HeadsetActive && HunchVr.LeftGrip > 0.8f && HunchVr.RightGrip > 0.8f) leave = true;
        if (!leave) { if (Time.time > seatHint) { seatHint = Time.time + 14f; Note(game.HeadsetActive ? "To get out: squeeze both grips." : "To get out: press BACKSPACE."); } return; }
        if (ride != null) Dismount(null);
        if (blastOn)
        {
            BlastEnd(); Vector3 off = blastRoot.TransformPoint(new Vector3(0f, 0.2f, -7.5f)), g; if (GroundAt(off.x, off.z, out g)) off = g + Vector3.up * 0.1f;
            TeleportTo(off, blastRoot.eulerAngles.y);
        }
    }
    // ================================================================== a real jetpack on your back while you fly
    Transform jetRoot; readonly List<Transform> jetFlames = new List<Transform>(); Light jetLight;
    void UpdateJetpack()
    {
        if (!avBody) return;
        if (!jetRoot)
        {
            jetRoot = new GameObject("Jetpack").transform; jetRoot.SetParent(avBody, false);
            var silver = Lit(new Color(0.8f, 0.82f, 0.86f)); var gold = Lit(new Color(0.83f, 0.62f, 0.2f)); var dark = Lit(new Color(0.14f, 0.15f, 0.18f)); var fire = Glow(new Color(4f, 1.6f, 0.3f)); var core = Glow(new Color(1.2f, 2.6f, 4f));
            foreach (float sx in new[] { -0.2f, 0.2f })
            {
                Prim(PrimitiveType.Cylinder, jetRoot, new Vector3(sx, 1.08f, -0.36f), new Vector3(0.15f, 0.27f, 0.15f), silver);
                Prim(PrimitiveType.Sphere, jetRoot, new Vector3(sx, 1.35f, -0.36f), Vector3.one * 0.15f, gold);
                Prim(PrimitiveType.Cylinder, jetRoot, new Vector3(sx, 1.2f, -0.36f), new Vector3(0.158f, 0.02f, 0.158f), gold);
                Prim(PrimitiveType.Cylinder, jetRoot, new Vector3(sx, 0.79f, -0.36f), new Vector3(0.19f, 0.04f, 0.19f), dark);
                var fl = new GameObject("Flame").transform; fl.SetParent(jetRoot, false); fl.localPosition = new Vector3(sx, 0.76f, -0.36f);
                Prim(PrimitiveType.Sphere, fl, new Vector3(0f, -0.2f, 0f), new Vector3(0.15f, 0.42f, 0.15f), fire); Prim(PrimitiveType.Sphere, fl, new Vector3(0f, -0.11f, 0f), new Vector3(0.08f, 0.22f, 0.08f), core); jetFlames.Add(fl);
            }
            Prim(PrimitiveType.Cube, jetRoot, new Vector3(0f, 1.1f, -0.36f), new Vector3(0.26f, 0.1f, 0.06f), gold);
            var lg = new GameObject("Jet light"); lg.transform.SetParent(jetRoot, false); lg.transform.localPosition = new Vector3(0f, 0.5f, -0.4f); jetLight = lg.AddComponent<Light>(); jetLight.type = LightType.Point; jetLight.color = new Color(1f, 0.6f, 0.25f); jetLight.range = 5f; jetLight.intensity = 0f;
        }
        bool on = game.flyMode && Playing;
        if (jetRoot.gameObject.activeSelf != on) jetRoot.gameObject.SetActive(on);
        if (!on) return;
        float f = 0.85f + 0.3f * Mathf.Sin(Time.time * 37f) + 0.15f * Mathf.Sin(Time.time * 61f);
        foreach (var fl in jetFlames) fl.localScale = new Vector3(1f, f, 1f); if (jetLight) jetLight.intensity = 1.6f * f;
    }
    // in the headset the big meeting button can be pressed three ways: touch it with a hand, point at it and pull a trigger, or the usual aim and trigger
    bool meetTrigWas; float meetPressCool;
    void UpdateMeetButtonVr()
    {
        if (!meetTable || !game.HeadsetActive || !Playing || game.menuOpen) return;
        bool trig = HunchVr.RightTrigger > 0.55f || HunchVr.LeftTrigger > 0.55f, down = trig && !meetTrigWas; meetTrigWas = trig;
        if (Time.time < meetPressCool || mtState == 1 || (mtState == 2 && game.menuOpen)) return;
        Vector3 b = meetTable.position + Vector3.up * 0.97f; Ray ray; if (!HunchVr.TryAim(out ray)) return;
        bool touch = (ray.origin - b).sqrMagnitude < 0.75f * 0.75f, aimed = false;
        if (down) { Vector3 to = b - ray.origin; float along = Vector3.Dot(to, ray.direction); aimed = along > 0f && along < 12f && (to - ray.direction * along).magnitude < 0.7f; }
        if (down && !aimed && cam) { Vector3 to = b - cam.position; float along = Vector3.Dot(to, cam.forward); aimed = along > 0f && along < 6f && (to - cam.forward * along).magnitude < 0.6f; }      // or simply look at it and pull a trigger
        if (!touch && !aimed) return;
        game.Buzz(0.8f, 0.2f); MeetingHere(); meetPressCool = Time.time + 3f;
    }
    // a meeting never follows you: go anywhere else (teleport, portal, another map) and it is over. The crew finish it by themselves.
    void UpdateMeetLeave()
    {
        if (mtState != 2 || autoOn) return;
        Vector3 at = meetCentre && meetTable ? meetTable.position : mtSpot;
        if (mapNow == 0 && Flat(Feet() - at).sqrMagnitude < 9f * 9f) return;
        if (menu && game.menuOpen) menu.Close();
        MeetingCancel("You left, so the meeting ended. Nothing was decided.");
    }
    void UpdateFrontier21(float dt) { UpdateEndlessGround(); UpdateNinja(dt); if (funRoot) UpdateBlaster(dt); UpdateSeatExit(); UpdateJetpack(); UpdateMeetButtonVr(); UpdateMeetLeave(); UpdateAirlocks(dt); UpdatePerf(); UpdateOrbs(); }

    public string ProbeNinja() { tool = NinjaTool; NinjaThrow(AimRay()); int live = 0; foreach (var s in ninjaStars) if (s.live) live++; return "thrown " + ninjaThrown + " in flight " + live; }
    public string ProbeBlast() { if (!blastOk) return "no blaster " + funLog; BlastToggle(); if (desk) desk.SetLook(blastRoot.eulerAngles.y, -35f); return "blaster on " + blastOn + " at " + blastAt.ToString("F0"); }
    public string ProbeBlastInfo() { int live = 0; foreach (var r in blastRocks) if (r.live) live++; return "blaster on " + blastOn + " score " + blastScore + " rocks " + live + " left " + blastLeft.ToString("F0"); }
    public string ProbeBlastAim() { foreach (var r in blastRocks) if (r.live) { Vector3 d = (r.t.position + r.vel * 0.4f - cam.position).normalized; if (desk) desk.SetLook(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, -Mathf.Asin(d.y) * Mathf.Rad2Deg); return "aiming at a rock " + (r.t.position - cam.position).magnitude.ToString("F0") + " m away"; } return "no rock"; }
    public string ProbeEdge() { game.flyMode = false; Vector3 g; float x = probeX, z = probeZ; EnsureGround(x, z); float y = GroundAt(x, z, out g) ? g.y : TerrHeight(x, z); TeleportTo(new Vector3(x, y + 0.5f, z), 0f); return "at " + x + "," + z + " ground " + y.ToString("F1"); }

    // ================================================================== a finished floor for the centre dome
    /// The hall is big and its floor is bare. This lays a raised deck over all of it: pale tiles in two tones, gold rings and a gold centre.
    /// It is solid, so you, the crew, the furniture and the green arrows all stand on it.
    int hubFloorCells; float hubFloorGap, hubTread;
    void BuildHubFloor()
    {
        const float cell = 1f; float lift = 0.30f; const int half = 31; Physics.SyncTransforms();
        // the level of the hall floor: the usual height of the ground a few metres from the middle
        var ys = new List<float>(); RaycastHit fh;
        for (int k = 0; k < 12; k++) { Vector3 d = Quaternion.Euler(0f, k * 30f + 15f, 0f) * Vector3.forward * (5f + (k % 3) * 4f); if (Physics.Raycast(new Vector3(d.x, 2.2f, d.z), Vector3.down, out fh, 6f, ~0, QueryTriggerInteraction.Ignore) && fh.normal.y > 0.8f) ys.Add(fh.point.y); }
        if (ys.Count < 4) return; ys.Sort(); float floorY = ys[ys.Count / 2];
        // how high the bottom stair is: the deck is raised until one ordinary step is left below it (about a foot in this hall)
        float tread = 99f;
        for (int ix = -27; ix <= 27; ix += 1) for (int iz = -27; iz <= 27; iz += 1)
        {
            if (ix * ix + iz * iz > 27 * 27) continue;
            foreach (var h in Physics.RaycastAll(new Vector3(ix + 0.5f, floorY + 1.2f, iz + 0.5f), Vector3.down, 1.3f, ~0, QueryTriggerInteraction.Ignore))
            { float up = h.point.y - floorY; if (h.normal.y > 0.9f && up > 0.2f && up < 0.75f && up < tread && !h.collider.GetComponentInParent<HunchTarget>()) tread = up; }
        }
        if (tread < 90f) lift = Mathf.Clamp(tread - 0.17f, 0.22f, 0.42f); hubTread = tread < 90f ? tread : 0f;
        Func<float, float, float> H = (vx, vz) => floorY + Mathf.Lerp(lift, 0.07f, Mathf.InverseLerp(27f, 29.6f, Mathf.Sqrt(vx * vx + vz * vz)));      // a gentle ramp down to the doors at the rim
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var tris = new[] { new List<int>(), new List<int>(), new List<int>() };
        for (int ix = -half; ix < half; ix++) for (int iz = -half; iz < half; iz++)
        {
            float x = (ix + 0.5f) * cell, z = (iz + 0.5f) * cell, rr = Mathf.Sqrt(x * x + z * z); if (rr > 30.5f) continue;
            bool here = false;
            foreach (var h in Physics.RaycastAll(new Vector3(x, floorY + 2.4f, z), Vector3.down, 3.2f, ~0, QueryTriggerInteraction.Ignore)) if (h.normal.y > 0.8f && Mathf.Abs(h.point.y - floorY) < 0.2f) { here = true; break; }
            if (!here) continue;
            int kind = rr < 2.5f || Mathf.Abs(rr - 12f) < 0.5f || Mathf.Abs(rr - 22f) < 0.5f ? 2 : ((ix + iz) & 1);
            int a = v.Count; float e = cell * 0.5f;
            v.Add(new Vector3(x - e, H(x - e, z - e), z - e)); v.Add(new Vector3(x - e, H(x - e, z + e), z + e)); v.Add(new Vector3(x + e, H(x + e, z + e), z + e)); v.Add(new Vector3(x + e, H(x + e, z - e), z - e));
            uv.Add(new Vector2(0f, 0f)); uv.Add(new Vector2(0f, 1f)); uv.Add(new Vector2(1f, 1f)); uv.Add(new Vector2(1f, 0f));
            var t = tris[kind]; t.Add(a); t.Add(a + 1); t.Add(a + 2); t.Add(a); t.Add(a + 2); t.Add(a + 3); hubFloorCells++;
        }
        if (hubFloorCells == 0) return; hubFloorGap = lift;
        var go = new GameObject("Centre dome floor"); go.transform.SetParent(root, false); go.transform.position = Vector3.zero;
        var m = new Mesh { name = "Centre dome floor" }; m.SetVertices(v); m.SetUVs(0, uv); m.subMeshCount = 3; for (int k = 0; k < 3; k++) m.SetTriangles(tris[k], k); m.RecalculateNormals(); m.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh = m;
        var m0 = new Material(Lit(new Color(0.80f, 0.83f, 0.88f))); var m1 = new Material(Lit(new Color(0.62f, 0.68f, 0.78f))); var m2 = new Material(Lit(new Color(0.86f, 0.66f, 0.22f)));
        foreach (var mt in new[] { m0, m1, m2 }) if (mt.HasProperty("_Smoothness")) mt.SetFloat("_Smoothness", 0.45f); if (m2.HasProperty("_Metallic")) m2.SetFloat("_Metallic", 0.8f);
        var r = go.AddComponent<MeshRenderer>(); r.sharedMaterials = new[] { m0, m1, m2 }; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        go.AddComponent<MeshCollider>().sharedMesh = m; Physics.SyncTransforms();
    }
    public string ProbeHubFloor() { return "centre dome floor: " + hubFloorCells + " square metres laid, raised " + hubFloorGap.ToString("F2") + " m, bottom stair was " + hubTread.ToString("F2") + " m up, feet " + Feet().ToString("F2"); }
    public string ProbeCheats() { HunchCheats.Hunch = hunchOn; HunchCheats.Creative = Creative; return HunchCheats.ModeName + ": walls " + HunchCheats.Walls + " fly " + HunchCheats.FlyOk + " skip " + HunchCheats.SkipOk + " teleport " + HunchCheats.TeleOk + " | hunch " + hunchOn; }
    public string ProbeAskCheats() { HunchSet(true); HunchAskCheats(); return ProbeCheats(); }
    public string ProbeBtn() { if (!meetTable) return "no table"; sim.phase = HabPhase.Mission; game.flyMode = false; Vector3 at = meetTable.position + new Vector3(0f, 0f, -3.4f); RaycastHit fh; if (Physics.Raycast(at + Vector3.up * 2f, Vector3.down, out fh, 4f, ~0, QueryTriggerInteraction.Ignore)) at.y = fh.point.y; TeleportTo(at + Vector3.up * 0.08f, 0f); Vector3 d = (meetTable.position + Vector3.up * 1.0f) - (at + Vector3.up * 1.5f); if (desk) desk.SetLook(0f, Mathf.Atan2(-d.y, d.z) * Mathf.Rad2Deg); return "standing at " + at.ToString("F1") + " table " + meetTable.position.ToString("F2") + " state " + mtState; }
    public string ProbeBtnState() { return "focus " + (focus ? focus.kind + "/" + focus.index : "none") + " '" + focusText + "' meeting state " + mtState + " centre " + meetCentre + " menu " + game.menuOpen + " feet " + Feet().ToString("F1"); }
}
