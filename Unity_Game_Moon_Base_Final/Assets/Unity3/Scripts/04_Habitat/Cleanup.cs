// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Smart Adaptive Habitat (realistic)
//  File    : Unity3/Scripts/04_Habitat/Cleanup.cs
//  Section : 04_Habitat
//  Purpose : HABITAT CLEANUP — switches off objects that don't answer a line in the research proposal
//            (Docs/Audit/Realistic_Game_Audit.md): the second set of bedrooms, duplicate exercise
//            corners, extra lounge, hobby props and the second bathroom. Nothing is deleted: the
//            objects stay in the model and the scene, only their renderers, colliders and
//            interactions are turned off at start. Set Enabled = false (or F9 panel, key 6) to bring
//            everything back.
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Hunch3
{
    public class HabitatCleanup : MonoBehaviour
    {
        public static bool Enabled = true;
        public static HabitatCleanup I;
        /// object name prefix → why it goes (audit row)
        public static readonly (string prefix, string why)[] Cut =
        {
            // 1 · second set of bedrooms: crew sleep in the SQ1–SQ4 domes; Q1–Q4 keep walls/floor/door only
            ("Q1_Bed", "#1 duplicate beds"), ("Q2_Bed", "#1"), ("Q3_Bed", "#1"), ("Q4_Bed", "#1"),
            ("Q1_Desk", "#1"), ("Q2_Desk", "#1"), ("Q3_Desk", "#1"), ("Q4_Desk", "#1"),
            ("Q1_Locker", "#1"), ("Q2_Locker", "#1"), ("Q3_Locker", "#1"), ("Q4_Locker", "#1"),
            ("Q1_MiniGreenhouse", "#1"), ("Q2_MiniGreenhouse", "#1"), ("Q3_MiniGreenhouse", "#1"), ("Q4_MiniGreenhouse", "#1"),
            ("Q1_Nameplate", "#1"), ("Q2_Nameplate", "#1"), ("Q3_Nameplate", "#1"), ("Q4_Nameplate", "#1"),
            ("Q1_VirtualWindow", "#1"), ("Q2_VirtualWindow", "#1"), ("Q3_VirtualWindow", "#1"), ("Q4_VirtualWindow", "#1"),
            ("Q1_Water", "#1"), ("Q2_Water", "#1"), ("Q3_Water", "#1"), ("Q4_Water", "#1"),
            // 3 · duplicate exercise: the D-module gym stays
            ("Q1_ResistanceBand", "#3 duplicate exercise"), ("Q2_ResistanceBand", "#3"), ("Q3_ResistanceBand", "#3"), ("Q4_ResistanceBand", "#3"),
            ("SQ1_Exercise", "#3"), ("SQ2_Exercise", "#3"), ("SQ3_Exercise", "#3"), ("SQ4_Exercise", "#3"),
            // 4 · extra lounge (Recreation module and wardroom stay; CB* is Configuration B and stays)
            ("W32_Lounge", "#4 extra lounge"), ("W33_Lounge", "#4"), ("W34_Lounge", "#4"),
            // 5 · hobby props with no sensor or protocol link
            ("R34_Sofa", "#5 second sofa"), ("R39_Music", "#5"), ("R40_Music", "#5"), ("R41_Guitar", "#5"),
            // 13 · second bathroom: LH (ISS-style WHC + UWMS next to ECLSS) stays; G laundry + recycler stay
            ("G30_Hygiene2", "#13 second bathroom"), ("G31_Hygiene2", "#13"), ("G32_Hygiene2", "#13"), ("G33_Hygiene2", "#13"), ("G34_Hygiene2", "#13"),
            ("G35_Hygiene2", "#13"), ("G36_Hygiene2", "#13"), ("G37_Hygiene2", "#13"), ("G38_Hygiene2", "#13"), ("G39_Hygiene2", "#13"), ("G48_Hygiene2", "#13"),
        };
        readonly List<Component> off = new List<Component>();
        public int hidden;

        void Awake() { I = this; if (Enabled) Apply(true); }

        public void Apply(bool on)
        {
            if (!on) { foreach (var c in off) if (c) Set(c, true); off.Clear(); hidden = 0; HLog.I("Cleanup", "all cut objects restored"); return; }
            var root = Util.Find("LunarHabitat_Unity3_Habitat"); if (!root) { HLog.W("Cleanup", "habitat root not found"); return; }
            var count = new Dictionary<string, int>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var hit = Cut.FirstOrDefault(c => t.name.StartsWith(c.prefix));
                if (hit.prefix == null) continue;
                foreach (var c in t.GetComponents<Component>())
                    if ((c is Renderer || c is Collider || c is HabInteractable) && IsOn(c)) { Set(c, false); off.Add(c); }
                count[hit.why] = count.TryGetValue(hit.why, out var n) ? n + 1 : 1; hidden++;
            }
            HLog.I("Cleanup", $"{hidden} objects switched off (not deleted): " + string.Join(", ", count.Select(kv => kv.Key + " ×" + kv.Value)));
        }
        static bool IsOn(Component c) => c is Renderer r ? r.enabled : c is Collider k ? k.enabled : ((Behaviour)c).enabled;
        static void Set(Component c, bool v) { if (c is Renderer r) r.enabled = v; else if (c is Collider k) k.enabled = v; else ((Behaviour)c).enabled = v; }
    }
}
