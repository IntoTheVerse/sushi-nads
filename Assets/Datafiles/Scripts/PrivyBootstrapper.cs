using UnityEngine;
using Privy;

public class PrivyBootstrapper : MonoBehaviour
{
    [Header("Privy Dashboard -> App -> Client")]
    [SerializeField] private string appId = "YOUR_APP_ID";
    [SerializeField] private string clientId = "YOUR_CLIENT_ID";

    private static bool initialized;

    private async void Awake()
    {
        if (initialized) return;

        var cfg = new PrivyConfig
        {
            AppId = appId,
            ClientId = clientId
        };
        PrivyManager.Initialize(cfg);
        initialized = true;

        var state = await PrivyManager.Instance.GetAuthState();
        Debug.Log($"Privy auth state: {state}");
    }
}
