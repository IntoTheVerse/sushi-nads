using System;
using UnityEngine;

public class CuttingCounter : BaseCounter, IHasProgress
{
    public static event EventHandler OnAnyCut;
    new public static void ResetStaticData() {
        OnAnyCut = null;
    }
    public event EventHandler<IHasProgress.OnProgressChangedEventArgs> OnProgressChanged;

    public event EventHandler OnCut;

    [SerializeField] private CuttingRecipeSO[] cuttingRecipeSOArray;
    private int cuttingProgress;
    public override void Interact(Player player){
        Debug.Log($"[CUT/INTERACT] frame={Time.frameCount} counter={name} hasKO={HasKitchenObject()} playerHas={player.HasKitchenObject()} onCounter={(HasKitchenObject() ? GetKitchenObject().GetKitchenObjectSO().name : "null")}");

        if (!HasKitchenObject()) {
            // There is no kitchen object on this counter
            if (player.HasKitchenObject()) {
                // Player is carrying something
                KitchenObject playerKitchenObject = player.GetKitchenObject(); // Get the player's kitchen object BEFORE changing parent
                // Check if player is carrying something that can be cut on this counter
                if (HasRecipeWithInput(playerKitchenObject.GetKitchenObjectSO())) {
                    // Player is carrying something that can be cut
                    // Debug.Log("Player is carrying a cuttable item. Placing on counter.");
                    playerKitchenObject.SetKitchenObjectParent(this);
                    cuttingProgress = 0;
                    CuttingRecipeSO cuttingRecipeSO = GetCuttingRecipeSOWithInput(playerKitchenObject.GetKitchenObjectSO());
                    OnProgressChanged?.Invoke(this, new IHasProgress.OnProgressChangedEventArgs {
                        progressNormalized = (float)cuttingProgress / cuttingRecipeSO.cuttingProgressMax
                    });
                } else {
                    // Player is not carrying anything that can be cut
                    // Debug.Log("Player is carrying an item, but it cannot be cut on this counter.");
                }
            } else {
                // Player is not carrying anything
                // Debug.Log("Player is not carrying anything to place on the counter.");
            }
        } else {
            // There is a kitchen object on this counter
            if (player.HasKitchenObject()) {
                // Player is carrying something
                // Player is carrying something
                if (player.GetKitchenObject().TryGetPlate(out PlateKitchenObject plateKitchenObject)) {
                    // Player is carrying a plate
                    if (plateKitchenObject.TryAddIngredient(GetKitchenObject().GetKitchenObjectSO())) {
                        GetKitchenObject().DestroySelf();
                    }
                }
                // Debug.Log("Counter has object, player has object. Cannot place item.");
            } else {
                // Player is not carrying anything
                // Debug.Log("Counter has object, player is empty. Taking item.");
                GetKitchenObject().SetKitchenObjectParent(player);
            }
        }
    }

    public override void InteractAlternate(Player player){
        Debug.Log($"[CUT/ALT] frame={Time.frameCount} counter={name} hasKO={HasKitchenObject()} onCounter={(HasKitchenObject() ? GetKitchenObject().GetKitchenObjectSO().name : "null")}");

        // Debug.Log("InteractAlternate");
        if (HasKitchenObject() && HasRecipeWithInput(GetKitchenObject().GetKitchenObjectSO())) {
            // There is a kitchen object on this counter which can be cut 
            // Debug.Log("There is a kitchen object on this counter");
            cuttingProgress++;
            OnCut?.Invoke(this, EventArgs.Empty);
            OnAnyCut?.Invoke(this, EventArgs.Empty);
            CuttingRecipeSO cuttingRecipeSO = GetCuttingRecipeSOWithInput(GetKitchenObject().GetKitchenObjectSO());
            OnProgressChanged?.Invoke(this, new IHasProgress.OnProgressChangedEventArgs {
                progressNormalized = (float)cuttingProgress / cuttingRecipeSO.cuttingProgressMax
            });
            if (cuttingRecipeSO != null) {
                if (cuttingProgress >= cuttingRecipeSO.cuttingProgressMax) {
                    KitchenObjectSO outputKitchenObjectSO = GetOutputForInput(GetKitchenObject().GetKitchenObjectSO());
                    if (outputKitchenObjectSO != null) {
                        GetKitchenObject().DestroySelf();
                        KitchenObject.SpawnKitchenObject(outputKitchenObjectSO, this);
                    }
                }
            }
        } else {
            // There is no kitchen object on this counter
            // Debug.Log("There is no kitchen object on this counter");
        }
    }

    private KitchenObjectSO GetOutputForInput(KitchenObjectSO inputKitchenObjectSO) {
        CuttingRecipeSO cuttingRecipeSO = GetCuttingRecipeSOWithInput(inputKitchenObjectSO);
        if (cuttingRecipeSO != null) {
            return cuttingRecipeSO.output;
        } else {
            return null;
        }
    }

    private bool HasRecipeWithInput(KitchenObjectSO inputKitchenObjectSO) {
        CuttingRecipeSO cuttingRecipeSO = GetCuttingRecipeSOWithInput(inputKitchenObjectSO);
        return cuttingRecipeSO != null;
    }

    private CuttingRecipeSO GetCuttingRecipeSOWithInput(KitchenObjectSO inputKitchenObjectSO) {
        foreach (CuttingRecipeSO cuttingRecipeSO in cuttingRecipeSOArray) {
            if (cuttingRecipeSO.input == inputKitchenObjectSO) {
                return cuttingRecipeSO;
            }
        }
        return null;
    }
}
