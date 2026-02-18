using System;
using UnityEngine;

public class TrashCounter : BaseCounter
{
    public static event EventHandler OnAnyObjectTrashed;

    new public static void ResetStaticData()
    {
        OnAnyObjectTrashed = null;
    }

    public override void Interact(Player player)
    {
        if (!player.HasKitchenObject())
        {
            return;
        }

        KitchenObject trashedObject = player.GetKitchenObject();
        string trashedObjectName = trashedObject != null && trashedObject.GetKitchenObjectSO() != null
            ? trashedObject.GetKitchenObjectSO().name
            : "unknown";

        Debug.Log($"[TrashCounter] Object trashed: {trashedObjectName}, GameplayEventTracker.Instance: {(GameplayEventTracker.Instance != null ? "exists" : "NULL")}");

        trashedObject.DestroySelf();
        OnAnyObjectTrashed?.Invoke(this, EventArgs.Empty);

        // Count every trashed object as a discard.
        if (GameplayEventTracker.Instance != null)
        {
            Debug.Log("[TrashCounter] Calling RecordRecipeDiscard()...");
            GameplayEventTracker.Instance.RecordRecipeDiscard();
        }
        else
        {
            Debug.LogWarning("[TrashCounter] GameplayEventTracker.Instance is NULL - cannot record discard!");
        }
    }
}
