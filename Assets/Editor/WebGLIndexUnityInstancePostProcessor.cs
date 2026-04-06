using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

public static class WebGLIndexUnityInstancePostProcessor
{
    private const string ThenNeedle = "}).then((unityInstance) => {";
    private const string QuitNeedle = "unityInstance.Quit().then(() => {";
    private const string AssignLine = "\n                window.unityInstance = unityInstance;";
    private const string ClearLine = "\n                    window.unityInstance = null;";

    [PostProcessBuild(999)]
    public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.WebGL)
        {
            return;
        }

        var indexPath = Path.Combine(pathToBuiltProject, "index.html");
        if (!File.Exists(indexPath))
        {
            Debug.LogWarning($"[WebGLIndexUnityInstancePostProcessor] index.html not found at {indexPath}");
            return;
        }

        var html = File.ReadAllText(indexPath);
        var updated = html;

        if (!updated.Contains("window.unityInstance = unityInstance;") && updated.Contains(ThenNeedle))
        {
            updated = updated.Replace(ThenNeedle, ThenNeedle + AssignLine);
        }

        if (!updated.Contains("window.unityInstance = null;") && updated.Contains(QuitNeedle))
        {
            updated = updated.Replace(QuitNeedle, QuitNeedle + ClearLine);
        }

        if (updated == html)
        {
            Debug.Log("[WebGLIndexUnityInstancePostProcessor] No index.html changes needed.");
            return;
        }

        File.WriteAllText(indexPath, updated);
        Debug.Log("[WebGLIndexUnityInstancePostProcessor] Patched index.html with window.unityInstance bridge.");
    }
}
