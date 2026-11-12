// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/06_Frontier/B_Maps_Minigames/HunchFrontier24.cs
//  Section : 06_Frontier/B_Maps_Minigames
//  Folder  : Extra maps and mini-games: solar farm, mining pit, Factory Land, Star Patrol, crew
//            radio + Star Clicker, Science Academy, My Astronaut, model kits, Fun Zone, endless
//            ground, Star Store pages.
//  Purpose : Twenty-fourth part of Moon Frontier: the STAR STORE pages and the things they sell. -
//            a gold STAR STORE button near the top of the main menu and the pause menu
//            (HunchMenu.SetStore)
//  Contains: HunchFrontier
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// Twenty-fourth part of Moon Frontier: the STAR STORE pages and the things they sell.
///   - a gold STAR STORE button near the top of the main menu and the pause menu (HunchMenu.SetStore)
///   - one page per add-on: buy (Meta's own checkout window), try it on first, wear it, restore purchases
///   - Lumi's outfits (glow collar, party hat, bubble helmet) and the OUTFITS button at the Pet House
///   - the supporter badge
/// Purchases come from HunchStore (Meta keeps them on the player's account). Nothing here is ever sold for star coins,
/// there are no timers or "only today" offers, and every page says plainly that the add-ons cost real money.
public partial class HunchFrontier
{
    HunchMenu.ExtraPage storePage; readonly Dictionary<string, HunchMenu.ExtraPage> storeDetail = new Dictionary<string, HunchMenu.ExtraPage>();
    string storeMsg = ""; float storeMsgT; Action storeHook;

    static readonly string[] PetOutfitNames = { "nothing", "rainbow glow collar", "party hat", "bubble space helmet" };
    int petOutfit, petPreview = -1; float petPreviewT; Transform petWear; Bounds petShape; bool petShapeOk;

    HunchMenu.ExtraPage StoreDetail(string sku) { HunchMenu.ExtraPage p; return sku != null && storeDetail.TryGetValue(sku, out p) ? p : storePage; }
    static string StorePackName(string sku) { var it = HunchStore.Find(sku); return it != null ? it.name : "STAR STORE"; }

    void BuildStore()
    {
        if (!menu) return;
        badgeOn = PlayerPrefs.GetInt("hunch_badge", 1) == 1;
        storeHook = () => { if (!this) { HunchStore.Changed -= storeHook; return; } StoreChanged(); };
        HunchStore.Changed += storeHook; HunchStore.Init();

        storePage = new HunchMenu.ExtraPage { title = "<color=#FFE27A>STAR</color> <color=#F0A8FF>STORE</color>" };
        storePage.build = m =>
        {
            if (storeMsg.Length > 0) m.AddItem("<color=#FFF2B0>" + storeMsg + "</color>", storeMsg, () => { }, 3);
            m.AddItem("No ads. No coin packs. Buy once, keep for ever.", StoreAbout(), () => { }, 3);
            foreach (var it in HunchStore.Items)
            {
                var item = it; bool own = HunchStore.Owns(item.sku);
                m.AddItem(own ? "<b>" + item.name + "</b>   OWNED - thank you!" : "<b>" + item.name + "</b>   " + HunchStore.Price(item.sku),
                          item.blurb + (own ? "" : "\nChoose it to see more, and to try it on before you buy."), () => m.OpenExtra(StoreDetail(item.sku)), own ? 2 : 1);
            }
            bool anyWear = false;
            for (int i = 0; i < Costumes.Length; i++)
            {
                if (!Costumes[i].premium || !CostumeOwned(i)) continue; int ii = i; anyWear = true;
                m.AddItem("Wear: " + Costumes[i].name + (Worn == ii ? "   (wearing)" : ""), "Puts it on straight away, on every crew member you play, and shows you wearing it.", () => { CostumePick(ii); ShowMe(); }, 2);
            }
            if (anyWear && StoreWear > 0) m.AddItem("Back to the classic suit", "Takes off the STAR STORE costume. You still own it and can put it back on any time.", () => { CostumePick(0); m.Rebuild(); });
            m.AddItem("See yourself (third-person view)", game.HeadsetActive ? "Your astronaut appears in front of you so you can see your costume. Aim at NEXT when you are done." : "Switches to third-person view so you can see your astronaut. V switches back.", () => ShowMe());
            m.AddItem("Restore purchases", "Asks your Meta account again for everything you have bought. The game also does this by itself every time it starts.", () => { StoreSay("Checking your Meta account..."); HunchStore.Refresh(StoreSay); });
            m.AddItem("Your purchases are safe", "STAR STORE items belong to your Meta account, not to this save. Restart, a reset, switching crew member, a new headset or reinstalling the game never removes them: the game asks Meta again every time it starts.", () => { }, 3);
            m.AddItem(HunchMenu.Copyright + "   " + HunchMenu.Thanks, "Lunar Habitat Commander is made by Jaineil R., a student developer, for the NASA HUNCH program. " + HunchMenu.Thanks + ", project partners. Every purchase supports the developer.", () => { }, 3);
        };
        foreach (var it in HunchStore.Items) storeDetail[it.sku] = MakeDetail(it);
        menu.SetStore(storePage, StoreButtonLabel);
        try { BuildKiosk(); } catch (Exception e) { HLog.E("Minigames", "store showcase: " + e); }
    }

