using UnityEngine;
using System;

public class StoveCounter : BaseCounter, IHasProgress
{
    public event EventHandler<IHasProgress.OnProgressChangedEventArgs> OnProgressChanged;
    public event EventHandler<OnStateChangedEventArgs> OnStateChanged;
    public StoveCounter.State GetState() => state;
    public bool HasItemOnTop() => HasKitchenObject();
    public bool IsCooking() => state == State.Frying;
    public bool IsDone() => state == State.Fried;
    public class OnStateChangedEventArgs : EventArgs {
        public State state;
    }
    public enum State {
        Idle,
        Frying,
        Fried,
        Burned,
    }
    private State state;
    [SerializeField] private FryingRecipeSO[] fryingRecipeSOArray;
    [SerializeField] private BurningRecipeSO[] burningRecipeSOArray;
    private float fryingTimer;
    private float burningTimer;
    private FryingRecipeSO fryingRecipeSO;
    private BurningRecipeSO burningRecipeSO;

    private void Start() {
        state = State.Idle;
    }

    private void Update() {
        if (HasKitchenObject()) {
            var currentKitchenObject = GetKitchenObject();
            if (currentKitchenObject == null) {
                ResetToIdle();
                return;
            }

            switch (state) {
                case State.Idle:
                    break;
                case State.Frying:
                    if (fryingRecipeSO == null) {
                        Debug.LogWarning($"[StoveCounter] Missing frying recipe while in Frying state on {name}. Resetting.", this);
                        ResetToIdle();
                        break;
                    }

                    fryingTimer += Time.deltaTime;
                    OnProgressChanged?.Invoke(this, new IHasProgress.OnProgressChangedEventArgs {
                        progressNormalized = fryingTimer / fryingRecipeSO.fryingTimerMax
                    });

                    if (fryingTimer > fryingRecipeSO.fryingTimerMax) {
                        currentKitchenObject.DestroySelf();
                        KitchenObject.SpawnKitchenObject(fryingRecipeSO.output, this);
                        // Debug.Log("Frying complete!");
                        state = State.Fried;
                        OnStateChanged?.Invoke(this, new OnStateChangedEventArgs
                        {
                            state = state
                        });
                        burningTimer = 0f;
                        var cookedObject = GetKitchenObject();
                        burningRecipeSO = cookedObject != null
                            ? GetBurningRecipeSOWithInput(cookedObject.GetKitchenObjectSO())
                            : null;
                        if (burningRecipeSO == null)
                        {
                            Debug.LogWarning($"[StoveCounter] No burning recipe for {cookedObject?.GetKitchenObjectSO()?.name ?? "unknown"} on {name}. Will stay in Fried.", this);
                        }
                        // Notify listeners (steam/lid/FX) that we've entered Fried
                        OnStateChanged?.Invoke(this, new OnStateChangedEventArgs { state = state });
                        OnProgressChanged?.Invoke(this, new IHasProgress.OnProgressChangedEventArgs {
                            progressNormalized = fryingTimer / fryingRecipeSO.fryingTimerMax
                        });
                        
                        // Record well-cooked to blockchain for scoring
                        if (GameplayEventTracker.Instance != null)
                        {
                            GameplayEventTracker.Instance.RecordWellCooked();
                        }
                    }
                    break;
                case State.Fried:
                    if (burningRecipeSO == null)
                    {
                        // Nothing left to burn, stay in Fried but keep progress at zero
                        OnProgressChanged?.Invoke(this, new IHasProgress.OnProgressChangedEventArgs {
                            progressNormalized = 0f
                        });
                        break;
                    }

                    currentKitchenObject = GetKitchenObject();
                    if (currentKitchenObject == null) {
                        ResetToIdle();
                        break;
                    }

                    burningTimer += Time.deltaTime;
                    OnProgressChanged?.Invoke(this, new IHasProgress.OnProgressChangedEventArgs {
                        progressNormalized = burningTimer / burningRecipeSO.burningTimerMax
                    });
                    if (burningTimer > burningRecipeSO.burningTimerMax) {
                        currentKitchenObject.DestroySelf();
                        KitchenObject.SpawnKitchenObject(burningRecipeSO.output, this);
                        // Debug.Log("Object Burned!");
                        state = State.Burned;
                        OnStateChanged?.Invoke(this, new OnStateChangedEventArgs {
                            state = state
                        });

                        OnProgressChanged?.Invoke(this, new IHasProgress.OnProgressChangedEventArgs {
                            progressNormalized = 0f
                        });
                        
                        // Record overcooked to blockchain for scoring
                        if (GameplayEventTracker.Instance != null)
                        {
                            GameplayEventTracker.Instance.RecordOverCooked();
                        }
                    }
                    break;
                case State.Burned:
                    break;
            }
            // Debug.Log(state);
        }
    }

    private void ResetToIdle()
    {
        state = State.Idle;
        burningTimer = 0f;
        fryingTimer = 0f;
        fryingRecipeSO = null;
        burningRecipeSO = null;
        OnStateChanged?.Invoke(this, new OnStateChangedEventArgs { state = state });
        OnProgressChanged?.Invoke(this, new IHasProgress.OnProgressChangedEventArgs { progressNormalized = 0f });
    }

