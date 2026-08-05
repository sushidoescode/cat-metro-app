using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CatMetro.EditorTools
{
    // CM-C2b review F4c: the device build needs a scene with a boot entry — without one,
    // criteria 7/8/9-device/11's device session cannot exist. Regenerates Assets/Scenes/
    // Game.unity (an empty GameObject carrying GameRoot, which self-initializes in Awake
    // through the StreamingAssets seam) and points EditorBuildSettings at it. Run headless:
    //   Unity -batchmode -quit -executeMethod CatMetro.EditorTools.SceneBootstrapper.BuildGameScene
    public static class SceneBootstrapper
    {
        [MenuItem("CatMetro/Build Game Scene")]
        public static void BuildGameScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("GameRoot");
            go.AddComponent<CatMetro.Bootstrap.GameRoot>();
            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Game.unity");
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/Game.unity", true),
            };
            AssetDatabase.SaveAssets();
            Debug.Log("SceneBootstrapper: Game.unity written and set as the sole build scene");
        }
    }
}
