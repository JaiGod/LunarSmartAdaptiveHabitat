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

    public static int Snap(bool apply, bool save = true)
    {
        var log = new System.Text.StringBuilder(); int moved = 0, flush = 0, skipped = 0;
        bool bf = Physics.queriesHitBackfaces; Physics.queriesHitBackfaces = true;      // the hull meshes are single-sided
        Physics.SyncTransforms(); var done = new System.Collections.Generic.HashSet<Transform>();
        foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (done.Contains(mr.transform)) continue; var m = mr.sharedMaterial; bool named = mr.name.Contains("CeilingLight") || mr.name.Contains("Ceiling_Light"); if (!named && (!m || !m.name.StartsWith("Emit_LED"))) continue;   // ceiling LED panels / rings (not grow lights, not mood strips)
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

    static string Path(Transform t) { var s = t.name; while (t.parent) { t = t.parent; s = t.name + "/" + s; } return s; }
}
