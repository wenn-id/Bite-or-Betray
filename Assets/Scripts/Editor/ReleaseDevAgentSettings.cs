using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BiteOrBetray.EditorTools
{
    public sealed class ReleaseDevAgentSettings : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        // Meta's order-1 callback injects the Editor server token even into Release builds.
        public int callbackOrder => int.MaxValue;

        // The SDK restores its cached connection settings after packaging as well.
        public void OnPostprocessBuild(BuildReport report) => OnPreprocessBuild(report);

        public void OnPreprocessBuild(BuildReport report)
        {
            if ((report.summary.options & BuildOptions.Development) != 0) return;
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Resources/DevAgentSettings.asset");
            if (asset == null) return;
            Sanitize(new SerializedObject(asset));
            AssetDatabase.SaveAssetIfDirty(asset);
            Debug.Log("[Bite or Betray] Release DevAgent disabled; development connection credentials stripped.");
        }

        internal static void Sanitize(SerializedObject settings)
        {
            foreach (string field in new[] { "accessToken", "witClientAccessToken", "serverAddress" })
            {
                var property = settings.FindProperty(field);
                if (property == null) throw new BuildFailedException("DevAgent settings field changed: " + field);
                property.stringValue = "";
            }
            var enabled = settings.FindProperty("enabled");
            if (enabled == null) throw new BuildFailedException("DevAgent enabled field is missing.");
            enabled.boolValue = false;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
