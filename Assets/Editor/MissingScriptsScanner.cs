using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MissingScriptsScanner
{
    [MenuItem("Tools/Diagnostics/Scan Missing Scripts/In Open Scenes")]
    public static void ScanOpenScenes()
    {
        int count = 0;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
            {
                continue;
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                count += ScanGameObjectHierarchy(root, scene.path);
            }
        }

        Debug.Log($"[MissingScriptsScanner] Done. Missing scripts found in open scenes: {count}");
    }

    [MenuItem("Tools/Diagnostics/Scan Missing Scripts/In Build Scenes + Prefabs")]
    public static void ScanBuildScenesAndPrefabs()
    {
        SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
        int count = 0;

        try
        {
            count += ScanBuildScenes();
            count += ScanAllPrefabs();
        }
        finally
        {
            EditorSceneManager.RestoreSceneManagerSetup(setup);
        }

        Debug.Log($"[MissingScriptsScanner] Done. Total missing scripts found: {count}");
    }

    private static int ScanBuildScenes()
    {
        int count = 0;
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        if (scenes == null || scenes.Length == 0)
        {
            Debug.LogWarning("[MissingScriptsScanner] No build scenes configured.");
            return 0;
        }

        foreach (EditorBuildSettingsScene buildScene in scenes)
        {
            if (!buildScene.enabled)
            {
                continue;
            }

            Scene scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Single);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                count += ScanGameObjectHierarchy(root, buildScene.path);
            }
        }

        Debug.Log($"[MissingScriptsScanner] Build scenes scan complete. Found: {count}");
        return count;
    }

    private static int ScanAllPrefabs()
    {
        int count = 0;
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                continue;
            }

            count += ScanGameObjectHierarchy(prefab, path);
        }

        Debug.Log($"[MissingScriptsScanner] Prefab scan complete. Found: {count}");
        return count;
    }

    private static int ScanGameObjectHierarchy(GameObject root, string assetPath)
    {
        int count = 0;
        var stack = new Stack<Transform>();
        stack.Push(root.transform);

        while (stack.Count > 0)
        {
            Transform current = stack.Pop();
            GameObject go = current.gameObject;

            Component[] components = go.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null)
                {
                    count++;
                    Debug.LogError(
                        $"[MissingScriptsScanner] Missing script at {GetHierarchyPath(go)} in asset {assetPath}",
                        go
                    );
                }
            }

            for (int i = 0; i < current.childCount; i++)
            {
                stack.Push(current.GetChild(i));
            }
        }

        return count;
    }

    private static string GetHierarchyPath(GameObject go)
    {
        string path = go.name;
        Transform parent = go.transform.parent;
        while (parent != null)
        {
            path = $"{parent.name}/{path}";
            parent = parent.parent;
        }

        return path;
    }
}
