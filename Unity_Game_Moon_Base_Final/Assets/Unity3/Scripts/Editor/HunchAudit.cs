// ================================================================================================
//  PROJECT ARES · Lunar Smart Adaptive Habitat (realistic)
//  File    : Unity3/Scripts/Editor/HunchAudit.cs
//  Purpose : Read-only inventory of the habitat scene for the "does every object answer a proposal
//            requirement?" audit. Opens 01_LunarHabitat additively, lists rooms (zones), object groups
//            (renderer name prefixes) with counts and sizes, every interactable kind, then closes the
//            scene again without saving. Writes HunchBridge/audit.txt. Changes nothing.
//  Run     : HunchBridge → invoke HunchAudit.Run
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class HunchAudit
{
    public static void Run()
    {
        var sb = new StringBuilder();
        const string path = "Assets/Unity3/Scenes/01_LunarHabitat.unity";
        var already = EditorSceneManager.GetSceneByPath(path);
        var scn = already.isLoaded ? already : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            var roots = scn.GetRootGameObjects();
            sb.AppendLine("ROOTS: " + string.Join(", ", roots.Select(r => r.name)));
            var all = roots.SelectMany(r => r.GetComponentsInChildren<Renderer>(true)).ToList();
            sb.AppendLine("renderers: " + all.Count);
            // object groups: name up to the 2nd underscore (e.g. Q1_Bed_Mattress → Q1_Bed)
            string Key(string n) { var p = n.Split('_'); return p.Length >= 2 ? p[0] + "_" + p[1] : n; }
            sb.AppendLine("\nGROUPS (prefix · parts · size of the group in m · centre)");
            foreach (var g in all.GroupBy(r => Key(r.name)).OrderBy(g => g.Key))
            {
                var b = g.First().bounds; foreach (var r in g) b.Encapsulate(r.bounds);
                sb.AppendLine($"{g.Key} · {g.Count()} · {b.size.x:0.0}x{b.size.y:0.0}x{b.size.z:0.0} · ({b.center.x:0},{b.center.y:0},{b.center.z:0})");
            }
            // interactables (type name + kind field if present)
            sb.AppendLine("\nINTERACTABLES (component · kind · count · example)");
            var comps = roots.SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).Where(c => c && c.GetType().Name.Contains("Interact")).ToList();
            foreach (var g in comps.GroupBy(c => c.GetType().Name + " · " + (c.GetType().GetField("kind")?.GetValue(c) ?? "")).OrderBy(g => g.Key))
                sb.AppendLine($"{g.Key} · {g.Count()} · {g.First().name}");
            // rooms: waypoints named WP_*
            sb.AppendLine("\nWAYPOINTS: " + string.Join(" ", roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Where(t => t.name.StartsWith("WP_")).Select(t => t.name).Distinct().OrderBy(n => n)));
        }
        finally { if (!already.isLoaded) EditorSceneManager.CloseScene(scn, true); }
        File.WriteAllText(Path.Combine("HunchBridge", "audit.txt"), sb.ToString());
    }
}
