using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class LoaderCallback : MonoBehaviour
{
    public bool testGame = true;
    private void Start()
    {
        StartCoroutine(LoadAfterDelay());
    }

    private IEnumerator LoadAfterDelay()
    {
        yield return new WaitForSeconds(6f); // wait for video (5s) + buffer
        if(testGame)
        {
             SceneManager.LoadScene("GameScene");
        }
        Loader.LoaderCallback();
    }
}
