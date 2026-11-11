// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/01_Core/HunchCheats.cs
//  Section : 01_Core
//  Folder  : Game rules and the habitat simulation (crew, systems, alerts), start-up, self-test,
//            cheats and the live link to the Crew Health web page.
//  Purpose : Every shortcut that bends the rules, in one place (Menu > Cheats and anti-cheat). Each
//            way of playing keeps its own four switches:
//  Contains: HunchCheats
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using UnityEngine;

/// Every shortcut that bends the rules, in one place (Menu > Cheats and anti-cheat).
/// Each way of playing keeps its own four switches:
///   Creative (free explore): cheats are allowed, so flying, skipping time and jumping to zones start ON.
///   Survival: every cheat starts OFF. The player may switch one on.
///   NASA HUNCH mode: the player is asked when the mode is switched on, and chooses.
/// Anti-cheat ON switches all of them off at once, in every mode.
public static class HunchCheats
{
    public const int WallsI = 0, FlyI = 1, SkipI = 2, TeleI = 3;
    public static bool Anti;
    public static bool Hunch, Creative;                                 // which set applies right now (kept up to date by the game)
    static readonly bool[,] on = new bool[3, 4];
    static bool ready;
    public static int Mode { get { return Hunch ? 2 : Creative ? 0 : 1; } }
    public static string ModeName { get { return Hunch ? "NASA HUNCH mode" : Creative ? "Creative" : "Survival"; } }

    /// every new game starts from the same settings: NASA HUNCH mode off, anti-cheat off, the cover hidden.
    /// Best scores, the letter style and the sound settings are kept.
    public static void ResetAll()
    {
        foreach (string k in new[] { "hunch_mode2", "hunch_teleport2", "cheat_anti", "cheat_fly", "cheat_skip", "cheat_tele", "cheat_fly_h", "cheat_skip_h", "cheat_walls_h", "hunch_wallpass", "hunch_cover2" }) PlayerPrefs.DeleteKey(k);
        Anti = false; Hunch = false; HunchNavAssist.wallPass = false; ready = false; Load();
    }
    public static void Load()
    {
        if (ready) return; ready = true;
        for (int m = 0; m < 3; m++) for (int i = 0; i < 4; i++) on[m, i] = false;
        on[0, FlyI] = true; on[0, SkipI] = true; on[0, TeleI] = true;      // creative: allowed. Walking through walls stays a switch you flip yourself.
    }
    public static void Save() { }                                        // nothing is kept between games
    public static bool Get(int i) { Load(); return on[Mode, i]; }
    public static void Flip(int i) { Load(); on[Mode, i] = !on[Mode, i]; }
    public static void SetAll(int mode, bool value) { Load(); for (int i = 0; i < 4; i++) on[mode, i] = value; }

    /// pushing through walls
    public static bool Walls { get { return !Anti && Get(WallsI); } }
    /// flying with F or the jetpack
    public static bool FlyOk { get { return !Anti && Get(FlyI); } }
    /// jumping forward in time in free explore
    public static bool SkipOk { get { return !Anti && Get(SkipI); } }
    /// jumping straight to a zone: Tab, Q, G, the number keys, the Travel page, X in the headset
    public static bool TeleOk { get { return !Anti && Get(TeleI); } }
    public static int OnCount { get { if (Anti) return 0; int n = 0; for (int i = 0; i < 4; i++) if (Get(i)) n++; return n; } }
}
