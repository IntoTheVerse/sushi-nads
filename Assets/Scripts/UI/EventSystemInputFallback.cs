using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Ensures UI clicks work in Editor/Desktop even if InputSystemUIInputModule
/// is misconfigured or bound to an invalid actions asset.
/// </summary>
public static class EventSystemInputFallback
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ApplyFallback()
    {
        // Keep mobile behavior untouched.
        if (Application.isMobilePlatform)
        {
            return;
        }

        EventSystem[] systems = Object.FindObjectsOfType<EventSystem>(true);
        foreach (var eventSystem in systems)
        {
            if (eventSystem == null)
            {
                continue;
            }

            StandaloneInputModule standalone = eventSystem.GetComponent<StandaloneInputModule>();
            if (standalone == null)
            {
                standalone = eventSystem.gameObject.AddComponent<StandaloneInputModule>();
            }
            standalone.enabled = true;

#if ENABLE_INPUT_SYSTEM
            InputSystemUIInputModule inputSystemUi = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (inputSystemUi != null)
            {
                // For Editor/Desktop testing, prefer the legacy standalone module
                // so mouse UI clicks remain reliable in Game view.
                inputSystemUi.enabled = false;
            }
#endif
        }
    }
}
