// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/04_UI_Menus_Audio/HunchVoice.cs
//  Section : 04_UI_Menus_Audio
//  Folder  : Menus, rounded UI, story intro, Star Store, music, voices (TTS/commands) and picture
//            quality.
//  Purpose : Voice for the game, using what Windows already has built in: - ARIA and the astronauts
//            SPEAK their lines through the Windows text-to-speech voices (on this PC)
//  Contains: HunchVoice
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using UnityEngine.Windows.Speech;
#endif

/// Voice for the game, using what Windows already has built in:
///  - ARIA and the astronauts SPEAK their lines through the Windows text-to-speech voices (on this PC)
///  - COMMANDS: a fixed list of spoken phrases is recognised through the microphone (on this PC; off until switched on in the menu)
///  - DICTATION: while the talk panel is open, free speech is turned into text ("speech to text"). This part uses Windows
///    online speech recognition, so it needs internet and Windows Settings > Privacy > Speech > Online speech recognition,
///    and the audio is processed by Microsoft's speech service. If it is not available, commands still work.
/// On other platforms, or if Windows speech is not available, the game simply stays text-only.
public class HunchVoice : MonoBehaviour
{
    public bool speakOn = true;
    public bool SpeakAvailable { get; private set; }
    public bool Listening { get; private set; }
    public string ListenProblem { get; private set; } = "";
    public string LastHeard { get; private set; } = "";
    public Action<string> onCommand;                         // receives the command key of a recognised phrase
    public Action<string> onDictation;                       // receives a finished sentence of free speech
    public bool Dictating { get; private set; }
    public string Hypothesis { get; private set; } = "";      // what it thinks you are saying, while you are still speaking
    public string DictationProblem { get; private set; } = "";
    float deafUntil;                                         // the microphone ignores the game's own voice
    readonly Queue<string> spoken = new Queue<string>(); string hypIn; bool dictEnded, wantDictation;

    readonly Dictionary<string, string> phrases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    readonly Queue<string> heard = new Queue<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var g = FindFirstObjectByType<HabitatGame>();
        if (g && !g.GetComponent<HunchVoice>()) g.gameObject.AddComponent<HunchVoice>();
    }

    public void AddPhrase(string spoken, string command) { phrases[spoken] = command; }

    // ------------------------------------------------------------------ speaking
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    System.Diagnostics.Process tts; bool ttsTried;
    // One small helper process that reads lines "voice|rate|text" and speaks them with the Windows voices.
    const string Script = "Add-Type -AssemblyName System.Speech; $s = New-Object System.Speech.Synthesis.SpeechSynthesizer; $v = @($s.GetInstalledVoices() | Where-Object { $_.Enabled } | ForEach-Object { $_.VoiceInfo.Name }); while ($true) { $l = [Console]::In.ReadLine(); if ($l -eq $null) { break }; $p = $l.Split('|', 3); if ($p.Length -lt 3) { continue }; $s.SpeakAsyncCancelAll(); if ($p[2] -eq '') { continue }; try { if ($v.Count -gt 0) { $s.SelectVoice($v[[int]$p[0] % $v.Count]) }; $s.Rate = [int]$p[1]; [void]$s.SpeakAsync($p[2]) } catch {} }";

    void StartTts()
    {
        if (ttsTried) return;
        ttsTried = true;
        if (Application.isBatchMode) return;
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("powershell.exe", "-NoLogo -NoProfile -NonInteractive -Command \"" + Script + "\"")
            { UseShellExecute = false, RedirectStandardInput = true, CreateNoWindow = true };
            tts = System.Diagnostics.Process.Start(psi);
            SpeakAvailable = tts != null && !tts.HasExited;
        }
        catch (Exception e) { tts = null; SpeakAvailable = false; Debug.Log("[HUNCH] voice output not available: " + e.Message); }
    }

    void Send(string line)
    {
        try { if (tts != null && !tts.HasExited) { tts.StandardInput.WriteLine(line); tts.StandardInput.Flush(); } else SpeakAvailable = false; }
        catch { SpeakAvailable = false; }
    }

    void StopTts()
    {
        try { if (tts != null && !tts.HasExited) { tts.StandardInput.Close(); if (!tts.WaitForExit(400)) tts.Kill(); } } catch { }
        tts = null;
    }
#else
    void StartTts() { }
    void Send(string line) { }
    void StopTts() { }
#endif

    static string Plain(string s)
    {
        s = System.Text.RegularExpressions.Regex.Replace(s ?? "", "<[^>]+>", " ");
        var sb = new StringBuilder(s.Length);
        foreach (char ch in s) sb.Append(ch == '\n' || ch == '\r' || ch == '|' || ch == '"' ? ' ' : (ch < 32 || ch > 126 ? ' ' : ch));
        return sb.ToString().Replace("ARIA:", "").Replace("CO2", "C O 2").Replace(" O2", " oxygen").Replace("ECLSS", "E C L S S").Replace("EVA", "E V A").Trim();
    }

    /// voice 0 = ARIA, 1-4 = the astronauts. Starting a new line stops the one being spoken.
    public void Say(string text, int voice = 0)
    {
        if (!speakOn || Time.timeScale > 2.5f) return;
        StartTts();
        if (!SpeakAvailable) return;
        int index = voice == 0 ? 1 : voice;              // ARIA uses the second installed voice when there is one
        int rate = voice == 0 ? 1 : (voice % 2 == 0 ? 0 : 2);
        string plain = Plain(text);
        Send(index + "|" + rate + "|" + plain);
        deafUntil = Time.unscaledTime + 0.9f + plain.Length / 13f;      // about how long the line takes to say
    }

    public void Hush() { if (SpeakAvailable) Send("0|0|"); }

    // for the development test helper: starts the voice helper, sends it a silent line, and says whether it is still running
    public string ProbeStart() { StartTts(); Send("0|0|"); return "started available=" + SpeakAvailable; }
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    public string ProbeAlive() { try { return tts == null ? "no process" : (tts.HasExited ? "EXITED code " + tts.ExitCode : "alive"); } catch (Exception e) { return "error " + e.Message; } }
#else
    public string ProbeAlive() { return "not windows"; }
