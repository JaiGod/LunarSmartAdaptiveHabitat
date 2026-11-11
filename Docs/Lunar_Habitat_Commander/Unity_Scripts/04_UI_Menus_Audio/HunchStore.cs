// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/04_UI_Menus_Audio/HunchStore.cs
//  Section : 04_UI_Menus_Audio
//  Folder  : Menus, rounded UI, story intro, Star Store, music, voices (TTS/commands) and picture
//            quality.
//  Purpose : The STAR STORE: four one-time (durable) add-ons sold through the Meta Horizon Store.
//            costume_golden_pumpkin Golden Pumpkin King costume
//  Contains: HunchStore, Item
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

/// The STAR STORE: four one-time (durable) add-ons sold through the Meta Horizon Store.
///   costume_golden_pumpkin   Golden Pumpkin King costume
///   pack_cosmic_suits        Cosmic Suit Pack (three glowing suits with their own helmets)
///   pack_lumi_outfits        Lumi Pet Outfit Pack (glow collar, party hat, bubble helmet)
///   pack_supporter           Supporter Pack (gold supporter badge, and everything above)
/// Meta keeps the record of what was bought, on the player's Meta account. The game asks Meta every time it starts, so a
/// Restart, a reset of the game, a new headset or a reinstall never loses a purchase. A copy of the list is kept on the
/// headset only so the items show up straight away and when there is no internet; Meta's answer always replaces it.
/// Nothing is sold for coins, and there are no consumables.
/// The SKUs here must match the add-ons created in the Developer Dashboard (Monetization > Add-ons) exactly.
public static class HunchStore
{
    public const string AppId = "1342889162245039";
    public const string GoldenPumpkin = "costume_golden_pumpkin", CosmicSuits = "pack_cosmic_suits", LumiOutfits = "pack_lumi_outfits", Supporter = "pack_supporter";

    public class Item { public string sku, name, blurb, planned; }
    public static readonly Item[] Items =
    {
        new Item { sku = GoldenPumpkin, name = "Golden Pumpkin King", planned = "$0.99", blurb = "A shining golden-orange suit with a royal crown and a glowing ruby. Every crew member you play can wear it." },
        new Item { sku = CosmicSuits, name = "Cosmic Suit Pack", planned = "$1.99", blurb = "Three glowing suits: Cosmic Nebula (pink and purple halo), Comet Racer (a comet streaks over your helmet) and Starlight Galaxy (a crown of stars)." },
        new Item { sku = LumiOutfits, name = "Lumi Pet Outfit Pack", planned = "$0.99", blurb = "Dress up Lumi the moon pup: a rainbow glow collar, a party hat and a bubble space helmet. Change outfits any time at the Pet House." },
        new Item { sku = Supporter, name = "Supporter Pack", planned = "$3.99", blurb = "Everything in the store, plus a gold SUPPORTER badge on your astronaut and your level screen. It helps a student developer keep making the game." },
    };
    public static Item Find(string sku) { foreach (var i in Items) if (i.sku == sku) return i; return null; }

    /// "pc" (no Meta store on this computer), "connecting", "ready", "offline" (using the copy on the headset)
    public static string State { get; private set; } = "not started";
    public static bool Ready { get { return State == "ready"; } }
    public static bool OnQuest { get { return Application.platform == RuntimePlatform.Android && !Application.isEditor; } }
    public static bool Busy { get; private set; }                    // the Meta checkout window is open
    public static event Action Changed;

    static readonly HashSet<string> owned = new HashSet<string>();
    static readonly Dictionary<string, string> prices = new Dictionary<string, string>();
    static bool started, initOk;
    const string CacheKey = "hunch_store_owned";

