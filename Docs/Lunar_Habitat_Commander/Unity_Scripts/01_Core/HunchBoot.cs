// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/01_Core/HunchBoot.cs
//  Section : 01_Core
//  Folder  : Game rules and the habitat simulation (crew, systems, alerts), start-up, self-test,
//            cheats and the live link to the Crew Health web page.
//  Purpose : Most parts of the game add themselves to the scene when the game starts. "Restart"
//            loads the scene again, and without this they would not come back: the menu lost
//            Explorer, Base systems and Maps, and the adventure layer was gone.
//  Contains: HunchBoot
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Most parts of the game add themselves to the scene when the game starts. "Restart" loads the scene again, and without
/// this they would not come back: the menu lost Explorer, Base systems and Maps, and the adventure layer was gone.
/// This runs every part's own start-up again each time a scene is loaded. Each one checks that it is not already there.
public static class HunchBoot
{
    static readonly string[] Parts = { "HunchLook", "HunchWorldFX", "HunchAria", "HunchVoice", "HunchGuide", "HunchFrontier", "HunchLink", "HunchMusic" };
    static bool first = true;
    public static int Restarts { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { first = true; Restarts = 0; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook() { SceneManager.sceneLoaded -= OnScene; SceneManager.sceneLoaded += OnScene; }

    static void OnScene(Scene s, LoadSceneMode mode)
    {
        if (first) { first = false; return; }      // the very first load: every part starts itself as before
        if (mode != LoadSceneMode.Single) return;
        Restarts++;
        foreach (var name in Parts)
        {
            try
            {
                var t = Type.GetType(name); if (t == null) continue;
                var m = t.GetMethod("Boot", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public, null, Type.EmptyTypes, null);
                if (m != null) m.Invoke(null, null);
            }
            catch (Exception e) { Debug.LogWarning("[HUNCH] could not restart " + name + ": " + (e.InnerException ?? e).Message); }
        }
        Debug.Log("[HUNCH] scene loaded again (restart " + Restarts + "): game parts re-attached");
    }
}
