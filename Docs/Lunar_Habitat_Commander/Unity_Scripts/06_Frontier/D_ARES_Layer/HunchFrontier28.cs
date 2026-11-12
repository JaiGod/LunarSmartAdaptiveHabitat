// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/06_Frontier/D_ARES_Layer/HunchFrontier28.cs
//  Section : 06_Frontier/D_ARES_Layer
//  Purpose : SIDE MISSION "Crew Health Rounds" — the six D1 health devices (Blender models in
//            Resources/Health) placed in the base, each with its own interaction:
//              Crew Health Station (Medical) · crew beacon + cortisol patch (Medical display)
//              night wristband + smart sleep bunk (Crew Quarters) · zone sensor pod (Galley)
//            Visit all six in any order: each shows live readings from the ARES mood engine and logs
//            to the AI log; finishing all six gives a banner. Progress is on the Project ARES checklist.
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public partial class HunchFrontier
{
    static readonly string[] HRKey = { "health_station", "crew_beacon", "cortisol_patch", "sleep_wristband", "sleep_bunk", "zone_sensor_pod" };
    static readonly string[] HRName = { "Crew Health Station", "Crew beacon (chest)", "Cortisol patch", "Night wristband", "Smart sleep bunk", "Zone sensor pod" };
    static readonly string[][] HRZone = { new[] { "medical" }, new[] { "medical" }, new[] { "medical" }, new[] { "quarters", "sleep" }, new[] { "quarters", "sleep" }, new[] { "galley", "hub" } };
    static readonly float[] HRSize = { 2.0f, 0.5f, 0.35f, 0.4f, 2.2f, 0.7f };            // biggest side in metres (small items are shown enlarged on a plinth)
    static readonly bool[] HRPlinth = { false, true, true, true, false, true };
    readonly bool[] hrDone = new bool[6]; readonly TextMeshPro[] hrTag = new TextMeshPro[6]; bool hrBonus; string hrPlaced = "";

    void BuildHealthRounds()
    {
        var hr = new GameObject("Crew Health Rounds").transform; hr.SetParent(aresRoot ? aresRoot : root, false);
        var plinthM = Lit(new Color(0.86f, 0.88f, 0.92f)); var ringM = Glow(new Color(0.6f, 0.4f, 2.2f));
        var used = new List<Vector3>();
        for (int k = 0; k < 6; k++)
        {
            int z = ZoneIndex(HRZone[k]); if (z < 0) { hrPlaced += HRKey[k] + ": no zone; "; continue; }
            Vector3 c = game.zones[z].position; Vector3 spot; if (!FreeSpot(c, used, out spot)) { hrPlaced += HRKey[k] + ": no space; "; continue; }
            used.Add(spot);
            var p = new GameObject("Health · " + HRName[k]).transform; p.SetParent(hr, false); p.position = spot;
            Vector3 look = c - spot; look.y = 0f; if (look.sqrMagnitude > 0.01f) p.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
            float baseY = 0f;
            if (HRPlinth[k]) { Prim(PrimitiveType.Cylinder, p, new Vector3(0f, 0.45f, 0f), new Vector3(0.5f, 0.45f, 0.5f), plinthM); Prim(PrimitiveType.Cylinder, p, new Vector3(0f, 0.905f, 0f), new Vector3(0.54f, 0.01f, 0.54f), ringM); baseY = 0.92f; }
            var pf = Resources.Load<GameObject>("Health/" + HRKey[k]);
            if (pf)
            {
                var g = Instantiate(pf, p); g.name = HRKey[k]; foreach (var col in g.GetComponentsInChildren<Collider>(true)) Destroy(col);
                var rs = g.GetComponentsInChildren<Renderer>(); if (rs.Length > 0)
                {
                    var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                    float big = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)); if (big > 1e-4f) g.transform.localScale *= HRSize[k] / big;
                    b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                    g.transform.position += new Vector3(p.position.x - b.center.x, p.position.y + baseY + 0.01f - b.min.y, p.position.z - b.center.z);
                }
            }
            else Prim(PrimitiveType.Cube, p, new Vector3(0f, baseY + 0.2f, 0f), Vector3.one * 0.4f, Glow(new Color(0.6f, 0.4f, 2.2f)));
            float h = baseY + (HRPlinth[k] ? HRSize[k] : HRSize[k] * 0.9f);
            FacButton(p, new Vector3(0f, Mathf.Max(0.6f, h * 0.5f), 0f), new Vector3(1.2f, Mathf.Max(1.2f, h), 1.2f), Clear(new Color(1f, 1f, 1f, 0f)), 1900 + k);
            hrTag[k] = TagLabel(p, new Vector3(0f, h + 0.45f, 0f), 0.9f, "", Color.white);
            hrPlaced += HRKey[k] + " @" + game.zones[z].id + "; ";
        }
        for (int k = 0; k < 6; k++) { int kk = k; HAdd("CREW HEALTH ROUNDS (side mission)", HRName[k], "Find it in " + HRZone[k][0] + " and use it.", () => hrDone[kk]); }
        UpdateHealthTags();
        HLog.I("ARES", "health rounds placed: " + hrPlaced);
    }

    bool FreeSpot(Vector3 c, List<Vector3> used, out Vector3 spot)
    {
        Physics.SyncTransforms();
        for (int ring = 0; ring < 4; ring++)
            for (int a = 0; a < 12; a++)
            {
                float r = 1.8f + ring * 1.1f, ang = (a * 30f + ring * 15f) * Mathf.Deg2Rad;
                var p = c + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
                RaycastHit hit; if (!Physics.Raycast(p + Vector3.up * 2.5f, Vector3.down, out hit, 6f)) continue;
                p.y = hit.point.y;
                if (Mathf.Abs(p.y - c.y) > 1.2f) continue;
                if (used.Any(u => (u - p).sqrMagnitude < 1.6f * 1.6f)) continue;
                if (Physics.CheckBox(p + Vector3.up * 1.1f, new Vector3(0.6f, 0.95f, 0.6f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) continue;
                spot = p; return true;
            }
        spot = c; return false;
    }

    void UpdateHealthTags()
    {
        for (int k = 0; k < 6; k++) if (hrTag[k]) hrTag[k].text = "<b>" + HRName[k] + "</b>\n<size=75%>" + (hrDone[k] ? "<color=#7CFC9A>checked</color>" : "<color=#C9A8FF>Health Rounds · use it</color>") + "</size>";
    }

    string HealthText(int i) { int k = i - 1900; return k >= 0 && k < 6 ? "health rounds: " + HRName[k] : ""; }

    void HealthUse(int i)
    {
        int k = i - 1900; if (k < 0 || k >= 6) return;
        var me = ares != null ? ares.C(1) : null;
        float st = me != null ? me.stress : 0.25f, md = me != null ? me.mood : 0.7f, fa = me != null ? me.fatigue : 0.2f, sq = me != null ? me.sleepQ : 0.85f, co = me != null ? me.cortisol : 0.3f;
        int hrBpm = Mathf.RoundToInt(60 + st * 28 + fa * 6), hrv = Mathf.RoundToInt(62 - st * 35);
        string msg;
        switch (k)
        {
            case 0: msg = "Check-in saved: BP " + Mathf.RoundToInt(114 + st * 18) + "/" + Mathf.RoundToInt(74 + st * 10) + " · HR " + hrBpm + " · SpO2 98% · 36.7 °C · ECG normal sinus rhythm. Medical numbers go to the flight surgeon; mood data does not."; break;
            case 1: msg = "Crew beacon: HR " + hrBpm + " bpm · HRV " + hrv + " ms" + (hrv < 45 ? " — low HRV = rising stress (compared with YOUR baseline)." : " — healthy for you."); break;
            case 2: msg = "Cortisol patch: " + (co > 0.5f ? "flatter daily curve than normal — chronic stress flag sent to YOU first." : "normal morning peak, low at night.") + " Fresh 48 h patch fitted."; if (me != null) me.stress = Mathf.Max(0f, me.stress - 0.01f); break;
            case 3: msg = "Night wristband: sleep score " + Mathf.RoundToInt(sq * 100) + " · deep " + (sq * 1.6f).ToString("0.0") + " h · REM " + (sq * 2.0f).ToString("0.0") + " h" + (sq < 0.7f ? " — below your baseline: early night + 1800 K light tonight." : " — at your baseline."); break;
            case 4: msg = "Smart bunk: mattress sensor " + (sq * 8.2f).ToString("0.0") + " h asleep, 3 position changes; mood light set to wind-down at 21:00, privacy curtain on."; break;
            default: msg = "Zone pod (shared zone, on-device): voice " + (md < 0.45f ? "flat and slow" : "bright") + ", face " + (st > 0.5f ? "tense" : "relaxed") + ", CO2 OK. Only you see this unless you share it."; if (me != null) me.mood = Mathf.Min(1f, me.mood + 0.02f); break;
        }
        sim.Say("ARIA: " + msg);
        if (!hrDone[k])
        {
            hrDone[k] = true; int n = hrDone.Count(d => d);
            if (ares != null) ares.Log(1, "health", "Health Rounds " + n + "/6: " + HRName[k]);
            Note("SIDE MISSION · Crew Health Rounds " + n + "/6");
            if (n == 6 && !hrBonus) { hrBonus = true; Banner("CREW HEALTH ROUNDS COMPLETE", "All six health devices checked — this is the data ARIA uses (with consent)", false); foreach (var c in sim.crew) { c.stress -= 3f; c.Clamp(); } }
            UpdateHealthTags();
        }
    }
}
