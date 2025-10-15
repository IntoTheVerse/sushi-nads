using UnityEngine;
using System;


// BaseCounter class serves as the base class for all counter types in the game
// It provides a common interface for player interaction with counters


public class BaseCounter : MonoBehaviour, IKitchenObjectParent
{
    public static event EventHandler OnAnyObjectPlacedHere;
    public static event EventHandler OnAnyObjectRemovedHere;
    // The point where kitchen objects will be placed on the counter

    public static void ResetStaticData() {
        OnAnyObjectPlacedHere = null;
        OnAnyObjectRemovedHere = null;                          // NEW
    }

    [SerializeField] private Transform counterTopPoint;

    // Reference to the kitchen object currently on this counter
    private KitchenObject kitchenObject;

    // Virtual method that defines how a counter interacts with the player
    // This method should be overridden by specific counter types (like ClearCounter)
    // The base implementation logs an error to ensure derived classes implement their own behavior
    public virtual void Interact(Player player) {
        Debug.LogError("BaseCounter.Interact() should be overridden");
    }

    public virtual void InteractAlternate(Player player) {
        Debug.LogError("BaseCounter.InteractAlternate() should be overridden");
    }

    
    // IKitchenObjectParent interface implementation
    // Returns the transform where kitchen objects should be placed
    public Transform GetKitchenObjectFollowTransform()
    {
        return counterTopPoint;
    }

    // Sets the kitchen object reference for this counter
    public void SetKitchenObject(KitchenObject kitchenObject)
    {
        this.kitchenObject = kitchenObject;
        if (kitchenObject != null) {
            OnAnyObjectPlacedHere?.Invoke(this, EventArgs.Empty);
        }
    }

    // Returns the kitchen object currently on this counter
    public KitchenObject GetKitchenObject()
    {
        return kitchenObject;
    }

    // Removes the reference to the kitchen object
    public void ClearKitchenObject()
    {
        kitchenObject = null;
        OnAnyObjectRemovedHere?.Invoke(this, EventArgs.Empty);  // NEW

    }

    // Checks if this counter currently has a kitchen object
    public bool HasKitchenObject()
    {
        return kitchenObject != null;
    }
}
