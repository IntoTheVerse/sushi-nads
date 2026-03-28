using UnityEngine;

/// <summary>
/// Singleton that controls the shared Loading spinner GameObject.
/// Uses a reference count so concurrent async calls don't hide the spinner early.
///
/// Usage:
///   LoadingManager.Instance.Show();
///   try   { await SomeAPICall(); }
///   finally { LoadingManager.Instance.Hide(); }
/// </summary>
public class LoadingManager : MonoBehaviour
{
    public static LoadingManager Instance { get; private set; }

    private GameObject loadingObject;
    private int refCount = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>Assign the Loading GameObject from ChefManager Awake.</summary>
    public void SetLoadingObject(GameObject obj)
    {
        loadingObject = obj;
        if (loadingObject != null)
            loadingObject.SetActive(false);
    }

    /// <summary>Increment ref-count and show spinner.</summary>
    public void Show()
    {
        refCount++;
        if (loadingObject != null)
            loadingObject.SetActive(true);
    }

    /// <summary>Decrement ref-count; hide spinner only when count reaches zero.</summary>
    public void Hide()
    {
        refCount = Mathf.Max(0, refCount - 1);
        if (refCount == 0 && loadingObject != null)
            loadingObject.SetActive(false);
    }

    /// <summary>Force-hide regardless of ref-count (use on scene cleanup).</summary>
    public void ForceHide()
    {
        refCount = 0;
        if (loadingObject != null)
            loadingObject.SetActive(false);
    }
}