    /// true for anything bought on this Meta account. The Supporter Pack includes the other three.
    public static bool Owns(string sku)
    {
        if (string.IsNullOrEmpty(sku)) return false;
        return owned.Contains(sku) || (sku != Supporter && owned.Contains(Supporter));
    }
    public static bool OwnsAnything { get { return owned.Count > 0; } }
    public static int OwnedCount { get { int n = 0; foreach (var i in Items) if (Owns(i.sku)) n++; return n; } }
    /// the price Meta shows for this country, or the planned price until Meta has answered
    public static string Price(string sku) { string p; if (prices.TryGetValue(sku, out p) && !string.IsNullOrEmpty(p)) return p; var it = Find(sku); return it != null ? it.planned : ""; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() { Init(); }

    public static void Init()
    {
        if (started) return; started = true;
        LoadCache();
        if (!OnQuest) { State = "pc"; Fire(); return; }
        State = "connecting";
        try
        {
            Oculus.Platform.Core.AsyncInitialize(AppId).OnComplete(m =>
            {
                if (m == null || m.IsError) { State = "offline"; Log("platform start failed: " + Err(m)); Fire(); return; }
                initOk = true; State = "ready"; Refresh(); LoadPrices();
            });
        }
        catch (Exception e) { State = "offline"; Log("platform start threw: " + e.Message); Fire(); }
    }

    /// asks Meta again what this account owns (the "Restore purchases" button, and every start)
    public static void Refresh(Action<string> done = null)
    {
        if (!OnQuest) { if (done != null) done("Store purchases live on your Meta account and show up in the Meta Quest version of the game."); return; }
        if (!initOk) { if (!started) Init(); if (done != null) done("Still connecting to the Meta store. Check the headset is online, then try again."); return; }
        try
        {
            Oculus.Platform.IAP.GetViewerPurchases().OnComplete(m =>
            {
                if (m != null && !m.IsError && m.Data != null) { Apply(m.Data, true); State = "ready"; if (done != null) done(OwnedCount > 0 ? "Restored " + OwnedCount + " store item" + (OwnedCount == 1 ? "" : "s") + " from your Meta account." : "Checked your Meta account: no store items yet."); return; }
                Log("purchases failed: " + Err(m) + " - using the copy on the headset");
                Oculus.Platform.IAP.GetViewerPurchasesDurableCache().OnComplete(c =>
                {
                    if (c != null && !c.IsError && c.Data != null) Apply(c.Data, false);
                    if (done != null) done("Could not reach the Meta store, so the items saved on this headset are used. They will be checked again next time you are online.");
                });
            });
        }
        catch (Exception e) { Log("refresh threw: " + e.Message); if (done != null) done("The Meta store did not answer. Try again in a moment."); }
    }

    static void LoadPrices()
    {
        try
        {
            var skus = new string[Items.Length]; for (int i = 0; i < Items.Length; i++) skus[i] = Items[i].sku;
            Oculus.Platform.IAP.GetProductsBySKU(skus).OnComplete(m =>
            {
                if (m == null || m.IsError || m.Data == null) { Log("prices failed: " + Err(m)); return; }
                foreach (var p in m.Data) if (p != null && !string.IsNullOrEmpty(p.Sku)) prices[p.Sku] = p.FormattedPrice;
                Fire();
            });
        }
        catch (Exception e) { Log("prices threw: " + e.Message); }
    }

    /// opens Meta's own checkout window for one add-on. Meta shows the price and asks the player to confirm;
    /// done(true, text) when it was bought, done(false, text) when it was cancelled or could not be bought.
    public static void Buy(string sku, Action<bool, string> done)
    {
        var it = Find(sku); string nm = it != null ? it.name : sku;
        if (Owns(sku)) { if (done != null) done(true, "You already own the " + nm + ". Thank you!"); return; }
        if (Application.isEditor)
        {
            // in the Unity Editor there is no Meta store: a pretend purchase lets you see exactly what a buyer gets. No money, not saved.
            testMode = true; owned.Add(sku); Fire();
            if (done != null) done(true, "TEST PURCHASE (Unity Editor only, no money): the " + nm + " is unlocked until you press Stop.");
            return;
        }
        if (!OnQuest) { if (done != null) done(false, "The " + nm + " can be bought in the Meta Quest version of the game, from the Meta Horizon Store."); return; }
        if (!initOk) { if (!started) Init(); if (done != null) done(false, "Still connecting to the Meta store. Check the headset is online and try again in a moment."); return; }
        if (Busy) return;
        Busy = true;
        try
        {
            Oculus.Platform.IAP.LaunchCheckoutFlow(sku).OnComplete(m =>
            {
                Busy = false;
                if (m != null && !m.IsError && m.Data != null)
                {
                    owned.Add(string.IsNullOrEmpty(m.Data.Sku) ? sku : m.Data.Sku); SaveCache(); Fire();
                    if (done != null) done(true, "Thank you! The " + nm + " is yours for ever, on every crew member and every headset with your Meta account.");
                    return;
                }
                string why = Err(m); bool cancelled = why.Contains("user_canceled") || why.Contains("cancel");
                Log("checkout " + sku + ": " + why);
                Refresh();      // a purchase can finish even when the window reports a problem, so ask Meta what is owned now
                if (done != null) done(false, cancelled ? "No problem: nothing was bought." : "The purchase did not go through, and you were not charged by the game. You can try again later.");
            });
        }
        catch (Exception e) { Busy = false; Log("checkout threw: " + e.Message); if (done != null) done(false, "The Meta store could not open. Try again in a moment."); }
    }

    // ---------------------------------------------------------------- the copy kept on the headset
    static void Apply(IEnumerable<Oculus.Platform.Models.Purchase> list, bool authoritative)
    {
        if (authoritative) owned.Clear();
        foreach (var p in list) if (p != null && !string.IsNullOrEmpty(p.Sku)) owned.Add(p.Sku);
        SaveCache(); Fire();
    }
    static void LoadCache()
    {
        try { foreach (var s in PlayerPrefs.GetString(CacheKey, "").Split('|')) if (s.Length > 0 && Find(s) != null) owned.Add(s); } catch { }
    }
    static void SaveCache() { if (testMode) return; PlayerPrefs.SetString(CacheKey, string.Join("|", owned)); PlayerPrefs.Save(); }
    static void Fire() { var c = Changed; if (c != null) try { c(); } catch (Exception e) { Debug.LogError("[HUNCH] store listener: " + e); } }
    static string Err(Oculus.Platform.Message m)
    {
        if (m == null) return "no answer";
        try { var e = m.GetError(); return e != null ? e.Code + " " + e.Message : "unknown"; } catch { return "unknown"; }
    }
    static void Log(string s) { Debug.Log("[HUNCH] store: " + s); }

    // ---------------------------------------------------------------- Editor testing only (never in the headset build)
    static bool testMode;
    /// pretends this account owns everything, so the items can be looked at in the Editor. Not saved.
    public static string TestGrantAll() { if (!Application.isEditor) return "editor only"; testMode = true; foreach (var i in Items) owned.Add(i.sku); Fire(); return "test: owns " + string.Join(",", owned); }
    public static string TestGrantSupporter() { if (!Application.isEditor) return "editor only"; testMode = true; owned.Clear(); owned.Add(Supporter); Fire(); return "test: supporter only -> pumpkin " + Owns(GoldenPumpkin) + " cosmic " + Owns(CosmicSuits) + " lumi " + Owns(LumiOutfits); }
    public static string TestClear() { if (!Application.isEditor) return "editor only"; testMode = true; owned.Clear(); Fire(); return "test: owns nothing"; }
    public static string Report() { return "state " + State + " owned [" + string.Join(",", owned) + "] prices " + prices.Count + " quest " + OnQuest; }
}
