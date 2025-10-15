using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public static class Loader
{
    public static Scene targetScene;
    public enum Scene {     
        MainMenuScene,
        GameScene,
        LoadingScene
    }

    public static void Load(Scene targetScene) {
        Loader.targetScene = targetScene;
        SceneManager.LoadScene(Scene.LoadingScene.ToString());
        // SceneManager.LoadScene(targetScene.ToString());
    }

    public static void LoaderCallback() {
        SceneManager.LoadScene(targetScene.ToString());
    }
}
