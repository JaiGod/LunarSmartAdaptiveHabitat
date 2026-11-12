// PROJECT ARES · editor helper: mounts every ceiling light fixture flush against the ceiling above it.
// Some LED panels were modelled at a fixed height and hung in mid-air under the curved dome roofs.
// Run through the bridge (Play mode OFF): invoke HunchCeilingSnap.Run   (dry run: HunchCeilingSnap.Report)
// → HunchBridge/ceiling_snap.txt, and the scene is saved. Builder calls it too, so a rebuild keeps the fix.
// © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class HunchCeilingSnap
{
    public static void Run() => Snap(true);
    public static void Report() => Snap(false);
    /// opens the habitat scene, snaps, saves, then goes back to the scene that was open (bridge-friendly)
    public static void RunInHabitat()
    {
        if (EditorApplication.isPlaying) { File.WriteAllText(System.IO.Path.Combine("HunchBridge", "ceiling_snap.txt"), "not while playing"); return; }
        var prev = EditorSceneManager.GetActiveScene().path;
        EditorSceneManager.OpenScene("Assets/Unity3/Scenes/01_LunarHabitat.unity");
        Snap(true);
        if (!string.IsNullOrEmpty(prev) && prev != "Assets/Unity3/Scenes/01_LunarHabitat.unity") EditorSceneManager.OpenScene(prev);
    }

    public static int Snap(bool apply, bool save = true)
    {
        var log = new System.Text.StringBuilder(); int moved = 0, flush = 0, skipped = 0;
        bool bf = Physics.queriesHitBackfaces; Physics.queriesHitBackfaces = true;      // the hull meshes are single-sided
        Physics.SyncTransforms(); var done = new System.Collections.Generic.HashSet<Transform>();
        foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (done.Contains(mr.transform)) continue; var m = mr.sharedMaterial; bool named = mr.name.Contains("CeilingLight") || mr.name.Contains("Ceiling_Light"); if (!named && (!m || !m.name.StartsWith("Emit_LED"))) continue;
            if (KeepHeight(mr.name)) { skipped++; continue; }   // door-header lights and cove rings sit where they are on purpose   // ceiling LED panels / rings (not grow lights, not mood strips)
            var b = mr.bounds; float wide = Mathf.Max(b.size.x, b.size.z);
            if (wide < 0.2f || b.size.y > 0.35f * wide) { skipped++; continue; }           // wall strips and vertical lamps stay where they are
            // the lowest ceiling point over the panel (centre + 4 corners), so no corner pokes through a curved roof
            float best = float.MaxValue; var self = mr.transform;
            Vector3 c = b.center, e = b.extents * 0.9f;
            foreach (var o in new[] { Vector3.zero, new Vector3(e.x, 0, e.z), new Vector3(-e.x, 0, e.z), new Vector3(e.x, 0, -e.z), new Vector3(-e.x, 0, -e.z) })
            {
                var from = new Vector3(c.x + o.x, b.max.y + 0.005f, c.z + o.z);
                foreach (var h in Physics.RaycastAll(from, Vector3.up, 2.0f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (h.transform == self || h.transform.IsChildOf(self)) continue;
                    var hr = h.collider.GetComponent<Renderer>(); if (hr && hr.sharedMaterial && hr.sharedMaterial.name.StartsWith("Emit_")) continue;   // another light, not the roof
                    if (h.distance < best) best = h.distance;
                }
            }
            if (best == float.MaxValue || best > 1.6f) { skipped++; continue; }              // nothing above within reach: not a ceiling fixture
            float lift = best - 0.01f;                                                        // 1 cm mounting gap
            if (lift < 0.03f) { flush++; continue; }
            log.AppendLine($"{Path(self)}  +{lift:0.00} m");
            // the fixture's housing / bezel (separate meshes right next to the LED) goes up with it
            var group = new System.Collections.Generic.List<Transform> { self }; var near = b; near.Expand(0.1f);
            if (self.parent) foreach (var sr in self.parent.GetComponentsInChildren<MeshRenderer>())
                if (sr.transform != self && !done.Contains(sr.transform) && sr.bounds.size.y < 0.4f && Mathf.Max(sr.bounds.size.x, sr.bounds.size.z) < wide * 1.6f && near.Intersects(sr.bounds) && !sr.transform.IsChildOf(self) && !self.IsChildOf(sr.transform)) group.Add(sr.transform);
            foreach (var g in group) { done.Add(g); if (apply) { Undo.RecordObject(g, "Ceiling snap"); g.position += Vector3.up * lift; EditorUtility.SetDirty(g); } }
            if (group.Count > 1) log.AppendLine($"    + {group.Count - 1} housing part(s)");
            moved++;
        }
        Physics.queriesHitBackfaces = bf;
        if (apply && save && moved > 0) { EditorSceneManager.MarkAllScenesDirty(); EditorSceneManager.SaveOpenScenes(); }
        var head = $"{(apply ? "MOVED" : "WOULD MOVE")} {moved} fixtures up to the ceiling · already flush {flush} · not ceiling fixtures {skipped}\n";
        Directory.CreateDirectory("HunchBridge"); File.WriteAllText(System.IO.Path.Combine("HunchBridge", "ceiling_snap.txt"), head + log);
        return moved;
    }

    static bool KeepHeight(string n) => n.Contains("DoorFrame") || n.Contains("Cove") || n.Contains("Pendant") || n.Contains("Lamp") || n.Contains("Emergency") || n.Contains("Retreat_DimLight");

    /// puts back the fixtures the first snap should not have moved (door-header lights, the wardroom cove ring, …)
    public static void RevertKept()
    {
        if (EditorApplication.isPlaying) return;
        var prev = EditorSceneManager.GetActiveScene().path; EditorSceneManager.OpenScene("Assets/Unity3/Scenes/01_LunarHabitat.unity");
        var lifts = new System.Collections.Generic.Dictionary<string, float> { { "LSW_Storage_DoorFrame_Light", .28f }, { "LNE_ECLSS_Air_DoorFrame_Light", .28f }, { "LSE_ECLSS_Water_DoorFrame_Light", .28f },
            { "LNW_Hygiene_DoorFrame_Light", .28f }, { "W40_Communal_CoveLight_Ring_2800K", .49f }, { "H34_StormShelter_EmergencyLight", .07f }, { "D65_Retreat_DimLight_Warm", .18f } };
        var log = new System.Text.StringBuilder(); var done = new System.Collections.Generic.HashSet<Transform>();
        foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!lifts.TryGetValue(mr.name, out float lift) || done.Contains(mr.transform)) continue;
            var self = mr.transform; var b = mr.bounds; float wide = Mathf.Max(b.size.x, b.size.z); var near = b; near.Expand(0.1f);
            var group = new System.Collections.Generic.List<Transform> { self };
            if (self.parent) foreach (var sr in self.parent.GetComponentsInChildren<MeshRenderer>())
                if (sr.transform != self && !done.Contains(sr.transform) && sr.bounds.size.y < 0.4f && Mathf.Max(sr.bounds.size.x, sr.bounds.size.z) < wide * 1.6f && near.Intersects(sr.bounds) && !sr.transform.IsChildOf(self) && !self.IsChildOf(sr.transform)) group.Add(sr.transform);
            foreach (var g in group) { done.Add(g); Undo.RecordObject(g, "Ceiling snap revert"); g.position -= Vector3.up * lift; EditorUtility.SetDirty(g); }
            log.AppendLine($"{mr.name} -{lift:0.00} m ({group.Count} parts)");
        }
        EditorSceneManager.MarkAllScenesDirty(); EditorSceneManager.SaveOpenScenes();
        File.WriteAllText(System.IO.Path.Combine("HunchBridge", "ceiling_revert.txt"), log.ToString());
        if (!string.IsNullOrEmpty(prev) && prev != "Assets/Unity3/Scenes/01_LunarHabitat.unity") EditorSceneManager.OpenScene(prev);
    }

    /// exact undo of a snap: removes the position overrides listed in HunchBridge/snap_undo.txt ("<fileID> <propertyPath>" per line)
    /// from the habitat prefab instance, so those parts go back to their imported-model positions, then snaps again (door/cove lights kept)
    public static void UndoAndResnap()
    {
        if (EditorApplication.isPlaying) return;
        var prev = EditorSceneManager.GetActiveScene().path; EditorSceneManager.OpenScene("Assets/Unity3/Scenes/01_LunarHabitat.unity");
        var want = new System.Collections.Generic.HashSet<string>(File.ReadAllLines(System.IO.Path.Combine("HunchBridge", "snap_undo.txt")).Select(l => l.Trim()).Where(l => l.Length > 0));
        int removed = 0, roots = 0;
        foreach (var go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (!PrefabUtility.IsAnyPrefabInstanceRoot(go)) continue;
            var mods = PrefabUtility.GetPropertyModifications(go); if (mods == null) continue;
            var keep = new System.Collections.Generic.List<PropertyModification>();
            foreach (var m in mods)
            {
                long id = 0; string g = null;
                if (m.target && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(m.target, out g, out id) && want.Contains(id + " " + m.propertyPath)) { removed++; continue; }
                keep.Add(m);
            }
            if (keep.Count != mods.Length) { PrefabUtility.SetPropertyModifications(go, keep.ToArray()); roots++; }
        }
        EditorSceneManager.MarkAllScenesDirty(); EditorSceneManager.SaveOpenScenes();
        int moved = Snap(true);
        File.AppendAllText(System.IO.Path.Combine("HunchBridge", "ceiling_snap.txt"), $"\nundo: removed {removed} overrides on {roots} prefab roots before re-snapping");
        if (!string.IsNullOrEmpty(prev) && prev != "Assets/Unity3/Scenes/01_LunarHabitat.unity") EditorSceneManager.OpenScene(prev);
    }

    static string Path(Transform t) { var s = t.name; while (t.parent) { t = t.parent; s = t.name + "/" + s; } return s; }
}
