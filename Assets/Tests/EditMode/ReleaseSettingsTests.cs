#if UNITY_EDITOR
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BiteOrBetray.Tests
{
    public class ReleaseSettingsTests
    {
        [Test]
        public void Release_strips_injected_development_credentials_and_disables_the_agent()
        {
            var source = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Resources/DevAgentSettings.asset");
            Assert.IsNotNull(source);
            var copy = Object.Instantiate(source);
            try
            {
                var settings = new SerializedObject(copy);
                foreach (string key in new[] { "accessToken", "witClientAccessToken", "serverAddress" })
                    settings.FindProperty(key).stringValue = "test-only-injected-value";
                settings.FindProperty("enabled").boolValue = true;
                settings.ApplyModifiedPropertiesWithoutUndo();
                var type = Type.GetType("BiteOrBetray.EditorTools.ReleaseDevAgentSettings, BiteOrBetray.Editor", true);
                type.GetMethod("Sanitize", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { settings });
                foreach (string key in new[] { "accessToken", "witClientAccessToken", "serverAddress" })
                    Assert.IsEmpty(settings.FindProperty(key).stringValue, key + " leaks into a Release package.");
                Assert.IsFalse(settings.FindProperty("enabled").boolValue);
            }
            finally { Object.DestroyImmediate(copy); }
        }
    }
}
#endif
