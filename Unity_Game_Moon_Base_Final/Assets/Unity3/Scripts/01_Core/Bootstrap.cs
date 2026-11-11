// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Smart Adaptive Habitat (realistic)
//  File    : Unity3/Scripts/01_Core/Bootstrap.cs
//  Section : 01_Core
//  Folder  : Start-up, shared state, menu scene, render settings and automated self-test.
//  Purpose : Makes the game start no matter how the scene's component links were saved. (A scene
//            can lose its link to a MonoBehaviour that lives in a file with a different name — that
//            caused the black screen.)
//  Contains: Bootstrap, DesktopUIClicker
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hunch3
{
    /// Makes the game start no matter how the scene's component links were saved.
    /// (A scene can lose its link to a MonoBehaviour that lives in a file with a different name — that caused the black screen.)
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded -= OnLoaded; SceneManager.sceneLoaded += OnLoaded;
            OnLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }
        static void OnLoaded(Scene s, LoadSceneMode m)
        {
            if (s.name.Contains("Menu") && !Object.FindFirstObjectByType<MenuBoot>()) { new GameObject("MenuBoot (auto)").AddComponent<MenuBoot>(); Debug.Log("[HUNCH3] MenuBoot started by bootstrap"); }
            if (s.name.Contains("LunarHabitat") && !Object.FindFirstObjectByType<HabitatBoot>()) { new GameObject("HabitatBoot (auto)").AddComponent<HabitatBoot>(); Debug.Log("[HUNCH3] HabitatBoot started by bootstrap"); }
        }
    }

    /// Desktop/Editor testing: mouse clicks press world-space and overlay buttons directly (VR uses the XR rays instead).
    public class DesktopUIClicker : MonoBehaviour
    {
        static DesktopUIClicker inst;
        public static void Ensure()
        {
            if (inst) return;
            inst = new GameObject("DesktopUIClicker").AddComponent<DesktopUIClicker>();
            Object.DontDestroyOnLoad(inst.gameObject);
        }
        void Update()
        {
            // the XR UI module can't take mouse input without a headset — turn it off so clicks aren't doubled
            foreach (var mod in Object.FindObjectsByType<BaseInputModule>(FindObjectsSortMode.None)) if (mod.enabled) mod.enabled = false;
            var ms = Mouse.current; if (ms == null || !ms.leftButton.wasPressedThisFrame) return;
            if (Cursor.lockState == CursorLockMode.Locked) return;
            Vector2 p = ms.position.ReadValue();
            Button best = null; float bestScore = float.MaxValue;
            foreach (var b in Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
            {
                if (!b.isActiveAndEnabled || !b.interactable) continue;
                var c = b.GetComponentInParent<Canvas>(); if (!c) continue; var root = c.rootCanvas;
                Camera cam = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : (root.worldCamera ? root.worldCamera : Camera.main);
                if (!RectTransformUtility.RectangleContainsScreenPoint((RectTransform)b.transform, p, cam)) continue;
                // overlay canvases sit in front of everything; higher sorting order wins; then nearest world canvas
                float score = root.renderMode == RenderMode.ScreenSpaceOverlay ? -1000 - root.sortingOrder : (cam ? Vector3.Distance(cam.transform.position, b.transform.position) : 0);
                // smaller rects (e.g. a button on top of a panel) win ties
                var r = ((RectTransform)b.transform).rect; score += r.width * r.height * 1e-7f;
                if (score < bestScore) { bestScore = score; best = b; }
            }
            if (best) best.onClick.Invoke();
        }
    }
}
