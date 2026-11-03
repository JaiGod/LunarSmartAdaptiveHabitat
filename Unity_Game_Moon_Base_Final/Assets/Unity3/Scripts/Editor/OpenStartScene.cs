using UnityEditor;
using UnityEditor.SceneManagement;

namespace Hunch3.EditorTools
{
    [InitializeOnLoad]
    public static class OpenStartScene
    {
        static OpenStartScene()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                const string m0 = "Assets/Unity3/Scenes/00_Menu_VRBox.unity", m1 = "Assets/Unity3/Scenes/01_LunarHabitat.unity";
                if (EditorBuildSettings.scenes.Length < 2 || EditorBuildSettings.scenes[0].path != m0 || EditorBuildSettings.scenes[1].path != m1)
                    EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(m0, true), new EditorBuildSettingsScene(m1, true) };
                var s = EditorSceneManager.GetActiveScene();
                if (string.IsNullOrEmpty(s.path) || !s.path.StartsWith("Assets/Unity3"))
                {
                    const string menu = "Assets/Unity3/Scenes/00_Menu_VRBox.unity";
                    if (System.IO.File.Exists(menu)) EditorSceneManager.OpenScene(menu);
                    EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(menu);
                }
            };
        }
        [MenuItem("HUNCH 3/Open Menu scene")] static void M() => EditorSceneManager.OpenScene("Assets/Unity3/Scenes/00_Menu_VRBox.unity");
        [MenuItem("HUNCH 3/Open Habitat scene")] static void H() => EditorSceneManager.OpenScene("Assets/Unity3/Scenes/01_LunarHabitat.unity");
    }
}