    string StoreButtonLabel()
    {
        int n = HunchStore.OwnedCount;
        return "<b>STAR STORE</b>   " + (n >= HunchStore.Items.Length ? "you own everything - thank you!" : "new costumes, Lumi outfits, Supporter Pack");
    }

    string StoreAbout()
    {
        string where = HunchStore.State == "pc" ? "On this computer the store can only be looked at: the add-ons are sold in the Meta Quest version of the game, through the Meta Horizon Store."
                     : HunchStore.State == "ready" ? "Connected to the Meta Horizon Store."
                     : HunchStore.State == "offline" ? "The Meta store cannot be reached right now, so the purchases saved on this headset are shown."
                     : "Connecting to the Meta Horizon Store...";
        return where + "\nThe add-ons are one-time purchases that cost real money. Meta's own window shows the price and asks before anything is bought, and parents can manage purchases on the Meta account. Every mission, map and game in Lunar Habitat Commander is free without them.";
    }

    HunchMenu.ExtraPage MakeDetail(HunchStore.Item it)
    {
        var p = new HunchMenu.ExtraPage { title = "<color=#FFE27A>" + it.name.ToUpper() + "</color>", parent = storePage };
        p.build = m =>
        {
            bool own = HunchStore.Owns(it.sku);
            if (storeMsg.Length > 0) m.AddItem("<color=#FFF2B0>" + storeMsg + "</color>", storeMsg, () => { }, 3);
            if (own) m.AddItem("<b>OWNED</b> - thank you for supporting the game!", "Yours for ever on your Meta account: on every crew member you play and on every headset you sign in to.", () => { }, 2);
            else m.AddItem("<b>BUY NOW   " + HunchStore.Price(it.sku) + "</b>", it.blurb + "\nThis costs real money. Meta's own window opens, shows the price and asks before anything is bought.", () => StoreBuyNow(it.sku), 1);

            if (it.sku == HunchStore.GoldenPumpkin || it.sku == HunchStore.CosmicSuits)
                for (int i = 0; i < Costumes.Length; i++)
                {
                    if (Costumes[i].sku != it.sku) continue; int ii = i; var c = Costumes[i];
                    if (own) m.AddItem("Wear: " + c.name + (Worn == ii ? "   (wearing)" : ""), "Puts it on straight away, on every crew member you play, and shows you wearing it.", () => { CostumePick(ii); ShowMe(); }, 2);
                    else m.AddItem("Try on: " + c.name, Playing ? "Your astronaut appears in front of you wearing it, on the level stage. Aim at NEXT when you are done." : "Start a game first, then open the STAR STORE from the menu to try it on.", () => { PreviewCostume(ii, false); if (!Playing) m.Rebuild(); });
                }
            if (it.sku == HunchStore.LumiOutfits)
            {
                if (own) m.AddItem("Lumi wears: " + PetOutfitNames[petOutfit] + "   (change)", "Goes through the outfits: nothing, glow collar, party hat, bubble space helmet. Each crew member's choice is saved. The OUTFITS button at the Pet House does the same.", () => { PetOutfitNext(); m.Rebuild(); });
                else for (int k = 1; k < PetOutfitNames.Length; k++) { int kk = k; m.AddItem("Try on Lumi: " + PetOutfitNames[k], mapNow == 12 ? "Lumi wears it for 20 seconds." : "Lumi lives at the Pet House on the Clinic and Pet House map. Go there and press the gold OUTFITS button to try these on.", () => { PetPreview(kk); m.Rebuild(); }); }
            }
            if (it.sku == HunchStore.Supporter)
            {
                m.AddItem("Includes: Golden Pumpkin King, Cosmic Suit Pack, Lumi Pet Outfit Pack", "Everything in the STAR STORE, plus the gold supporter badge. If you already own some of them, you still get the badge.", () => { }, 3);
                if (own) m.AddItem("Supporter badge: " + (badgeOn ? "ON" : "OFF"), "A gold star on your astronaut's chest and SUPPORTER on your level screen.", () => { badgeOn = !badgeOn; PlayerPrefs.SetInt("hunch_badge", badgeOn ? 1 : 0); DressAvatar(); if (lvStage) LvDress(); m.Rebuild(); });
                else m.AddItem("Try on: supporter badge", Playing ? "Your astronaut appears in front of you wearing the gold badge." : "Start a game first, then open the STAR STORE from the menu to try it on.", () => { PreviewCostume(Worn, true); if (!Playing) m.Rebuild(); });
            }
        };
        return p;
    }

