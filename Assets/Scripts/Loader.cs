using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public static class Loader
{
    public static Scene targetScene = Scene.MainMenuScene;
    // Build index for the loading/splash screen (set to 1 per request)
    private const int SplashScreenBuildIndex = 1;
    public enum Scene {   
        SplashScreen,  
        MainMenuScene,
        GameScene
    }

    public static void Load(Scene targetScene) {
        Loader.targetScene = targetScene;
        SceneManager.LoadScene(SplashScreenBuildIndex);
    }

    public static void LoaderCallback() {
        // If no target was set (e.g., splash was the first scene), fall back to main menu
        if (targetScene == Scene.SplashScreen)
        {
            targetScene = Scene.MainMenuScene;
        }

        SceneManager.LoadScene(targetScene.ToString());
    }
}
