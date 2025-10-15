using System;
using System.Collections.Generic;
using UnityEngine;

public class PlateKitchenObject : KitchenObject
{
    // [SerializeField] private List<KitchenObjectSO> validKitchenObjectSO;

    // private List<KitchenObjectSO> kitchenObjectSOList;

    // private void Awake() {
    //     kitchenObjectSOList = new List<KitchenObjectSO>();
    // }
    public event EventHandler<OnIngredientAddedEventArgs> OnIngredientAdded;
    public class OnIngredientAddedEventArgs : EventArgs {
        public KitchenObjectSO kitchenObjectSO;
    }
    [Serializable]
    public struct VariantRule
    {
        public KitchenObjectSO baseSO;   // e.g., NoriCone
        public KitchenObjectSO inputSO;  // e.g., RiceSmall
        public KitchenObjectSO outputSO; // e.g., RiceBig
    }
    [SerializeField] private List<KitchenObjectSO> validKitchenObjectSOList;
    public static event System.EventHandler OnAnyIngredientAdded;

    private List<KitchenObjectSO> kitchenObjectSOList;
    [SerializeField] private List<KitchenObjectSO> baseIngredients;
    [Header("Variant Rules (per base)")]
    [SerializeField] private List<VariantRule> variantRules = new();
    private void Awake() {
        kitchenObjectSOList = new List<KitchenObjectSO>();
    }

    private bool IsBase(KitchenObjectSO so) => baseIngredients != null && baseIngredients.Contains(so);
    private bool HasBase()
    {
        if (baseIngredients == null || baseIngredients.Count == 0) return true; // no base defined -> no gating
        foreach (var so in kitchenObjectSOList) if (baseIngredients.Contains(so)) return true;
        return false;
    }
    private bool TryGetCurrentBase(out KitchenObjectSO baseSO)
    {
        if (baseIngredients != null)
            foreach (var so in kitchenObjectSOList)
                if (baseIngredients.Contains(so)) { baseSO = so; return true; }
        baseSO = null; return false;
    }

    private KitchenObjectSO ResolveVariantForBase(KitchenObjectSO input)
    {
        if (TryGetCurrentBase(out var b))
        {
            for (int i = 0; i < variantRules.Count; i++)
            {
                var r = variantRules[i];
                if (r.baseSO == b && r.inputSO == input) return r.outputSO;
            }
        }
        return input;
    }

    public bool TryAddIngredient(KitchenObjectSO kitchenObjectSO)
    {
        // BASE-FIRST gate
        bool hasBase = TryGetCurrentBase(out _);
        bool isBase = IsBase(kitchenObjectSO);
        if (!hasBase && !isBase)
        {
            Debug.Log($"[Plate] Refused {kitchenObjectSO.name}: base required first");
            return false;
        }
        if (hasBase && isBase)
        {
            Debug.Log($"[Plate] Refused {kitchenObjectSO.name}: base already present");
            return false;
        }

        // Resolve size/variant based on current base BEFORE validating
        KitchenObjectSO effectiveSO = ResolveVariantForBase(kitchenObjectSO);
        if (effectiveSO != kitchenObjectSO)
        {
            Debug.Log($"[Plate] Converted {kitchenObjectSO.name} -> {effectiveSO.name} for current base");
        }

        // Validate against FINAL (effective) ingredient
        if (!validKitchenObjectSOList.Contains(effectiveSO))
        {
            Debug.Log($"[Plate] Refused {effectiveSO.name}: NOT VALID for plate");
            return false;
        }

        // No duplicates (check against effective)
        if (kitchenObjectSOList.Contains(effectiveSO))
        {
            Debug.Log($"[Plate] Refused {effectiveSO.name}: DUPLICATE");
            return false;
        }

        // Accept
        kitchenObjectSOList.Add(effectiveSO);
        OnIngredientAdded?.Invoke(this, new OnIngredientAddedEventArgs { kitchenObjectSO = effectiveSO });
        OnAnyIngredientAdded?.Invoke(this, EventArgs.Empty);
        return true;
    }
    public List<KitchenObjectSO> GetKitchenObjectSOList() {
        return kitchenObjectSOList;
    }
}
