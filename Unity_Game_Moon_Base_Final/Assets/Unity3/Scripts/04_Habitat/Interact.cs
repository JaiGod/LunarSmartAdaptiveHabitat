// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Smart Adaptive Habitat (realistic)
//  File    : Unity3/Scripts/04_Habitat/Interact.cs
//  Section : 04_Habitat
//  Folder  : The base itself: zones and their light/sound/temperature/air, dome status lights,
//            doors and hatches, ladders, loose props in 1/6 g, Moon environment, layout export.
//  Purpose : Every tagged object in the habitat gets one of these; VR (trigger/select) and desktop
//            (E / click) both call Activate().
//  Contains: HabInteractable, DesktopInteractor
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Hunch3
{
    /// Every tagged object in the habitat gets one of these; VR (trigger/select) and desktop (E / click) both call Activate().
    public class HabInteractable : MonoBehaviour
    {
        public string kind; HabTag htag; Vector3 home; bool toggled; float cooldown, lastDoorDist = 99f;
        public static HabInteractable Hover;

        void Start()
        {
            htag = GetComponent<HabTag>(); kind = htag ? htag.Interact : kind; home = transform.localPosition;
            var xr = GetComponent<XRSimpleInteractable>();
            if (!xr && GetComponent<Collider>() && !GetComponent<XRGrabInteractable>()) xr = gameObject.AddComponent<XRSimpleInteractable>();
            if (xr) xr.selectEntered.AddListener(_ => Activate());
            // Doors never block anyone: door leaves stand OPEN (hidden in their wall pocket) and are never solid.
            // Hatches don't swing (the FBX axis conversion made them flip into a bar across the doorway).
            bool sealedToVacuum = kind == "hatch_swing" && (name.Contains("Emergency") || name.Contains("OuterHatch"));
            if ((kind == "door_slide" || kind == "curtain_toggle" || kind == "hatch_swing") && !sealedToVacuum)
                foreach (var c in GetComponents<Collider>())
                {
                    if (c is MeshCollider mc) mc.convex = true;   // trigger mesh colliders must be convex
                    c.isTrigger = true;                           // still clickable / pointable, never solid
                }
            if (kind == "door_slide") { var r = GetComponent<Renderer>(); if (r) r.enabled = false; }
            if (name.Contains("OuterHatch")) Invoke(nameof(SealOuterHatch), 0.2f);
        }

        /// The airlock's outer hatch was modelled swung half open (you could see vacuum through it). Swing it SHUT into its
        /// frame in the hull so the airlock is sealed: same leaf, turned flat across the doorway, flush with the hull side.
        void SealOuterHatch()
        {
            var r = GetComponent<Renderer>(); if (!r) return;
            var mf = GetComponent<MeshFilter>(); Vector3 ext = mf && mf.sharedMesh ? mf.sharedMesh.bounds.size : Vector3.one;
            // widest horizontal axis of the leaf = its width
            Vector3 w = Vector3.right; float best = -1;
            for (int i = 0; i < 3; i++)
            {
                Vector3 ax = i == 0 ? transform.right : i == 1 ? transform.up : transform.forward; Vector3 hz = new Vector3(ax.x, 0, ax.z);
                float len = Mathf.Abs(ext[i] * (i == 0 ? transform.lossyScale.x : i == 1 ? transform.lossyScale.y : transform.lossyScale.z));
                if (hz.magnitude > 0.7f && len > best) { best = len; w = hz.normalized; }
            }
            var b = r.bounds;
            // hinge = the leaf edge nearest the hull (largest z on this side of the airlock); the doorway is centred on the porch
            var porch = Util.Find("A81_Porch_Platform");
            float doorX = porch ? porch.position.x : b.center.x;
            float hullZ = b.max.z;
            float yaw = Vector3.SignedAngle(w, Vector3.right, Vector3.up); if (Mathf.Abs(yaw) > 90f) yaw -= Mathf.Sign(yaw) * 180f;
            transform.RotateAround(b.center, Vector3.up, yaw);
            var nb = r.bounds;
            transform.position += new Vector3(doorX - nb.center.x, 0, (hullZ - 0.05f) - nb.center.z);
            toggled = true;
            Debug.Log($"[HUNCH3] airlock outer hatch sealed: turned {yaw:0}°, now at {r.bounds.center}");
        }

        public string Prompt
        {
            get
            {
                switch (kind)
                {
                    case "door_slide": return toggled ? "Open door" : "Close door (privacy)";
                    case "sleep": return "Sleep (lights out · 1 real minute)";
                    case "health_check": return "AI robotic health check (your consent)";
                    case "sit": return "Sit";
                    case "team_meeting": return "Start team meeting";
                    case "lavatory_use": return "Use lavatory (UWMS)";
                    case "hygiene_use": return "Use hygiene station";
                    case "suitport_don_doff": return "Don EVA suit via suitport (go outside)";
                    case "climb_ladder": return "Climb ladder";
                    case "airlock_cycle": return "Airlock (contingency, suit required)";
                    case "tend_plant": return "Tend plants";
                    case "harvest": return "Harvest";
                    case "pickup": return "Pick up";
                    default:
                        if (kind.StartsWith("ui_")) return "Open " + kind.Substring(3).Replace("_", " ");
                        if (kind.StartsWith("exercise")) return "Exercise (" + kind.Substring(9) + ")";
                        return kind.Replace("_", " ");
                }
            }
        }

        public void Activate()
        {
            if (Time.time < cooldown) return; cooldown = Time.time + 0.6f;
            HabitatOps.I?.OnHab(this);      // Daily Ops task progress
            var eng = Sim.I ? Sim.I.engine : null; var me = eng?.C(1); var pr = PlayerRig.I;
            switch (kind)
            {
                case "door_slide": HUD.I?.Toast("Doorways stay open so nobody is ever trapped. Use the door-lock panel for privacy."); break;
                case "hatch_swing":
                    if (name.Contains("Emergency") || name.Contains("OuterHatch")) HUD.I?.Toast("⛔ This hatch opens onto the lunar surface (vacuum). It stays sealed — go outside with a suit through the suitport in the Airlock module.");
                    else HUD.I?.Toast("Pressure hatch — held open. It closes automatically only if a module loses pressure.");
                    break;
                case "curtain_toggle": transform.localScale = new Vector3(transform.localScale.x, toggled ? 1f : 0.15f, transform.localScale.z); toggled = !toggled; break;
                case "door_lock_toggle": toggled = !toggled; HUD.I?.Toast(toggled ? "Pod locked — only you (or CMO in a declared emergency) can open it." : "Pod unlocked."); break;
                case "sleep": Sleep(me); break;
                case "health_check": { int own = name.Length > 2 && name.StartsWith("SQ") && char.IsDigit(name[2]) ? name[2] - '0' : 1; if (HealthBot.All.TryGetValue(own, out var hb)) hb.Request(pr); else HUD.I?.Toast("Health station offline."); } break;
                case "sit": pr?.Sit(GetComponent<Renderer>() ? GetComponent<Renderer>().bounds.center + Vector3.up * 0.05f - Vector3.up * 0.45f : transform.position); Social(me, 0.01f); break;
                case "team_meeting": Panels.Open("meeting"); break;
                case "exercise_treadmill": case "exercise_cycle": case "exercise_resistive": case "exercise_band": Exercise(me); break;
                case "tend_plant": case "harvest": Plant(me); break;
                case "cook_eat": if (me != null) { me.mood += 0.03f; } HUD.I?.Toast("Meal rehydrated & warmed. Eating with others boosts cohesion."); Social(me, 0.03f); break;
                case "hygiene_use": HUD.I?.Toast("Hygiene: no-rinse shampoo & body shower recycle ~98% of water through the WPA."); break;
                case "lavatory_use": HUD.I?.Toast("UWMS: lid up → fan starts. Airflow + 1/6 g carry waste in; urine → UPA → drinking water, solids → heat-melt compactor → shielding tiles."); if (me != null) me.stress -= 0.02f; break;
                case "laundry_use": HUD.I?.Toast("Water-efficient wash cycle started (3 L, recycled)."); break;
                case "recycle_to_filament": HUD.I?.Toast("Packaging plastic → 1.75 mm filament for the spare-part printer."); break;
                case "print_spare_part": PrintPart(); break;
                case "robot_control": HUD.I?.Toast("Inspection robot dispatched: hull & seal scan of the loop hallways."); if (me != null) { me.autonomy += 0.05f; } break;
                case "science_task": HUD.I?.Toast("Regolith sample logged. Task time & error rate feed the fatigue signal."); if (me != null) me.fatigue += 0.02f; break;
                case "medical_exam": Panels.Open("cmo"); break;
                case "eclss_service": Panels.Open("eclss"); break;
                case "airlock_cycle": Airlock(pr); break;
                case "suitport_don_doff": if (pr) { if (pr.suit == SuitState.IVA) pr.BeginDon(NearestSuit()); else pr.BeginDoff(); } break;
                case "climb_ladder": Ladder(pr); break;
                case "play_music": case "play_game": if (me != null) { me.novelty += 0.08f; me.mood += 0.03f; } Social(me, 0.04f); HUD.I?.Toast("Game/music time — shared fun is a top counter to monotony & conflict."); break;
                case "toggle_scent_opt_in": if (me != null) HUD.I?.Toast("Personal scent diffuser is opt-in only; set your scents in the Crew Interface."); break;
                case "open_storage": transform.localScale = Vector3.Scale(transform.localScale, new Vector3(1, toggled ? 1 / 0.98f : 0.98f, 1)); toggled = !toggled; HUD.I?.Toast("Stowage opened (inventory tracked by RFID)."); break;
                case "pickup": Pickup(); break;
                case "drive_vehicle": Panels.Open("drive"); break;
                case "conflict_protocol_deployable": HabitatEnvironment.I?.SetConfigB(!GameState.configB); break;
                case "sensor_status": case "actuator": HUD.I?.Toast(name.Replace("X_", "").Replace("_", " ") + (htag ? "  ·  " + htag.Get("sensor_type") : "")); break;
                case "inspect_wiring": HUD.I?.Toast("Smart-habitat wiring: sensor bus + power trunks run in cable trays to the Command node computer room."); break;
                case "ui_view_screen": HUD.I?.Toast("Earth view / nature scene rotated (combats sensory monotony)."); if (me != null) me.novelty += 0.02f; break;
                case "ui_wellness_override_panel": case "ui_personal_environment": Panels.Open("crew"); break;
                case "ui_ai_dashboard": case "ui_smart_habitat": case "ui_mission_log": Panels.Open("dashboard"); break;
                case "ui_earth_call": EarthCall(me); break;
                case "ui_cmo_alerts": Panels.Open("cmo"); break;
                case "ui_lab_task": HUD.I?.Toast("Lab task started. Performance metrics are a fatigue signal — shared with you only."); break;
                case "ui_fabricator": PrintPart(); break;
                default: HUD.I?.Toast(Prompt); break;
            }
        }

        static void Social(CrewMember me, float amt) { if (me == null) return; me.lastSocialHour = SimClock.hours; me.mood += amt; }
        int NearestSuit()
        {
            int best = 1; float bd = 1e9f;
            for (int n = 1; n <= 4; n++) { var t = Util.Find($"CREW{n}_EVA_Suit_Root"); if (!t) continue; float d = (t.position - transform.position).sqrMagnitude; if (d < bd) { bd = d; best = n; } }
            return best;
        }
        void SlideDoor()
        {
            // A sliding door moves ALONG its own width (into the wall pocket), never along the passage.
            Vector3 a = transform.parent ? transform.parent.TransformPoint(home) : home;
            Vector3 axis = SlideAxis(out float width);
            var doors = Util.FindPrefix("WP_").Where(w => w.name.Contains("Door") || w.name.EndsWith("_In") || w.name.Contains("Hall"));
            float off = Mathf.Clamp(width * 0.95f, 0.5f, 1.2f);
            // closing = slide toward the doorway it covers; opening = back to where it was modelled (in the pocket)
            float dPlus = doors.Any() ? doors.Min(w => (a + axis * off - w.position).sqrMagnitude) : 0, dMinus = doors.Any() ? doors.Min(w => (a - axis * off - w.position).sqrMagnitude) : 1;
            Vector3 target = toggled ? a : a + axis * (dPlus < dMinus ? off : -off);
            transform.position = target; toggled = !toggled;
            HUD.I?.Toast(toggled ? "Door closed (privacy). Walk up to it or press E to open." : "Door open.");
        }
        Vector3 SlideAxis(out float width)
        {
            // the leaf's widest horizontal local axis (works whatever the FBX axis conversion did to transform.right)
            var mf = GetComponent<MeshFilter>(); Vector3 ext = mf && mf.sharedMesh ? mf.sharedMesh.bounds.size : Vector3.one;
            Vector3 best = Vector3.right; width = 0.8f; float bw = -1;
            for (int i = 0; i < 3; i++)
            {
                Vector3 ax = i == 0 ? transform.right : i == 1 ? transform.up : transform.forward;
                float s = (i == 0 ? transform.lossyScale.x : i == 1 ? transform.lossyScale.y : transform.lossyScale.z);
                float len = Mathf.Abs(ext[i] * s); Vector3 h = new Vector3(ax.x, 0, ax.z);
                if (h.magnitude < 0.7f) continue;                    // vertical axis
                if (len > bw) { bw = len; best = h.normalized; width = len; }
            }
            return best;
        }
        void Update()
        {
            // closed sliding doors open automatically when someone walks up to them (nobody gets trapped)
            return;   // doors no longer move (they stay open)
#pragma warning disable CS0162
            if (kind != "door_slide" || !toggled) return;
            var pr = PlayerRig.I; if (!pr) return;
            Vector3 a = transform.parent ? transform.parent.TransformPoint(home) : home;
            float dist = Vector3.Distance(new Vector3(pr.FeetPosition.x, 0, pr.FeetPosition.z), new Vector3(transform.position.x, 0, transform.position.z));
            bool approaching = dist < lastDoorDist - 0.005f; lastDoorDist = dist;
            if (dist < 1.4f && approaching && Time.time > cooldown + 1.5f) { cooldown = Time.time; SlideDoor(); }
#pragma warning restore CS0162
        }
        void Sleep(CrewMember me)
        {
            string nm = name.StartsWith("SQ") ? name.Substring(1) : name;
            char pod = nm.Length > 1 && nm[0] == 'Q' ? nm[1] : '1';
            if (pod != '1' && char.IsDigit(pod)) { var o = Sim.I.engine.C(pod - '0'); if (BedFinder.I) BedFinder.I.Show(true, $"That's {o.name}'s bed — private space."); else HUD.I?.Toast($"That's {o.name}'s sleep pod — private space. Your quarters are Quarters 1 (follow N → My quarters)."); return; }
            if (SleepSystem.I) SleepSystem.I.BeginSleep(transform);
        }
        void Exercise(CrewMember me)
        {
            if (me == null) return; me.exercising = true; me.lastExerciseHour = SimClock.hours; me.stress -= 0.1f; me.mood += 0.05f; me.fatigue += 0.05f;
            HUD.I?.Toast("Exercise session logged (protects bone & muscle in 1/6 g). Heart-rate rise is context-tagged so the AI won't mistake it for anxiety.");
            CancelInvoke(nameof(StopEx)); Invoke(nameof(StopEx), 15f);
        }
        void StopEx() { if (Sim.I != null) Sim.I.engine.C(1).exercising = false; }
        void Plant(CrewMember me)
        {
            if (me != null) { me.mood += 0.05f; me.novelty += 0.03f; }
            transform.localScale *= 1.03f;
            HUD.I?.Toast("Plant tended — behavioural activation (a top evidence-based depression countermeasure).");
        }
        void EarthCall(CrewMember me)
        {
            if (me != null) { me.mood += 0.12f; me.lastSocialHour = SimClock.hours; }
            HUD.I?.Toast("Family call connected (1.3 s each way). Private booth: no AI audio/video recording.");
        }
        void PrintPart()
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder); g.name = "Printed_Spare_Part"; g.transform.position = transform.position + Vector3.up * 0.6f; g.transform.localScale = new Vector3(0.08f, 0.04f, 0.08f);
            g.AddComponent<Rigidbody>().mass = 0.2f; g.AddComponent<XRGrabInteractable>();
            HUD.I?.Toast("Spare part printed (ECLSS fan bracket). Grab it!");
        }
        GameObject held;
        void Pickup()
        {
            if (PlayerRig.I && !PlayerRig.I.isXR)
            {
                if (held == null) { held = gameObject; var rb = GetComponent<Rigidbody>(); if (rb) rb.isKinematic = true; transform.SetParent(PlayerRig.I.cam.transform, true); transform.localPosition = new Vector3(0.25f, -0.25f, 0.6f); HUD.I?.Toast("Carrying item (1/6 g: feels ~6x lighter). E again to drop."); }
                else { transform.SetParent(null, true); var rb = GetComponent<Rigidbody>(); if (rb) rb.isKinematic = false; held = null; }
            }
        }
        void Airlock(PlayerRig pr)
        {
            if (!pr) return;
            if (pr.suit == SuitState.IVA) { HUD.I?.Toast("BLOCKED: you can't go outside without an EVA suit. Use the suitports in the suit room (Airlock module)."); return; }
            HUD.I?.Procedure("Airlock cycle: depress 4 min → outer hatch → (contingency path for cargo / injured crew)");
        }
        void Ladder(PlayerRig pr)
        {
            if (!pr) return;
            if (LadderClimb.I) { LadderClimb.I.Begin(pr); return; }
            bool down = pr.FeetPosition.y > 2.5f;
            var t = Util.Find(down ? "SPAWN_Ladder_Bottom" : "SPAWN_Ladder_Top"); if (t) pr.Teleport(t.position);
        }
    }

    /// One aim for every interaction system, resolved once per frame. A thin centre ray was too fussy (you had to hit
    /// a 3 cm switch exactly), so: 1) the centre ray, 2) if it misses, the nearest usable thing within a small cone
    /// that you can actually see. Only ONE target wins (old-style interactable or smart system), so keys never clash.
    public static class Aim
    {
        static int frame = -1; public static HabInteractable Hab; public static SmartObject Smart;
        const float HabReach = 2.9f, SmartReach = 3.8f, Cone = 14f;
        public static void Resolve()
        {
            if (frame == Time.frameCount) return; frame = Time.frameCount; Hab = null; Smart = null;
            var pr = PlayerRig.I; if (!pr || !pr.cam || pr.isXR) return;
            Vector3 o = ThirdPersonView.Active ? ThirdPersonView.Eye : pr.cam.transform.position, f = pr.cam.transform.forward;
            // 1) straight ray: first thing you look at that isn't you or an empty trigger volume
            foreach (var h in Physics.RaycastAll(new Ray(o, f), SmartReach, ~0, QueryTriggerInteraction.Collide).OrderBy(h => h.distance))
            {
                if (Mine(h.collider, pr)) continue;
                var hi = h.distance <= HabReach ? h.collider.GetComponentInParent<HabInteractable>() : null;
                var so = h.collider.GetComponentInParent<SmartObject>();
                if (hi) { Hab = hi; return; }
                if (so) { Smart = so; return; }
                if (h.collider.isTrigger) continue;
                break;
            }
            // 2) aim assist: the usable thing closest to the crosshair, inside a 14° cone, in clear view
            float best = Cone;
            foreach (var c in Physics.OverlapSphere(o, SmartReach, ~0, QueryTriggerInteraction.Collide))
            {
                if (Mine(c, pr)) continue;
                var hi = c.GetComponentInParent<HabInteractable>(); var so = hi ? null : c.GetComponentInParent<SmartObject>();
                if (!hi && !so) continue;
                var b = c.bounds; Vector3 p = b.ClosestPoint(o + f * Vector3.Distance(o, b.center)); Vector3 to = p - o; float d = to.magnitude;
                if (d < 0.05f || (hi && d > HabReach)) continue;
                float ang = Vector3.Angle(f, to); if (ang >= best) continue;
                if (Physics.Linecast(o, p - to.normalized * 0.05f, out var blk, ~0, QueryTriggerInteraction.Ignore) && blk.collider != c && !Mine(blk.collider, pr)
                    && !(hi && blk.collider.GetComponentInParent<HabInteractable>() == hi) && !(so && blk.collider.GetComponentInParent<SmartObject>() == so)) continue;   // something solid in the way
                best = ang; Hab = hi; Smart = so;
            }
        }
        static bool Mine(Collider c, PlayerRig pr) => c.transform.IsChildOf(pr.transform) || c.GetComponentInParent<CharacterController>() || (pr.origin && c.transform.IsChildOf(pr.origin.transform));
    }

    /// Desktop: look at an object & press E / left-click. Shows a prompt.
    public class DesktopInteractor : MonoBehaviour
    {
        void Update()
        {
            var pr = PlayerRig.I; if (!pr || !pr.cam) return;
            if (pr.suit == SuitState.EVA)
            {
                var sp = Util.Find($"CREW{pr.donnedSuit}_EVA_Suit_Root");
                if (sp && Vector3.Distance(new Vector3(sp.position.x, 0, sp.position.z), new Vector3(pr.FeetPosition.x, 0, pr.FeetPosition.z)) < 2.2f)
                {
                    HUD.I?.SetPrompt("E / trigger: dock at suitport & doff suit");
                    if (Pressed() || XRTrigger()) pr.BeginDoff();
                    return;
                }
            }
            // suit room: no need to aim at the small hatch — standing there is enough (works in VR too)
            if (pr.suit == SuitState.IVA && Nav.wp.TryGetValue("WP_A_Suit", out var suitWp) && Vector3.Distance(pr.FeetPosition, suitWp.position) < 1.6f)
            {
                HUD.I?.SetPrompt("E / trigger: put on an EVA suit through the suitport (go outside)");
                if (Pressed() || XRTrigger()) { int n = 1; float bd = 1e9f; for (int k = 1; k <= 4; k++) { var t = Util.Find($"CREW{k}_EVA_Suit_Root"); if (!t) continue; float d = (t.position - pr.FeetPosition).sqrMagnitude; if (d < bd) { bd = d; n = k; } } pr.BeginDon(n); }
                return;
            }
            HabInteractable.Hover = null;
            if (!pr.isXR)
            {
                Aim.Resolve();
                var hi = Aim.Hab;
                if (hi) { HabInteractable.Hover = hi; HUD.I?.SetPrompt(""); if (Pressed()) hi.Activate(); return; }   // the bottom card explains it
                if (Aim.Smart) { HUD.I?.SetPrompt(""); return; }
            }
            HUD.I?.SetPrompt("");
        }
        static bool Pressed()
        {
            var kb = Keyboard.current; var ms = Mouse.current;
            return (kb != null && kb.eKey.wasPressedThisFrame) || (ms != null && ms.leftButton.wasPressedThisFrame && !Panels.AnyOpen && Cursor.lockState == CursorLockMode.Locked);
        }
        static bool XRTrigger()
        {
            var d = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand);
            return d.isValid && d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool v) && v;
        }
    }
}
