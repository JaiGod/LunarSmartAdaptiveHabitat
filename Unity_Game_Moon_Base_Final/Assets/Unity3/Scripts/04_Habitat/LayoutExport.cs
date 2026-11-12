// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Smart Adaptive Habitat (realistic)
//  File    : Unity3/Scripts/04_Habitat/LayoutExport.cs
//  Section : 04_Habitat
//  Folder  : The base itself: zones and their light/sound/temperature/air, dome status lights,
//            doors and hatches, ladders, loose props in 1/6 g, Moon environment, layout export.
//  Purpose : Exports the real habitat layout (zones, walls footprint, sensors, environmental
//            control nodes, waypoints, Config-B objects) to Assets/Unity3/layout_export.json so the
//            D2 floor plans are drawn to scale from the actual model.
//  Contains: LayoutExport
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hunch3
{
    /// Exports the real habitat layout (zones, walls footprint, sensors, environmental control nodes, waypoints, Config-B objects)
    /// to Assets/Unity3/layout_export.json so the D2 floor plans are drawn to scale from the actual model.
    public class LayoutExport : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded += (s, m) => { if (s.name.Contains("LunarHabitat")) new GameObject("LayoutExport").AddComponent<LayoutExport>(); };
            if (SceneManager.GetActiveScene().name.Contains("LunarHabitat")) new GameObject("LayoutExport").AddComponent<LayoutExport>();
        }
        void Start() { Invoke(nameof(Export), 2.5f); }
        static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
        static string Esc(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "'");

        void Export()
        {
            var sb = new StringBuilder("{\n\"zones\":[\n");
            var env = HabitatEnvironment.I;
            if (env) sb.Append(string.Join(",\n", env.zones.Select(z => $"{{\"name\":\"{Esc(z.name)}\",\"type\":\"{Esc(z.type)}\",\"x\":{F(z.center.x)},\"y\":{F(z.center.y)},\"z\":{F(z.center.z)},\"r\":{F(z.radius)},\"lighting\":\"{Esc(z.lighting)}\",\"purpose\":\"{Esc(z.purpose)}\",\"lights\":{z.lights.Count}}}")));
            sb.Append("\n],\n\"objects\":[\n");
            var root = Util.Find("LunarHabitat_Unity3_Habitat");
            var rs = root ? root.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
            sb.Append(string.Join(",\n", rs.Where(r => r.bounds.size.magnitude > 0.05f).Select(r =>
            {
                var b = r.bounds; var tag = r.GetComponent<HabTag>();
                return $"{{\"n\":\"{Esc(r.name)}\",\"cx\":{F(b.center.x)},\"cy\":{F(b.center.y)},\"cz\":{F(b.center.z)},\"sx\":{F(b.size.x)},\"sy\":{F(b.size.y)},\"sz\":{F(b.size.z)},\"col\":{(r.GetComponent<Collider>() ? 1 : 0)},\"act\":{(r.gameObject.activeInHierarchy ? 1 : 0)},\"i\":\"{Esc(tag ? tag.Interact : "")}\"}}";
            })));
            sb.Append("\n],\n\"waypoints\":[\n");
            sb.Append(string.Join(",\n", Nav.wp.Select(kv => $"{{\"n\":\"{kv.Key}\",\"x\":{F(kv.Value.position.x)},\"y\":{F(kv.Value.position.y)},\"z\":{F(kv.Value.position.z)},\"links\":[{string.Join(",", Nav.adj[kv.Key].Select(l => "\"" + l + "\""))}]}}")));
            sb.Append("\n],\n\"empties\":[\n");
            var empt = root ? root.GetComponentsInChildren<Transform>(true).Where(t => !t.GetComponent<Renderer>() && (t.name.StartsWith("SPAWN_") || t.name.StartsWith("ZONE_") || t.name.StartsWith("X_") || t.name.StartsWith("S_"))) : Enumerable.Empty<Transform>();
            sb.Append(string.Join(",\n", empt.Select(t => $"{{\"n\":\"{Esc(t.name)}\",\"x\":{F(t.position.x)},\"y\":{F(t.position.y)},\"z\":{F(t.position.z)}}}")));
            sb.Append("\n]\n}\n");
            try { System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "Unity3", "layout_export.json"), sb.ToString()); HLog.I("Layout", "layout exported"); } catch (System.Exception e) { Debug.LogWarning(e); }
        }
    }
}
