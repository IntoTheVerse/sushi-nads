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
        SplashScreen
    }

    public static void Load(Scene targetScene) {
        Loader.targetScene = targetScene;
        SceneManager.LoadScene(Scene.SplashScreen.ToString());
        // SceneManager.LoadScene(targetScene.ToString());
    }

    public static void LoaderCallback() {
        SceneManager.LoadScene(targetScene.ToString());
    }
}
