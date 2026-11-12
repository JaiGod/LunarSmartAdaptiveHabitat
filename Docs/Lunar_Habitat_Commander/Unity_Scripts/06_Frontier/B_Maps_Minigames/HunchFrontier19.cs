// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/06_Frontier/B_Maps_Minigames/HunchFrontier19.cs
//  Section : 06_Frontier/B_Maps_Minigames
//  Folder  : Extra maps and mini-games: solar farm, mining pit, Factory Land, Star Patrol, crew
//            radio + Star Clicker, Science Academy, My Astronaut, model kits, Fun Zone, endless
//            ground, Star Store pages.
//  Purpose : Nineteenth part of Moon Frontier (full game only; NASA HUNCH mode hides all of it): -
//            four more tools, each with a model made in Blender: Plasma repeater, Laser cannon,
//            Star minigun, Comet sword.
//  Contains: HunchFrontier, SliceObj, SliceHalf
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// Nineteenth part of Moon Frontier (full game only; NASA HUNCH mode hides all of it):
///   - four more tools, each with a model made in Blender: Plasma repeater, Laser cannon, Star minigun, Comet sword.
///     Like the zapper they are kid-friendly: an alien that is hit is beamed home, nobody is hurt.
///   - the Fun Zone, three pads on open ground by the mining field:
///       EMOTE STAGE     try every dance move and emote, one button each
///       ZERO-G DOME     an anti-gravity module: you float, loose things drift, and there are floating stars to catch
///       SPACE SLICER    asteroids, ice and comets are tossed up; slice them with the Comet sword. Do not slice the satellites.
public partial class HunchFrontier
{
    // ================================================================== more tools
    public const int PlasmaRep = 6, Laser = 7, Minigun = 8, Sword = 9;
    static readonly string[] ExtraGunModel = { "PlasmaRepeater", "LaserCannon", "StarMinigun", "CometSword", "NinjaStar" };
    GameObject[] extraGun; Material beamRed, beamGold; int extraShown = -2;