    void StoreBuyNow(string sku)
    {
        if (HunchStore.Busy) return;
        if (HunchStore.OnQuest) StoreSay("Opening the Meta store...");
        HunchStore.Buy(sku, (ok, text) =>
        {
            if (!this) return;
            StoreSay(text);
            if (!ok) return;
            Banner("THANK YOU!", StorePackName(sku) + " unlocked", false);
            if (sku == HunchStore.GoldenPumpkin) { CostumePick(6); ShowMe(); }
            else if (sku == HunchStore.CosmicSuits) { CostumePick(10); ShowMe(); }
            else if (sku == HunchStore.LumiOutfits) { if (petOutfit == 0) PetOutfitNext(); }
            else if (sku == HunchStore.Supporter) { badgeOn = true; PlayerPrefs.SetInt("hunch_badge", 1); DressAvatar(); if (lvStage) LvDress(); }
        });
    }

    void StoreSay(string s) { if (!this) return; storeMsg = s; storeMsgT = 25f; Note(s); if (menu && menu.IsOpen) menu.RefreshOpen(); }
    void StoreChanged() { DressAvatar(); if (lvStage) LvDress(); PetDress(); if (menu && menu.IsOpen) menu.RefreshOpen(); }

    /// lets the player look at their own astronaut: third-person view on a computer, the level stage in front of you in the headset
    void ShowMe()
    {
        if (menu && menu.IsOpen) menu.Close();
        if (!Playing) { StoreSay("Start a game first to see your astronaut."); return; }
        if (game.HeadsetActive) { if (lvOpen) LvClose(); LvShow(false, lvLevel); LvDress(); }
        else SetThird(true);
    }

    /// shows the astronaut on the level stage wearing a costume (and the badge) without buying it
    void PreviewCostume(int i, bool badge)
    {
        if (!Playing) { StoreSay("Start a game first, then open the STAR STORE from the menu to try things on."); return; }
        if (menu && menu.IsOpen) menu.Close(); if (lvOpen) LvClose();
        lvPreview = Mathf.Clamp(i, 0, Costumes.Length - 1); lvPreviewBadge = badge;
        LvShow(false, lvLevel); LvDress();
        string nm = badge ? "Supporter badge" : Costumes[lvPreview].name;
        lvHead.text = "<b>TRY-ON</b>";
        lvInfo.text = "<b>" + nm + "</b>\nIn the STAR STORE (menu).   Aim at NEXT when you are done.";
        Play(cPop, 0.6f);
    }

