// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Smart Adaptive Habitat (realistic)
//  File    : Unity3/Scripts/01_Core/Diagnostics.cs
//  Section : 01_Core
//  Folder  : Start-up, shared state, menu scene, render settings and automated self-test.
//  Purpose : Writes where Unity actually placed key objects (empties vs meshes) so frame mismatches
//            can be fixed with data.
//  Contains: Diagnostics
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hunch3
{
    /// Writes where Unity actually placed key objects (empties vs meshes) so frame mismatches can be fixed with data.
    public class Diagnostics : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init() { SceneManager.sceneLoaded += (s, m) => { if (s.name.Contains("LunarHabitat")) new GameObject("Diagnostics").AddComponent<Diagnostics>(); }; if (SceneManager.GetActiveScene().name.Contains("LunarHabitat")) new GameObject("Diagnostics").AddComponent<Diagnostics>(); }
        void Start() { Invoke(nameof(Dump), 1.5f); }
        static string V(Vector3 v) => $"({v.x:0.00}, {v.y:0.00}, {v.z:0.00})";
        void Dump()
        {
            var sb = new StringBuilder();
            var root = Util.Find("LunarHabitat_Unity3_Habitat");
            if (root) sb.AppendLine($"HAB ROOT pos {V(root.position)} rot {V(root.eulerAngles)} scale {V(root.lossyScale)}");
            string[] names = { "WP_C_Mid", "WP_A_Suit", "WP_B_Lab", "WP_Up_Table", "ZONE_Nature_Greenhouse", "ZONE_Airlock_EVA", "SPAWN_Player", "SPAWN_EVA_Outside", "CREW4_IVA_Root", "CREW1_EVA_Suit_Root", "POD_Crew1_Root",
                               "C10_PressureHull", "A10_PressureHull", "B10_PressureHull", "W01_MeetingTable_Top", "LH65_UWMS_Toilet_Body_Titanium", "Q1_Bed_Mattress", "E30_3DPrinter_Polymer_SpareParts" };
            foreach (var n in names)
            {
                var t = Util.Find(n); if (!t) { sb.AppendLine($"{n}: MISSING"); continue; }
                var r = t.GetComponent<Renderer>();
                sb.AppendLine($"{n}: pos {V(t.position)} rot {V(t.eulerAngles)} scale {V(t.lossyScale)} parent {(t.parent ? t.parent.name : "-")}" + (r ? $" bounds {V(r.bounds.center)}" : ""));
            }
            if (PlayerRig.I) sb.AppendLine($"PLAYER feet {V(PlayerRig.I.FeetPosition)} cam {V(PlayerRig.I.cam.transform.position)}");
            string p = System.IO.Path.Combine(Application.dataPath, "Unity3", "diag_positions.txt");
            System.IO.File.WriteAllText(p, sb.ToString()); HLog.I("Diagnostics", "report → " + p);
        }
    }
}
