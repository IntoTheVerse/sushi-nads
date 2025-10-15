using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// KitchenObject class represents a physical kitchen object in the game world
// It handles the object's properties and its relationship with its parent (counter or player)
public class KitchenObject : MonoBehaviour 
{
    // Reference to the Scriptable Object that defines this kitchen object's properties
    [SerializeField] private KitchenObjectSO kitchenObjectSO;
    // Reference to the current parent of this kitchen object (either a counter or the player)
    private IKitchenObjectParent kitchenObjectParent;

    // Returns the Scriptable Object containing this kitchen object's properties
    public KitchenObjectSO GetKitchenObjectSO()
    {
        return kitchenObjectSO;
    }

    // Sets a new parent for this kitchen object and handles the transfer logic
    public void SetKitchenObjectParent(IKitchenObjectParent kitchenObjectParent)
    {
        // Clear the reference from the old parent if it exists
        if (this.kitchenObjectParent != null)
        {
            this.kitchenObjectParent.ClearKitchenObject();
        }

        // Set the new parent
        this.kitchenObjectParent = kitchenObjectParent;

        // Safety check to ensure the new parent doesn't already have a kitchen object
        if (kitchenObjectParent.HasKitchenObject())
        {
            Debug.LogError("IKitchenObject already has a kitchen object!");
        }
        // Set this object as the parent's kitchen object
        kitchenObjectParent.SetKitchenObject(this);

        // Update the object's transform to follow its new parent
        transform.parent = kitchenObjectParent.GetKitchenObjectFollowTransform();
        transform.localPosition = Vector3.zero;
    }

    // Returns the current parent of this kitchen object
    public IKitchenObjectParent GetKitchenObjectParent()
    {
        return kitchenObjectParent;
    }

    public void DestroySelf() {
        kitchenObjectParent.ClearKitchenObject();
        Destroy(gameObject);
    }

    public bool TryGetPlate(out PlateKitchenObject plateKitchenObject) {
        if (this is PlateKitchenObject) {
            plateKitchenObject = this as PlateKitchenObject;
            return true;
        } else {
            // set plateKitchenObject to null as function output parameter
            plateKitchenObject = null;
            return false;
        }
    }

    public static KitchenObject SpawnKitchenObject(KitchenObjectSO kitchenObjectSO, IKitchenObjectParent kitchenObjectParent) {
        Transform kitchenObjectTransform = Instantiate(kitchenObjectSO.prefab);
        KitchenObject kitchenObject = kitchenObjectTransform.GetComponent<KitchenObject>();
        kitchenObject.SetKitchenObjectParent(kitchenObjectParent);
        return kitchenObject;
    }
}