    // ================================================================== Lumi's outfits
    void PetOutfitNext()
    {
        petOutfit = (petOutfit + 1) % PetOutfitNames.Length; PlayerPrefs.SetInt("hunch_pet_outfit_" + player, petOutfit);
        PetDress(); petJump = 0.6f; Play(cPop, 0.5f);
    }
    void PetPreview(int k)
    {
        if (mapNow != 12 || !petRoot) { StoreSay("Lumi lives at the Pet House on the Clinic and Pet House map. Go there and press the gold OUTFITS button to try the outfits on."); return; }
        if (menu && menu.IsOpen) menu.Close();
        petPreview = k; petPreviewT = 20f; PetDress(); petJump = 0.8f; Play(cPop, 0.6f);
        Note("Try-on: Lumi in the " + PetOutfitNames[k] + ". The Lumi Pet Outfit Pack is in the STAR STORE.");
    }
    /// the box around everything drawn under a transform, in that transform's own space
    static Bounds LocalBounds(Transform t)
    {
        var b = new Bounds(); bool any = false;
        foreach (var r in t.GetComponentsInChildren<Renderer>())
        {
            var wb = r.bounds;
            for (int i = 0; i < 8; i++)
            {
                var c = new Vector3((i & 1) == 0 ? wb.min.x : wb.max.x, (i & 2) == 0 ? wb.min.y : wb.max.y, (i & 4) == 0 ? wb.min.z : wb.max.z);
                var lp = t.InverseTransformPoint(c); if (!any) { b = new Bounds(lp, Vector3.zero); any = true; } else b.Encapsulate(lp);
            }
        }
        return b;
    }
    void PetDress()
    {
        if (!petBody || !petShapeOk) return;
        if (petWear) Destroy(petWear.gameObject); petWear = null;
        int k = petPreview >= 0 ? petPreview : (HunchStore.Owns(HunchStore.LumiOutfits) ? petOutfit : 0);
        if (k <= 0 || k >= PetOutfitNames.Length) return;
        petWear = new GameObject("Outfit").transform; petWear.SetParent(petBody, false);
        var b = petShape; float h = b.size.y, w = Mathf.Min(b.size.x, b.size.z); Vector3 c = b.center;
        switch (k)
        {
            case 1:      // a rainbow of glowing beads round the neck, with a little gold tag at the front
                {
                    float y = b.min.y + h * 0.45f, rad = w * 0.46f;
                    for (int i = 0; i < 16; i++) { float a = i * Mathf.PI * 2f / 16f; Color gc = Color.HSVToRGB(i / 16f, 0.75f, 1f) * 2.4f; gc.a = 1f; Prim(PrimitiveType.Sphere, petWear, new Vector3(c.x + Mathf.Cos(a) * rad, y, c.z + Mathf.Sin(a) * rad), Vector3.one * w * 0.13f, Glow(gc)); }
                    Prim(PrimitiveType.Cylinder, petWear, new Vector3(c.x, y - w * 0.09f, c.z - rad - w * 0.04f), new Vector3(w * 0.13f, w * 0.012f, w * 0.13f), Lit(new Color(1f, 0.8f, 0.2f))).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    break;
                }
            case 2:      // a striped party hat with a glowing pom-pom
                {
                    var hat = new GameObject("Party hat").transform; hat.SetParent(petWear, false); hat.localPosition = new Vector3(c.x, b.max.y - h * 0.1f, c.z); hat.localRotation = Quaternion.Euler(-10f, 0f, 12f);
                    float s = w * 0.42f; Color[] stripe = { new Color(1f, 0.35f, 0.7f, 1f), new Color(1f, 0.85f, 0.25f, 1f), new Color(0.3f, 0.8f, 1f, 1f) };
                    for (int i = 0; i < 6; i++) Prim(PrimitiveType.Cylinder, hat, new Vector3(0f, i * s * 0.22f, 0f), new Vector3(s * (1f - i * 0.15f), s * 0.11f, s * (1f - i * 0.15f)), Glow(stripe[i % 3] * 1.15f));
                    Prim(PrimitiveType.Sphere, hat, new Vector3(0f, 6f * s * 0.22f, 0f), Vector3.one * s * 0.3f, Glow(new Color(2.7f, 2.4f, 2.7f)));
                    break;
                }
            case 3:      // a clear bubble space helmet with a silver ring and a little light on top
                {
                    float hy = b.min.y + h * 0.7f, r = h * 0.36f;
                    Prim(PrimitiveType.Sphere, petWear, new Vector3(c.x, hy, c.z), Vector3.one * r * 2f, Clear(new Color(0.7f, 0.9f, 1f, 0.22f)));
                    Prim(PrimitiveType.Cylinder, petWear, new Vector3(c.x, hy - r * 0.78f, c.z), new Vector3(r * 1.35f, r * 0.08f, r * 1.35f), Lit(new Color(0.85f, 0.88f, 0.95f)));
                    Prim(PrimitiveType.Sphere, petWear, new Vector3(c.x, hy + r * 1.05f, c.z), Vector3.one * r * 0.18f, Glow(new Color(0.6f, 2.4f, 2.7f)));
                    break;
                }
        }
    }

