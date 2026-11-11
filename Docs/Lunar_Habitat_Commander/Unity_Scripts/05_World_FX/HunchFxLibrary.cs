// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/05_World_FX/HunchFxLibrary.cs
//  Section : 05_World_FX
//  Folder  : Living-world effects (Sun, earthshine, storms, doors, rockets) and the friendly extras
//            (robot buddy, stars, badges).
//  Purpose : The effect prefabs taken from imported asset packs (for example Hovl Studio "Magic
//            Effects FREE"). Filled in by the menu HUNCH > Use imported effect packs. Lives in
//            Resources so the built game includes it.
//  Contains: HunchFxLibrary, HunchFx, HunchFxLife
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// The effect prefabs taken from imported asset packs (for example Hovl Studio "Magic Effects FREE").
/// Filled in by the menu HUNCH > Use imported effect packs. Lives in Resources so the built game includes it.
public class HunchFxLibrary : ScriptableObject
{
    public List<string> names = new List<string>();
    public List<GameObject> prefabs = new List<GameObject>();
}

/// Small helper the game uses to play those effects by name. If the pack is missing, nothing happens.
public static class HunchFx
{
    public static bool enabled = true;
    static Dictionary<string, GameObject> map; static bool tried;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { map = null; tried = false; enabled = true; }

    static void Load()
    {
        if (tried) return;
        tried = true; map = new Dictionary<string, GameObject>();
        var lib = Resources.Load<HunchFxLibrary>("HunchFxLibrary");
        if (!lib) { Debug.Log("[HUNCH] no effect library found (HUNCH > Use imported effect packs creates it)"); return; }
        for (int i = 0; i < lib.names.Count && i < lib.prefabs.Count; i++) if (lib.prefabs[i]) map[lib.names[i]] = lib.prefabs[i];
        Debug.Log("[HUNCH] effect library loaded: " + map.Count + " effects");
    }

    public static int Count { get { Load(); return map.Count; } }

    /// The particle material of one of the pack's effects, for effects the game builds itself.
    public static Material MaterialOf(string name)
    {
        Load(); GameObject prefab;
        if (!map.TryGetValue(name, out prefab)) return null;
        var r = prefab.GetComponentInChildren<ParticleSystemRenderer>(true);
        return r ? r.sharedMaterial : null;
    }
    public static bool Has(string name) { Load(); return enabled && map.ContainsKey(name); }
    public static GameObject Prefab(string name) { Load(); GameObject p; return map.TryGetValue(name, out p) ? p : null; }
    public static List<string> NamesStarting(string prefix) { Load(); var l = new List<string>(); foreach (var k in map.Keys) if (k.StartsWith(prefix)) l.Add(k); l.Sort(); return l; }

    /// emitSeconds > 0: a one-off that fades out by itself. emitSeconds <= 0: keeps playing until Stop() is called.
    public static GameObject Spawn(string name, Vector3 pos, float scale, float emitSeconds, Transform parent = null)
    {
        Load();
        GameObject prefab;
        if (!enabled || Application.isBatchMode || Time.timeScale > 2.5f || !map.TryGetValue(name, out prefab)) return null;
        var g = Object.Instantiate(prefab, pos, prefab.transform.rotation);
        g.name = "FX " + name;
        g.transform.localScale = Vector3.one * scale;
        if (parent) g.transform.SetParent(parent, true);
        foreach (var c in g.GetComponentsInChildren<Collider>()) Object.Destroy(c);
        if (emitSeconds > 0f) g.AddComponent<HunchFxLife>().emit = emitSeconds;
        return g;
    }

    public static void Stop(GameObject g)
    {
        if (!g) return;
        g.transform.SetParent(null, true);
        var life = g.GetComponent<HunchFxLife>(); if (!life) life = g.AddComponent<HunchFxLife>();
        life.emit = 0.01f;
    }
}

public class HunchFxLife : MonoBehaviour
{
    public float emit = 1f;
    IEnumerator Start()
    {
        yield return new WaitForSeconds(emit);
        foreach (var ps in GetComponentsInChildren<ParticleSystem>()) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        foreach (var l in GetComponentsInChildren<Light>()) l.enabled = false;
        yield return new WaitForSeconds(4f);
        Destroy(gameObject);
    }
}
