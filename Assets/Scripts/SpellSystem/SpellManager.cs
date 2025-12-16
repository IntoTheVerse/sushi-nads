using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages spell activation, cooldowns, and effects (haste, enchantment, etc.)
/// Singleton pattern for easy access across the game.
/// </summary>
public class SpellManager : MonoBehaviour
{
    public static SpellManager Instance { get; private set; }

    [Header("Haste Spell")]
    [SerializeField] private float hasteSpeedMultiplier = 2f; // player walks 2x faster
    [SerializeField] private float hasteDuration = 5f; // lasts 5 seconds
    [SerializeField] private float hasteCooldown = 10f; // cooldown before can cast again

    [Header("Enchantment Spell")]
    [SerializeField] private float enchantmentDuration = 5f; // effect duration (visual feedback)
    [SerializeField] private float enchantmentCooldown = 8f; // cooldown before can cast again
    [SerializeField] private bool showPopupWhenFull = true; // show popup if ingredient already prepared

    [Header("Plate Call Spell")]
    [SerializeField] private float plateCallCooldown = 6f;
    [SerializeField] private KitchenObjectSO plateKitchenObjectSO;

    [Header("Nori Swap Spell")]
    [SerializeField] private float noriSwapCooldown = 4f;
    [SerializeField] private KitchenObjectSO noriRollSO;
    [SerializeField] private KitchenObjectSO noriConeSO;

    [Header("Magnet Plating Spell")]
    [SerializeField] private float magnetPlatingCooldown = 10f;
    [SerializeField] private float magnetPlatingRadius = 6f;

    // Spell state tracking
    private float hasteEndTime = 0f;
    private float hasteCooldownEndTime = 0f;
    private float enchantmentCooldownEndTime = 0f;
    private float plateCallCooldownEndTime = 0f;
    private float noriSwapCooldownEndTime = 0f;
    private float magnetPlatingCooldownEndTime = 0f;

