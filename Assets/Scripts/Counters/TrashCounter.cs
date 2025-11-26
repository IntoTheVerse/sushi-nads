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
            
            Debug.Log($"[TrashCounter] Object trashed - isPlate: {isPlate}, GameplayEventTracker.Instance: {(GameplayEventTracker.Instance != null ? "exists" : "NULL")}");
            
            player.GetKitchenObject().DestroySelf();
            OnAnyObjectTrashed?.Invoke(this, EventArgs.Empty);
            
            // Record recipe discard to blockchain for scoring (only plates count as recipes)
            if (isPlate)
            {
                if (GameplayEventTracker.Instance != null)
                {
                    Debug.Log("[TrashCounter] 🗑️ Calling RecordRecipeDiscard()...");
                    GameplayEventTracker.Instance.RecordRecipeDiscard();
                }
                else
                {
                    Debug.LogWarning("[TrashCounter] ⚠️ GameplayEventTracker.Instance is NULL - cannot record recipe discard!");
                }
            }
            else
            {
                Debug.Log("[TrashCounter] Object is not a plate - skipping recipe discard recording");
            }
        }
        
    }
}
