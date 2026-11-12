// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Smart Adaptive Habitat (realistic)
//  File    : Unity3/Scripts/04_Habitat/Dressing.cs
//  Section : 04_Habitat
//  Purpose : FUNCTION-FIRST HABITAT DRESSING
//  Why     : Review: "large flat white walls ... every wall should communicate a function". Real habitats
//            (ISS, Gateway, Artemis surface concepts) show their systems: access panels, vents, cable runs,
//            stowage, fire safety, water, air-quality sensing. Each item here has a job and sits in the room
//            that uses it, mounted flush on the wall (never on a walkway, never floating).
//  Contains: HabitatDressing (wardroom table refit + per-room wall equipment + stowage)
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Hunch3
{
    public class HabitatDressing : MonoBehaviour
    {
        enum Item { AccessPanel, Vent, CableTray, Locker, FireStation, WaterStation, AirMonitor, SensorNode, ToolBoard, CTBStack, RepairKit }

        /// room → what that room's job needs on its walls / floor
        static readonly (string wp, Item[] items)[] Rooms =
        {
            ("WP_EclA_In", new[] { Item.AccessPanel, Item.Vent, Item.CableTray, Item.AirMonitor, Item.FireStation, Item.AccessPanel }),
            ("WP_EclW_In", new[] { Item.AccessPanel, Item.CableTray, Item.WaterStation, Item.Vent }),
            ("WP_E_C",     new[] { Item.ToolBoard, Item.RepairKit, Item.CTBStack, Item.CableTray, Item.Locker, Item.Vent }),
            ("WP_H_C",     new[] { Item.Locker, Item.AirMonitor, Item.SensorNode, Item.WaterStation, Item.Vent }),
            ("WP_N_C",     new[] { Item.SensorNode, Item.AirMonitor, Item.FireStation, Item.CableTray, Item.Vent }),
            ("WP_R_C",     new[] { Item.WaterStation, Item.AirMonitor, Item.CTBStack, Item.Vent, Item.SensorNode }),
            ("WP_K_C",     new[] { Item.Locker, Item.Locker, Item.FireStation, Item.SensorNode }),
            ("WP_Sto_In",  new[] { Item.CTBStack, Item.CTBStack, Item.Locker, Item.Locker, Item.RepairKit }),
            ("WP_Up_Table",new[] { Item.WaterStation, Item.Vent, Item.AirMonitor }),
            ("WP_Up_Galley",new[]{ Item.Vent, Item.FireStation }),
            ("WP_B_Lab",   new[] { Item.ToolBoard, Item.AirMonitor, Item.Locker, Item.CableTray }),
            ("WP_C_Mid",   new[] { Item.WaterStation, Item.AirMonitor, Item.SensorNode }),
            ("WP_D_Ex",    new[] { Item.Vent, Item.WaterStation, Item.SensorNode }),
            ("WP_G_C",     new[] { Item.Vent, Item.AccessPanel }),
            ("WP_CoreL_C", new[] { Item.AccessPanel, Item.CableTray, Item.FireStation }),
            ("WP_A_Suit",  new[] { Item.Locker, Item.FireStation, Item.RepairKit }),
            ("WP_Hyg_In",  new[] { Item.Vent, Item.WaterStation }),
        };

        readonly List<Vector3> placed = new List<Vector3>();
        readonly List<(TextMesh t, Vector3 p)> monitors = new List<(TextMesh, Vector3)>();
        Transform root; Font font; int made; float nextMon;
        readonly System.Text.StringBuilder log = new System.Text.StringBuilder();

        void Start() { Invoke(nameof(BuildAll), 2.5f); }      // after the habitat colliders and the wellness systems are in

        void BuildAll()
        {
            root = new GameObject("HabitatDressing").transform;
            try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            bool bf = Physics.queriesHitBackfaces; Physics.queriesHitBackfaces = true;
            try { RefitWardroomTable(); } catch (System.Exception e) { log.AppendLine("table: " + e.Message); }
            foreach (var r in Rooms)
            {
                if (!Nav.wp.TryGetValue(r.wp, out var w) || !w) continue;
                int k = 0; foreach (var it in r.items) if (Place(it, w.position, k++)) made++;
            }
            Physics.queriesHitBackfaces = bf;
            log.Insert(0, $"habitat dressing: {made} items\n"); HLog.I("Habitat", $"dressing placed {made} wall/stowage items");
            try { System.IO.File.WriteAllText(System.IO.Path.Combine(GameState.ReportsDir, "dressing.txt"), log.ToString()); } catch { }
        }

        void Update()
        {
            if (Time.time < nextMon || monitors.Count == 0) return; nextMon = Time.time + 2f;
            var env = HabitatEnvironment.I; if (!env) return;
            foreach (var (t, p) in monitors)
            {
                if (!t) continue; var z = env.NearestZone(p, 14f);
                float co2 = z != null ? z.co2 : 520f, tc = z != null ? z.tempC : 21.5f, rh = z != null ? z.humidity : 50f;
                string c = co2 > 2500 ? "#FF5A5A" : co2 > 1500 ? "#FFB74D" : "#7CFC9A";
                t.text = $"AIR QUALITY\n<color={c}>CO2 {co2:0} ppm</color>\nO2 {env.o2Percent:0.0}%  {tc:0.0}C\nRH {rh:0}%";
            }
        }

        // ------------------------------------------------------------------ wardroom table
        /// The wardroom table was a flat cyan disc with a wood rim. A real habitat table: graphite composite top,
        /// anodised edge, a recessed touch display showing the crew day, tablets and drink pouches at the seats.
        void RefitWardroomTable()
        {
            var top = Util.Find("W01_MeetingTable_Top"); if (!top) return;
            var inlay = Util.Find("W02_MeetingTable_Inlay_Display"); var ped = Util.Find("W03_MeetingTable_Pedestal"); var ring = Util.Find("W04_MeetingTable_FootRing");
            var tr = top.GetComponent<Renderer>(); if (!tr) return;
            tr.sharedMaterial = M("TableComposite", new Color(.19f, .2f, .22f), 0f, .45f);
            if (ped && ped.GetComponent<Renderer>()) ped.GetComponent<Renderer>().sharedMaterial = M("Aluminium_Brushed", new Color(.62f, .64f, .67f), .85f, .55f);
            if (ring && ring.GetComponent<Renderer>()) ring.GetComponent<Renderer>().sharedMaterial = M("Aluminium_Brushed", new Color(.62f, .64f, .67f), .85f, .55f);
            var tb = tr.bounds; float R = Mathf.Min(tb.extents.x, tb.extents.z); float y = tb.max.y;
            float ir = R * .45f;
            if (inlay && inlay.GetComponent<Renderer>()) { var ib = inlay.GetComponent<Renderer>().bounds; ir = Mathf.Min(ib.extents.x, ib.extents.z); y = Mathf.Max(y, ib.max.y); inlay.GetComponent<Renderer>().sharedMaterial = M("DarkGlass", new Color(.03f, .04f, .05f), 0f, .92f); }
            // edge trim: thin anodised ring on the rim
            var trim = Prim(PrimitiveType.Cylinder, "Table_EdgeTrim", root, new Vector3(tb.center.x, tb.max.y - .012f, tb.center.z), new Vector3(R * 2.01f, .012f, R * 2.01f), M("Aluminium_Anodised", new Color(.42f, .45f, .5f), .8f, .6f));
            // recessed round touch display: crew day timeline + status (texture drawn here, no assets needed)
            var scr = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(scr.GetComponent<Collider>()); scr.name = "Table_TouchDisplay"; scr.transform.SetParent(root, true);
            scr.transform.position = new Vector3(tb.center.x, y + .002f, tb.center.z); scr.transform.rotation = Quaternion.Euler(90, 0, 0); scr.transform.localScale = Vector3.one * ir * 1.9f;
            var sm = new Material(Resources.Load<Material>("Hunch3_UnlitTransparent") ?? new Material(Shader.Find("Universal Render Pipeline/Unlit"))); var tex = TableScreen(); sm.mainTexture = tex; if (sm.HasProperty("_BaseMap")) sm.SetTexture("_BaseMap", tex); sm.color = Color.white; if (sm.HasProperty("_BaseColor")) sm.SetColor("_BaseColor", Color.white);
            scr.GetComponent<Renderer>().sharedMaterial = sm; scr.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // at the seats: 2 tablets, 3 drink pouches, a rugged laptop at the head of the table
            float pr = (ir + R) * .5f + .04f;
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f + 15f; var dir = Quaternion.Euler(0, a, 0) * Vector3.forward; var p = new Vector3(tb.center.x, tb.max.y, tb.center.z) + dir * pr;
                var face = Quaternion.LookRotation(-dir);
                if (i == 0) Laptop(p, face); else if (i % 2 == 1) Tablet(p, face * Quaternion.Euler(0, 12, 0)); else Pouch(p + Quaternion.Euler(0, 90, 0) * dir * .08f);
            }
            log.AppendLine($"wardroom table refit (r {R:0.00} m, display r {ir:0.00} m)");
            _ = trim;
        }

        Texture2D TableScreen()
        {
            int n = 512; var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp }; var px = new Color32[n * n];
            Color32 bg = new Color32(8, 18, 32, 255), grid = new Color32(22, 48, 72, 255), cy = new Color32(70, 210, 255, 255), gr = new Color32(110, 250, 150, 255), am = new Color32(255, 190, 80, 255), wh = new Color32(200, 225, 240, 255);
            Color32[] crew = { cy, gr, am, new Color32(240, 140, 200, 255) };
            float c = n / 2f;
            for (int yy = 0; yy < n; yy++) for (int xx = 0; xx < n; xx++)
            {
                float dx = xx - c, dy = yy - c, r = Mathf.Sqrt(dx * dx + dy * dy) / c; Color32 col = new Color32(0, 0, 0, 0);
                if (r <= 1f)
                {
                    col = bg;
                    if ((xx % 32 == 0 || yy % 32 == 0) && r < .93f) col = grid;
                    float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg; if (ang < 0) ang += 360f;
                    if (r > .80f && r < .90f) { int q = (int)(ang / 90f); float f = ang % 90f; col = f > 4f && f < 86f ? crew[q] : bg; if (f > 4f && f < 86f && f > 20f + q * 12f) col = new Color32((byte)(crew[q].r / 3), (byte)(crew[q].g / 3), (byte)(crew[q].b / 3), 255); }   // crew readiness arcs
                    if (r > .94f) col = new Color32(40, 90, 120, 255);                                                                                       // bezel glow
                    // day timeline: 6 bars (sleep · ops · exercise · meals · science · free)
                    for (int b = 0; b < 6; b++) { int by = (int)(c - 90 + b * 30); int len = new[] { 150, 210, 90, 120, 180, 70 }[b]; if (yy >= by && yy < by + 16 && xx > c - 130 && xx < c - 130 + len) col = b == 2 ? gr : b == 3 ? am : cy; }
                    if (yy > c + 110 && yy < c + 118 && xx > c - 130 && xx < c + 130) col = wh;   // title rule
                    if (Mathf.Abs(dx) < 3 && dy > -110 && dy < 100 && (yy / 6) % 2 == 0) col = wh;  // "now" marker
                }
                px[yy * n + xx] = col;
            }
            t.SetPixels32(px); t.Apply(); return t;
        }

        void Tablet(Vector3 p, Quaternion r)
        {
            var g = new GameObject("Tablet").transform; g.SetParent(root, false); g.SetPositionAndRotation(p, r);
            Part(g, PrimitiveType.Cube, new Vector3(0, .005f, 0), new Vector3(.17f, .01f, .24f), M("RuggedCase", new Color(.13f, .14f, .15f), 0, .3f));
            Part(g, PrimitiveType.Cube, new Vector3(0, .0105f, 0), new Vector3(.15f, .001f, .2f), Emit("TabletScreen", new Color(.15f, .45f, .65f)));
        }
        void Laptop(Vector3 p, Quaternion r)
        {
            var g = new GameObject("Laptop_HPZBook").transform; g.SetParent(root, false); g.SetPositionAndRotation(p, r);
            var body = M("LaptopBody", new Color(.1f, .1f, .11f), .2f, .4f);
            Part(g, PrimitiveType.Cube, new Vector3(0, .01f, 0), new Vector3(.34f, .02f, .24f), body);
            Part(g, PrimitiveType.Cube, new Vector3(0, .021f, .02f), new Vector3(.3f, .002f, .12f), M("Keys", new Color(.05f, .05f, .05f), 0, .2f));
            var lid = new GameObject("Lid").transform; lid.SetParent(g, false); lid.localPosition = new Vector3(0, .02f, -.12f); lid.localRotation = Quaternion.Euler(-105, 0, 0);
            Part(lid, PrimitiveType.Cube, new Vector3(0, 0, .12f), new Vector3(.34f, .012f, .24f), body);
            Part(lid, PrimitiveType.Cube, new Vector3(0, -.007f, .12f), new Vector3(.31f, .001f, .2f), Emit("LaptopScreen", new Color(.2f, .5f, .7f)));
            // velcro + tether: everything on the Moon is tied down
            Part(g, PrimitiveType.Cube, new Vector3(.19f, .004f, 0), new Vector3(.03f, .008f, .1f), M("Velcro", new Color(.08f, .08f, .08f), 0, .1f));
        }
        void Pouch(Vector3 p)
        {
            var g = new GameObject("DrinkPouch").transform; g.SetParent(root, false); g.position = p; g.rotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
            Part(g, PrimitiveType.Cube, new Vector3(0, .055f, 0), new Vector3(.07f, .11f, .025f), M("PouchFoil", new Color(.75f, .76f, .78f), .7f, .6f));
            Part(g, PrimitiveType.Cube, new Vector3(0, .07f, -.0135f), new Vector3(.055f, .05f, .002f), M("PouchLabel", new Color(.9f, .55f, .2f), 0, .3f));
            Part(g, PrimitiveType.Cylinder, new Vector3(.02f, .125f, 0), new Vector3(.008f, .02f, .008f), M("Straw", new Color(.3f, .6f, .9f), 0, .5f));
        }

        // ------------------------------------------------------------------ wall + floor equipment
        bool Place(Item it, Vector3 c, int k)
        {
            bool floor = it == Item.CTBStack || it == Item.RepairKit || it == Item.Locker;
            float h = it switch { Item.CableTray => 2.05f, Item.Vent => 1.95f, Item.SensorNode => 1.75f, Item.AirMonitor => 1.5f, Item.FireStation => 1.2f, Item.WaterStation => 1.15f, Item.ToolBoard => 1.35f, Item.AccessPanel => .75f, _ => .9f };
            float floorY = c.y;
            if (Physics.Raycast(c + Vector3.up * .5f, Vector3.down, out var fh, 2.5f, ~0, QueryTriggerInteraction.Ignore)) floorY = fh.point.y;
            for (int tryN = 0; tryN < 16; tryN++)
            {
                float ang = (k * 67f + tryN * 23f) % 360f; var dir = Quaternion.Euler(0, ang, 0) * Vector3.forward;
                var o = new Vector3(c.x, floorY + Mathf.Min(h, 1.6f), c.z);
                if (!Physics.Raycast(o, dir, out var hit, 7f, ~0, QueryTriggerInteraction.Ignore) || hit.distance < .9f) continue;
                if (hit.collider.GetComponentInParent<SmartObject>() || hit.collider.GetComponentInParent<HabInteractable>() || hit.collider.GetComponentInParent<DoorPassage>()) continue;
                var n = hit.normal; if (Vector3.Dot(n, -dir) < 0) n = -n;                 // single-sided hull: face the room
                var nh = new Vector3(n.x, 0, n.z); if (nh.sqrMagnitude < .3f) continue; nh.Normalize();   // wall, not floor/ceiling
                var wallP = hit.point; wallP.y = floorY + h;
                // the wall must really be there at mounting height (curved domes lean in)
                if (!Physics.Raycast(wallP + nh * .6f, -nh, out var wh, 1.2f, ~0, QueryTriggerInteraction.Ignore)) continue;
                var p = wh.point; var wn = wh.normal; if (Vector3.Dot(wn, nh) < 0) wn = -wn;
                if (placed.Any(q => (q - p).sqrMagnitude < 1.3f * 1.3f)) continue;
                if (Physics.CheckBox(p + nh * .35f + Vector3.up * (floor ? .45f - h : 0), new Vector3(.3f, .3f, .2f), Quaternion.LookRotation(nh), ~0, QueryTriggerInteraction.Ignore)) continue;   // something already there
                var rot = Quaternion.LookRotation(-new Vector3(wn.x, 0, wn.z).normalized, Vector3.up);   // +Z into the wall, faces the room
                if (floor) p = new Vector3(p.x, floorY, p.z);
                var g = new GameObject(it.ToString()).transform; g.SetParent(root, false); g.SetPositionAndRotation(p, rot);
                Build(it, g);
                placed.Add(p); log.AppendLine($"{it} @ {c:F1} -> {p:F1}");
                return true;
            }
            return false;
        }

        void Build(Item it, Transform g)
        {
            Material hull = M("HullPanel", new Color(.82f, .83f, .85f), 0, .35f), dark = M("Graphite", new Color(.16f, .17f, .19f), .1f, .4f), alu = M("Aluminium_Brushed", new Color(.62f, .64f, .67f), .85f, .55f);
            switch (it)
            {
                case Item.AccessPanel:      // removable maintenance panel: recessed seam, quarter-turn fasteners, label
                    Part(g, PrimitiveType.Cube, new Vector3(0, 0, -.006f), new Vector3(.62f, .62f, .012f), M("PanelSeam", new Color(.3f, .32f, .35f), 0, .3f));
                    Part(g, PrimitiveType.Cube, new Vector3(0, 0, -.013f), new Vector3(.58f, .58f, .006f), hull);
                    foreach (var sx in new[] { -.25f, .25f }) foreach (var sy in new[] { -.25f, .25f }) Part(g, PrimitiveType.Cylinder, new Vector3(sx, sy, -.017f), new Vector3(.025f, .003f, .025f), alu, 90);
                    Label(g, "ACCESS · ECLSS LOOP B", new Vector3(0, .21f, -.018f), .009f, Color.black); break;
                case Item.Vent:             // air return grille: dark frame + slats (airflow is what keeps CO2 from pooling in 1/6 g)
                    Part(g, PrimitiveType.Cube, new Vector3(0, 0, -.01f), new Vector3(.5f, .26f, .02f), dark);
                    for (int i = 0; i < 6; i++) Part(g, PrimitiveType.Cube, new Vector3(0, -.1f + i * .04f, -.024f), new Vector3(.46f, .012f, .01f), alu);
                    break;
                case Item.CableTray:        // overhead cable run: tray + 3 colour-coded bundles (power, data, sensor)
                    Part(g, PrimitiveType.Cube, new Vector3(0, 0, -.06f), new Vector3(2.2f, .02f, .12f), alu);
                    Part(g, PrimitiveType.Cube, new Vector3(0, .04f, -.118f), new Vector3(2.2f, .08f, .006f), alu);
                    Color[] cc = { new Color(.85f, .2f, .2f), new Color(.2f, .45f, .9f), new Color(.95f, .8f, .2f) };
                    for (int i = 0; i < 3; i++) Part(g, PrimitiveType.Cylinder, new Vector3(0, .028f, -.03f - i * .032f), new Vector3(.024f, 1.1f, .024f), M("Cable" + i, cc[i], 0, .4f), 0, 90);
                    break;
                case Item.Locker:           // crew stowage locker (ISS-style CTB rack front)
                    Part(g, PrimitiveType.Cube, new Vector3(0, .9f, -.22f), new Vector3(.55f, 1.8f, .44f), hull).gameObject.AddComponent<BoxCollider>();
                    Part(g, PrimitiveType.Cube, new Vector3(0, .9f, -.442f), new Vector3(.003f, 1.7f, .004f), dark);
                    foreach (var y in new[] { .6f, 1.2f }) Part(g, PrimitiveType.Cube, new Vector3(0, y, -.442f), new Vector3(.5f, .004f, .004f), dark);
                    Part(g, PrimitiveType.Cube, new Vector3(.2f, 1f, -.452f), new Vector3(.025f, .18f, .02f), alu);
                    Part(g, PrimitiveType.Cube, new Vector3(-.18f, 1.62f, -.443f), new Vector3(.012f, .012f, .002f), Emit("LockerLED", new Color(.3f, 1f, .4f)));
                    Label(g, "STOWAGE  L-" + (placed.Count + 10), new Vector3(0, 1.72f, -.445f), .012f, Color.black); break;
                case Item.FireStation:      // emergency: portable fire extinguisher + PBA mask, red frame
                    Part(g, PrimitiveType.Cube, new Vector3(0, 0, -.09f), new Vector3(.42f, .62f, .18f), Emit("EmergencyRed", new Color(.75f, .08f, .08f), .25f));
                    Part(g, PrimitiveType.Cube, new Vector3(0, 0, -.181f), new Vector3(.36f, .56f, .004f), M("GLASS_Cabinet", new Color(.7f, .8f, .85f, .3f), 0, .95f));
                    Part(g, PrimitiveType.Cylinder, new Vector3(-.07f, -.05f, -.09f), new Vector3(.1f, .22f, .1f), M("ExtinguisherRed", new Color(.8f, .1f, .1f), .3f, .6f));
                    Part(g, PrimitiveType.Sphere, new Vector3(.1f, .1f, -.09f), new Vector3(.13f, .15f, .08f), M("PBAMask", new Color(.9f, .75f, .2f), 0, .4f));
                    Label(g, "FIRE · PBA", new Vector3(0, .27f, -.184f), .014f, Color.white); break;
                case Item.WaterStation:     // potable water dispenser (hot/ambient), drip tray, 98% recovered water
                    Part(g, PrimitiveType.Cube, new Vector3(0, 0, -.08f), new Vector3(.34f, .44f, .16f), hull);
                    Part(g, PrimitiveType.Cube, new Vector3(0, -.24f, -.12f), new Vector3(.3f, .02f, .2f), alu);
                    foreach (var sx in new[] { -.07f, .07f }) Part(g, PrimitiveType.Cylinder, new Vector3(sx, -.08f, -.17f), new Vector3(.025f, .03f, .025f), alu);
                    Part(g, PrimitiveType.Cube, new Vector3(-.07f, .12f, -.161f), new Vector3(.05f, .02f, .002f), Emit("HotLED", new Color(1f, .35f, .2f)));
                    Part(g, PrimitiveType.Cube, new Vector3(.07f, .12f, -.161f), new Vector3(.05f, .02f, .002f), Emit("AmbLED", new Color(.3f, .7f, 1f)));
                    Label(g, "POTABLE H2O", new Vector3(0, .19f, -.162f), .011f, new Color(.1f, .3f, .6f)); break;
                case Item.AirMonitor:       // live air-quality readout (reads the habitat's CO2 / O2 / temp / RH for this room)
                    Part(g, PrimitiveType.Cube, new Vector3(0, 0, -.025f), new Vector3(.26f, .2f, .05f), dark);
                    Part(g, PrimitiveType.Cube, new Vector3(0, 0, -.051f), new Vector3(.23f, .16f, .002f), M("ScreenBlack", new Color(.02f, .03f, .04f), 0, .9f));
                    var tm = Label(g, "AIR QUALITY", new Vector3(-.105f, .072f, -.053f), .0065f, Color.white, TextAnchor.UpperLeft); monitors.Add((tm, g.position)); break;
                case Item.SensorNode:       // AI sensing node: CO2 + motion + mic array; green ring = privacy-compliant mode
                    Part(g, PrimitiveType.Cylinder, new Vector3(0, 0, -.015f), new Vector3(.11f, .015f, .11f), hull, 90);
                    Part(g, PrimitiveType.Cylinder, new Vector3(0, 0, -.031f), new Vector3(.08f, .002f, .08f), Emit("NodeRing", new Color(.3f, 1f, .5f), .8f), 90);
                    Part(g, PrimitiveType.Cylinder, new Vector3(0, 0, -.033f), new Vector3(.06f, .002f, .06f), dark, 90); break;
                case Item.ToolBoard:        // tool station: pegboard + tethered tools + torque wrench
                    Part(g, PrimitiveType.Cube, new Vector3(0, 0, -.01f), new Vector3(1f, .7f, .02f), M("Pegboard", new Color(.3f, .33f, .36f), .2f, .3f));
                    for (int i = 0; i < 5; i++) Part(g, PrimitiveType.Cube, new Vector3(-.38f + i * .19f, -.02f, -.04f), new Vector3(.04f, .3f - (i % 2) * .08f, .025f), i == 2 ? M("TorqueRed", new Color(.75f, .15f, .12f), .3f, .5f) : alu);
                    Part(g, PrimitiveType.Cube, new Vector3(0, .26f, -.022f), new Vector3(.9f, .05f, .004f), M("ToolLabel", new Color(.95f, .75f, .15f), 0, .3f));
                    Label(g, "TOOLS · TETHER BEFORE USE", new Vector3(0, .27f, -.025f), .01f, Color.black); break;
                case Item.CTBStack:         // Cargo Transfer Bags (the white soft stowage bags used on the ISS)
                    var ctb = M("CTB_Nomex", new Color(.9f, .9f, .86f), 0, .15f);
                    for (int i = 0; i < 3; i++) { var bag = Part(g, PrimitiveType.Cube, new Vector3((i == 2 ? .05f : 0), .13f + i * .26f, -.3f), new Vector3(.5f, .25f, .42f), ctb); bag.gameObject.AddComponent<BoxCollider>(); Label(g, "CTB " + (i + 1) + "/3", new Vector3(0, .17f + i * .26f, -.512f), .01f, new Color(.2f, .3f, .5f)); }
                    break;
                case Item.RepairKit:        // IVA repair kit: orange hard case
                    var rk = Part(g, PrimitiveType.Cube, new Vector3(0, .11f, -.3f), new Vector3(.5f, .22f, .32f), M("RepairOrange", new Color(.95f, .45f, .1f), 0, .5f)); rk.gameObject.AddComponent<BoxCollider>();
                    Part(g, PrimitiveType.Cube, new Vector3(0, .225f, -.3f), new Vector3(.2f, .03f, .04f), dark);
                    Label(g, "IVA REPAIR KIT", new Vector3(0, .15f, -.462f), .011f, Color.black); break;
            }
        }

        // ------------------------------------------------------------------ helpers
        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        static Material M(string n, Color c, float metal, float smooth)
        {
            if (mats.TryGetValue(n, out var m) && m) return m;
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"); m = new Material(sh) { name = "DR_" + n }; m.color = c; if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal); if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (c.a < 1f && m.HasProperty("_Surface")) { m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0); m.SetFloat("_SrcBlend", 5); m.SetFloat("_DstBlend", 10); m.SetFloat("_ZWrite", 0); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = 3000; }
            return mats[n] = m;
        }
        static Material Emit(string n, Color c, float k = 1.6f)
        {
            if (mats.TryGetValue("E_" + n, out var m) && m) return m;
            m = M("E_" + n, c * .6f, 0, .5f); m.EnableKeyword("_EMISSION"); if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c * k); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; return m;
        }
        static Transform Part(Transform parent, PrimitiveType t, Vector3 lp, Vector3 s, Material m, float rx = 0, float rz = 0)
        {
            var g = GameObject.CreatePrimitive(t); Object.Destroy(g.GetComponent<Collider>()); g.transform.SetParent(parent, false);
            g.transform.localPosition = lp; g.transform.localRotation = Quaternion.Euler(rx, 0, rz); g.transform.localScale = s;
            var r = g.GetComponent<Renderer>(); r.sharedMaterial = m; r.shadowCastingMode = s.magnitude < .3f ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On; return g.transform;
        }
        static Transform Prim(PrimitiveType t, string name, Transform parent, Vector3 pos, Vector3 s, Material m)
        {
            var g = GameObject.CreatePrimitive(t); Object.Destroy(g.GetComponent<Collider>()); g.name = name; g.transform.SetParent(parent, false); g.transform.position = pos; g.transform.localScale = s; g.GetComponent<Renderer>().sharedMaterial = m; return g.transform;
        }
        TextMesh Label(Transform parent, string s, Vector3 lp, float size, Color c, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var g = new GameObject("Label"); g.transform.SetParent(parent, false); g.transform.localPosition = lp; g.transform.localRotation = Quaternion.identity;   // +Z points into the wall: the room looks along +Z, so the text reads correctly
            var t = g.AddComponent<TextMesh>(); t.text = s; t.characterSize = size; t.fontSize = 64; t.anchor = anchor; t.alignment = anchor == TextAnchor.UpperLeft ? TextAlignment.Left : TextAlignment.Center; t.color = c; t.richText = true;
            if (font) { t.font = font; g.GetComponent<MeshRenderer>().sharedMaterial = font.material; }
            return t;
        }
    }
}
