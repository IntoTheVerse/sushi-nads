using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering;

public class TrashCounter : BaseCounter
{
    public static event EventHandler OnAnyObjectTrashed;

    new public static void ResetStaticData() {
        OnAnyObjectTrashed = null;
    }

    public override void Interact(Player player)
    {
        if (player.HasKitchenObject())
        {
            // Check if it's a plate (recipe) being discarded
            bool isPlate = player.GetKitchenObject().TryGetPlate(out PlateKitchenObject plateKitchenObject);
            
            player.GetKitchenObject().DestroySelf();
            OnAnyObjectTrashed?.Invoke(this, EventArgs.Empty);
            
            // Record recipe discard to blockchain for scoring (only plates count as recipes)
            if (isPlate && GameplayEventTracker.Instance != null)
            {
                GameplayEventTracker.Instance.RecordRecipeDiscard();
            }
        }
        
    }
}