    void ExtraGunShow(int eff)
    {
        if (extraGun == null)
        {
            extraGun = new GameObject[ExtraGunModel.Length];
            for (int i = 0; i < ExtraGunModel.Length; i++)
            {
                try
                {
                    var src = KitSource(false, ExtraGunModel[i]); if (!src) continue;
                    var g = Instantiate(src); g.name = ExtraGunModel[i]; g.SetActive(true); g.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 180f, 0f) * src.transform.localRotation);
                    foreach (var c in g.GetComponentsInChildren<Collider>(true)) Destroy(c);
                    var b = BoundsOf(g); float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)); if (longest < 0.0001f) { Destroy(g); continue; }
                    var holder = new GameObject("View " + ExtraGunModel[i]); holder.transform.SetPositionAndRotation(b.center, Quaternion.identity); g.transform.SetParent(holder.transform, true);
                    holder.transform.localScale = Vector3.one * ((i == 3 ? 0.95f : (i == 4 ? 0.26f : 0.46f)) / longest);
                    holder.transform.SetParent(designRoot, false); holder.transform.localPosition = i == 3 ? new Vector3(0.02f, 0.1f, 0.3f) : new Vector3(0f, -0.02f, 0.12f);
                    holder.transform.localRotation = i == 3 ? Quaternion.Euler(-55f, 0f, 0f) : Quaternion.identity;      // the sword is held blade up and forward
                    foreach (var r in holder.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    holder.SetActive(false); extraGun[i] = holder;
                }
                catch (Exception e) { HLog.W("Minigames", "tool model " + ExtraGunModel[i] + ": " + e.Message); }
            }
        }
        if (eff == extraShown) return; extraShown = eff;
        for (int i = 0; i < extraGun.Length; i++) if (extraGun[i]) extraGun[i].SetActive(eff == PlasmaRep + i);
    }

    string ExtraToolTip(string fire)
    {
        switch (tool)
        {
            case PlasmaRep: return "  -  hold " + fire + ": plasma balls, one after another";
            case Laser: return "  -  hold " + fire + ": a steady red laser, three times as strong as the zapper";
            case Minigun: return "  -  hold " + fire + ": a stream of little star bolts";
            case Sword: return "  -  " + fire + " to swing; hold it and sweep in Space Slicer";
            case NinjaTool: return "  -  " + fire + " throws a spinning star: it flies in an arc and can ricochet once";
            default: return "";
        }
    }

    /// the four new tools; true when one of them took the trigger
    bool ExtraToolFire(Ray ray, bool held, bool pressed)
    {
        switch (tool)
        {
            case PlasmaRep: if (held && cool <= 0f && ballLife <= 0f) { cool = 0.3f; FirePlasma(ray); } return true;
            case Laser: if (held && cool <= 0f) { cool = 0.07f; if (!beamRed) beamRed = Glow(new Color(3.2f, 0.45f, 0.35f)); FireShot(ray, 3f, beamRed, 0f, 0.22f); } return true;
            case Minigun: if (held && cool <= 0f) { cool = 0.06f; if (!beamGold) beamGold = Glow(new Color(3f, 2.3f, 0.5f)); FireShot(ray, 0.6f, beamGold, 1.5f, 0.16f); } return true;
            case Sword: if (pressed && cool <= 0f) { cool = 0.4f; SwordSwing(ray); } return true;
            case NinjaTool: if (pressed && cool <= 0f) { cool = 0.28f; NinjaThrow(ray); } return true;
        }
        return false;
    }

    void FireShot(Ray ray, float power, Material mat, float spreadDeg, float volume)
    {
        if (spreadDeg > 0f) ray.direction = Quaternion.AngleAxis(R(-spreadDeg, spreadDeg), cam.up) * Quaternion.AngleAxis(R(-spreadDeg, spreadDeg), cam.right) * ray.direction;
        float range = mapNow == 8 ? 700f : 70f; RaycastHit h; Vector3 end = ray.origin + ray.direction * range;
        if (Cast(ray, out h, range))
        {
            end = h.point; var tg = h.collider.GetComponentInParent<HunchTarget>();
            if (tg && tg.kind == 1 && tg.index < aliens.Count) HitAlien(aliens[tg.index], zapLv * power, false);
            else if (tg && tg.kind == 32) ZrHit(tg.index);
            else if (tg && tg.kind == 36) { if (tg.index >= 200) BossHit(tg.index - 200); else StarHit(tg.index); }
            else if (tg && tg.kind == 37) SliceHit(tg.index);
            else if (mapNow == 3) zrCombo = 0;
        }
        if (mapNow == 3 && mgOn) zrShots++;
        recoil = 0.45f; ShowBeam(Muzzle(), end, mat); Play(cZap, volume); game.Buzz(0.2f, 0.03f);
    }

    void SwordSwing(Ray ray)
    {
        recoil = 2.4f; Play(cPop, 0.5f); game.Buzz(0.5f, 0.08f);
        Vector3 from = cam.position, fw = ray.direction;
        for (int i = 0; i < aliens.Count; i++)
        {
            var a = aliens[i]; if (a == null || !a.alive || !a.t) continue; Vector3 to = a.t.position + Vector3.up * 0.5f - from;
            if (to.magnitude < 3.6f && Vector3.Angle(fw, to) < 65f) HitAlien(a, zapLv * 4f, false);
        }
        RaycastHit h;
        if (Cast(ray, out h, 3.8f))
        {
            var tg = h.collider.GetComponentInParent<HunchTarget>();
            if (tg && tg.kind == 32) ZrHit(tg.index); else if (tg && tg.kind == 36) { if (tg.index >= 200) BossHit(tg.index - 200); else StarHit(tg.index); }
        }
        SliceSweep(ray, 16f, 9f);
    }

    // ================================================================== emotes
    public static readonly string[] EmoteNames = { "Arm wave", "Spin", "Side sway", "Star jump", "Hello wave", "Robot", "Floss", "Cheer", "Moon shuffle", "Twist" };
    int emoteMove = -1;
    public void Emote(int k)
    {
        if (!Playing) return;
        emoteMove = Mathf.Clamp(k, 0, EmoteNames.Length - 1); danceStart = Time.time; danceUntil = Time.time + 6f; danced = true;
        if (!game.HeadsetActive && !thirdPerson) { danceAuto = true; SetThird(true); }
        Note("Emote: " + EmoteNames[emoteMove]); Play(cPop, 0.5f);
    }
    void EmotePose(int move, float t, float b, ref float sL, ref float sR, ref float aL, ref float aR, ref float lL, ref float lR, ref float roll, ref float bounce, ref float spin, ref float spread)
    {
        switch (move)
        {
            case 4: sR = 150f + 25f * Mathf.Sin(t * 9f); sL = -8f; roll = 3f * Mathf.Sin(t * 3f); break;
            case 5: { float q = Mathf.Round(Mathf.Sin(t * 5f)); aL = 80f * q; aR = -80f * q; sL = -10f; sR = 10f; spin = 20f * Mathf.Round(Mathf.Sin(t * 2.5f)); break; }
            case 6: sL = 55f * b; sR = 55f * b; roll = -12f * b; aL = 25f * b; aR = 25f * b; break;
            case 7: bounce = Mathf.Abs(Mathf.Sin(t * Mathf.PI * 2f)) * 0.25f; sL = -170f + 10f * b; sR = 170f - 10f * b; spread = 8f; break;
            case 8: lL = 30f * Mathf.Sin(t * 6f); lR = -30f * Mathf.Sin(t * 6f); roll = 4f * b; sL = -15f; sR = 15f; bounce = 0.02f; break;
            default: spin = 40f * Mathf.Sin(t * 6f); sL = -70f; sR = 70f; bounce = Mathf.Abs(b) * 0.04f; break;
        }
    }

    // ================================================================== the Fun Zone
    Transform funRoot; bool stageOk, zeroOk, sliceOk; Vector3 stageAt, zeroAt, sliceAt; string funLog = "";
    const float ZeroR = 7f;

    void BuildFunZone()
    {
        funRoot = new GameObject("Fun zone").transform; funRoot.SetParent(root, false);
        var deck = Lit(new Color(0.22f, 0.24f, 0.3f)); var gold = Lit(new Color(0.83f, 0.62f, 0.2f)); var post = Lit(new Color(0.75f, 0.77f, 0.82f));
        Vector3 g; FindMoonbases();
        // every pad stands at one of the five other moonbases (or by the main base when that ground is not free)
        // ---- emote stage
        if (BaseSpot(2, 7f, out g))
        {
            stageOk = true; stageAt = g; var st = new GameObject("Emote stage").transform; st.SetParent(funRoot, false); st.position = g;
            Vector3 toField = fieldC - g; toField.y = 0f; if (toField.sqrMagnitude < 1f) toField = Vector3.forward; st.rotation = Quaternion.LookRotation(toField.normalized, Vector3.up);
            Prim(PrimitiveType.Cylinder, st, new Vector3(0f, 0.15f, 0f), new Vector3(9f, 0.15f, 9f), deck); Solid(st, new Vector3(0f, 0.15f, 0f), new Vector3(6.4f, 0.3f, 6.4f));      // a flat box to stand on (a squashed cylinder's own collider is a big ball)
            Prim(PrimitiveType.Cylinder, st, new Vector3(0f, 0.31f, 0f), new Vector3(9.3f, 0.02f, 9.3f), Glow(new Color(2.4f, 0.8f, 2.6f)));
            Prim(PrimitiveType.Cylinder, st, new Vector3(0f, 0.32f, 0f), new Vector3(8.6f, 0.021f, 8.6f), deck);
            Color[] lamp = { new Color(2.6f, 0.6f, 0.6f), new Color(0.6f, 2.4f, 0.8f), new Color(0.6f, 1.2f, 2.8f), new Color(2.6f, 2.2f, 0.5f) };
            for (int k = 0; k < 4; k++) { float a = (-60f + k * 40f) * Mathf.Deg2Rad; var p = new Vector3(Mathf.Sin(a) * 5.2f, 0f, -Mathf.Cos(a) * 5.2f); Prim(PrimitiveType.Cylinder, st, p + Vector3.up * 2.2f, new Vector3(0.12f, 2.2f, 0.12f), post); Prim(PrimitiveType.Sphere, st, p + Vector3.up * 4.5f, Vector3.one * 0.55f, Glow(lamp[k])); }
            var sign = Label(st, "<b>EMOTE STAGE</b>\n<size=55%>Step up and press a button to try a move. B is still the dance party.</size>", 0f, 0f, 5f, 40f, 5f, new Color(1f, 0.84f, 0.3f)); sign.transform.localPosition = new Vector3(0f, 6.4f, -5.2f); mapLabels.Add(sign);
            for (int k = 0; k < EmoteNames.Length; k++)
            {
                float a = (-72f + k * 16f) * Mathf.Deg2Rad; var p = new Vector3(Mathf.Sin(a) * 3.6f, 0.3f, Mathf.Cos(a) * 3.6f);
                var ped = new GameObject("Emote button " + k).transform; ped.SetParent(st, false); ped.localPosition = p; ped.localRotation = Quaternion.LookRotation(new Vector3(-p.x, 0f, -p.z).normalized, Vector3.up);
                Prim(PrimitiveType.Cylinder, ped, new Vector3(0f, 0.45f, 0f), new Vector3(0.22f, 0.45f, 0.22f), post, true);
                Color c = Color.HSVToRGB(k / (float)EmoteNames.Length, 0.65f, 1f);
                FacButton(ped, new Vector3(0f, 0.98f, 0f), new Vector3(0.42f, 0.14f, 0.42f), Glow(c * 1.8f), 1600 + k);
                var l = Label(ped, "<b>" + EmoteNames[k] + "</b>", 0f, 0f, 0.9f, 3f, 0.5f, Color.white); l.transform.localPosition = new Vector3(0f, 1.45f, 0f); mapLabels.Add(l);
            }
        }
        else funLog += "no room for the emote stage; ";
        // ---- zero-G dome (the anti-gravity module)
        if (BaseSpot(1, 9f, out g))
        {
            zeroOk = true; zeroAt = g; var z = new GameObject("Zero-G dome").transform; z.SetParent(funRoot, false); z.position = g;
            Prim(PrimitiveType.Cylinder, z, new Vector3(0f, 0.1f, 0f), new Vector3(ZeroR * 2f + 1f, 0.1f, ZeroR * 2f + 1f), deck); Solid(z, new Vector3(0f, 0.1f, 0f), new Vector3(ZeroR * 1.45f, 0.2f, ZeroR * 1.45f));
            Prim(PrimitiveType.Cylinder, z, new Vector3(0f, 0.21f, 0f), new Vector3(ZeroR * 2f + 1.4f, 0.02f, ZeroR * 2f + 1.4f), Glow(new Color(0.5f, 1.6f, 2.8f)));
            Prim(PrimitiveType.Cylinder, z, new Vector3(0f, 0.22f, 0f), new Vector3(ZeroR * 2f + 0.6f, 0.021f, ZeroR * 2f + 0.6f), deck);
            Prim(PrimitiveType.Sphere, z, new Vector3(0f, 0.2f, 0f), Vector3.one * ZeroR * 2f, Clear(new Color(0.55f, 0.85f, 1f, 0.12f)));
            for (int k = 0; k < 6; k++) { float a = k * 60f * Mathf.Deg2Rad; Prim(PrimitiveType.Cylinder, z, new Vector3(Mathf.Sin(a) * (ZeroR + 0.2f), 0.6f, Mathf.Cos(a) * (ZeroR + 0.2f)), new Vector3(0.5f, 0.6f, 0.5f), gold); }
            var sign = Label(z, "<b>ZERO-G DOME</b>\n<size=55%>Anti-gravity module: step inside and you float. Bump the drifting shapes and catch the stars.</size>", 0f, 0f, 5f, 40f, 5f, new Color(0.6f, 0.9f, 1f)); sign.transform.localPosition = new Vector3(0f, ZeroR + 2.2f, 0f); mapLabels.Add(sign);
            zeroLabel = Label(z, "", 0f, 0f, 1.6f, 9f, 1.4f, Color.white); zeroLabel.transform.localPosition = new Vector3(0f, 2.6f, ZeroR + 1.6f); mapLabels.Add(zeroLabel);
            var ped = new GameObject("Gravity switch").transform; ped.SetParent(z, false); ped.localPosition = new Vector3(1.6f, 0.2f, ZeroR + 1.6f);
            Prim(PrimitiveType.Cylinder, ped, new Vector3(0f, 0.45f, 0f), new Vector3(0.24f, 0.45f, 0.24f), post, true);
            FacButton(ped, new Vector3(0f, 0.98f, 0f), new Vector3(0.46f, 0.14f, 0.46f), Glow(new Color(0.5f, 1.8f, 2.8f)), 1620);
            Color[] cols = { new Color(0.95f, 0.4f, 0.4f), new Color(0.4f, 0.85f, 0.5f), new Color(0.4f, 0.6f, 0.95f), new Color(0.95f, 0.8f, 0.3f), new Color(0.8f, 0.5f, 0.95f) };
            for (int k = 0; k < 12; k++)
            {
                var o = Prim(k % 3 == 0 ? PrimitiveType.Cube : (k % 3 == 1 ? PrimitiveType.Sphere : PrimitiveType.Capsule), z, new Vector3(R(-3.5f, 3.5f), R(1.5f, 4.5f), R(-3.5f, 3.5f)), Vector3.one * R(0.35f, 0.7f), Lit(cols[k % cols.Length]), true);
                o.name = "Floating shape"; var rb = o.AddComponent<Rigidbody>(); rb.useGravity = false; rb.mass = 1f; rb.linearDamping = 0.25f; rb.angularDamping = 0.2f; rb.linearVelocity = new Vector3(R(-0.5f, 0.5f), R(-0.3f, 0.3f), R(-0.5f, 0.5f)); rb.angularVelocity = new Vector3(R(-1f, 1f), R(-1f, 1f), R(-1f, 1f));
                zeroBodies.Add(rb);
            }
            for (int k = 0; k < 8; k++)
            {
                var s = Prim(PrimitiveType.Sphere, z, Vector3.zero, new Vector3(0.5f, 0.5f, 0.16f), Glow(new Color(3f, 2.4f, 0.5f))).transform; s.name = "Zero-G star";
                s.localPosition = Quaternion.Euler(0f, k * 45f, 0f) * new Vector3(0f, 1.6f + (k % 4) * 0.9f, 2.2f + (k % 3) * 1.1f); zeroStars.Add(s); zeroHome.Add(s.localPosition);
            }
        }
        else funLog += "no room for the zero-G dome; ";
        // ---- Space Slicer
        if (BaseSpot(0, 9f, out g))
        {
            sliceOk = true; sliceAt = g; var s = new GameObject("Space Slicer").transform; s.SetParent(funRoot, false); s.position = g; sliceRoot = s;
            Vector3 toField = fieldC - g; toField.y = 0f; if (toField.sqrMagnitude < 1f) toField = Vector3.forward; s.rotation = Quaternion.LookRotation(-toField.normalized, Vector3.up);      // you stand facing away from the field, at open sky
            Prim(PrimitiveType.Cylinder, s, new Vector3(0f, 0.1f, 0f), new Vector3(5f, 0.1f, 5f), deck); Solid(s, new Vector3(0f, 0.1f, 0f), new Vector3(3.5f, 0.2f, 3.5f));
            Prim(PrimitiveType.Cylinder, s, new Vector3(0f, 0.21f, 0f), new Vector3(5.3f, 0.02f, 5.3f), Glow(new Color(2.8f, 1.4f, 0.4f)));
            Prim(PrimitiveType.Cylinder, s, new Vector3(0f, 0.22f, 0f), new Vector3(4.6f, 0.021f, 4.6f), deck);
            Prim(PrimitiveType.Cube, s, new Vector3(0f, 0.25f, 8f), new Vector3(10f, 0.5f, 1.2f), post, true);      // the launcher
            for (int k = -2; k <= 2; k++) Prim(PrimitiveType.Cylinder, s, new Vector3(k * 2f, 0.6f, 8f), new Vector3(0.7f, 0.25f, 0.7f), gold);
            var sign = Label(s, "<b>SPACE SLICER</b>\n<size=55%>Asteroids, ice and comets fly up. Slice them with the Comet sword: hold and sweep. Leave the satellites alone!</size>", 0f, 0f, 5f, 44f, 5f, new Color(1f, 0.84f, 0.3f)); sign.transform.localPosition = new Vector3(0f, 9.5f, 9f); mapLabels.Add(sign);
            sliceLabel = Label(s, "", 0f, 0f, 1.8f, 12f, 1.6f, Color.white); sliceLabel.transform.localPosition = new Vector3(0f, 2.9f, 8f); mapLabels.Add(sliceLabel);
            var ped = new GameObject("Slicer start").transform; ped.SetParent(s, false); ped.localPosition = new Vector3(1.9f, 0.2f, 1.2f);
            Prim(PrimitiveType.Cylinder, ped, new Vector3(0f, 0.45f, 0f), new Vector3(0.24f, 0.45f, 0.24f), post, true);
            FacButton(ped, new Vector3(0f, 0.98f, 0f), new Vector3(0.46f, 0.14f, 0.46f), Glow(new Color(0.6f, 2.6f, 0.8f)), 1630);
            var rock = Lit(new Color(0.42f, 0.4f, 0.38f)); var ice = Lit(new Color(0.6f, 0.9f, 1f)); var comet = Glow(new Color(2.2f, 2.2f, 2.6f)); var sat = Lit(new Color(0.85f, 0.7f, 0.25f)); var panel = Lit(new Color(0.15f, 0.25f, 0.6f));
            for (int k = 0; k < 12; k++)
            {
                var o = new SliceObj(); var t = new GameObject("Slice object").transform; t.SetParent(funRoot, false); o.t = t; o.kind = k < 4 ? 0 : (k < 7 ? 1 : (k < 10 ? 2 : 3));
                if (o.kind == 0) Prim(PrimitiveType.Sphere, t, Vector3.zero, new Vector3(1.1f, 0.9f, 1f), rock);
                else if (o.kind == 1) Prim(PrimitiveType.Cube, t, Vector3.zero, Vector3.one * 0.85f, ice).transform.localRotation = Quaternion.Euler(30f, 20f, 10f);
                else if (o.kind == 2) { Prim(PrimitiveType.Sphere, t, Vector3.zero, Vector3.one * 0.8f, comet); Prim(PrimitiveType.Capsule, t, new Vector3(0f, -0.7f, 0f), new Vector3(0.35f, 0.6f, 0.35f), Clear(new Color(0.7f, 0.85f, 1f, 0.45f))); }
                else { Prim(PrimitiveType.Cube, t, Vector3.zero, new Vector3(0.6f, 0.6f, 0.6f), sat); Prim(PrimitiveType.Cube, t, new Vector3(0.9f, 0f, 0f), new Vector3(1f, 0.05f, 0.5f), panel); Prim(PrimitiveType.Cube, t, new Vector3(-0.9f, 0f, 0f), new Vector3(1f, 0.05f, 0.5f), panel); }
                var col = t.gameObject.AddComponent<SphereCollider>(); col.radius = 0.75f; var tg = t.gameObject.AddComponent<HunchTarget>(); tg.kind = 37; tg.index = k;
                t.gameObject.SetActive(false); sliceObjs.Add(o);
            }
            for (int k = 0; k < 8; k++) { var hgo = Prim(PrimitiveType.Sphere, funRoot, Vector3.zero, new Vector3(0.7f, 0.7f, 0.35f), rock); hgo.name = "Slice half"; hgo.SetActive(false); sliceHalves.Add(new SliceHalf { t = hgo.transform, r = hgo.GetComponent<Renderer>() }); }
            sliceBest = PlayerPrefs.GetInt("slice_best", 0);
        }
        else funLog += "no room for Space Slicer; ";
        if (BaseSpot(3, 12f, out g)) BuildStarShower(g); else funLog += "no room for Star Shower; ";
        if (BaseSpot(4, 9f, out g)) BuildTargetTower(g); else funLog += "no room for Target Tower; ";
        BuildBlaster();
        BuildMoonGates();
        Physics.SyncTransforms();
        HLog.I("Minigames", "fun zone: stage " + stageOk + ", zero-G " + zeroOk + ", slicer " + sliceOk + " " + funLog);
    }

    /// open, level ground for a pad: first beside the emote stage, then anywhere round the mining field. Never on top of something else.
    bool FunSpot(float clear, out Vector3 at)
    {
        at = Vector3.zero; Vector3 g; Vector3 centre = fieldC;
        for (int pass = 0; pass < 2; pass++)
        {
            foreach (float dist in pass == 0 ? new[] { 22f, 28f, 34f, 42f, 50f } : new[] { 40f, 50f, 60f, 72f, 84f, 96f })
                for (int k = 0; k < 18; k++)
                {
                    float a = (k * 20f + (pass == 0 ? 10f : 0f)) * Mathf.Deg2Rad; Vector3 c = pass == 0 ? centre : fieldC; float x = c.x + Mathf.Sin(a) * dist, z = c.z + Mathf.Cos(a) * dist;
                    if (!OpenAt(x, z, clear, out g) || !OpenAt(x, z, clear * 0.5f, out g)) continue;
                    if (Physics.CheckSphere(g + Vector3.up * (clear + 1.2f), clear, ~0, QueryTriggerInteraction.Ignore)) continue;
                    bool near = false; foreach (var u in usedSpots) if ((u - g).sqrMagnitude < (clear + 9f) * (clear + 9f)) { near = true; break; }
                    if (near) continue;
                    at = g; usedSpots.Add(g); return true;
                }
        }
        return false;
    }

    public void GoFun(int which)
    {
        if (hunchOn) { Note("The Fun Zone belongs to the full game. Switch NASA HUNCH mode off to visit it."); return; }
        if (!HunchCheats.TeleOk) { Note("Jumping to places is switched off (Menu > Cheats and anti-cheat). Walk there: it is by the mining field."); return; }
        if (mapNow != 0) GoMap(0);
        bool ok = which == 0 ? stageOk : (which == 1 ? zeroOk : sliceOk); if (!ok) { Note("That pad could not be placed on this ground."); return; }
        game.flyMode = false;
        if (which == 0) TeleportTo(stageAt + Vector3.up * 0.4f, 0f);
        else if (which == 1) TeleportTo(zeroAt + new Vector3(0f, 0.3f, ZeroR + 3f), 180f);
        else TeleportTo(sliceAt + Vector3.up * 0.3f, sliceRoot ? sliceRoot.eulerAngles.y : 0f);
        Note(which == 0 ? "Emote stage, at Dance Base. Press a button to try a move." : (which == 1 ? "Zero-G dome. Step inside to float." : "Space Slicer. Press the green button to start."));
    }

    string FunText(int i)
    {
        if (i >= 1600 && i < 1600 + EmoteNames.Length) return "try the move: " + EmoteNames[i - 1600];
        if (i == 1620) return "gravity in the dome: switch " + (zeroOn ? "ON (stop floating)" : "OFF (float)");
        if (i == 1640) return "call a team meeting here (the clock stops while you talk and vote)";
        if (i >= 1650) return MoonText(i);
        if (i == 1630) return sliceOn ? "Space Slicer is running" : "start Space Slicer (45 seconds)   best " + sliceBest;
        return "";
    }
    bool FunUse(int i)
    {
        if (i >= 1600 && i < 1600 + EmoteNames.Length) { Emote(i - 1600); return true; }
        if (i == 1620) { zeroOn = !zeroOn; Note(zeroOn ? "Gravity OFF in the dome. Float away!" : "Gravity back ON in the dome."); Play(cPop, 0.5f); return true; }
        if (i == 1630) { if (!sliceOn) SliceStart(); return true; }
        if (i == 1640) { MeetingHere(); return true; }
        if (i >= 1650) return MoonUse(i);
        return false;
    }

    // ------------------------------------------------------------------ zero-G dome
    readonly List<Rigidbody> zeroBodies = new List<Rigidbody>(); readonly List<Transform> zeroStars = new List<Transform>(); readonly List<Vector3> zeroHome = new List<Vector3>();
    TextMeshPro zeroLabel; bool zeroOn = true, zeroInside, zeroMadeFly; int zeroGot; float zeroReset, zeroTime;

    void UpdateZeroG(float dt)
    {
        if (!zeroOk) return;
        Vector3 c = zeroAt + Vector3.up * 0.2f, head = cam.position; bool inside = mapNow == 0 && !hunchOn && (head - c).magnitude < ZeroR - 0.3f && head.y > zeroAt.y - 0.5f;
        if (inside && zeroOn && !game.flyMode && !autoOn) { game.flyMode = true; zeroMadeFly = true; if (!zeroInside) { Note("Zero gravity! " + (game.HeadsetActive ? "Push the stick and look where you want to go." : "WASD to drift, Space up, C down.")); zeroTime = 0f; } }
        if ((!inside || !zeroOn) && zeroMadeFly) { zeroMadeFly = false; game.flyMode = false; }
        zeroInside = inside && zeroOn;
        // loose shapes: no gravity while the module is on, kept inside, nudged away when you bump them
        foreach (var rb in zeroBodies)
        {
            if (!rb) continue; rb.useGravity = !zeroOn;
            Vector3 off = rb.position - c; float d = off.magnitude;
            if (d > ZeroR - 1.2f) rb.AddForce(-off.normalized * (d - (ZeroR - 1.2f)) * 6f, ForceMode.Acceleration);
            if (rb.position.y < zeroAt.y + 0.6f) rb.AddForce(Vector3.up * 3f, ForceMode.Acceleration);
            Vector3 push = rb.position - head; if (zeroInside && push.magnitude < 1.1f) rb.AddForce(push.normalized * 4f, ForceMode.VelocityChange);
            if (zeroOn && rb.linearVelocity.sqrMagnitude < 0.02f) rb.AddForce(new Vector3(R(-1f, 1f), R(-0.6f, 0.6f), R(-1f, 1f)) * 0.4f, ForceMode.VelocityChange);
        }
        // the stars to catch
        if (zeroInside) zeroTime += dt;
        for (int k = 0; k < zeroStars.Count; k++)
        {
            var s = zeroStars[k]; if (!s.gameObject.activeSelf) continue;
            s.localPosition = zeroHome[k] + Vector3.up * Mathf.Sin(Time.time * 1.3f + k) * 0.25f; s.Rotate(0f, 120f * dt, 0f, Space.Self);
            if (zeroInside && (s.position - head).sqrMagnitude < 1.3f * 1.3f)
            {
                s.gameObject.SetActive(false); zeroGot++; Play(cPop, 0.6f); game.Buzz(0.4f, 0.06f);
                if (zeroGot >= zeroStars.Count) { Note("All " + zeroStars.Count + " zero-G stars in " + Mathf.RoundToInt(zeroTime) + " seconds!"); if (kid) kid.Toast("Zero-G star catcher!", 4f); Play(cWin, 0.6f); zeroReset = Time.time + 6f; }
            }
        }
        if (zeroReset > 0f && Time.time > zeroReset) { zeroReset = 0f; zeroGot = 0; zeroTime = 0f; foreach (var s in zeroStars) s.gameObject.SetActive(true); }
        if (zeroLabel) zeroLabel.text = "<b>Gravity " + (zeroOn ? "<color=#7CFC9A>OFF</color>" : "<color=#FFB060>ON</color>") + "</b>\n<size=70%>stars " + zeroGot + " of " + zeroStars.Count + "</size>";
    }

    // ------------------------------------------------------------------ Space Slicer
    class SliceObj { public Transform t; public int kind; public Vector3 vel, spin; public bool live; }
    class SliceHalf { public Transform t; public Renderer r; public Vector3 vel; public float life; }
    readonly List<SliceObj> sliceObjs = new List<SliceObj>(); readonly List<SliceHalf> sliceHalves = new List<SliceHalf>();
    Transform sliceRoot; TextMeshPro sliceLabel; bool sliceOn; float sliceLeft, sliceNext; int sliceScore, sliceCombo, sliceBest, sliceToolWas; float sliceComboUntil;

    void SliceStart()
    {
        if (!sliceOk) return;
        sliceOn = true; sliceLeft = 45f; sliceNext = 0.6f; sliceScore = 0; sliceCombo = 0; sliceToolWas = tool; tool = Sword;
        foreach (var o in sliceObjs) { o.live = false; o.t.gameObject.SetActive(false); }
        Note("Space Slicer! Hold " + (game.HeadsetActive ? "the trigger" : "the mouse button") + " and sweep across the space rocks."); Play(cWin, 0.5f);
    }
    void SliceEnd()
    {
        sliceOn = false; foreach (var o in sliceObjs) { o.live = false; o.t.gameObject.SetActive(false); }
        if (tool == Sword) tool = sliceToolWas;
        bool best = sliceScore > sliceBest; if (best) { sliceBest = sliceScore; PlayerPrefs.SetInt("slice_best", sliceBest); }
        Note("Space Slicer: " + sliceScore + " points" + (best ? "  -  a new best!" : "   (best " + sliceBest + ")")); if (best && kid) kid.Toast("New best: " + sliceScore + "!", 4f); Play(cWin, 0.7f);
    }
    void SliceHit(int i)
    {
        if (i >= 100) { TargetHit(i - 100); return; }
        if (!sliceOn || i < 0 || i >= sliceObjs.Count) return; var o = sliceObjs[i]; if (!o.live) return;
        o.live = false; o.t.gameObject.SetActive(false);
        if (o.kind == 3) { sliceScore = Mathf.Max(0, sliceScore - 3); sliceCombo = 0; Note("Oops, that was a satellite!  -3"); Play(cThud, 0.6f); game.Buzz(0.8f, 0.15f); return; }
        if (Time.time < sliceComboUntil) sliceCombo++; else sliceCombo = 1; sliceComboUntil = Time.time + 1.2f;
        int pts = 1 + (sliceCombo >= 3 ? 1 : 0) + (o.kind == 2 ? 1 : 0); sliceScore += pts;
        Play(cPop, 0.55f); game.Buzz(0.35f, 0.05f);
        try { Fx2("sparks explode yellow", "electro hit", o.t.position, 0.6f, 0.6f, 1f); } catch { }
        // two halves fly apart
        Vector3 side = sliceRoot ? sliceRoot.right : Vector3.right; int made = 0;
        foreach (var h in sliceHalves)
        {
            if (h.life > 0f) continue; h.life = 0.9f; h.t.gameObject.SetActive(true); h.t.position = o.t.position + side * (made == 0 ? -0.25f : 0.25f); h.t.rotation = o.t.rotation;
            h.vel = o.vel * 0.5f + side * (made == 0 ? -2.6f : 2.6f) + Vector3.up * 1.2f; if (h.r) h.r.sharedMaterial = o.t.GetComponentInChildren<Renderer>().sharedMaterial;
            if (++made >= 2) break;
        }
    }
    /// everything near the aim line is sliced: this is the sweep
    void SliceSweep(Ray ray, float reach, float coneDeg)
    {
        if (!sliceOn) return;
        for (int i = 0; i < sliceObjs.Count; i++)
        {
            var o = sliceObjs[i]; if (!o.live) continue; Vector3 to = o.t.position - ray.origin; float d = to.magnitude;
            if (d < reach && Vector3.Angle(ray.direction, to) < Mathf.Max(coneDeg, Mathf.Atan2(0.8f, d) * Mathf.Rad2Deg)) SliceHit(i);
        }
    }
    void UpdateSlicer(float dt)
    {
        if (!sliceOk) return;
        foreach (var h in sliceHalves) if (h.life > 0f) { h.life -= dt; h.vel += Vector3.down * 5f * dt; h.t.position += h.vel * dt; h.t.Rotate(200f * dt, 90f * dt, 0f); if (h.life <= 0f) h.t.gameObject.SetActive(false); }
        if (sliceLabel) sliceLabel.text = sliceOn ? "<b>" + sliceScore + "</b>   <size=70%>" + Mathf.CeilToInt(sliceLeft) + " s" + (sliceCombo >= 3 ? "   combo x" + sliceCombo : "") + "</size>" : "<size=70%>press the green button to play\nbest " + sliceBest + "</size>";
        if (!sliceOn) return;
        if (hunchOn || mapNow != 0 || !Playing || (Feet() - sliceAt).sqrMagnitude > 30f * 30f) { SliceEnd(); return; }
        sliceLeft -= dt; if (sliceLeft <= 0f) { SliceEnd(); return; }
        // toss the next one up from the launcher
        sliceNext -= dt;
        if (sliceNext <= 0f)
        {
            sliceNext = Mathf.Lerp(0.95f, 0.5f, 1f - sliceLeft / 45f);
            var free = new List<SliceObj>(); foreach (var o in sliceObjs) if (!o.live) free.Add(o);
            if (free.Count > 0)
            {
                var o = free[rng.Next(free.Count)]; o.live = true; o.t.gameObject.SetActive(true);
                o.t.position = sliceRoot.TransformPoint(new Vector3(R(-4f, 4f), 0.9f, 8f)); o.vel = sliceRoot.TransformDirection(new Vector3(R(-1.2f, 1.2f), R(9.5f, 12.5f), R(-0.6f, 0.4f))); o.spin = new Vector3(R(-140f, 140f), R(-140f, 140f), R(-140f, 140f));
            }
        }
        foreach (var o in sliceObjs)
        {
            if (!o.live) continue; o.vel += Vector3.down * 6f * dt; o.t.position += o.vel * dt; o.t.Rotate(o.spin * dt, Space.Self);
            if (o.t.position.y < sliceAt.y - 0.5f) { o.live = false; o.t.gameObject.SetActive(false); if (o.kind != 3) sliceCombo = 0; }
        }
        // the sweep: hold the button with the sword in hand and pass the aim over them
        bool held = game.HeadsetActive ? HunchVr.RightTrigger > 0.55f : (Mouse.current != null && Mouse.current.leftButton.isPressed && Cursor.lockState == CursorLockMode.Locked);
        if (held && tool == Sword && !game.menuOpen) SliceSweep(AimRay(), 26f, 2.2f);
    }

    void UpdateFunZone(float dt)
    {
        if (meetCentre && mtState != 2) meetCentre = false;
        if (meetLabel) meetLabel.text = mtState == 2 && meetCentre ? "<b>MEETING IN PROGRESS</b>" : "<b>TEAM MEETING</b>\n<size=60%>press the big round button</size>";
        UpdateTracking(dt); UpdateMoonbases(dt); UpdateFrontier21(dt);
        if (!funRoot) return;
        if (Time.time >= danceUntil && emoteMove >= 0 && !danceAuto) emoteMove = -1;
        UpdateZeroG(dt); UpdateSlicer(dt);
    }

    public string ProbeFun() { return "stage " + stageOk + stageAt.ToString("F0") + " zero " + zeroOk + zeroAt.ToString("F0") + " slicer " + sliceOk + sliceAt.ToString("F0") + " | " + funLog + " tools " + ToolNames.Length + " tool " + ToolNames[tool] + " zeroInside " + zeroInside + " stars " + zeroGot + " slice on " + sliceOn + " score " + sliceScore + " emote " + emoteMove; }
    public string ProbeFunGo() { GoFun(probeInt); return ProbeFun(); }
    public string ProbeEmote() { Emote(probeInt); return "emote " + emoteMove + " " + EmoteNames[emoteMove]; }
    public string ProbeSlice() { SliceStart(); return "slicer on " + sliceOn; }
    public string ProbeSliceAll() { int n = 0; for (int i = 0; i < sliceObjs.Count; i++) if (sliceObjs[i].live) { SliceHit(i); n++; } return "sliced " + n + " score " + sliceScore; }
    public string ProbeTool() { tool = Mathf.Clamp(probeInt, 0, ToolNames.Length - 1); return "tool " + ToolNames[tool]; }
    public string ProbeFire() { probeHold = Time.time + probeY; return "holding the trigger for " + probeY + " s with " + ToolNames[tool]; }

    // ================================================================== the meeting table in the centre dome (both modes)
    Transform meetTable; bool meetCentre; readonly Vector3[] meetSeat = new Vector3[4]; Vector3 meetAt; TextMeshPro meetLabel;
    void BuildMeetingTable()
    {
        RaycastHit fh; if (!Physics.Raycast(new Vector3(0f, 5f, 0f), Vector3.down, out fh, 8f, ~0, QueryTriggerInteraction.Ignore) || fh.normal.y < 0.8f) { funLog += "no floor for the meeting table; "; return; }
        Vector3 foot = fh.point; float seen = -999f;
        foreach (var h in Physics.RaycastAll(new Vector3(0f, 5f, 0f), Vector3.down, 9f, ~0, QueryTriggerInteraction.Ignore)) { var mr = h.collider.GetComponent<MeshRenderer>(); if (mr && mr.enabled && h.normal.y > 0.8f && h.point.y > seen) seen = h.point.y; }
        if (seen > -900f && seen < foot.y) foot.y = seen;
        if (Physics.CheckBox(fh.point + Vector3.up * 1.4f, new Vector3(3.4f, 1.1f, 3.4f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) { funLog += "the centre of the dome is not free for the meeting table; "; return; }
        meetAt = fh.point; meetTable = new GameObject("Meeting table").transform; meetTable.SetParent(root, false); meetTable.position = foot;
        var wood = Lit(new Color(0.5f, 0.36f, 0.24f)); var steel = Lit(new Color(0.75f, 0.77f, 0.82f)); var seat = Lit(new Color(0.2f, 0.32f, 0.55f)); var gold = Lit(new Color(0.83f, 0.62f, 0.2f)); var screen = Glow(new Color(0.5f, 1.3f, 2f));
        Prim(PrimitiveType.Cylinder, meetTable, new Vector3(0f, 0.38f, 0f), new Vector3(0.5f, 0.38f, 0.5f), steel);
        Prim(PrimitiveType.Cylinder, meetTable, new Vector3(0f, 0.78f, 0f), new Vector3(3.4f, 0.04f, 3.4f), wood);
        Prim(PrimitiveType.Cylinder, meetTable, new Vector3(0f, 0.775f, 0f), new Vector3(3.5f, 0.03f, 3.5f), gold);
        Solid(meetTable, new Vector3(0f, 0.45f, 0f), new Vector3(2.5f, 0.9f, 2.5f));
        // the big round button, in the middle where everyone can reach it
        Prim(PrimitiveType.Cylinder, meetTable, new Vector3(0f, 0.86f, 0f), new Vector3(1.0f, 0.04f, 1.0f), gold);
        var bigBtn = FacButton(meetTable, new Vector3(0f, 1.0f, 0f), new Vector3(1.0f, 0.42f, 1.0f), Glow(new Color(2.8f, 0.5f, 0.4f)), 1640); bigBtn.name = "Big meeting button"; foreach (var br in bigBtn.GetComponentsInChildren<Renderer>()) br.enabled = false;      // the press area is a box you cannot see; what you see is the round dome
        Prim(PrimitiveType.Sphere, meetTable, new Vector3(0f, 0.93f, 0f), new Vector3(1.0f, 0.5f, 1.0f), Glow(new Color(2.8f, 0.5f, 0.4f)));
        string[] who = { "Commander", "Dr. Okafor", "Eng. Tanaka", "Sci. Rivera" };
        for (int k = 0; k < 4; k++)
        {
            Quaternion q = Quaternion.Euler(0f, 45f + k * 90f, 0f); Vector3 d = q * Vector3.forward;
            var ch = new GameObject("Meeting chair " + (k + 1)).transform; ch.SetParent(meetTable, false); ch.localPosition = d * 2.45f; ch.localRotation = Quaternion.LookRotation(-d, Vector3.up);
            Prim(PrimitiveType.Cube, ch, new Vector3(0f, 0.45f, 0f), new Vector3(0.55f, 0.08f, 0.55f), seat); Prim(PrimitiveType.Cube, ch, new Vector3(0f, 0.85f, -0.26f), new Vector3(0.55f, 0.75f, 0.07f), seat);
            Prim(PrimitiveType.Cylinder, ch, new Vector3(0f, 0.22f, 0f), new Vector3(0.1f, 0.22f, 0.1f), steel); Prim(PrimitiveType.Cylinder, ch, new Vector3(0f, 0.02f, 0f), new Vector3(0.5f, 0.02f, 0.5f), steel);
            meetSeat[k] = ch.position + Vector3.up * 0.05f;
            // a small desk screen and a name plate at every place
            var desk = new GameObject("Meeting desk " + (k + 1)).transform; desk.SetParent(meetTable, false); desk.localPosition = d * 1.25f + Vector3.up * 0.82f; desk.localRotation = Quaternion.LookRotation(-d, Vector3.up);
            Prim(PrimitiveType.Cube, desk, new Vector3(0f, 0.16f, 0f), new Vector3(0.5f, 0.3f, 0.03f), screen).transform.localRotation = Quaternion.Euler(-20f, 0f, 0f);
            Prim(PrimitiveType.Cube, desk, new Vector3(0f, 0.015f, 0.05f), new Vector3(0.3f, 0.03f, 0.2f), steel);
            var nm = Label(desk, "<b>" + who[k] + "</b>", 0f, 0f, 0.5f, 2f, 0.3f, Color.white); nm.transform.localPosition = new Vector3(0f, 0.42f, 0f); mapLabels.Add(nm);
        }
        meetLabel = Label(meetTable, "", 0f, 0f, 1.5f, 12f, 1.6f, new Color(1f, 0.84f, 0.3f)); meetLabel.transform.localPosition = new Vector3(0f, 2.7f, 0f); mapLabels.Add(meetLabel);
        Physics.SyncTransforms();
    }
    /// A team meeting brings you to the round table in the centre dome. This is the one jump that is always allowed, even with teleport switched off.
    bool MeetBeam()
    {
        if (!meetTable) return false;
        if (mapNow != 0) GoMap(0);
        if (ride != null) Dismount(null); if (blastOn) BlastEnd(); if (sliceOn) SliceEnd();
        game.flyMode = false; meetCentre = true;
        Vector3 seat = meetSeat[Mathf.Clamp(playAs, 0, 3)], dir = seat - meetTable.position; dir.y = 0f; dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.forward;
        if (Flat(Feet() - meetTable.position).sqrMagnitude > 5.5f * 5.5f)
        {
            Vector3 stand = meetTable.position + dir * 3.5f; RaycastHit fh; if (Physics.Raycast(stand + Vector3.up * 2f, Vector3.down, out fh, 4f, ~0, QueryTriggerInteraction.Ignore)) stand.y = fh.point.y;
            TeleportTo(stand + Vector3.up * 0.08f, Mathf.Atan2(-dir.x, -dir.z) * Mathf.Rad2Deg);
            try { Fx2("pack teleporter", "teleport", Feet() + Vector3.up * 0.2f, 0.3f, 0.3f, 1.1f); } catch { }
        }
        return true;
    }
    void MeetingHere()
    {
        if (!Playing || (mtState == 0 && Time.time < meetPressCool)) return;
        if (sim.phase != HabPhase.Mission) { Banner("NOT YET", "Finish the commissioning walk first, then the team can meet.", true); sim.Say("ARIA: Finish the commissioning walk first. Team meetings start once the mission is running."); return; }
        if (mtState == 0) CallMeeting("You called the team to the table in the centre dome.", -1);
        if (mtState == 0) { Note("The team cannot meet right now."); return; }
        MeetBeam();
        if (mtState == 1) StartMeeting(); else if (menu && pageMeet != null && !game.menuOpen) menu.OpenExtra(pageMeet);
        Note("Team meeting at the round table. The clock stops while you talk and vote.");
    }
    /// while a meeting at the round table is on, the crew sit in their chairs
    bool MeetPose(int k, Transform av)
    {
        if (!meetCentre || mtState != 2 || !meetTable || k < 0 || k > 3) return false;
        av.position = meetSeat[k]; Vector3 look = meetTable.position - meetSeat[k]; look.y = 0f; if (look.sqrMagnitude > 0.01f) av.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
        return true;
    }

    // ================================================================== live tracking board (VR and desktop)
    // What the headset can really measure: where your head and hands are, how tall you stand, how fast you move, and where you look.
    // Eye tracking is read only when the headset has it. Face tracking and heart rate need sensors a Quest 2 or 3 does not have, and the board says so.
    TextMeshPro trackBoard, trackBack; float trackAt, trackSpeed, trackMoved, trackLog; Vector3 trackLast; float trackHeightMax; string trackFile = ""; readonly List<UnityEngine.XR.InputDevice> trackDevs = new List<UnityEngine.XR.InputDevice>();
    public bool trackOn = true;
    void BuildTrackBoard()
    {
        if (!meetTable) return;
        var b = new GameObject("Live tracking board").transform; b.SetParent(root, false); b.position = meetTable.position + new Vector3(0f, 0f, -6.2f); b.rotation = Quaternion.identity;
        Prim(PrimitiveType.Cube, b, new Vector3(0f, 1.9f, 0f), new Vector3(3.6f, 2.5f, 0.08f), Lit(new Color(0.05f, 0.08f, 0.14f)), true);
        Prim(PrimitiveType.Cube, b, new Vector3(0f, 0.35f, 0f), new Vector3(0.2f, 0.7f, 0.2f), Lit(new Color(0.75f, 0.77f, 0.82f)));
        // the text sat 2 cm BEHIND the dark panel (panel face at z -0.04, text at +0.06), so the board hid it and only the
        // letters poking past its edge showed. Text now sits in front of the panel on BOTH faces, sized to fit inside it.
        trackBoard = Label(b, "", 0f, 0f, 0.5f, 3.3f, 2.3f, Color.white); trackBoard.transform.localPosition = new Vector3(0f, 1.9f, -0.07f); trackBoard.alignment = TextAlignmentOptions.TopLeft;   // NOT a billboard map label: those turn to face you and swung the text into the panel
        trackBack = Label(b, "", 0f, 0f, 0.5f, 3.3f, 2.3f, Color.white); trackBack.transform.localPosition = new Vector3(0f, 1.9f, 0.07f); trackBack.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); trackBack.alignment = TextAlignmentOptions.TopLeft;
        foreach (var t in new[] { trackBoard, trackBack }) { t.enableAutoSizing = true; t.fontSizeMin = 0.2f; t.fontSizeMax = 0.5f; t.overflowMode = TextOverflowModes.Truncate; t.rectTransform.sizeDelta = new Vector2(3.3f, 2.3f); }
    }
    void UpdateTracking(float dt)
    {
        if (!trackBoard || !trackOn) return;
        Vector3 head = cam.position; float step = (head - trackLast).magnitude; trackLast = head; if (step < 3f) { trackMoved += step; trackSpeed = Mathf.Lerp(trackSpeed, step / Mathf.Max(0.0001f, dt), 1f - Mathf.Exp(-4f * dt)); }
        if (Time.unscaledTime < trackAt) return; trackAt = Time.unscaledTime + 0.25f;
        bool vr = game.HeadsetActive; float height = head.y - Feet().y; if (vr && height > trackHeightMax) trackHeightMax = height;
        Vector3 e = cam.eulerAngles; float pitch = Mathf.DeltaAngle(0f, e.x), yaw = e.y;
        // eyes: only on a headset that reports them
        string eyes = "not available on this headset"; Quaternion gaze = cam.rotation; bool eyesOk = false;
        if (vr)
        {
            try
            {
                UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(UnityEngine.XR.InputDeviceCharacteristics.EyeTracking, trackDevs);
                foreach (var d in trackDevs) { Quaternion r; if (d.isValid && d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out r)) { gaze = origin.transform.rotation * r; eyesOk = true; break; } }
            }
            catch { }
            if (eyesOk) { Vector3 g = Quaternion.Inverse(cam.rotation) * (gaze * Vector3.forward); eyes = "right/left " + (Mathf.Atan2(g.x, g.z) * Mathf.Rad2Deg).ToString("F0") + " deg, up/down " + (Mathf.Asin(Mathf.Clamp(g.y, -1f, 1f)) * Mathf.Rad2Deg).ToString("F0") + " deg"; }
        }
        RaycastHit hit; string looking = "open space"; if (Cast(new Ray(head, gaze * Vector3.forward), out hit, 60f)) { var tg = hit.collider.GetComponentInParent<HunchTarget>(); looking = (hit.collider.transform.root.name.Length > 22 ? hit.collider.transform.root.name.Substring(0, 22) : hit.collider.transform.root.name) + ", " + hit.distance.ToString("F1") + " m"; }
        string hands = "";
        if (vr && rightCtrl) hands = "\nRight hand   " + (rightCtrl.position - head).magnitude.ToString("F2") + " m from your head";
        string activity = trackSpeed < 0.15f ? "still" : (trackSpeed < 1.6f ? "walking" : (trackSpeed < 4.5f ? "jogging" : "running"));
        trackBoard.text = "<b><color=#9AD8FF>LIVE TRACKING</color></b>   <size=70%>" + (vr ? "headset" : "desktop: the camera stands in for your head") + "</size>\n<size=78%>"
            + "Height   " + height.ToString("F2") + " m" + (vr ? "   (tallest " + trackHeightMax.ToString("F2") + ")" : "") + "\n"
            + "Position   x " + head.x.ToString("F1") + "   z " + head.z.ToString("F1") + "   floor " + Feet().y.ToString("F1") + "\n"
            + "Head   turned " + yaw.ToString("F0") + " deg, tilted " + (-pitch).ToString("F0") + " deg\n"
            + "Movement   " + activity + ", " + trackSpeed.ToString("F1") + " m/s, " + Mathf.RoundToInt(trackMoved) + " m so far\n"
            + "Looking at   " + looking + "\n"
            + "Eyes   " + eyes + hands + "\n"
            + "<color=#9AA6BA>Face   needs a headset with face sensors (Quest Pro)\nHeart rate   needs a heart sensor; the headset has none</color></size>";
        if (trackBack) trackBack.text = trackBoard.text;
        // one line a second into a file, so the numbers can be charted afterwards
        if (Time.unscaledTime >= trackLog)
        {
            trackLog = Time.unscaledTime + 1f;
            try
            {
                if (trackFile.Length == 0) { string dir = System.IO.Path.Combine(Application.dataPath, "..", "Reports"); System.IO.Directory.CreateDirectory(dir); trackFile = System.IO.Path.Combine(dir, "tracking_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv"); System.IO.File.WriteAllText(trackFile, "seconds,headset,height_m,x,y,z,yaw_deg,pitch_deg,speed_m_s,activity,eyes_tracked,looking_at\n"); }
                System.IO.File.AppendAllText(trackFile, Time.time.ToString("F1") + "," + (vr ? 1 : 0) + "," + height.ToString("F2") + "," + head.x.ToString("F2") + "," + head.y.ToString("F2") + "," + head.z.ToString("F2") + "," + yaw.ToString("F0") + "," + (-pitch).ToString("F0") + "," + trackSpeed.ToString("F2") + "," + activity + "," + (eyesOk ? 1 : 0) + ",\"" + looking.Replace("\"", "'") + "\"\n");
            }
            catch { trackLog = float.MaxValue; }
        }
    }
    public string ProbeZeroIn() { if (!zeroOk) return "no dome " + funLog; game.flyMode = false; TeleportTo(zeroAt + Vector3.up * 2f, 0f); return "in the dome"; }
    public string ProbeTrack() { return trackBoard ? trackBoard.text.Replace("\n", " | ") + " || file " + trackFile : "no board " + funLog; }
    public string ProbeMeetEnd() { MeetingVote(0); if (menu) menu.Close(); return "meeting state " + mtState; }
    public string ProbeMeetKey() { sim.phase = HabPhase.Mission; MeetingGo(); return "meeting state " + mtState + " centre " + meetCentre + " feet " + Feet().ToString("F1") + " teleport cheat " + HunchCheats.TeleOk; }
    public string ProbeMeetHere() { sim.phase = HabPhase.Mission; MeetingHere(); return "meeting state " + mtState + " centre " + meetCentre + " table " + (meetTable ? meetTable.position.ToString("F1") : "none") + " " + funLog; }
    public string ProbeMeetView() { if (!meetTable) return "no table " + funLog; game.flyMode = true; TeleportTo(meetTable.position + new Vector3(6f, 3.2f, 6f), 225f); if (desk) desk.SetLook(225f, 22f); return "at the table"; }

    // ================================================================== VR mirror: the astronaut copies your arms
    [NonSerialized] public bool mirrorMe;
    /// In the headset the astronaut stands in front of you like a mirror. Each arm points where your hand is, measured from your shoulder.
    void MirrorArms(float yaw)
    {
        if (!leftCtrl) foreach (var t in origin.GetComponentsInChildren<Transform>(true)) if (t.name == "Left Controller") leftCtrl = t;
        Quaternion inv = Quaternion.Inverse(Quaternion.Euler(0f, yaw, 0f)); Vector3 right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right, neck = cam.position + Vector3.down * 0.22f;
        if (rightCtrl) { Vector3 v = inv * (rightCtrl.position - (neck + right * 0.19f)); if (v.sqrMagnitude > 0.0004f) armL.localRotation = Quaternion.FromToRotation(Vector3.down, new Vector3(-v.x, v.y, v.z).normalized); }
        if (leftCtrl) { Vector3 v = inv * (leftCtrl.position - (neck - right * 0.19f)); if (v.sqrMagnitude > 0.0004f) armR.localRotation = Quaternion.FromToRotation(Vector3.down, new Vector3(-v.x, v.y, v.z).normalized); }
    }
}
