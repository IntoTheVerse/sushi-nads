using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ClearCounter class represents a counter that can hold kitchen objects and interact with the player
public class ClearCounter : BaseCounter
{
    // Reference to the kitchen object that can be spawned on this counter
    [SerializeField] private KitchenObjectSO kitchenObjectSO;
    // Update is called once per frame
    // private void Update()
    // {
    //     // Testing functionality: Press T to transfer object to second counter
    //     if (testing && Input.GetKeyDown(KeyCode.T)) {
    //         if (kitchenObject != null)
    //         {
    //             kitchenObject.SetKitchenObjectParent(secondClearCounter);
    //         }
    //     }
    // }

    // Called when the player interacts with this counter
    public override void Interact(Player player) {
        // Debug.Log("Player Interacted with ClearCounter");
        if (!HasKitchenObject()) { 
            // There is no kitchen object on this counter
            if (player.HasKitchenObject()) {
                // Player is carrying something
                player.GetKitchenObject().SetKitchenObjectParent(this);
            } else {
                // Player is not carrying anything
                // Debug.Log("Player is not carrying anything");
            }
        } else {
            // There is a kitchen object on this counter
            if (player.HasKitchenObject()) {
                // Player is carrying something
                if (player.GetKitchenObject().TryGetPlate(out PlateKitchenObject plateKitchenObject)) {
                    // Player is carrying a plate
                    if (plateKitchenObject.TryAddIngredient(GetKitchenObject().GetKitchenObjectSO())) {
                        GetKitchenObject().DestroySelf();
                    }
                } else {
                    // Player is not carrying a plate but something else
                    if (GetKitchenObject().TryGetPlate(out plateKitchenObject)) {
                        // There is a plate on this counter
                        if (plateKitchenObject.TryAddIngredient(player.GetKitchenObject().GetKitchenObjectSO())) {
                            player.GetKitchenObject().DestroySelf();
                        }
                    }
                }
            } else {
                // Player is not carrying anything
                GetKitchenObject().SetKitchenObjectParent(player);
            }
        }
    }
}
