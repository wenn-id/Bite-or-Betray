using System.Collections;
using System.Linq;
using BiteOrBetray.Table3D;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BiteOrBetray.Tests.PlayMode
{
    /// <summary>
    /// Smoke test tanpa headset: scene gameplay dimuat seperti di build (scene 0), menu tampil, tanpa material pink.
    /// Dijalankan runner laptop (Tools/Autopilot/unity_runner.py) dengan -testPlatform PlayMode.
    /// </summary>
    public class SceneSmokeTests
    {
        const string Scene = "CandyParlour3D";

        [UnityTest]
        public IEnumerator Gameplay_scene_loads_into_main_menu_without_error_shaders()
        {
            yield return SceneManager.LoadSceneAsync(Scene, LoadSceneMode.Single);
            for (int i = 0; i < 60; i++) yield return null;

            var director = Object.FindFirstObjectByType<CandyTableDirector>();
            Assert.IsNotNull(director, "CandyTableDirector tidak ada di " + Scene);

            var broken = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                .SelectMany(r => r.sharedMaterials.Where(m => m == null || m.shader == null || !m.shader.isSupported
                                                              || m.shader.name == "Hidden/InternalErrorShader")
                    .Select(m => r.name + ":" + (m == null ? "null" : m.name + "/" + (m.shader ? m.shader.name : "null"))))
                .Distinct().ToList();
            Assert.IsEmpty(broken, "Material pink/rusak: " + string.Join(", ", broken.Take(20)));
        }
    }
}
