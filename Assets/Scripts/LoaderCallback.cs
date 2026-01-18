using UnityEngine;
using System.Collections;

public class LoaderCallback : MonoBehaviour
{
    private void Start()
    {
        StartCoroutine(LoadAfterDelay());
    }

    private IEnumerator LoadAfterDelay()
    {
        yield return new WaitForSeconds(6f); // wait for video (5s) + buffer
        Loader.LoaderCallback();
    }
}
