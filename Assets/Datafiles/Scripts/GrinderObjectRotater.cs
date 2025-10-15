using UnityEngine;

public class GrinderObjectRotater : MonoBehaviour
{
    [SerializeField] private float degreesPerSecond = 90f; // tweak in Inspector

    void Update()
    {
        transform.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f, Space.Self);
    }
}
