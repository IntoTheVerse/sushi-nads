public enum GameSceneLaunchMode
{
    None,
    Tutorial,
    NormalGameplay
}

public static class GameSceneLaunchContext
{
    private static GameSceneLaunchMode pendingMode = GameSceneLaunchMode.None;

    public static void SetTutorialMode()
    {
        pendingMode = GameSceneLaunchMode.Tutorial;
    }

    public static void SetNormalMode()
    {
        pendingMode = GameSceneLaunchMode.NormalGameplay;
    }

    public static GameSceneLaunchMode ConsumeMode()
    {
        GameSceneLaunchMode mode = pendingMode;
        pendingMode = GameSceneLaunchMode.None;
        return mode;
    }
}
