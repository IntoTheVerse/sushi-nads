using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class AutoIncrementVersionCode : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
        {
            int versionCode = PlayerSettings.Android.bundleVersionCode;
            versionCode++;
            PlayerSettings.Android.bundleVersionCode = versionCode;

            string[] parts = PlayerSettings.bundleVersion.Split('.');
            int major = 1, minor = 0, patch = 0;

            if (parts.Length >= 1) int.TryParse(parts[0], out major);
            if (parts.Length >= 2) int.TryParse(parts[1], out minor);
            if (parts.Length >= 3) int.TryParse(parts[2], out patch);

            patch++; 
            
            if (patch >= 100)
            {
                patch = 0;
                minor++;
            }
            if (minor >= 100)
            {
                minor = 0;
                major++;
            }

            string newVersion = $"{major}.{minor}.{patch}";
            PlayerSettings.bundleVersion = newVersion;

            Debug.Log($"[AutoIncrementVersionCode] Updated versionCode: {versionCode}, bundleVersion: {newVersion}");
        }
    }
}
