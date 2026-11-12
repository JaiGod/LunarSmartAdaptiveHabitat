// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Smart Adaptive Habitat (realistic)
//  File    : Unity3/Scripts/04_Habitat/SmartObjects.cs
//  Section : 04_Habitat
//  Purpose : Every wellness / medical / life-support asset is a SYSTEM, not a prop. Look at one and it
//            highlights in its category colour with a holographic label; context keys run real actions
//            that change crew health and habitat telemetry; the card shows live readings + AI suggestions;
//            holding the right mouse button runs an engineering scan (integrity, last service, AI failure
//            prediction) and E while scanning performs maintenance. Equipment wears out over mission time.
//  Colours : green = life support · blue = psychological health · yellow = research/ops ·
//            purple = medical · red = emergency
//  Contains: SysCat, SmartAction, SmartObject, SmartInteractor, WellnessSpawner, SmartCatalog
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Hunch3
{
    public enum SysCat { LifeSupport, Psych, Research, Medical, Emergency }

    public class SmartAction
    {
        public Key key; public string label; public Func<SmartObject, string> run;
        public SmartAction(Key k, string l, Func<SmartObject, string> r) { key = k; label = l; run = r; }
    }

    /// One habitat system the crew can operate. Built by WellnessSpawner from a Resources/Wellness FBX.
    public class SmartObject : MonoBehaviour
    {
        public static readonly List<SmartObject> All = new List<SmartObject>();
        public static SmartObject Hover, VrHover; public int sel;   // VR: the option the stick-click picked
        public string key, title, why, state = "Nominal", mode = "";
        public SysCat cat; public float integrity = .9f, lastServiceHours, powerW = 40f; public int uses;
        public readonly List<SmartAction> actions = new List<SmartAction>();
        public Func<SmartObject, string[]> telemetry = _ => new string[0];
        public Func<SmartObject, string[]> advice = _ => new string[0];
        public Action<SmartObject> tick;
        public GameObject model; public Bounds localBounds; public Vector3 facing = Vector3.forward;
        GameObject highlight; TextMesh label; float lastHours = -1, nextLabel;

        public bool Failed => integrity < .35f;
        public float Eff => Failed ? .5f : 1f;                    // a worn-out system only gives half its benefit
        public static Color CatColor(SysCat c) => c == SysCat.LifeSupport ? new Color(.25f, 1f, .45f) : c == SysCat.Psych ? new Color(.35f, .62f, 1f) : c == SysCat.Research ? new Color(1f, .85f, .2f) : c == SysCat.Medical ? new Color(.78f, .45f, 1f) : new Color(1f, .3f, .3f);
        public static string CatName(SysCat c) => c == SysCat.LifeSupport ? "LIFE SUPPORT" : c == SysCat.Psych ? "PSYCHOLOGICAL HEALTH" : c == SysCat.Research ? "RESEARCH / OPERATIONS" : c == SysCat.Medical ? "MEDICAL" : "EMERGENCY";
        public static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

        public void Init()
        {
            All.Add(this);
            var bc = gameObject.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.center = localBounds.center; bc.size = localBounds.size + Vector3.one * .06f;
            AddSolidColliders(model);
            try
            {
                var xr = gameObject.AddComponent<XRSimpleInteractable>();
                xr.hoverEntered.AddListener(_ => VrHover = this); xr.hoverExited.AddListener(_ => { if (VrHover == this) VrHover = null; });
                xr.selectEntered.AddListener(_ => { HabitatOps.vrSel = true; if (Failed) { SmartInteractor.Report(this, Service(), Key.E); return; } if (actions.Count > 0) { var a = actions[Mathf.Clamp(sel, 0, actions.Count - 1)]; SmartInteractor.Report(this, a.run(this), a.key); } });
            } catch (Exception e) { HLog.W("Interaction", "XR on " + key + ": " + e.Message); }
            BuildHighlight();
            lastServiceHours = SimClock.hours - UnityEngine.Random.Range(24f, 24f * 20f);
        }
        public static void AddSolidColliders(GameObject g)
        {
            if (!g) return;
            foreach (var mf in g.GetComponentsInChildren<MeshFilter>())
                if (mf.sharedMesh && mf.sharedMesh.bounds.size.magnitude * mf.transform.lossyScale.magnitude > .5f && !mf.GetComponent<Collider>())
                    mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
        }
        void OnDestroy() { All.Remove(this); }

        void BuildHighlight()
        {
            highlight = new GameObject("Highlight"); highlight.transform.SetParent(transform, false);
            var sh = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            var m = new Material(sh); m.color = CatColor(cat);
            Vector3 mn = localBounds.min - Vector3.one * .03f, mx = localBounds.max + Vector3.one * .03f;
            Vector3[] c = { new Vector3(mn.x, mn.y, mn.z), new Vector3(mx.x, mn.y, mn.z), new Vector3(mx.x, mn.y, mx.z), new Vector3(mn.x, mn.y, mx.z) };
            Vector3 up = new Vector3(0, mx.y - mn.y, 0);
            void Line(params Vector3[] pts)
            {
                var g = new GameObject("edge"); g.transform.SetParent(highlight.transform, false); var lr = g.AddComponent<LineRenderer>();
                lr.useWorldSpace = false; lr.material = m; lr.startColor = lr.endColor = CatColor(cat); lr.widthMultiplier = .012f; lr.positionCount = pts.Length; lr.SetPositions(pts); lr.shadowCastingMode = ShadowCastingMode.Off;
            }
            Line(c[0], c[1], c[2], c[3], c[0]); Line(c[0] + up, c[1] + up, c[2] + up, c[3] + up, c[0] + up);
            for (int i = 0; i < 4; i++) Line(c[i], c[i] + up);
            var lg = new GameObject("HoloLabel"); lg.transform.SetParent(highlight.transform, false); lg.transform.localPosition = new Vector3(localBounds.center.x, mx.y + .12f, localBounds.center.z);
            label = lg.AddComponent<TextMesh>(); label.characterSize = .011f; label.fontSize = 60; label.anchor = TextAnchor.LowerCenter; label.alignment = TextAlignment.Left; label.richText = true;
            try { var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); if (f) { label.font = f; lg.GetComponent<MeshRenderer>().sharedMaterial = f.material; } } catch { }
            highlight.SetActive(false);
        }

        /// What the player should read at a glance: GREEN = use it, YELLOW = information, RED = emergency or broken.
        public static readonly Color Green = new Color(.3f, 1f, .5f), Yellow = new Color(1f, .82f, .25f), Red = new Color(1f, .3f, .3f);
        public bool InfoOnly => actions.Count > 0 && System.Text.RegularExpressions.Regex.IsMatch(actions[0].label, @"^(View|Read|Review|Check|Show|Inspect|See|Diagnos|Monitor)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        public Color PurposeColor => Failed || cat == SysCat.Emergency ? Red : InfoOnly ? Yellow : Green;
        public string Verb => Failed ? "Repair" : actions.Count > 0 ? actions[0].label.Split(' ')[0] : "Use";
        public void SetHighlight(bool on)
        {
            if (!highlight) return;
            if (on) { var pc = PurposeColor; foreach (var lr in highlight.GetComponentsInChildren<LineRenderer>(true)) { lr.startColor = lr.endColor = pc; lr.sharedMaterial.color = pc; } }
            if (highlight.activeSelf != on) { highlight.SetActive(on); nextLabel = 0; }
        }

        public string Status => Failed ? "<color=#ff5555>MAINTENANCE REQUIRED</color>" : state;

        void Update()
        {
            // wear: ~1.2 % per mission day (a system nobody services fails in ~6 weeks)
            if (lastHours < 0) lastHours = SimClock.hours;
            float dh = SimClock.hours - lastHours; lastHours = SimClock.hours;
            if (dh > 0 && dh < 48) integrity = Mathf.Max(.05f, integrity - dh * .0005f);
            tick?.Invoke(this);
            if (highlight && highlight.activeSelf)
            {
                var cam = PlayerRig.I ? PlayerRig.I.cam : Camera.main;
                if (cam) { label.transform.rotation = Quaternion.LookRotation(label.transform.position - cam.transform.position); }
                if (Time.time > nextLabel) { nextLabel = Time.time + .3f; label.text = PlayerRig.I && PlayerRig.I.isXR ? LabelText() : ""; }   // desktop uses the bottom card
            }
        }

        string LabelText()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append($"<color=#{Hex(CatColor(cat))}>[ {CatName(cat)} ]</color>\n<b>{title.ToUpper()}</b>\nStatus: {Status}\n");
            foreach (var l in telemetry(this).Take(3)) sb.Append(l).Append('\n');
            sb.Append($"Integrity {integrity * 100:0}%\n");
            sb.Append("<color=#9fdcff>");
            foreach (var a in actions) sb.Append($"[{a.key}] {a.label}   ");
            sb.Append("\n[TAB] Diagnostics   hold [RMB] Engineering scan</color>");
            return sb.ToString();
        }

        public string[] Diagnostics()
        {
            var l = new List<string> { $"<b>{title.ToUpper()}</b>  ·  {CatName(cat)}", why, "", "<b>LIVE TELEMETRY</b>" };
            l.AddRange(telemetry(this)); l.Add("");
            var adv = advice(this); if (adv.Length > 0) { l.Add("<b>AI RECOMMENDATIONS</b>"); l.AddRange(adv.Select(a => "+ " + a)); }
            l.Add(""); l.Add($"Used {uses}x this mission · mode: {(string.IsNullOrEmpty(mode) ? "standard" : mode)}");
            return l.ToArray();
        }
        public string[] Engineering()
        {
            float days = (SimClock.hours - lastServiceHours) / 24f, fail = Mathf.Pow(1f - integrity, 1.6f) * 70f;
            return new[] { $"<b>ENGINEERING INSPECTION · {title.ToUpper()}</b>", $"Integrity: {integrity * 100:0}%", $"Last maintenance: {days:0} days ago", $"AI failure prediction (30 d): {fail:0}%", $"Power draw: {powerW:0} W", Failed ? "<color=#ff6666>Output halved until serviced.</color>" : "Within limits.", "", "<b>[E] perform maintenance</b> while scanning (≈10 min of crew time)" };
        }
        public string Service()
        {
            integrity = 1f; lastServiceHours = SimClock.hours; var me = SmartCatalog.Me; if (me != null) { me.fatigue += .02f; me.autonomy += .02f; me.Clamp01(); }
            return $"{title}: serviced — integrity 100%. Maintenance logged to the habitat dashboard.";
        }
    }

    /// Desktop look-at selection, context keys, bottom card and hold-RMB engineering scan.
    public class SmartInteractor : MonoBehaviour
    {
        // ONE interaction card, bottom-centre, above the key bar: what you're looking at, what it's for, and exactly which key
        // does what. No floating world text on desktop (it overlapped the HUD panels) — the outline shows the target.
        public static bool CardVisible; bool stickPrev; bool diag; public static bool debugDiag; string last = ""; float lastUntil; SmartObject lastObj; bool helpShown;
        Image card, stripe; Text cardText; RectTransform cardRect;
        public static void Report(SmartObject so, string msg, Key key = Key.None)
        {
            HabitatOps.I?.OnUse(so, key);
            if (string.IsNullOrEmpty(msg)) return; so.uses++;
            var si = FindFirstObjectByType<SmartInteractor>(); if (si) { si.last = msg; si.lastUntil = Time.time + 7f; si.lastObj = so; } else HUD.I?.Toast(msg);
        }
        bool Scanning => false;   // right mouse is mouse-look in this game, so repair is simply E on a broken system

        void BuildCard()
        {
            var hud = HUD.I ? HUD.I.HudCanvas : null; if (!hud || card) return;
            var BC = new Vector2(0.5f, 0);
            card = UIKit.Panel(hud.transform, "InteractionCard", BC, new Vector2(-60, 58), new Vector2(800, 120), new Color(0.03f, 0.06f, 0.13f, 0.94f), BC);
            cardRect = card.rectTransform; card.raycastTarget = false;
            stripe = UIKit.Panel(card.transform, "CategoryStripe", new Vector2(0, 0.5f), Vector2.zero, new Vector2(8, 120), Color.cyan, new Vector2(0, 0.5f)); stripe.raycastTarget = false;
            var sr = stripe.rectTransform; sr.anchorMin = new Vector2(0, 0); sr.anchorMax = new Vector2(0, 1); sr.sizeDelta = new Vector2(8, 0);
            cardText = UIKit.Label(card.transform, "", 20, Color.white, new Vector2(0, 1), new Vector2(22, -10), new Vector2(764, 600), TextAnchor.UpperLeft, new Vector2(0, 1));
            card.gameObject.SetActive(false);
        }
        static string Pretty(string n)
        {
            var parts = n.Split('_').ToList(); if (parts.Count > 1 && parts[0].Any(char.IsDigit)) parts.RemoveAt(0);
            parts.RemoveAll(x => x == "0" || x == "1" || x == "Glow" || x == "Top" || x == "Body" || x == "Deck" || x == "Interior");
            return string.Join(" ", parts);
        }
        static string KeyTag(string k) => $"<b><color=#FFD54F>[{k}]</color></b>";
        static string FirstSentence(string s) { if (string.IsNullOrEmpty(s)) return ""; int i = s.IndexOf(". "); return i > 0 ? s.Substring(0, i + 1) : s; }

        void Update()
        {
            var pr = PlayerRig.I; SmartObject hit = null; HabInteractable hab = null;
            if (pr && pr.cam && !pr.isXR) { Aim.Resolve(); hit = Aim.Smart; hab = Aim.Hab; }      // shared aim: one target per frame
            else if (pr && pr.isXR)
            {
                hit = SmartObject.VrHover;      // the right-hand ray hovers it; trigger = do it, stick-click = next option
                var rh = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand);
                bool st = rh.isValid && rh.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxisClick, out bool c2) && c2;
                if (st && !stickPrev && hit && hit.actions.Count > 0) hit.sel = (hit.sel + 1) % hit.actions.Count;
                stickPrev = st;
            }
            if (SmartObject.Hover && SmartObject.Hover != hit) SmartObject.Hover.SetHighlight(false);
            SmartObject.Hover = hit;
            if (!card) BuildCard();
            UpdateCard(hit, hab, pr);
            if (!hit) { diag = false; return; }
            hit.SetHighlight(true);
            if (!helpShown) { helpShown = true; HUD.I?.Toast("Look at anything with an outline: the card at the bottom tells you what it is and which key to press."); }
            var kb = Keyboard.current; if (kb == null || Panels.AnyOpen) return;
            if (hit.Failed) { if (kb.eKey.wasPressedThisFrame) Report(hit, hit.Service(), Key.E); return; }   // broken: E repairs it
            foreach (var a in hit.actions)
                if (kb[a.key].wasPressedThisFrame) { Report(hit, a.run(hit), a.key); break; }
        }

        void UpdateCard(SmartObject so, HabInteractable hab, PlayerRig pr)
        {
            if (!card) return;
            bool recent = Time.time < lastUntil && lastObj && lastObj == so;
            if ((!so && !hab) || (HabitatOps.I && HabitatOps.I.WelcomeOpen)) { card.gameObject.SetActive(false); CardVisible = false; HUD.I?.LiftToast(0); HUD.I?.ShowPrompt(true); return; }
            bool vr = pr && pr.isXR;
            var sb = new System.Text.StringBuilder(); Color c = UIKit.Accent;
            if (so)
            {
                c = so.PurposeColor;
                sb.Append($"<size=24><color=#{SmartObject.Hex(c)}><b>[{so.Verb}]</b></color>  <b>{so.title}</b></size>   <size=16><color=#9FB3C8>{SmartObject.CatName(so.cat)}</color>  ·  {so.Status}  ·  condition {so.integrity * 100:0}%</size>\n");
                if (so.Failed)
                {
                    sb.Append("<size=20><color=#FF7070><b>BROKEN</b></color>  ").Append(KeyTag(vr ? "Trigger" : "E")).Append(" <b>Repair now</b></size>\n");
                    foreach (var l in so.Engineering().Skip(1).Take(3)) sb.Append("<size=16><color=#C9D6E8>").Append(l).Append("</color></size>\n");
                }
                else
                {
                    sb.Append("<size=17><color=#C9D6E8>").Append(FirstSentence(so.why)).Append("</color></size>\n");
                    sb.Append("<size=17><b>Live:</b> ").Append(string.Join("  ·  ", so.telemetry(so).Where(x => !string.IsNullOrEmpty(x)))).Append("</size>\n");
                    var adv = so.advice(so); if (adv.Length > 0) sb.Append("<size=17><b>AI suggests:</b> ").Append(string.Join("  ·  ", adv.Take(2))).Append("</size>\n");
                    sb.Append("<size=19>");
                    if (vr) { for (int k = 0; k < so.actions.Count; k++) sb.Append(k == so.sel ? "<b><color=#7CFFB2>> " + so.actions[k].label + "</color></b>" : "<color=#9FB3C8>" + so.actions[k].label + "</color>").Append("     "); sb.Append("\n").Append(KeyTag("Trigger")).Append(" do it     ").Append(KeyTag("Stick click")).Append(" next option"); }
                    else foreach (var a in so.actions) sb.Append(KeyTag(a.key.ToString())).Append(' ').Append(a.label).Append("     ");
                    sb.Append("</size>");
                }
                if (recent) sb.Append("\n<size=18><color=#7CFFB2>Done: ").Append(last).Append("</color></size>");
            }
            else
            {
                c = SmartObject.Green; var hv = string.IsNullOrEmpty(hab.Prompt) ? "Use" : hab.Prompt.Split(' ')[0];
                sb.Append($"<size=24><color=#{SmartObject.Hex(c)}><b>[{hv}]</b></color>  <b>{Pretty(hab.name)}</b></size>\n");
                sb.Append("<size=19>").Append(vr ? KeyTag("Trigger") : KeyTag("E") + " or " + KeyTag("click")).Append(' ').Append(hab.Prompt).Append("</size>");
            }
            var other = HUD.I ? HUD.I.PromptText : "";      // e.g. "Okafor is here. press I ..." from the missions — shown inside the card instead of on top of it
            if (!string.IsNullOrEmpty(other)) sb.Append("\n<size=16><color=#F48FB1>").Append(other).Append("</color></size>");
            HUD.I?.ShowPrompt(false);
            card.gameObject.SetActive(true); CardVisible = true; stripe.color = c;
            float baseY = (HUD.I ? HUD.I.KeysBarTop : 52) + 8; cardRect.anchoredPosition = new Vector2(vr ? 0 : -60, baseY);
            cardText.text = sb.ToString();
            float h = Mathf.Clamp(cardText.preferredHeight + 22, 70, 260); cardRect.sizeDelta = new Vector2(800, h);
            HUD.I?.LiftToast(baseY + h + 12);      // the toast sits above the card, never under it
        }
    }

    /// Builds every wellness / medical / life-support system from Resources/Wellness and finds a clear,
    /// reachable spot for it in the right zone (never on a walkway, never through a wall).
    public class WellnessSpawner : MonoBehaviour
    {
        public struct Spec { public string key, anchor; public bool wall; public float plinth, scale; public Spec(string k, string a, bool w = false, float p = 0, float s = 1) { key = k; anchor = a; wall = w; plinth = p; scale = s; } }
        /// New hardware only where the habitat has no equivalent yet; it goes in the zone that uses it, on a wall when it is a wall unit.
        /// (The habitat already has a greenhouse, ECLSS racks, a medical bay, VR retreat, lounge and beds: those are WRAPPED below
        /// as smart systems instead of adding duplicates — every cubic metre on the Moon is expensive.)
        public static readonly Spec[] Realistic =
        {
            // audit (Docs/Audit): heart-rate + BP monitors folded into the health station; wall mood tracker, AI wristband,
            // extra drone and extra console removed (each duplicated something that already answers the same proposal line)
            new Spec("oxygen_tanks", "WP_Sto_In", true),
            new Spec("ai_health_hub", "WP_N_C"), new Spec("hologram_companion", "WP_Up_Lounge"),
            new Spec("emergency_mh_station", "WP_R_C|WP_G_C", true), new Spec("circadian_light", "WP_SQ1_Bed", false, .55f, 1f),
            // Crew Health Rounds (side mission): the six D1 health devices
            new Spec("health_station", "WP_H_W|WP_H_NE"), new Spec("zone_sensor_pod", "WP_K_C|WP_R_C|WP_N_C", true, 0, 1.6f),
            new Spec("crew_beacon", "WP_H_NE|WP_H_W", false, .95f, 3f), new Spec("cortisol_patch", "WP_H_W|WP_H_NE", false, .95f, 6f),
            new Spec("sleep_wristband", "WP_SQ1_Health|WP_SQ1_Bed", false, .95f, 5f), // sleep bunk: the SQ1 sleep pod (smart_bed) does this job

        };
        /// existing habitat equipment turned into smart systems: renderer-name prefix → catalog entry (+ optional title)
        public static readonly (string prefix, string key, string title)[] Wraps =
        {
            ("LE00_Rack_CO2_Removal", "eclss_core", "CO2 Removal (CDRA)"), ("LE01_Rack_O2_Generation", "eclss_core", "Oxygen Generation (OGA)"),
            ("LE02_Rack_TraceContaminant", "eclss_core", "Trace Contaminant Control"), ("LE03_Rack_Sabatier", "eclss_core", "Sabatier Reactor"),
            ("C40_GrowRack_N", "greenhouse", "Greenhouse Grow Rack N"), ("C40_GrowRack_S", "greenhouse", "Greenhouse Grow Rack S"),
            ("H40_Medical_ExamCouch", "medical_scanner", "Medical Exam Couch & Scanner"), ("H45_CMO_Tier3_Alert", "ai_health_hub", "CMO Tier-3 Alert Console"),
            ("N31_Display_AI_PsychMonitoring", "ai_health_hub", "AI Psych-Monitoring Dashboard"), ("D63_VR_Headset_Dock", "sanctuary_pod", "VR Retreat"),
            ("D51_EarthView_Display_0", "earth_window", "Earth View Display"), ("D40_Treadmill", "exercise", "Treadmill"), ("D45_CycleErgometer", "exercise", "Cycle Ergometer"),
            ("D48_ResistiveDevice", "exercise", "Resistive Exercise Device"), ("SQ1_SleepPod_Mattress", "smart_bed", "Your Sleep Pod"),
            ("SQ1_HealthBot_DockTray", "health_robot", "AI Health Robot"), ("R52_FamilyCallBooth", "family_call", "Family Call Booth"), ("E38_RobotArm", "maintenance_drone", "Maintenance Robot Arm"),
            ("A71_Airlock_ControlPanel", "airlock", "Airlock Control"), ("W01_MeetingTable", "wardroom", "Wardroom Table"),
        };
        public Spec[] specs = Realistic; public int placed;
        public static readonly Dictionary<string, Material> matCache = new Dictionary<string, Material>();

        void Start() { Invoke(nameof(SpawnAll), 2.5f); }
        void SpawnAll()
        {
            var log = new List<string>();
            foreach (var s in specs)
            {
                try { log.Add(Spawn(s)); } catch (Exception e) { log.Add($"{s.key}: ERROR {e.Message}"); }
            }
            foreach (var w in Wraps) { try { log.Add(Wrap(w.prefix, w.key, w.title)); } catch (Exception e) { log.Add($"wrap {w.prefix}: ERROR {e.Message}"); } }
            HLog.I("Interaction", "smart systems placed " + placed + "/" + specs.Length + "\n" + string.Join("\n", log));
            try { System.IO.Directory.CreateDirectory("Reports"); System.IO.File.WriteAllLines("Reports/smart_objects.txt", log); } catch { }
        }

        string Wrap(string prefix, string key, string title)
        {
            var root = Util.Find("LunarHabitat_Unity3_Habitat"); if (!root) return "wrap: habitat root missing";
            var rs = root.GetComponentsInChildren<Renderer>().Where(r => r.name.StartsWith(prefix) && r.enabled).ToList();
            if (rs.Count == 0) return $"wrap {prefix}: not found";
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            var g = new GameObject("SMART_wrap_" + prefix.TrimEnd('_')); g.transform.position = new Vector3(b.center.x, b.min.y, b.center.z);
            var so = g.AddComponent<SmartObject>(); so.key = key; so.localBounds = new Bounds(new Vector3(0, b.extents.y, 0), b.size);
            SmartCatalog.Configure(so); if (!string.IsNullOrEmpty(title)) so.title = title;
            var hi = rs.Select(r => r.GetComponentInParent<HabInteractable>()).FirstOrDefault(h => h);
            if (hi)
            {   // E keeps doing exactly what this equipment always did; the smart layer adds diagnostics, scan, wear and the other keys
                so.actions.RemoveAll(a => a.key == Key.E);
                so.actions.Insert(0, new SmartAction(Key.E, hi.Prompt, _ => { hi.Activate(); return ""; }));
            }
            so.Init(); placed++;
            return $"wrap {prefix} → {key} ({title}) {rs.Count} parts, size {b.size:F2}{(hi ? ", keeps its E action" : "")}";
        }

        public static GameObject LoadModel(string key)
        {
            var pf = Resources.Load<GameObject>("Wellness/" + key); if (!pf) return null;
            var g = Instantiate(pf); g.name = key; FixMaterials(g); return g;
        }
        string Spawn(Spec s)
        {
            var model = LoadModel(s.key); if (!model) return $"{s.key}: model missing in Resources/Wellness";
            var anchors = s.anchor.Split('|').Select(n => Nav.wp.TryGetValue(n, out var t) && t ? t : Util.Find(n)).Where(t => t).ToList();
            if (anchors.Count == 0) { Destroy(model); return $"{s.key}: anchor {s.anchor} not found"; }
            var root = new GameObject("SMART_" + s.key);
            // front of the asset (Blender -Y) from the FRONT_MARKER empty, measured before anything rotates
            model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); model.transform.localScale *= s.scale;
            var fm = Util.FindDeep(model.transform, "FRONT_MARKER"); Vector3 front = fm ? fm.position - model.transform.position : Vector3.back; front.y = 0; front = front.sqrMagnitude > 1e-4f ? front.normalized : Vector3.back;
            var mb = WorldBounds(model);
            if (s.plinth > 0)
            {
                var p = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Destroy(p.GetComponent<Collider>()); p.name = "Display_Plinth"; p.transform.SetParent(root.transform, false);
                p.transform.localScale = new Vector3(.45f, s.plinth / 2f, .45f); p.transform.localPosition = new Vector3(0, s.plinth / 2f, 0); p.GetComponent<Renderer>().sharedMaterial = Lit("Plinth", new Color(.88f, .9f, .93f));
                model.transform.position = new Vector3(-mb.center.x, s.plinth - mb.min.y + .01f, -mb.center.z);
            }
            else model.transform.position = new Vector3(-mb.center.x, -mb.min.y + .01f, -mb.center.z);
            model.transform.SetParent(root.transform, true);
            var lb = WorldBounds(root);   // root still at origin, identity → world == local
            string how = ""; Transform anchor = null;
            foreach (var an in anchors) if (Place(root.transform, lb, front, an, s.wall, out how)) { anchor = an; break; }
            if (!anchor) { Destroy(root); return $"{s.key}: no clear spot near {s.anchor} (skipped)"; }
            var so = root.AddComponent<SmartObject>(); so.key = s.key; so.model = model; so.localBounds = lb; so.facing = root.transform.rotation * front;
            SmartCatalog.Configure(so); so.Init(); placed++;
            if (!s.key.StartsWith("recovery_pod")) StaticBatchingUtility.Combine(model);   // hundreds of small parts → a few draw calls
            return $"{s.key}: placed {how} near {anchor.name} at {root.transform.position:F2} size {lb.size:F2}";
        }

        static Bounds WorldBounds(GameObject g)
        {
            var rs = g.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) return new Bounds(g.transform.position, Vector3.one * .3f);
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b;
        }

        static bool Blocked(Vector3 center, Vector3 half, Quaternion rot, Transform self, Collider ignore = null)
        {
            foreach (var c in Physics.OverlapBox(center, half, rot, ~0, QueryTriggerInteraction.Ignore))
            {
                if (c == ignore) continue;
                if (c.GetComponentInParent<SmartObject>()) return true;
                if (self && c.transform.IsChildOf(self)) continue;
                if (PlayerRig.I && c.transform.IsChildOf(PlayerRig.I.transform)) continue;
                if (c.GetComponentInParent<CrewAgent>()) continue;
                return true;
            }
            return false;
        }

        static bool Place(Transform root, Bounds lb, Vector3 front, Transform anchor, bool wall, out string how)
        {
            // the habitat hull is single-sided mesh colliders: from inside, rays would pass straight through the walls
            bool bf = Physics.queriesHitBackfaces; Physics.queriesHitBackfaces = true;
            try { return PlaceInner(root, lb, front, anchor, wall, out how); } finally { Physics.queriesHitBackfaces = bf; }
        }
        static bool PlaceInner(Transform root, Bounds lb, Vector3 front, Transform anchor, bool wall, out string how)
        {
            how = "";
            Vector3 a = anchor.position; float floorY = a.y;   // waypoints sit on the deck
            Vector3 af = new Vector3(a.x, floorY, a.z);
            Vector3 half = lb.extents; float radius = Mathf.Max(half.x, half.z);
            int seed = Mathf.Abs(root.name.GetHashCode()) % 360;
            for (int pass = 0; pass < 2; pass++)
            {
                float clear = pass == 0 ? .7f : .3f;
                if (wall)
                {
                    for (int k = 0; k < 48; k++)
                    {
                        float ang = seed + k * 7.5f; var dir = Quaternion.Euler(0, ang, 0) * Vector3.forward;
                        foreach (float hgt in new[] { 1.3f, 1.0f })
                        {
                            if (!Physics.Raycast(af + Vector3.up * hgt, dir, out var wh, 5f, ~0, QueryTriggerInteraction.Ignore) || Mathf.Abs(wh.normal.y) > .3f) continue;
                            if (wh.collider.GetComponentInParent<SmartObject>() || wh.collider.GetComponentInParent<CrewAgent>()) continue;
                            var n = new Vector3(wh.normal.x, 0, wh.normal.z).normalized; if (Vector3.Dot(n, dir) > 0) n = -n;   // back-face hit → face the room
                            var rot = Quaternion.FromToRotation(front, n);
                            var foot = wh.point + n * (half.z + .04f); foot.y = floorY;
                            if (Try(root, lb, rot, foot, floorY, clear, out how, wh.collider)) { how = "on wall" + how; return true; }
                        }
                    }
                }
                else
                {
                    for (float r = Mathf.Max(1f, radius + .5f); r < 6f; r += .35f)
                        for (int k = 0; k < 18; k++)
                        {
                            var dir = Quaternion.Euler(0, seed + k * 20f, 0) * Vector3.forward; var p = af + dir * r;
                            if (Physics.Linecast(af + Vector3.up * 1.1f, p + Vector3.up * 1.1f, ~0, QueryTriggerInteraction.Ignore) || Physics.Linecast(af + Vector3.up * .5f, p + Vector3.up * .5f, ~0, QueryTriggerInteraction.Ignore)) continue;   // a wall or partition in between
                            var face = af - p; face.y = 0; var rot = Quaternion.FromToRotation(front, face.normalized);
                            if (Try(root, lb, rot, p, floorY, clear, out how)) { how = "on floor" + how; return true; }
                        }
                }
            }
            return false;
        }

        static bool Try(Transform root, Bounds lb, Quaternion rot, Vector3 foot, float floorY, float clear, out string how, Collider ignore = null)
        {
            how = "";
            if (!Physics.Raycast(foot + Vector3.up * 1.2f, Vector3.down, out var gh, 2.4f, ~0, QueryTriggerInteraction.Ignore) || Mathf.Abs(gh.point.y - floorY) > .15f) return false;
            Vector3 pos = new Vector3(foot.x, gh.point.y, foot.z) - rot * new Vector3(lb.center.x, 0, lb.center.z);
            Vector3 c = pos + rot * lb.center + Vector3.up * .04f, h = lb.extents + new Vector3(.04f, -.06f, .04f);
            if (Blocked(c, h, rot, root, ignore)) return false;
            // other smart systems (their small parts have no solid colliders): keep 25 cm between footprints
            var mine = new Bounds(c, Vector3.zero); foreach (var corner in new[] { new Vector3(1, 1, 1), new Vector3(-1, 1, 1), new Vector3(1, 1, -1), new Vector3(-1, 1, -1) }) mine.Encapsulate(c + rot * Vector3.Scale(h, corner));
            mine.Expand(new Vector3(.5f, 0, .5f));
            foreach (var o in SmartObject.All) { if (!o || o.transform == root) continue; var oc = o.transform.position + o.transform.rotation * o.localBounds.center; var ob = new Bounds(oc, Vector3.zero); foreach (var corner in new[] { new Vector3(1, 1, 1), new Vector3(-1, 1, 1), new Vector3(1, 1, -1), new Vector3(-1, 1, -1) }) ob.Encapsulate(oc + o.transform.rotation * Vector3.Scale(o.localBounds.extents, corner)); var a2 = new Vector2(mine.center.x - ob.center.x, mine.center.z - ob.center.z); if (Mathf.Abs(a2.x) < mine.extents.x + ob.extents.x && Mathf.Abs(a2.y) < mine.extents.z + ob.extents.z && Mathf.Abs(mine.center.y - ob.center.y) < 2f) return false; }
            float rad = Mathf.Max(lb.extents.x, lb.extents.z);
            foreach (var w in Nav.wp.Values)
            {
                if (!w || Mathf.Abs(w.position.y - gh.point.y) > 1.6f) continue;
                var d = w.position - c; d.y = 0; if (d.magnitude < rad + clear) return false;      // keep walkways & doorways clear
            }
            root.SetPositionAndRotation(pos, rot); how = clear < .5f ? " (tight)" : ""; return true;
        }

        // ---------------- materials: FBX colours → URP Lit; EMIT_* glow, GLASS_* transparent ----------------
        public static Material Lit(string name, Color col)
        {
            if (matCache.TryGetValue(name, out var m) && m) return m;
            var sh = Shader.Find("Universal Render Pipeline/Lit"); m = sh ? new Material(sh) : new Material(Shader.Find("Standard")); m.name = "SO_" + name;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col); m.color = col;
            string n = name.ToLower(); bool metal = n.Contains("alumin") || n.Contains("gold");
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal ? .9f : 0f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", metal ? .72f : n.Contains("screen") || n.Contains("glass") ? .9f : .4f);
            if (name.Contains("EMIT_"))
            {
                m.EnableKeyword("_EMISSION"); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", new Color(col.r, col.g, col.b) * 2.4f);
            }
            if (name.Contains("GLASS_"))
            {
                col.a = n.Contains("water") ? .45f : n.Contains("electro") ? .45f : .22f;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
                m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0); m.SetOverrideTag("RenderType", "Transparent");
                m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha); m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = (int)RenderQueue.Transparent;
            }
            matCache[name] = m; return m;
        }
        public static void FixMaterials(GameObject g)
        {
            foreach (var r in g.GetComponentsInChildren<Renderer>())
            {
                var ms = r.sharedMaterials;
                for (int i = 0; i < ms.Length; i++)
                {
                    if (!ms[i]) continue; string n = ms[i].name.Replace(" (Instance)", "");
                    Color c = ms[i].HasProperty("_BaseColor") ? ms[i].GetColor("_BaseColor") : ms[i].HasProperty("_Color") ? ms[i].color : Color.white;
                    ms[i] = Lit(n, c);
                }
                r.sharedMaterials = ms;
            }
        }
    }

    /// What each system is for, what it shows, and what each key really does to the crew & habitat.
    public static class SmartCatalog
    {
        public static CrewMember Me => Sim.I != null && Sim.I.engine != null ? Sim.I.engine.C(1) : null;
        static HabitatEnvironment Env => HabitatEnvironment.I;
        static float Co2 => Env && Env.zones.Count > 0 ? Env.zones.Average(z => z.co2) : 520f;
        static string P(float v) => $"{Mathf.Clamp01(v) * 100:0}%";
        static void Fx(float stress = 0, float mood = 0, float fatigue = 0, float sleep = 0, float novelty = 0, float autonomy = 0, float eff = 1)
        {
            var c = Me; if (c == null) return;
            c.stress += stress * eff; c.mood += mood * eff; c.fatigue += fatigue * eff; c.novelty += novelty * eff; c.autonomy += autonomy * eff;
            c.sleepQ = Mathf.Clamp01(c.sleepQ + sleep * eff); c.Clamp01();
        }
        static void Co2All(float d) { if (Env) foreach (var z in Env.zones) z.co2 = Mathf.Max(420f, z.co2 + d); }
        public static float filterDays = 14f, waterRecovery = 92f, o2Kg = 27.2f, harvestKg; static int earthView, lightMode, podMode;
        static readonly string[] PodModes = { "Circadian Reset", "Stress Recovery", "VR Relaxation", "Isolation Countermeasure", "Medical Recovery" };

        static void A(SmartObject so, Key k, string label, Func<SmartObject, string> run) => so.actions.Add(new SmartAction(k, label, run));

        public static void Configure(SmartObject so)
        {
            var me = Me;
            switch (so.key)
            {
                case "recovery_pod_healthy":
                    so.title = "Adaptive Recovery Pod"; so.cat = SysCat.Psych; so.powerW = 120;
                    so.why = "Sleep + recovery capsule. Sensor mat, EEG headrest and canopy sensors feed the AI; it adapts light, temperature, sound and the canopy tint, and shows Earth / a counsellor when stress is detected.";
                    so.telemetry = s => new[] { $"Sleep quality {P(Me?.sleepQ ?? .85f)}", $"Stress {P(Me?.stress ?? .2f)} · Fatigue {P(Me?.fatigue ?? .2f)}", $"Mode: {PodModes[podMode]}" };
                    so.advice = s => Advice();
                    A(so, Key.E, "Enter pod", s => { if (SleepSystem.I) { SleepSystem.I.BeginSleep(s.transform); return "Recovery pod: canopy closing, lights dimming to 1800 K. Sleep well."; } Fx(fatigue: -.25f, sleep: .12f, eff: s.Eff); return "Recovery pod: 20-minute recovery nap logged (fatigue down, sleep quality up)."; });
                    A(so, Key.F, "Sleep analytics", s => $"Sleep analytics: quality {P(Me?.sleepQ ?? .85f)} · deep 22% · REM 21% · HR 54 · 3 wake-ups. Trend: {(Me != null && Me.sleepQ < .7f ? "worse than your baseline — AI suggests a circadian reset" : "at your baseline")}.");
                    A(so, Key.R, "Run recovery session", s => { podMode = (podMode + 1) % PodModes.Length; s.mode = PodModes[podMode]; switch (podMode) { case 0: Fx(sleep: .08f, fatigue: -.05f, eff: s.Eff); break; case 1: Fx(stress: -.15f, eff: s.Eff); break; case 2: Fx(stress: -.08f, novelty: .1f, eff: s.Eff); break; case 3: Fx(mood: .08f, novelty: .06f, eff: s.Eff); break; default: Fx(fatigue: -.1f, eff: s.Eff); break; } return $"Recovery session: {PodModes[podMode]} running. Expected recovery +18 mental wellness."; });
                    so.tick = PodTick; break;
                case "medical_scanner":
                    so.title = "Smart Medical Scanner Bay"; so.cat = SysCat.Medical; so.powerW = 450;
                    so.why = "CMO station: non-contact mmWave + thermal + PPG scan arch, vitals monitor, AED, ultrasound, O2 and IV. Every scan is consent-gated and stays with the crew member unless they share it.";
                    so.telemetry = s => new[] { "HR 66 · SpO2 98% · BP 118/76", "Temp 37.0 °C · Resp 14", $"Last scan: {(s.uses > 0 ? "this session" : "none today")}" };
                    so.advice = s => new[] { "Weekly full scan keeps the personal baseline accurate", "Share with CMO only with consent (Crew panel)" };
                    A(so, Key.E, "Begin scan", s => { Fx(stress: -.02f); if (Me != null) Me.shareHealthWithCmo = Me.shareHealthWithCmo; return "Scan complete (your consent): HR 66, HRV 52 ms, SpO2 98%, BP 118/76, 37.0 °C, no anomalies. Saved to YOUR health record."; });
                    A(so, Key.F, "Crew health history", s => "Health history (yours): 14 scans · resting HR 64→62 · BP stable · sleep 7.3 h avg · no CMO alerts.");
                    break;
                case "heart_rate_monitor":
                    so.title = "Heart-Rate & Vitals Monitor"; so.cat = SysCat.Medical; so.powerW = 35;
                    so.why = "Chest-strap ECG + finger pulse-ox. Heart rate and HRV are core stress/anxiety signals — exercise is context-tagged so it is never mistaken for anxiety.";
                    so.telemetry = s => new[] { $"HR {Mathf.RoundToInt(60 + (Me?.stress ?? .2f) * 40)} bpm · HRV {Mathf.RoundToInt(70 - (Me?.stress ?? .2f) * 40)} ms", "SpO2 98% · Resp 14", Me != null && Me.exercising ? "Context: EXERCISE (not anxiety)" : "Context: rest" };
                    A(so, Key.E, "Measure now", s => $"Heart: {Mathf.RoundToInt(60 + (Me?.stress ?? .2f) * 40)} bpm, HRV {Mathf.RoundToInt(70 - (Me?.stress ?? .2f) * 40)} ms. {(Me != null && Me.stress > .5f ? "HRV is low — try the 6-breaths/min exercise at the sanctuary pod." : "Within your normal range.")}");
                    A(so, Key.F, "24 h trend", s => "24 h: resting HR 58–64, HRV dips after the 14:00 task block (fatigue pattern), recovers after exercise.");
                    break;
                case "bp_monitor":
                    so.title = "Blood-Pressure Tracker"; so.cat = SysCat.Medical; so.powerW = 15;
                    so.why = "Automatic cuff. Fluid shifts in 1/6 g and chronic stress both show up in blood pressure; readings auto-log to the CMO only with consent.";
                    so.telemetry = s => new[] { $"Last: {118 + Mathf.RoundToInt((Me?.stress ?? .2f) * 14)}/{76 + Mathf.RoundToInt((Me?.stress ?? .2f) * 8)} mmHg", "Pulse 68", "Trend: stable" };
                    A(so, Key.E, "Take reading", s => $"Blood pressure {118 + Mathf.RoundToInt((Me?.stress ?? .2f) * 14)}/{76 + Mathf.RoundToInt((Me?.stress ?? .2f) * 8)} mmHg, pulse 68. Logged to your record.");
                    break;
                case "smart_bed":
                    so.title = "AI Sleep-Detection Bed"; so.cat = SysCat.Medical; so.powerW = 25;
                    so.why = "Observation bed in the medical bay: under-mattress ballistocardiography tracks heart rate, breathing and movement with nothing worn; the headboard software shows the hypnogram.";
                    so.telemetry = s => new[] { $"Sleep score {Mathf.RoundToInt((Me?.sleepQ ?? .85f) * 100)}", "HR 54 · Resp 13 · 19.5 °C", "Stages: deep 22% · REM 21%" };
                    so.advice = s => Advice();
                    A(so, Key.E, "Rest (observed)", s => { Fx(fatigue: -.15f, sleep: .06f, eff: s.Eff); return "Resting in the observation bed: breathing and HR tracked contact-free. Fatigue down."; });
                    A(so, Key.F, "Sleep software", s => "Sleep software: 7 h 38 m · sleep score 86 · 3 awakenings · temperature held at 19.5 °C.");
                    break;
                case "ai_health_hub":
                    so.title = "AI Health Detection System"; so.cat = SysCat.Psych; so.powerW = 300;
                    so.why = "On-board edge computer: fuses 9 sensor types into scores for the 6 conditions. No cloud, encrypted, hard limits enforced in code, human review every 7 days.";
                    so.telemetry = s => new[] { $"Your wellbeing {P(Me?.Wellbeing ?? .8f)} · tier {Me?.MaxTier ?? 0}", "Sensor fusion 9/9 · confidence 0.94", "Hard-limit violations blocked: logged" };
                    A(so, Key.E, "Open AI dashboard", s => { Panels.Open("dashboard"); return ""; });
                    A(so, Key.F, "Explain my scores", s => $"Your scores: stress {P(Me?.stress ?? .2f)}, fatigue {P(Me?.fatigue ?? .2f)}, mood {P(Me?.mood ?? .75f)}. Only you see these unless you choose to share.");
                    break;
                case "habitat_console":
                    so.title = "Smart Habitat Control Console"; so.cat = SysCat.Research; so.powerW = 180;
                    so.why = "Command & comms: habitat health, power, radiation, crew wellness trends (aggregate only — never one person's data without consent).";
                    so.telemetry = s => new[] { $"O2 {(Env ? Env.o2Percent : 21f):0.0}% · CO2 {Co2:0} ppm", $"Power {(Env ? Env.powerKW : 52):0} kW · solar {(Env ? Env.solarKW : 12):0} kW", "Radiation 0.4 mSv/day" };
                    A(so, Key.E, "Habitat dashboard", s => { Panels.Open("dashboard"); return ""; });
                    A(so, Key.F, "Crew wellness trend", s => "Crew wellness (aggregate, 7 days): sleep 7.4 h · stress down 12% · no open alerts.");
                    break;
                case "sensor_wall":
                    so.title = "Biometrics Sensor Array"; so.cat = SysCat.LifeSupport; so.powerW = 60;
                    so.why = "Room sensors: camera (shared zones only), mic array, paired CO2, temperature, UWB location anchor, radiation dosimeter. If one fails the AI drops that signal and tells the crew.";
                    so.telemetry = s => new[] { $"CO2 {Co2:0} ppm · 21.5 °C", "18 / 18 sensors online", s.Failed ? "DRIFT DETECTED — recalibrate" : "Calibration OK" };
                    A(so, Key.E, "Sensor status", s => "Sensors: 18/18 online. Cameras exist only in shared zones — never in pods, bathrooms, VR retreat or call booth.");
                    A(so, Key.R, "Recalibrate", s => { s.integrity = Mathf.Max(s.integrity, .95f); return "Sensor array recalibrated against the reference CO2 cell."; });
                    break;
                case "eclss_core":
                    so.title = "ECLSS Core"; so.cat = SysCat.LifeSupport; so.powerW = 2200;
                    so.why = "Environmental Control & Life Support: CO2 scrubbers, oxygen generation, water recovery. High CO2 causes fatigue and headaches, so it feeds the fatigue signal.";
                    so.telemetry = s => new[] { $"O2 {(Env ? Env.o2Percent : 21f):0.0}% · CO2 {Co2:0} ppm", $"Water recovery {waterRecovery:0}%", $"AI: filter change in {filterDays:0} days" };
                    so.advice = s => Co2 > 800 ? new[] { "CO2 is high — replace the scrubber filter (R)" } : new string[0];
                    A(so, Key.E, "Inspect atmosphere", s => $"Atmosphere: O2 {(Env ? Env.o2Percent : 21f):0.0}%, CO2 {Co2:0} ppm, 70.3 kPa. CO2 removal {(s.Failed ? "DEGRADED" : "active")}.");
                    A(so, Key.R, "Replace scrubber filter", s => { filterDays = 90; Co2All(-350f * s.Eff); s.integrity = Mathf.Max(s.integrity, .9f); Fx(fatigue: -.05f); return "Scrubber filter replaced: CO2 dropping in every zone — the crew will feel sharper."; });
                    A(so, Key.F, "Water recovery", s => $"Water recovery {waterRecovery:0}% · urine → UPA → drinking water · tank {(Env ? Env.waterL : 2850):0} L.");
                    so.tick = s => { filterDays = Mathf.Max(0, filterDays - Time.deltaTime / 600f); if (s.Failed && Env) foreach (var z in Env.zones) z.co2 += Time.deltaTime * .5f; };
                    break;
                case "oxygen_tanks":
                    so.title = "Oxygen Tank Rack"; so.cat = SysCat.LifeSupport; so.powerW = 0;
                    so.why = "High-pressure O2 reserve: backup if oxygen generation fails, and medical oxygen for the CMO. Oxidizer — no flames, regulators checked weekly.";
                    so.telemetry = s => new[] { $"Reserve {o2Kg:0.0} kg (4 x 6.8 kg)", "Manifold 2200 psi", o2Kg < 12 ? "LOW — order resupply" : "Days of backup: 9" };
                    A(so, Key.E, "Check pressures", s => "O2 tanks: 2200 / 2150 / 2050 / 980 psi. Tank 4 below 50% — swap scheduled.");
                    A(so, Key.R, "Swap empty tank", s => { o2Kg = 27.2f; s.integrity = 1f; return "Tank 4 swapped and leak-checked. Reserve back to 27.2 kg."; });
                    break;
                case "greenhouse":
                    so.title = "Modular Lunar Greenhouse"; so.cat = SysCat.LifeSupport; so.powerW = 900;
                    so.why = "Hydroponic towers make food and oxygen; tending plants is behavioural activation — the top evidence-based countermeasure for low mood.";
                    so.telemetry = s => new[] { "Growth 94% · pH 6.0 · EC 1.8", "O2 production +2.1 kg/day", $"Harvested {harvestKg:0.0} kg" };
                    A(so, Key.E, "Enter care mode", s => { Fx(mood: .06f, stress: -.05f, novelty: .03f, eff: s.Eff); return "Care mode: you pruned and checked the towers. Mood up (behavioural activation)."; });
                    A(so, Key.F, "Inspect plant health", s => "Plant health: 96 healthy, 3 yellowing (nutrient EC low in tower 2), 0 mould.");
                    A(so, Key.G, "Harvest", s => { harvestKg += .4f; Fx(mood: .05f, eff: s.Eff); return "Harvested 0.4 kg of lettuce & herbs — fresh food is a morale boost."; });
                    break;
                case "emotional_garden":
                    so.title = "Emotional Support Garden"; so.cat = SysCat.Psych; so.powerW = 120;
                    so.why = "Flowers, soil smell and a bench: biophilic design that lowers stress and breaks monotony. The AI suggests it for low mood.";
                    so.telemetry = s => new[] { "16 blooms · soil 22 °C", "Water 1.2 L today", "Mood effect: +12 (crew avg)" };
                    A(so, Key.E, "Plant & tend", s => { Fx(mood: .06f, stress: -.04f, novelty: .05f, eff: s.Eff); return "You planted new seeds with the AI planting arm. Something to watch grow."; });
                    A(so, Key.G, "Sit on the bench", s => { Fx(stress: -.06f, eff: s.Eff); return "Five quiet minutes among the flowers. Stress down."; });
                    break;
                case "sanctuary_pod":
                    so.title = "Personal Sanctuary Pod"; so.cat = SysCat.Psych; so.powerW = 90;
                    so.why = "Noise-cancelling acoustic shell, recliner, VR headset dock and holo-plants. The breathing light guides 6 breaths per minute. No cameras or mics inside.";
                    so.telemetry = s => new[] { "Noise -38 dB", $"Calm {P(1 - (Me?.stress ?? .2f))}", "Occupied: no" };
                    A(so, Key.E, "Breathing session", s => { Fx(stress: -.12f, eff: s.Eff); return "Guided breathing (6 breaths/min, 5 min). Heart-rate variability up, stress down."; });
                    A(so, Key.R, "VR nature session", s => { Fx(stress: -.06f, novelty: .12f, mood: .04f, eff: s.Eff); return "VR: forest walk on Earth. Novelty up — fights sensory monotony."; });
                    break;
                case "earth_window":
                    so.title = "Virtual Earth Window"; so.cat = SysCat.Psych; so.powerW = 140;
                    so.why = "A live Earth view and nature scenes on a high-brightness panel — counters isolation and the grey monotony of the lunar surface.";
                    so.telemetry = s => new[] { $"View: {new[] { "Earth live", "Forest", "Ocean", "Home city" }[earthView]}", "1.3 s signal delay", "" };
                    A(so, Key.E, "Change view", s => { earthView = (earthView + 1) % 4; Fx(novelty: .05f, mood: .03f, eff: s.Eff); return $"Window view: {new[] { "Earth live", "Forest", "Ocean", "Home city" }[earthView]}."; });
                    break;
                case "hologram_companion":
                    so.title = "AI Companion Hologram"; so.cat = SysCat.Psych; so.powerW = 80;
                    so.why = "A virtual counsellor for check-ins and CBT-style coaching. It suggests, never orders; you can switch it off anytime; it never reports you without telling you first.";
                    so.telemetry = s => new[] { "Companion: available", $"Check-ins this week: {s.uses}", "Privacy: on-board only" };
                    A(so, Key.E, "Talk to companion", s => { Fx(stress: -.06f, mood: .04f, autonomy: .02f, eff: s.Eff); var c = Me; return c != null && c.stress > .5f ? "Companion: \"You've had a heavy few days. Want to try 5 minutes in the sanctuary pod, or call home?\"" : c != null && c.mood < .5f ? "Companion: \"How about tending the greenhouse with Okafor this afternoon?\"" : "Companion: \"You're doing well. Anything on your mind today?\""; });
                    A(so, Key.F, "Mood check-in", s => { if (Me != null) { Me.selfMood = Me.mood < .5f ? .6f : .2f; Me.selfStress = Me.stress; Me.selfAt = SimClock.hours; } return "Mood check-in saved (2 questions). Your answer counts more than any sensor for 24 h."; });
                    break;
                case "stress_lounge":
                    so.title = "Stress Recovery Lounge"; so.cat = SysCat.Psych; so.powerW = 110;
                    so.why = "Soft seating, warm 2700 K light, nature mode on the wall screen — the shared place to decompress together after hard shifts.";
                    so.telemetry = s => new[] { "Light 2700 K warm", "Nature mode: forest", $"Crew here: {CrewAgent.All.Count(a => a && Vector3.Distance(a.transform.position, s.transform.position) < 4f)}" };
                    A(so, Key.E, "Sit & unwind", s => { Fx(stress: -.08f, mood: .05f, eff: s.Eff); if (Me != null) Me.lastSocialHour = SimClock.hours; return "You sit back in the lounge. Stress down, and time near crewmates counts as social contact."; });
                    A(so, Key.F, "Nature mode", s => { Fx(novelty: .06f, eff: s.Eff); return "Lounge screen: rain forest soundscape and view."; });
                    break;
                case "emergency_mh_station":
                    so.title = "Emergency Mental Health Station"; so.cat = SysCat.Emergency; so.powerW = 20;
                    so.why = "24/7 private line to the flight surgeon / psychologist, breathing guide and comfort kit. Always works — even if the AI is switched off.";
                    so.telemetry = s => new[] { "Line to Earth: ready (1.3 s delay)", "Privacy curtain: available", "AI cannot listen in" };
                    A(so, Key.E, "TALK NOW", s => { Fx(stress: -.1f, mood: .04f); return "Private line open to the flight psychologist. Nothing is recorded by the habitat AI."; });
                    A(so, Key.R, "Breathing exercise", s => { Fx(stress: -.08f); return "Box breathing: in 4 s · hold 4 s · out 4 s · hold 4 s. Repeat 6 times."; });
                    A(so, Key.F, "Comfort kit", s => { Fx(stress: -.03f); return "Comfort kit: weighted blanket, noise-cancelling headset, family photos, stress ball."; });
                    break;
                case "mood_tracker":
                    so.title = "Mood Health Tracker"; so.cat = SysCat.Psych; so.powerW = 10;
                    so.why = "Voluntary 2-tap mood check-in. Self-report is weighted above sensors for 24 h, and the trend helps the AI learn your personal baseline.";
                    so.telemetry = s => new[] { $"Your mood {P(Me?.mood ?? .75f)}", "14-day trend: improving", "Visible to: only you" };
                    A(so, Key.E, "Check in: good", s => { if (Me != null) { Me.selfMood = .2f; Me.selfStress = .2f; Me.selfAt = SimClock.hours; } Fx(autonomy: .02f); return "Logged: feeling good. Thanks — your answer outranks the sensors for 24 h."; });
                    A(so, Key.R, "Check in: low", s => { if (Me != null) { Me.selfMood = .7f; Me.selfStress = .6f; Me.selfAt = SimClock.hours; } return "Logged: feeling low. The AI will OFFER (never force) support: greenhouse, call home or the sanctuary pod."; });
                    break;
                case "maintenance_drone":
                    so.title = "Autonomous Maintenance Drone"; so.cat = SysCat.Research; so.powerW = 250;
                    so.why = "Inspects seals, filters and wiring, brushes off lunar dust and predicts failures before they happen — so the crew spends less time on chores.";
                    so.telemetry = s => { var worst = SmartObject.All.Where(o => o != s).OrderBy(o => o.integrity).FirstOrDefault(); return new[] { "Battery 86%", worst ? $"Worst system: {worst.title} {worst.integrity * 100:0}%" : "All systems nominal", "Dust brushes: 72% life" }; };
                    A(so, Key.E, "Dispatch inspection", s => { var worst = SmartObject.All.Where(o => o != s).OrderBy(o => o.integrity).FirstOrDefault(); if (!worst) return "Drone: nothing to inspect."; worst.integrity = Mathf.Min(1f, worst.integrity + .3f * s.Eff); worst.lastServiceHours = SimClock.hours; return $"Drone inspected and cleaned the {worst.title}: integrity now {worst.integrity * 100:0}%."; });
                    A(so, Key.R, "Recharge", s => { s.integrity = Mathf.Max(s.integrity, .9f); return "Drone docked and charging."; });
                    break;
                case "ai_wristband":
                    so.title = "AI Monitoring Wristband (display, 5x scale)"; so.cat = SysCat.Psych; so.powerW = 0;
                    so.why = "Worn at night: PPG heart rate, skin conductance (stress), skin temperature and motion (sleep). The mood ring shows your state only to you unless you choose to share it.";
                    so.telemetry = s => new[] { $"HR {Mathf.RoundToInt(58 + (Me?.stress ?? .2f) * 30)} · stress {(Me != null && Me.stress > .5f ? "HIGH" : "LOW")}", $"Sleep {P(Me?.sleepQ ?? .85f)}", $"Mood ring: {(Me != null && Me.shareMoodPublic ? "shared" : "private")}" };
                    A(so, Key.E, "Sync wristband", s => "Wristband synced: last night HR 52–61, 2 stress spikes (both during the 14:00 task), sleep 7 h 20 m.");
                    A(so, Key.F, "Share mood ring", s => { if (Me != null) Me.shareMoodPublic = !Me.shareMoodPublic; return Me != null && Me.shareMoodPublic ? "Your mood ring is now visible to crewmates (you can switch it off any time)." : "Your mood ring is private again."; });
                    break;
                // ---------------- Crew Health Rounds: the six D1 health devices ----------------
                case "crew_beacon":
                    so.title = "Crew Beacon (chest, display 6x)"; so.cat = SysCat.Medical; so.powerW = 0;
                    so.why = "Worn on the chest: 2-lead ECG gives heart rate and HRV (stress), skin response, and a UWB tag that tells the AI which room you are in. HRV is the earliest stress signal the habitat has.";
                    so.telemetry = s => new[] { $"HR {Mathf.RoundToInt(60 + (Me?.stress ?? .2f) * 28)} bpm · HRV {Mathf.RoundToInt(62 - (Me?.stress ?? .2f) * 35)} ms", $"Skin response {(Me != null && Me.stress > .5f ? "raised" : "calm")}", "Location: tracked (UWB)" };
                    A(so, Key.E, "Read the beacon", s => $"Crew beacon: HR {Mathf.RoundToInt(60 + (Me?.stress ?? .2f) * 28)} bpm, HRV {Mathf.RoundToInt(62 - (Me?.stress ?? .2f) * 35)} ms. Low HRV = rising stress; the AI compares it with your own baseline, not anyone else's.");
                    break;
                case "sleep_wristband":
                    so.title = "Night Wristband (display 5x)"; so.cat = SysCat.Psych; so.powerW = 0;
                    so.why = "Worn in bed: PPG heart rate + accelerometer give sleep stages (light, deep, REM), wake-ups and a sleep score every morning.";
                    so.telemetry = s => new[] { $"Sleep score {Mathf.RoundToInt((Me?.sleepQ ?? .85f) * 100)}", "Deep 1.4 h · REM 1.7 h", "2 wake-ups" };
                    A(so, Key.E, "Download last night", s => { Fx(sleep: .02f); return $"Night wristband: sleep score {Mathf.RoundToInt((Me?.sleepQ ?? .85f) * 100)}. Deep sleep came early, REM late. {(Me != null && Me.sleepQ < .7f ? "Below your baseline: the AI suggests an early night and the 1800 K wind-down light." : "Right at your baseline.")}"; });
                    break;
                case "cortisol_patch":
                    so.title = "Cortisol Patch (display 6x)"; so.cat = SysCat.Medical; so.powerW = 0;
                    so.why = "Microneedle patch: measures the stress hormone cortisol 4x a day for 48 h. One high value means little; a flattened daily curve means chronic stress.";
                    so.telemetry = s => new[] { $"Cortisol {(Me != null && Me.stress > .5f ? "HIGH for the time of day" : "normal daily curve")}", "Patch: 31 h left", "Next sample 14:00" };
                    A(so, Key.E, "Fit a fresh patch", s => { Fx(stress: -.01f); return $"New cortisol patch fitted (48 h). Last curve: {(Me != null && Me.stress > .5f ? "flatter than normal — chronic stress flag sent to YOU first" : "normal morning peak, low at night")}."; });
                    break;
                case "zone_sensor_pod":
                    so.title = "Zone Sensor Pod"; so.cat = SysCat.Psych; so.powerW = 6;
                    so.why = "One per zone: paired CO2 sensors everywhere; voice tone and facial expression only in SHARED zones, processed on the pod. Raw audio and video never leave it.";
                    so.telemetry = s => new[] { $"CO2 {Co2:0} ppm", $"Voice/face mood: {(Me != null && Me.mood < .4f ? "low" : "steady")}", "Raw data: never stored" };
                    A(so, Key.E, "Mood check (consent)", s => { Fx(mood: .02f); return $"Zone pod: voice {(Me != null && Me.mood < .4f ? "flat and slow" : "bright")}, face {(Me != null && Me.stress > .5f ? "tense" : "relaxed")}. Only you see this unless you share it."; });
                    break;
                case "health_station":
                    so.title = "Crew Health Station"; so.cat = SysCat.Medical; so.powerW = 90;
                    so.why = "Daily vitals kiosk: live ECG, blood-pressure cuff, pulse-oximeter, thermometer. Medical numbers go to the flight surgeon; mood data does not.";
                    so.telemetry = s => new[] { $"BP {Mathf.RoundToInt(114 + (Me?.stress ?? .2f) * 18)}/{Mathf.RoundToInt(74 + (Me?.stress ?? .2f) * 10)}", "SpO2 98% · 36.7 °C", $"ECG: sinus rhythm" };
                    A(so, Key.E, "Daily check-in", s => { Fx(stress: -.02f); return $"Check-in saved: BP {Mathf.RoundToInt(114 + (Me?.stress ?? .2f) * 18)}/{Mathf.RoundToInt(74 + (Me?.stress ?? .2f) * 10)}, HR {Mathf.RoundToInt(60 + (Me?.stress ?? .2f) * 28)}, SpO2 98%, 36.7 °C, ECG normal sinus rhythm."; });
                    break;
                case "sleep_bunk":
                    so.title = "Smart Sleep Bunk"; so.cat = SysCat.Psych; so.powerW = 25;
                    so.why = "Under-mattress sensor (backup when the wristband is off), circadian mood light and a privacy curtain. Sleep is the #1 predictor of next-day mood on long missions.";
                    so.telemetry = s => new[] { $"Sleep quality {P(Me?.sleepQ ?? .85f)}", "Light: 1800 K after 21:00", "Curtain: private" };
                    A(so, Key.E, "Check the bunk sensor", s => $"Smart bunk: {(Me != null ? Me.sleepQ * 8.2f : 7.1f):0.0} h asleep, 3 position changes, light set to wind-down at 21:00.");
                    break;
                case "circadian_light":
                    so.title = "Adaptive Circadian Light"; so.cat = SysCat.Psych; so.powerW = 12;
                    so.why = "Bedside sunrise lamp: blue-rich light to wake, warm melatonin-safe light at night. Keeps the body clock on a 24 h day under a 29.5-day lunar day.";
                    so.telemetry = s => new[] { $"Mode: {new[] { "Sunrise 06:30", "Focus 6500 K", "Wind-down 1800 K" }[lightMode]}", "Melatonin-safe after 21:00", "" };
                    A(so, Key.E, "Change light mode", s => { lightMode = (lightMode + 1) % 3; if (lightMode == 2) Fx(sleep: .05f); if (lightMode == 1) Fx(fatigue: -.04f); return $"Circadian light: {new[] { "sunrise alarm set for 06:30", "focus light 6500 K", "wind-down 1800 K (melatonin-safe)" }[lightMode]}."; });
                    break;
                case "exercise":
                    so.title = "Exercise Station"; so.cat = SysCat.Medical; so.powerW = 150;
                    so.why = "2.5 h a day protects bone and muscle in 1/6 g, and exercise is one of the strongest countermeasures for low mood and stress. Heart-rate rises here are tagged so the AI never mistakes a workout for anxiety.";
                    so.telemetry = s => new[] { $"Today: {(Me != null && SimClock.hours - Me.lastExerciseHour < 24 ? "done" : "not yet")}", "Bone-loss risk: low with daily use", $"Fatigue {P(Me?.fatigue ?? .2f)}" };
                    A(so, Key.E, "Work out", s => { Fx(stress: -.1f, mood: .05f, fatigue: .05f, eff: s.Eff); if (Me != null) Me.lastExerciseHour = SimClock.hours; return "Workout logged. Stress down, mood up."; });
                    A(so, Key.F, "Training plan", s => "Plan: 30 min treadmill (harness 70% body weight), 45 min resistive, 15 min cycle. Rest day every 7th day."); break;
                case "health_robot":
                    so.title = "AI Health Robot"; so.cat = SysCat.Medical; so.powerW = 60;
                    so.why = "Morning check: BP, SpO2, temperature, heart rate. It only scans you after you say yes, and you see every number it sees.";
                    so.telemetry = s => new[] { "Docked · charged", "Daily check: 07:00", "Consent required each time" };
                    A(so, Key.E, "Request a check", s => "Health robot: please sit. (Use the robot directly to run the full consent-based check.)"); break;
                case "family_call":
                    so.title = "Family Call Booth"; so.cat = SysCat.Psych; so.powerW = 40;
                    so.why = "Private calls home are the strongest counter to isolation. Acoustic curtain, no AI audio or video recording, 1.3 s delay each way.";
                    so.telemetry = s => new[] { $"Last call home: {(Me != null ? (SimClock.hours - Me.lastSocialHour).ToString("0") + " h ago" : "-")}", "Delay 1.3 s each way", "Not recorded" };
                    A(so, Key.E, "Call home", s => { Fx(mood: .12f, stress: -.05f); if (Me != null) Me.lastSocialHour = SimClock.hours; return "Family call connected. Private: no AI recording."; }); break;
                case "airlock":
                    so.title = "Airlock Control"; so.cat = SysCat.Emergency; so.powerW = 400;
                    so.why = "Pressure control for EVA. Suitports are the normal way out; the airlock is the contingency path for cargo or an injured crew member.";
                    so.telemetry = s => new[] { "Pressure 70.3 kPa", "Outer hatch: sealed", "Suits: 4 docked" };
                    A(so, Key.E, "Airlock status", s => "Airlock: pressurised, outer hatch sealed. Go outside through the suitports."); break;
                case "wardroom":
                    so.title = "Wardroom Table"; so.cat = SysCat.Psych; so.powerW = 20;
                    so.why = "Shared meals and team meetings are where crew cohesion is built; the inlay display shows the day plan.";
                    so.telemetry = s => new[] { "Seats 4", "Next meal: 12:00", "Meeting: 08:30 daily" };
                    A(so, Key.E, "Start team meeting", s => { Panels.Open("meeting"); return ""; }); break;
                default:
                    so.title = so.key.Replace("_", " "); so.cat = SysCat.Research; so.why = "Habitat system.";
                    A(so, Key.E, "Use", s => s.title + " used."); break;
            }
        }

        static string[] Advice()
        {
            var c = Me; if (c == null) return new string[0];
            var l = new List<string>();
            if (c.sleepQ < .75f) { l.Add("Circadian light therapy 20 min"); l.Add("Earth nature simulation before bed"); }
            if (c.stress > .45f) { l.Add("Stress Recovery mode (R) or the sanctuary pod"); l.Add("Talk to the AI companion or call home"); }
            if (c.mood < .55f) l.Add("Greenhouse activity session (behavioural activation)");
            if (c.fatigue > .5f) l.Add("20-minute recovery nap");
            if (l.Count == 0) l.Add("You're at your baseline — keep the routine");
            l.Add("Expected recovery: +18 mental wellness");
            return l.ToArray();
        }

        /// The pod model itself changes with the person's state: green = healthy, yellow = fatigue (circadian fix), blue = stress (calm protocol).
        static float nextPod;
        static void PodTick(SmartObject so)
        {
            if (Time.time < nextPod || Me == null) return; nextPod = Time.time + 3f;
            string want = Me.stress > .55f ? "stress" : (Me.fatigue > .55f || Me.sleepQ < .6f) ? "fatigue" : "healthy";
            so.state = want == "stress" ? "<color=#5b8cff>STRESS — calm protocol</color>" : want == "fatigue" ? "<color=#ffd23f>FATIGUE — circadian reset</color>" : "<color=#3fff7a>HEALTHY</color>";
            if (so.model && so.model.name == "recovery_pod_" + want) return;
            var m = WellnessSpawner.LoadModel("recovery_pod_" + want); if (!m) return;
            m.transform.SetParent(so.transform, false);
            if (so.model) { m.transform.localPosition = so.model.transform.localPosition; m.transform.localRotation = so.model.transform.localRotation; m.transform.localScale = so.model.transform.localScale; UnityEngine.Object.Destroy(so.model); }
            so.model = m; SmartObject.AddSolidColliders(m);
        }
    }

    /// Test hooks for the editor bridge (invoke Hunch3.SmartDebug.X): start the habitat and walk up to each system.
    public static class SmartDebug
    {
        static int idx;
        public static void StartHabitat() { MenuBoot.Go(GameMode.Sandbox); }
        public static void Diag() { SmartInteractor.debugDiag = !SmartInteractor.debugDiag; }
        public static void TestAlert() { HabitatOps.I?.DebugAlert(); }
        public static void TestCheer() { HabitatOps.I?.DebugCheer(); }
        public static void VisitNext()
        {
            var l = SmartObject.All.Where(o => o).ToList(); if (l.Count == 0 || !PlayerRig.I) return;
            var so = l[idx++ % l.Count]; var c = so.transform.position + so.transform.rotation * so.localBounds.center;
            float r = Mathf.Max(so.localBounds.extents.x, so.localBounds.extents.z);
            var stand = c + so.facing * (r + 1.3f); stand.y = so.transform.position.y + .02f;
            var look = c - stand; look.y = 0;
            PlayerRig.I.Teleport(stand, Quaternion.LookRotation(look).eulerAngles.y);
            System.IO.File.WriteAllText("HunchBridge/visit.txt", $"{idx}/{l.Count} {so.key} at {so.transform.position:F2} stand {stand:F2}");
        }
    }
}
