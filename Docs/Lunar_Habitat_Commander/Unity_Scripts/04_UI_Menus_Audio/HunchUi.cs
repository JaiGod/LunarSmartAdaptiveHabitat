// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Scripts/04_UI_Menus_Audio/HunchUi.cs
//  Section : 04_UI_Menus_Audio
//  Folder  : Menus, rounded UI, story intro, Star Store, music, voices (TTS/commands) and picture
//            quality.
//  Purpose : One place for the friendly look of every flat panel on screen: soft rounded corners
//            instead of sharp rectangles. Used by the menus, the dashboards, the guide panels and
//            the bars. Thin lines and full-screen fades stay square.
//  Contains: HunchUi
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using UnityEngine;

/// One place for the friendly look of every flat panel on screen: soft rounded corners instead of sharp rectangles.
/// Used by the menus, the dashboards, the guide panels and the bars. Thin lines and full-screen fades stay square.
public static class HunchUi
{
    static readonly int[] Radii = { 4, 7, 11, 16, 22 };
    static GUIStyle[] styles;

    static Texture2D Make(int r)
    {
        int n = r * 2 + 4; var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            float cx = x + 0.5f, cy = y + 0.5f;
            float dx = Mathf.Max(r - cx, cx - (n - r), 0f), dy = Mathf.Max(r - cy, cy - (n - r), 0f);
            float a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
            px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
        }
        t.SetPixels32(px); t.Apply(false, true); return t;
    }

    static void Init()
    {
        if (styles != null && styles[0] != null && styles[0].normal.background) return;
        styles = new GUIStyle[Radii.Length];
        for (int i = 0; i < Radii.Length; i++) { int r = Radii[i]; styles[i] = new GUIStyle { border = new RectOffset(r + 1, r + 1, r + 1, r + 1) }; styles[i].normal.background = Make(r); }
    }

    /// a filled panel with rounded corners; maxRadius in pixels (0 = as round as the screen size suggests)
    public static void Round(Rect r, Color c, float maxRadius = 0f)
    {
        if (Event.current == null || Event.current.type != EventType.Repaint || r.width <= 0f || r.height <= 0f) return;
        float m = Mathf.Min(r.width, r.height);
        bool square = m < 9f || r.width >= Screen.width * 0.97f || r.height >= Screen.height * 0.97f;      // thin lines and full-screen fades
        GUI.color = c;
        if (square) GUI.DrawTexture(r, Texture2D.whiteTexture);
        else
        {
            Init(); float cap = maxRadius > 0f ? maxRadius : 16f * Screen.height / 1080f; int k = 0;
            for (int i = 0; i < Radii.Length; i++) if (Radii[i] * 2 + 2 <= m && Radii[i] <= cap + 0.5f) k = i;
            if (Radii[k] * 2 + 2 > m) GUI.DrawTexture(r, Texture2D.whiteTexture); else styles[k].Draw(r, false, false, false, false);
        }
        GUI.color = Color.white;
    }

    /// a bar that fills from the left: a soft track with a rounded fill
    public static void Bar(Rect r, float frac, Color fill, Color track)
    {
        Round(r, track, r.height * 0.5f);
        frac = Mathf.Clamp01(frac); if (frac <= 0.001f) return;
        Round(new Rect(r.x, r.y, Mathf.Max(Mathf.Min(r.height, r.width), r.width * frac), r.height), fill, r.height * 0.5f);
    }

    /// a little on/off switch, as on a phone
    public static void Switch(Rect r, bool on)
    {
        Round(r, on ? new Color(0.36f, 0.86f, 0.52f, 1f) : new Color(0.55f, 0.6f, 0.7f, 0.55f), r.height * 0.5f);
        float d = r.height - 6f; Round(new Rect(on ? r.xMax - d - 3f : r.x + 3f, r.y + 3f, d, d), Color.white, d * 0.5f);
    }
}