    public event EventHandler OnHasteActivated;
    public event EventHandler OnHasteEnded;
    public event EventHandler OnEnchantmentActivated;
    public event EventHandler OnEnchantmentFailed;
    public event EventHandler OnPlateCallActivated;
    public event EventHandler OnPlateCallFailed;
    public event EventHandler OnNoriSwapActivated;
    public event EventHandler OnNoriSwapFailed;
    public event EventHandler OnMagnetPlatingActivated;
    public event EventHandler OnMagnetPlatingFailed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        // Check if haste has expired
        if (hasteEndTime > 0f && Time.time >= hasteEndTime)
        {
            EndHaste();
        }
    }

    /// <summary>
    /// Activate haste spell: player walks faster for a duration
    /// </summary>
    public bool ActivateHaste()
    {
        // Check cooldown
        if (Time.time < hasteCooldownEndTime)
        {
            Debug.Log($"Haste on cooldown. Ready in {(hasteCooldownEndTime - Time.time):F1}s");
            return false;
        }

        if (Player.Instance == null) return false;

        // If haste already active, extend duration instead of restarting
        if (hasteEndTime > Time.time)
        {
            hasteEndTime = Time.time + hasteDuration;
            Debug.Log($"Haste extended. Will end at {hasteEndTime}");
            return true;
        }

        hasteEndTime = Time.time + hasteDuration;
        hasteCooldownEndTime = Time.time + hasteCooldown;
        Player.Instance.SetSpeedMultiplier(hasteSpeedMultiplier);
        OnHasteActivated?.Invoke(this, EventArgs.Empty);
        Debug.Log($"Haste spell activated. Speed x{hasteSpeedMultiplier} for {hasteDuration}s, cooldown {hasteCooldown}s");
        return true;
    }

    private void EndHaste()
    {
        if (Player.Instance == null) return;
        hasteEndTime = 0f;
        Player.Instance.SetSpeedMultiplier(1f); // reset to normal speed
        OnHasteEnded?.Invoke(this, EventArgs.Empty);
        Debug.Log("Haste spell ended. Speed returned to normal.");
    }

    public bool IsHasteActive()
    {
        return hasteEndTime > Time.time;
    }

    public float GetHasteRemainingDuration()
    {
        if (!IsHasteActive()) return 0f;
        return Mathf.Max(0f, hasteEndTime - Time.time);
    }

    public float GetHasteCooldownRemaining()
    {
        if (Time.time >= hasteCooldownEndTime) return 0f;
        return Mathf.Max(0f, hasteCooldownEndTime - Time.time);
    }

    /// <summary>
    /// Activate enchantment spell: transform held ingredient to final form
    /// If already prepared, show popup.
    /// </summary>
    public bool ActivateEnchantment()
    {
        // Check cooldown
        if (Time.time < enchantmentCooldownEndTime)
        {
            Debug.Log($"Enchantment on cooldown. Ready in {(enchantmentCooldownEndTime - Time.time):F1}s");
            OnEnchantmentFailed?.Invoke(this, EventArgs.Empty);
            return false;
        }

        if (Player.Instance == null || !Player.Instance.HasKitchenObject())
        {
            ShowPopup("Hold an ingredient to be enchanted!");
            OnEnchantmentFailed?.Invoke(this, EventArgs.Empty);
            return false;
        }

        var kitchenObject = Player.Instance.GetKitchenObject();
        if (kitchenObject == null)
        {
            ShowPopup("Hold an ingredient to be enchanted!");
            OnEnchantmentFailed?.Invoke(this, EventArgs.Empty);
            return false;
        }

        var heldSO = kitchenObject.GetKitchenObjectSO();
        if (heldSO == null)
        {
            ShowPopup("Hold an ingredient to be enchanted!");
            OnEnchantmentFailed?.Invoke(this, EventArgs.Empty);
            return false;
        }

        // Try to find the final form of this ingredient (first via cutting, then via stove)
        KitchenObjectSO finalSO = FindFinalFormByCutting(heldSO);
        if (finalSO == null)
        {
            finalSO = FindFinalFormByStove(heldSO);
        }

        if (finalSO == null || finalSO == heldSO)
        {
            // No transformation found or already in final form
            ShowPopup("This ingredient is already prepared!");
            OnEnchantmentFailed?.Invoke(this, EventArgs.Empty);
            return false;
        }

        // Transform: destroy held object and spawn final form
        kitchenObject.DestroySelf();
        KitchenObject.SpawnKitchenObject(finalSO, Player.Instance);
        enchantmentCooldownEndTime = Time.time + enchantmentCooldown;
        OnEnchantmentActivated?.Invoke(this, EventArgs.Empty);
        Debug.Log($"Enchanted {heldSO.objectName} into {finalSO.objectName}");
        ShowPopup($"Enchanted: {heldSO.objectName} -> {finalSO.objectName}");
        ResetCountersAfterInstantTransform();
        return true;
    }

    public float GetEnchantmentCooldownRemaining()
    {
        if (Time.time >= enchantmentCooldownEndTime) return 0f;
        return Mathf.Max(0f, enchantmentCooldownEndTime - Time.time);
    }

    /// <summary>
    /// Summon a plate directly into the player's hand if free.
    /// </summary>
    public bool ActivatePlateCall()
    {
        if (Time.time < plateCallCooldownEndTime)
        {
            Debug.Log($"Plate Call on cooldown. Ready in {(plateCallCooldownEndTime - Time.time):F1}s");
            OnPlateCallFailed?.Invoke(this, EventArgs.Empty);
            return false;
        }

        if (plateKitchenObjectSO == null)
        {
            Debug.LogWarning("Plate Call failed: no plate KitchenObjectSO assigned.");
            OnPlateCallFailed?.Invoke(this, EventArgs.Empty);
            return false;
        }

        if (Player.Instance == null)
        {
            OnPlateCallFailed?.Invoke(this, EventArgs.Empty);
            return false;
        }

        if (Player.Instance.HasKitchenObject())
        {
            ShowPopup("Hand must be empty to summon a plate.");
            OnPlateCallFailed?.Invoke(this, EventArgs.Empty);
            return false;
        }

        KitchenObject.SpawnKitchenObject(plateKitchenObjectSO, Player.Instance);
        plateCallCooldownEndTime = Time.time + plateCallCooldown;
        OnPlateCallActivated?.Invoke(this, EventArgs.Empty);
        Debug.Log("Plate Call succeeded: plate summoned to player.");
        return true;
    }

    /// <summary>
    /// Swap the held nori base between roll and cone variants.
    /// </summary>
    public bool ActivateNoriSwap()
    {
        if (Time.time < noriSwapCooldownEndTime)
        {
            Debug.Log($"Nori Swap on cooldown. Ready in {(noriSwapCooldownEndTime - Time.time):F1}s");
            OnNoriSwapFailed?.Invoke(this, EventArgs.Empty);
            return false;
        }

        if (Player.Instance == null || !Player.Instance.HasKitchenObject())
        {
            ShowPopup("Hold a nori base to swap it.");
            OnNoriSwapFailed?.Invoke(this, EventArgs.Empty);
            return false;
        }

        if (noriRollSO == null || noriConeSO == null)
        {
            Debug.LogWarning("Nori Swap failed: nori references are not assigned.");
            OnNoriSwapFailed?.Invoke(this, EventArgs.Empty);
            return false;
        }

        KitchenObject held = Player.Instance.GetKitchenObject();
        KitchenObjectSO heldSO = held.GetKitchenObjectSO();
        KitchenObjectSO target = null;

        if (heldSO == noriRollSO)
        {
            target = noriConeSO;
        }
        else if (heldSO == noriConeSO)
        {
            target = noriRollSO;
        }
        else
        {
            ShowPopup("Hold a Nori Roll or Nori Cone to swap.");
            OnNoriSwapFailed?.Invoke(this, EventArgs.Empty);
            return false;
        }

        held.DestroySelf();
        KitchenObject.SpawnKitchenObject(target, Player.Instance);
        noriSwapCooldownEndTime = Time.time + noriSwapCooldown;
        OnNoriSwapActivated?.Invoke(this, EventArgs.Empty);
        Debug.Log($"Nori Swap succeeded: {heldSO.objectName} -> {target.objectName}");
        return true;
    }

    /// <summary>
    /// Pull nearby prepared ingredients from counters onto the held plate.
    /// </summary>
    public bool ActivateMagnetPlating()
    {
        if (Time.time < magnetPlatingCooldownEndTime)
        {
            Debug.Log($"Magnet Plating on cooldown. Ready in {(magnetPlatingCooldownEndTime - Time.time):F1}s");
            OnMagnetPlatingFailed?.Invoke(this, EventArgs.Empty);
            return false;
        }

        if (Player.Instance == null || !Player.Instance.HasKitchenObject() || !Player.Instance.GetKitchenObject().TryGetPlate(out PlateKitchenObject plate))
        {
            ShowPopup("Hold a plate to pull ingredients onto it.");
            OnMagnetPlatingFailed?.Invoke(this, EventArgs.Empty);
            return false;
        }

        List<BaseCounter> countersInRange = GetCountersInRange();
        bool addedAny = false;

        // Pass 1: make sure a base is pulled first if missing
        if (!plate.HasBaseIngredient())
        {
            addedAny |= PullIngredientsToPlate(countersInRange, plate, onlyBase: true);
        }

        // Pass 2: pull any other valid prepared ingredients
        addedAny |= PullIngredientsToPlate(countersInRange, plate, onlyBase: false);

        if (!addedAny)
        {
            ShowPopup("No prepared ingredients found for magnet plating.");
            OnMagnetPlatingFailed?.Invoke(this, EventArgs.Empty);
            return false;
        }

        magnetPlatingCooldownEndTime = Time.time + magnetPlatingCooldown;
        OnMagnetPlatingActivated?.Invoke(this, EventArgs.Empty);
        Debug.Log("Magnet Plating pulled prepared ingredients to the plate.");
        return true;
    }

    public float GetHasteCooldownDuration() => hasteCooldown;
    public float GetEnchantmentCooldownDuration() => enchantmentCooldown;
    public float GetPlateCallCooldownDuration() => plateCallCooldown;
    public float GetNoriSwapCooldownDuration() => noriSwapCooldown;
    public float GetMagnetPlatingCooldownDuration() => magnetPlatingCooldown;

    public float GetPlateCallCooldownRemaining()
    {
        if (Time.time >= plateCallCooldownEndTime) return 0f;
        return Mathf.Max(0f, plateCallCooldownEndTime - Time.time);
    }

    public float GetNoriSwapCooldownRemaining()
    {
        if (Time.time >= noriSwapCooldownEndTime) return 0f;
        return Mathf.Max(0f, noriSwapCooldownEndTime - Time.time);
    }

    public float GetMagnetPlatingCooldownRemaining()
    {
        if (Time.time >= magnetPlatingCooldownEndTime) return 0f;
        return Mathf.Max(0f, magnetPlatingCooldownEndTime - Time.time);
    }

    private KitchenObjectSO FindFinalFormByCutting(KitchenObjectSO input)
    {
        // Try to find any cutting recipe that accepts this input
        var cutters = FindObjectsOfType<CuttingCounter>(true);
        foreach (var cutter in cutters)
        {
            var output = cutter.GetOutputForInputPublic(input);
            if (output != null && output != input)
            {
                // Recursively try to find further transformations
                var further = FindFinalFormByCutting(output);
                return further ?? output;
            }
        }
        return null;
    }

    private KitchenObjectSO FindFinalFormByStove(KitchenObjectSO input)
    {
        // Try to find any stove recipe that accepts this input
        var stoves = FindObjectsOfType<StoveCounter>(true);
        foreach (var stove in stoves)
        {
            var output = stove.GetOutputForInputPublic(input);
            if (output != null && output != input)
            {
                // Recursively try to find further transformations (e.g., cooked -> burned)
                var further = FindFinalFormByStove(output);
                return further ?? output;
            }
        }
        return null;
    }

    private void ShowPopup(string message)
    {
        if (!showPopupWhenFull) return;
        Debug.Log($"[Spell Popup] {message}");
        // TODO: integrate with UI system to show visual popup
    }

    private List<BaseCounter> GetCountersInRange()
    {
        var counters = new List<BaseCounter>(FindObjectsOfType<BaseCounter>(true));
        if (magnetPlatingRadius <= 0f || Player.Instance == null) return counters;

        float radiusSqr = magnetPlatingRadius * magnetPlatingRadius;
        Vector3 origin = Player.Instance.transform.position;
        for (int i = counters.Count - 1; i >= 0; i--)
        {
            if ((counters[i].transform.position - origin).sqrMagnitude > radiusSqr)
            {
                counters.RemoveAt(i);
            }
        }
        return counters;
    }

    private void ResetCountersAfterInstantTransform()
    {
        // Clear any lingering progress/animations on empty processing counters
        var cutters = FindObjectsOfType<CuttingCounter>(true);
        foreach (var cutter in cutters)
        {
            cutter.ForceResetProgressIfEmpty();
        }

        var stoves = FindObjectsOfType<StoveCounter>(true);
        foreach (var stove in stoves)
        {
            if (!stove.HasItemOnTop()) stove.ForceResetToIdle();
        }
    }

    private bool PullIngredientsToPlate(IEnumerable<BaseCounter> counters, PlateKitchenObject plate, bool onlyBase)
    {
        bool added = false;
        foreach (var counter in counters)
        {
            KitchenObject source = counter.GetKitchenObject();
            if (source == null) continue;
            if (source == (KitchenObject)plate) continue; // never pull the held plate

            KitchenObjectSO sourceSO = source.GetKitchenObjectSO();
            if (sourceSO == null) continue;

            bool isBase = plate.IsBaseIngredient(sourceSO);
            if (onlyBase && !isBase) continue;
            if (!onlyBase && isBase && plate.HasBaseIngredient()) continue;

            if (plate.TryAddIngredient(sourceSO))
            {
                RemoveSourceKitchenObject(counter, source);
                added = true;
                if (onlyBase) break; // only one base needed
            }
        }

        return added;
    }

    private void RemoveSourceKitchenObject(BaseCounter counter, KitchenObject source)
    {
        var stove = counter as StoveCounter;
        source.DestroySelf();
        stove?.ForceResetToIdle();
    }
}