    // ================================================================== test hooks
    public string ProbeStore() { return HunchStore.Report() + " | worn " + Worn + " " + Costumes[Worn].name + " | badge " + BadgeShown + " | lumi outfit " + petOutfit + " shape " + (petShapeOk ? petShape.size.ToString("F2") + " at " + petShape.center.ToString("F2") : "none") + " | msg " + storeMsg + " | stage " + (lvStage ? lvStage.gameObject.activeInHierarchy + " at " + lvStage.position.ToString("F1") + " feet " + Feet().ToString("F1") + " fig " + (lvFigure ? lvFigure.gameObject.activeInHierarchy.ToString() : "none") : "none") + " open " + lvOpen + " preview " + lvPreview + " | kiosk " + kioskNote; }
    public string ProbeStoreOpen() { if (!menu || storePage == null) return "no store page"; menu.OpenExtra(storePage); return "store open"; }
    public string ProbeStoreDetail() { var it = HunchStore.Items[Mathf.Clamp(probeInt, 0, HunchStore.Items.Length - 1)]; if (!menu) return "no menu"; menu.OpenExtra(StoreDetail(it.sku)); return "detail " + it.name; }
    public string ProbeTryOn() { PreviewCostume(probeInt, false); return "try-on " + lvPreview + " open " + lvOpen; }
    /// the test camera renders outside the frame loop, facing +x; put the stage where it can see it
    public string ProbeStageFront() { if (!lvStage) return "no stage"; Vector3 f = game.origin.Camera.transform.forward; f.y = 0f; f.Normalize(); lvStage.position = Feet() + f * 3.6f; lvStage.rotation = Quaternion.LookRotation(f, Vector3.up); return "stage moved to " + lvStage.position.ToString("F1"); }
    public string ProbeWear() { CostumePick(Mathf.Clamp(probeInt, 0, Costumes.Length - 1)); if (!lvOpen) LvShow(false, lvLevel); LvDress(); return "worn " + Worn + " " + Costumes[Worn].name; }
    public string ProbeBuy() { var it = HunchStore.Items[Mathf.Clamp(probeInt, 0, HunchStore.Items.Length - 1)]; StoreBuyNow(it.sku); return "buy " + it.sku + " -> " + storeMsg; }
    public string ProbeLumiView()
    {
        if (mapNow != 12) GoMap(12);
        if (mapNow != 12 || !petRoot) return "not at the pet house (map " + mapNow + ")";
        game.flyMode = false; TeleportTo(mapC[12] + PetAt + new Vector3(1.6f, 0f, -2.2f), -35f); Face(petRoot.position + Vector3.up * 0.9f);
        petOutfit = Mathf.Clamp(probeInt, 0, PetOutfitNames.Length - 1); PetDress();
        return "lumi outfit " + PetOutfitNames[petOutfit] + " worn " + (petWear != null) + " lumi at " + petRoot.position.ToString("F1");
    }

