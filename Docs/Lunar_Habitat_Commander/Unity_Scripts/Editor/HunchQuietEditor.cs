// ================================================================================================
//  PROJECT ARES · NASA HUNCH 2025–26 · Lunar Habitat Commander (sci-fi)
//  File    : HUNCH/Editor/HunchQuietEditor.cs
//  Section : Editor
//  Folder  : Editor tools (not in the built game): VR/Quest/web builders, asset-pack and model
//            kits, store setup, probes, the HunchBridge automation and USB helpers.
//  Purpose : the same Console filter while the Editor is not playing
//  Contains: HunchQuietEditor
//  © 2026 Jaineil R. — with thanks to Angel C. and Farhan K.
// ================================================================================================
using UnityEditor;

/// the same Console filter while the Editor is not playing
[InitializeOnLoad]
static class HunchQuietEditor
{
    static HunchQuietEditor() { HunchQuiet.Install(); }
}
