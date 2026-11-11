// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/01_Core/HunchQuiet.cs
//  Section : 01_Core
//  Folder  : Game rules and the habitat simulation (crew, systems, alerts), start-up, self-test,
//            cheats and the live link to the Crew Health web page.
//  Purpose : Keeps the Console clean of notices that come from Unity's own packages and are not
//            faults of this game: - "no eye tracking device" and "hand tracking subsystem not
//            running" (the headset simply has none switched on)
//  Contains: HunchQuiet
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using UnityEngine;

/// Keeps the Console clean of notices that come from Unity's own packages and are not faults of this game:
///   - "no eye tracking device" and "hand tracking subsystem not running" (the headset simply has none switched on)
///   - "Event Camera must be set on World Space Canvas" (the game sets that camera itself a moment later)
///   - the Unity AI package saying it has no subscription
/// Everything else, and every error, goes through untouched.
public class HunchQuiet : ILogHandler
{
    static ILogHandler inner;
    static readonly string[] Skip =
    {
        "Could not find a device that supports eye tracking",
        "Hand Tracking Subsystem not found or not running",
        "Event Camera must be set on World Space Canvas",
        "Error reason is 'NoSubscription'",
        "Deleting invalid font reference",
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Install()
    {
        if (Debug.unityLogger.logHandler is HunchQuiet) return;
        inner = Debug.unityLogger.logHandler; Debug.unityLogger.logHandler = new HunchQuiet();
    }

    public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
    {
        if (logType == LogType.Warning || logType == LogType.Log)
        {
            string s = format ?? "";
            try { if (args != null && args.Length > 0) s = string.Format(format, args); } catch { if (args != null && args.Length > 0 && args[0] != null) s = args[0].ToString(); }
            for (int i = 0; i < Skip.Length; i++) if (s.IndexOf(Skip[i], StringComparison.Ordinal) >= 0) return;
        }
        if (inner != null) inner.LogFormat(logType, context, format, args);
    }
    public void LogException(Exception exception, UnityEngine.Object context) { if (inner != null) inner.LogException(exception, context); }
}