    // ================================================================== the STAR STORE showcase in the base: see every item for real
    /// six glowing pedestals near where you start: the four store costumes on real astronauts, Lumi in her outfits, and the
    /// supporter star. Aim at one: if you own it you put it on straight away, if not its store page opens (with try-on).
    Transform kiosk; readonly List<TextMeshPro> kioskTags = new List<TextMeshPro>(); readonly List<Transform> kioskSpin = new List<Transform>(); float kioskTagT; string kioskNote = "";
    static readonly int[] KioskCostume = { 6, 10, 11, 12 };
    static readonly string[] KioskSku = { HunchStore.GoldenPumpkin, HunchStore.CosmicSuits, HunchStore.CosmicSuits, HunchStore.CosmicSuits, HunchStore.LumiOutfits, HunchStore.Supporter };
    void BuildKiosk()
    {
        int zi = ZoneIndex("airlock"); if (zi < 0) zi = 0; Vector3 c0 = game.zones[zi].position, pos, face;
        bool ok = FreeNear(c0, 0f, new Vector3(3.7f, 1.4f, 1.1f), new[] { 5f, 7f, 9f, 12f }, out pos, out face);
        if (!ok && mapGateOk) { ok = FreeNear(mapGateAt, 90f, new Vector3(3.7f, 1.4f, 1.1f), new[] { 8f, 11f, 14f }, out pos, out face); }
        kioskNote = (ok ? "placed" : "fallback") + " at " + pos.ToString("F1");
        kiosk = new GameObject("STAR STORE showcase").transform; kiosk.SetParent(root, true); kiosk.position = pos; kiosk.rotation = Quaternion.LookRotation(face, Vector3.up);
        Color gold = new Color(1f, 0.76f, 0.16f), violet = new Color(0.62f, 0.24f, 0.98f);
        Prim(PrimitiveType.Cube, kiosk, new Vector3(0f, 1.5f, -0.95f), new Vector3(7.4f, 3f, 0.12f), Lit(new Color(0.16f, 0.08f, 0.28f)));
        Prim(PrimitiveType.Cube, kiosk, new Vector3(0f, 3.05f, -0.9f), new Vector3(7.6f, 0.12f, 0.2f), Glow(new Color(2.6f, 1.9f, 0.4f)));
        Prim(PrimitiveType.Cube, kiosk, new Vector3(0f, 0.02f, -0.9f), new Vector3(7.6f, 0.08f, 0.2f), Glow(new Color(1.6f, 0.6f, 2.6f)));
        var sign = Label(kiosk, "<b><color=#FFE27A>STAR</color> <color=#F0A8FF>STORE</color></b>\n<size=45%><color=#FFFFFF>aim at an item: wear it, or try it on</color></size>", 0f, 0f, 3.2f, 40f, 6f, Color.white);
        sign.transform.localPosition = new Vector3(0f, 2.55f, -0.86f); sign.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        for (int k = 0; k < 6; k++)
        {
            float x = -2.9f + k * 1.16f; var p = new GameObject("Pedestal " + k).transform; p.SetParent(kiosk, false); p.localPosition = new Vector3(x, 0f, 0f);
            Prim(PrimitiveType.Cylinder, p, new Vector3(0f, 0.12f, 0f), new Vector3(0.95f, 0.12f, 0.95f), Lit(new Color(0.2f, 0.12f, 0.34f)));
            Prim(PrimitiveType.Cylinder, p, new Vector3(0f, 0.25f, 0f), new Vector3(1f, 0.012f, 1f), Glow(Color.Lerp(new Color(2.6f, 1.9f, 0.4f), new Color(1.6f, 0.6f, 2.8f), k / 5f)));
            var spin = new GameObject("Item").transform; spin.SetParent(p, false); spin.localPosition = new Vector3(0f, 0.26f, 0f); kioskSpin.Add(spin);
            if (k < 4)
            {
                var c = Costumes[KioskCostume[k]]; bool model = KitSource(false, "Astronaut");
                var f = Figure(spin, Vector3.zero, Lit(c.suit), Lit(new Color(0.16f, 0.34f, 0.82f))); f.localScale = Vector3.one * 0.72f; Hat(f, new Vector3(0f, model ? 1.86f : 1.78f, 0f), c.hat);
            }
            else if (k == 4)
            {
                var lumi = KitSource(false, "Lumi");
                if (lumi) { var g = Instantiate(lumi, spin); g.SetActive(true); g.transform.localPosition = Vector3.zero; g.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * lumi.transform.localRotation; g.transform.localScale = lumi.transform.localScale * 0.55f; foreach (var col in g.GetComponentsInChildren<Collider>(true)) Destroy(col); }
                else Prim(PrimitiveType.Sphere, spin, new Vector3(0f, 0.45f, 0f), Vector3.one * 0.8f, Lit(new Color(0.45f, 0.85f, 0.82f)));
                var hat = new GameObject("Party hat").transform; hat.SetParent(spin, false); hat.localPosition = new Vector3(0f, lumi ? 1.0f : 0.85f, 0f);
                Color[] stripe = { new Color(1f, 0.35f, 0.7f, 1f), new Color(1f, 0.85f, 0.25f, 1f), new Color(0.3f, 0.8f, 1f, 1f) };
                for (int i = 0; i < 5; i++) Prim(PrimitiveType.Cylinder, hat, new Vector3(0f, i * 0.06f, 0f), new Vector3(0.28f * (1f - i * 0.17f), 0.03f, 0.28f * (1f - i * 0.17f)), Glow(stripe[i % 3] * 1.15f));
                Prim(PrimitiveType.Sphere, hat, new Vector3(0f, 0.32f, 0f), Vector3.one * 0.09f, Glow(new Color(2.7f, 2.4f, 2.7f)));
            }
            else { var b = new GameObject("Star").transform; b.SetParent(spin, false); b.localPosition = new Vector3(0f, 1.0f, 0f); Badge(b, Vector3.zero, 7f); }
            FacButton(p, new Vector3(0f, 0.95f, 0f), new Vector3(1f, 1.7f, 1f), Clear(new Color(1f, 1f, 1f, 0f)), 1800 + k);
            kioskTags.Add(TagLabel(p, new Vector3(0f, 1.95f, 0.1f), 1.0f, "", Color.white));
        }
        Solid(kiosk, new Vector3(0f, 1.5f, -0.95f), new Vector3(7.4f, 3f, 0.2f));
    }
    string KioskName(int k) { return k < 4 ? Costumes[KioskCostume[k]].name : (k == 4 ? "Lumi Pet Outfit Pack" : "Supporter Pack"); }
    bool KioskOwned(int k) { return HunchStore.Owns(KioskSku[k]); }
    void UpdateKiosk(float dt)
    {
        if (!kiosk) return;
        bool show = !hunchOn; if (kiosk.gameObject.activeSelf != show) kiosk.gameObject.SetActive(show);
        if (!show) return;
        for (int i = 0; i < kioskSpin.Count; i++) if (kioskSpin[i]) kioskSpin[i].localRotation = Quaternion.Euler(0f, Mathf.Sin(Time.time * 0.6f + i) * 35f, 0f);
        kioskTagT -= dt; if (kioskTagT > 0f) return; kioskTagT = 0.5f;
        for (int k = 0; k < kioskTags.Count; k++)
        {
            bool own = KioskOwned(k), wearing = k < 4 && Worn == KioskCostume[k];
            kioskTags[k].text = "<b>" + KioskName(k) + "</b>\n<size=80%>" + (wearing ? "<color=#7CFC9A>WEARING</color>" : own ? "<color=#7CFC9A>OWNED - aim to wear</color>" : "<color=#FFE27A>" + HunchStore.Price(KioskSku[k]) + "</color>  try it on") + "</size>";
        }
    }
    string KioskText(int i)
    {
        int k = i - 1800; if (k < 0 || k > 5) return "";
        if (KioskOwned(k)) return k < 4 ? "wear the " + KioskName(k) : (k == 4 ? "Lumi's outfits: OUTFITS button at the Pet House" : "supporter badge: " + (badgeOn ? "on" : "off"));
        return "see the " + KioskName(k) + " (STAR STORE)";
    }
    void KioskUse(int i)
    {
        int k = i - 1800; if (k < 0 || k > 5) return;
        if (!KioskOwned(k)) { if (menu && storePage != null) menu.OpenExtra(StoreDetail(KioskSku[k])); return; }
        if (k < 4) { CostumePick(KioskCostume[k]); ShowMe(); }
        else if (k == 4) Note("Lumi's outfits are yours! Go to the Clinic and Pet House map and press the gold OUTFITS button.");
        else { badgeOn = !badgeOn; PlayerPrefs.SetInt("hunch_badge", badgeOn ? 1 : 0); DressAvatar(); Note("Supporter badge " + (badgeOn ? "ON" : "OFF") + "."); }
    }
    public string ProbeKioskView() { if (!kiosk) return "no kiosk"; game.flyMode = false; TeleportTo(kiosk.position + kiosk.forward * 5.5f, kiosk.eulerAngles.y + 180f); Face(kiosk.position + Vector3.up * 1.3f); return "kiosk " + kioskNote; }
}
