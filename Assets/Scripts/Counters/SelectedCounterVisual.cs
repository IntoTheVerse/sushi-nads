using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// SelectedCounterVisual class handles the visual feedback when a counter is selected by the player
public class SelectedCounterVisual : MonoBehaviour
{
    // Reference to the ClearCounter this visual belongs to
    [SerializeField] private BaseCounter baseCounter;
    // Reference to the GameObject that will be shown/hidden to indicate selection
    [SerializeField] private GameObject[] visualGameObjectArray;

    // Called when the script is first enabled
    private void Start()
    {
        // Subscribe to the Player's counter selection event
        Player.Instance.OnSelectedCounterChanged += Player_OnSelectedCounterChanged;
    }

    // Event handler for when the player selects a different counter
    private void Player_OnSelectedCounterChanged(object sender, Player.OnSelectedCounterChangedEventArgs e)
    {
        // Check if this counter is the one that was selected
        if (e.selectedCounter == baseCounter)
        {
            Show();  // Show visual if this counter is selected
        } else
        {
            Hide();  // Hide visual if this counter is not selected
        }
    }

    // Shows the visual indicator for this counter
    private void Show()
    {
        foreach(GameObject visualGameObject in visualGameObjectArray) {
        visualGameObject.SetActive(true);
        }
    }

    // Hides the visual indicator for this counter
    private void Hide()
    {
        foreach(GameObject visualGameObject in visualGameObjectArray) {
            visualGameObject.SetActive(false);
        }
    }
}