    public override void Interact(Player player)
    {
        if (!HasKitchenObject()) {
            // There is no kitchen object on this counter
            if (player.HasKitchenObject()) {
                // Player is carrying something
                KitchenObject playerKitchenObject = player.GetKitchenObject(); // Store reference before transfer
                if (HasRecipeWithInput(playerKitchenObject.GetKitchenObjectSO())) {
                    // Player is carrying something that can be fried
                    // Debug.Log("Player is carrying a fryable item. Placing on counter.");
                    playerKitchenObject.SetKitchenObjectParent(this);
                    fryingRecipeSO = GetFryingRecipeSOWithInput(playerKitchenObject.GetKitchenObjectSO());
                    state = State.Frying;
                    fryingTimer = 0f;
                    OnStateChanged?.Invoke(this, new OnStateChangedEventArgs {
                        state = state
                    });
                    OnProgressChanged?.Invoke(this, new IHasProgress.OnProgressChangedEventArgs {
                        progressNormalized = fryingTimer / fryingRecipeSO.fryingTimerMax
                    });
                } else {
                    // Player is not carrying anything that can be cut
                    // Debug.Log("Player is carrying an item, but it cannot be fried on this counter.");
                }
            } else {
                // Player is not carrying anything
                // Debug.Log("Player is not carrying anything to place on the counter.");
            }
        } else {
            // There is a kitchen object on this counter
            if (player.HasKitchenObject()) {
                // Player is carrying something
                // Debug.Log("Counter has object, player has object. Cannot place item.");
                // Player is carrying something
                if (player.GetKitchenObject().TryGetPlate(out PlateKitchenObject plateKitchenObject)) {
                    // Player is carrying a plate
                    if (plateKitchenObject.TryAddIngredient(GetKitchenObject().GetKitchenObjectSO())) {
                        GetKitchenObject().DestroySelf();
                        state = State.Idle;
                        OnStateChanged?.Invoke(this, new OnStateChangedEventArgs {
                            state = state
                        });

                        OnProgressChanged?.Invoke(this, new IHasProgress.OnProgressChangedEventArgs {
                            progressNormalized = 0f
                        });
                    }
                }

            } else {
                // Player is not carrying anything
                // Debug.Log("Counter has object, player is empty. Taking item.");
                GetKitchenObject().SetKitchenObjectParent(player);
                state = State.Idle;
                OnStateChanged?.Invoke(this, new OnStateChangedEventArgs {
                    state = state
                });

                OnProgressChanged?.Invoke(this, new IHasProgress.OnProgressChangedEventArgs {
                    progressNormalized = 0f
                });
            }
        }
    }

    private KitchenObjectSO GetOutputForInput(KitchenObjectSO inputKitchenObjectSO) {
        FryingRecipeSO fryingRecipeSO = GetFryingRecipeSOWithInput(inputKitchenObjectSO);
        if (fryingRecipeSO != null) {
            return fryingRecipeSO.output;
        } else {
            return null;
        }
    }

    // Public wrapper so external systems can query what this stove counter would produce for a given input
    public KitchenObjectSO GetOutputForInputPublic(KitchenObjectSO inputKitchenObjectSO)
    {
        return GetOutputForInput(inputKitchenObjectSO);
    }

    // Public query: can this stove counter produce the given output (used when matching recipe requirements)?
    public bool CanProduce(KitchenObjectSO outputKitchenObjectSO)
    {
        foreach (FryingRecipeSO fryingRecipeSO in fryingRecipeSOArray)
        {
            if (fryingRecipeSO.output == outputKitchenObjectSO) return true;
        }
        foreach (BurningRecipeSO burningRecipeSO in burningRecipeSOArray)
        {
            if (burningRecipeSO.output == outputKitchenObjectSO) return true;
        }
        return false;
    }

    private bool HasRecipeWithInput(KitchenObjectSO inputKitchenObjectSO) {
        FryingRecipeSO fryingRecipeSO = GetFryingRecipeSOWithInput(inputKitchenObjectSO);
        return fryingRecipeSO != null;
    }

    // Public wrapper for external checks (e.g., highlight manager)
    public bool CanProcess(KitchenObjectSO inputKitchenObjectSO)
    {
        return HasRecipeWithInput(inputKitchenObjectSO);
    }

    private FryingRecipeSO GetFryingRecipeSOWithInput(KitchenObjectSO inputKitchenObjectSO) {
        foreach (FryingRecipeSO fryingRecipeSO in fryingRecipeSOArray) {
            if (fryingRecipeSO.input == inputKitchenObjectSO) {
                return fryingRecipeSO;
            }
        }
        return null;
    }

    private BurningRecipeSO GetBurningRecipeSOWithInput(KitchenObjectSO inputKitchenObjectSO) {
        foreach (BurningRecipeSO burningRecipeSO in burningRecipeSOArray) {
            if (burningRecipeSO.input == inputKitchenObjectSO) {
                return burningRecipeSO;
            }
        }
        return null;
    }
   
}