#endif

    // ------------------------------------------------------------------ listening
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    KeywordRecognizer rec;

    public bool StartListening()
    {
        if (Listening) return true;
        ListenProblem = "";
        try
        {
            if (Application.isBatchMode) { ListenProblem = "not available here"; return false; }
            if (!PhraseRecognitionSystem.isSupported) { ListenProblem = "Windows speech recognition is not available on this PC"; return false; }
            if (Microphone.devices == null || Microphone.devices.Length == 0) { ListenProblem = "no microphone found"; return false; }
            var list = new List<string>(phrases.Keys);
            if (list.Count == 0) { ListenProblem = "no commands set up"; return false; }
            rec = new KeywordRecognizer(list.ToArray(), ConfidenceLevel.Medium);
            rec.OnPhraseRecognized += a => { lock (heard) heard.Enqueue(a.text); };
            rec.Start();
            Listening = rec.IsRunning;
            if (!Listening) ListenProblem = "the recogniser did not start (check Windows Settings > Privacy > Microphone and Speech)";
        }
        catch (Exception e) { ListenProblem = e.Message; Listening = false; rec = null; }
        if (!Listening) Debug.Log("[HUNCH] voice commands not available: " + ListenProblem);
        return Listening;
    }

    public void StopListening()
    {
        StopDictation();
        try { if (rec != null) { if (rec.IsRunning) rec.Stop(); rec.Dispose(); } } catch { }
        rec = null; Listening = false;
    }

    // ---- free speech to text. Windows cannot run this and the command list at the same moment, so they take turns.
    DictationRecognizer dict;

    public bool StartDictation()
    {
        wantDictation = true; DictationProblem = "";
        if (Dictating) return true;
        try
        {
            if (Application.isBatchMode || !PhraseRecognitionSystem.isSupported) { DictationProblem = "Windows speech recognition is not available"; return false; }
            if (Microphone.devices == null || Microphone.devices.Length == 0) { DictationProblem = "no microphone found"; return false; }
            if (PhraseRecognitionSystem.Status == SpeechSystemStatus.Running) PhraseRecognitionSystem.Shutdown();
            dict = new DictationRecognizer();
            dict.InitialSilenceTimeoutSeconds = 30f; dict.AutoSilenceTimeoutSeconds = 30f;
            dict.DictationHypothesis += t => { lock (spoken) hypIn = t; };
            dict.DictationResult += (t, c) => { lock (spoken) { spoken.Enqueue(t); hypIn = ""; } };
            dict.DictationComplete += cause => { lock (spoken) dictEnded = true; if (cause != DictationCompletionCause.Complete && cause != DictationCompletionCause.TimeoutExceeded && cause != DictationCompletionCause.PauseLimitExceeded && cause != DictationCompletionCause.Canceled) DictationProblem = "speech to text stopped (" + cause + ")"; };
            dict.DictationError += (e, h) => { DictationProblem = "speech to text needs internet and Windows Settings > Privacy > Speech > Online speech recognition"; lock (spoken) dictEnded = true; };
            dict.Start();
            Dictating = true; dictEnded = false;
        }
        catch (Exception e) { DictationProblem = e.Message; EndDictation(); }
        if (!Dictating) Debug.Log("[HUNCH] speech to text not available: " + DictationProblem);
        return Dictating;
    }

    void EndDictation()
    {
        try { if (dict != null) { if (dict.Status == SpeechSystemStatus.Running) dict.Stop(); dict.Dispose(); } } catch { }
        dict = null; Dictating = false; Hypothesis = "";
        try { if (rec != null && PhraseRecognitionSystem.Status != SpeechSystemStatus.Running) PhraseRecognitionSystem.Restart(); } catch { }
    }

    public void StopDictation() { wantDictation = false; if (dict != null || Dictating) EndDictation(); }
#else
    public bool StartListening() { ListenProblem = "voice commands need Windows"; return false; }
    public void StopListening() { Listening = false; }
    public bool StartDictation() { DictationProblem = "speech to text needs Windows"; return false; }
    public void StopDictation() { }
    void EndDictation() { }
#endif

    void Update()
    {
        bool deaf = Time.unscaledTime < deafUntil;
        while (true)
        {
            string text;
            lock (heard) { if (heard.Count == 0) break; text = heard.Dequeue(); }
            if (deaf) continue;
            LastHeard = text;
            string cmd;
            if (phrases.TryGetValue(text, out cmd) && onCommand != null) onCommand(cmd);
        }
        if (Dictating || wantDictation)
        {
            bool ended; string hyp;
            lock (spoken) { ended = dictEnded; dictEnded = false; hyp = hypIn; }
            Hypothesis = deaf ? "" : (hyp ?? "");
            while (true)
            {
                string text;
                lock (spoken) { if (spoken.Count == 0) break; text = spoken.Dequeue(); }
                if (deaf || string.IsNullOrEmpty(text)) continue;
                LastHeard = text;
                if (onDictation != null) onDictation(text);
            }
            if (ended) { bool again = wantDictation && DictationProblem.Length == 0; EndDictation(); if (again) StartDictation(); }     // Windows ends a session after a long silence: start the next one
        }
    }

    void OnDestroy() { StopListening(); StopTts(); }
    void OnApplicationQuit() { StopListening(); StopTts(); }
}
