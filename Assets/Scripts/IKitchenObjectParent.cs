using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// IKitchenObjectParent interface defines the contract for any object that can hold kitchen objects
// This interface is implemented by both the Player and ClearCounter classes
public interface IKitchenObjectParent
{
    // Returns the transform where the kitchen object should be positioned
    // This allows the kitchen object to follow its parent (e.g., counter top or player's hand)
    public Transform GetKitchenObjectFollowTransform();

    // Sets the reference to the kitchen object this parent is holding
    // Called when a kitchen object is picked up or placed
    public void SetKitchenObject(KitchenObject kitchenObject);

    // Returns the kitchen object currently being held
    // Returns null if no object is being held
    public KitchenObject GetKitchenObject();

    // Removes the reference to the currently held kitchen object
    // Called when the object is transferred to another parent
    public void ClearKitchenObject();

    // Checks if this parent is currently holding a kitchen object
    // Used for validation before picking up new objects
    public bool HasKitchenObject();
}
