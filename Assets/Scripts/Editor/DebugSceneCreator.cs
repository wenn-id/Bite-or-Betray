using BiteOrBetray.DebugTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BiteOrBetray.EditorTools
{
    public static class DebugSceneCreator
    {
        const string ParentFolder = "Assets/BiteOrBetray";
        const string SceneFolder = ParentFolder + "/Scenes";
        const string ScenePath = SceneFolder + "/DebugTest.unity";

        [MenuItem("Bite or Betray/Buat Scene Debug Test")]
        public static void CreateDebugScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var go = new GameObject("BiteOrBetray Debug Game");
            go.AddComponent<BiteOrBetrayDebugGame>();

            var cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.12f, 0.10f, 0.15f);
            }

            if (!AssetDatabase.IsValidFolder(ParentFolder)) AssetDatabase.CreateFolder("Assets", "BiteOrBetray");
            if (!AssetDatabase.IsValidFolder(SceneFolder)) AssetDatabase.CreateFolder(ParentFolder, "Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = go;
            Debug.Log("[Bite or Betray] Scene debug dibuat di " + ScenePath + ". Tekan Play untuk mulai.");
        }
    }
}
