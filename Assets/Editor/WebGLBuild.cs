using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Menu item and pre-build setup for WebGL builds.
/// Use: File > Build Settings > WebGL > Build, or Assets > Build WebGL from menu.
/// </summary>
public static class WebGLBuild
{
    private const string BuildOutputPath = "WebGLBuild";

    [MenuItem("Assets/Build WebGL")]
    public static void BuildWebGL()
    {
        BuildWebGLInternal(false);
    }

    [MenuItem("Assets/Build WebGL (Development)")]
    public static void BuildWebGLDevelopment()
    {
        BuildWebGLInternal(true);
    }

    private static void BuildWebGLInternal(bool developmentBuild)
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
        {
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            {
                Debug.LogError("[WebGLBuild] Failed to switch to WebGL build target.");
                return;
            }
        }

        EditorUserBuildSettings.development = developmentBuild;

        var options = BuildOptions.None;
        if (developmentBuild)
        {
            options |= BuildOptions.Development;
        }

        var report = BuildPipeline.BuildPlayer(
            EditorBuildSettings.scenes,
            BuildOutputPath,
            BuildTarget.WebGL,
            options
        );

        if (report.summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[WebGLBuild] Build succeeded. Output: {System.IO.Path.GetFullPath(BuildOutputPath)}");
        }
        else
        {
            Debug.LogError($"[WebGLBuild] Build failed: {report.summary.result}");
        }
    }
}
