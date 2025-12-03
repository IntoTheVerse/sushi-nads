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

    // Spell state tracking
    private float hasteEndTime = 0f;
    private float hasteCooldownEndTime = 0f;
    private float enchantmentCooldownEndTime = 0f;

    public event EventHandler OnHasteActivated;
    public event EventHandler OnHasteEnded;
    public event EventHandler OnEnchantmentActivated;
    public event EventHandler OnEnchantmentFailed;

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
            Debug.Log($"🏃 Haste on cooldown. Ready in {(hasteCooldownEndTime - Time.time):F1}s");
            return false;
        }

        if (Player.Instance == null) return false;

        // If haste already active, extend duration instead of restarting
        if (hasteEndTime > Time.time)
        {
            hasteEndTime = Time.time + hasteDuration;
            Debug.Log($"🏃 Haste extended! Will end at {hasteEndTime}");
            return true;
        }

        hasteEndTime = Time.time + hasteDuration;
        hasteCooldownEndTime = Time.time + hasteCooldown;
        Player.Instance.SetSpeedMultiplier(hasteSpeedMultiplier);
        OnHasteActivated?.Invoke(this, EventArgs.Empty);
        Debug.Log($"🏃 Haste spell activated! Speed x{hasteSpeedMultiplier} for {hasteDuration}s, cooldown {hasteCooldown}s");
        return true;
    }

    private void EndHaste()
    {
        if (Player.Instance == null) return;
        hasteEndTime = 0f;
        Player.Instance.SetSpeedMultiplier(1f); // reset to normal speed
        OnHasteEnded?.Invoke(this, EventArgs.Empty);
        Debug.Log("🏃 Haste spell ended. Speed returned to normal.");
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
            Debug.Log($"✨ Enchantment on cooldown. Ready in {(enchantmentCooldownEndTime - Time.time):F1}s");
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
        Debug.Log($"✨ Enchanted {heldSO.objectName} → {finalSO.objectName}");
        ShowPopup($"Enchanted: {heldSO.objectName} → {finalSO.objectName}");
        return true;
    }

    public float GetEnchantmentCooldownRemaining()
    {
        if (Time.time >= enchantmentCooldownEndTime) return 0f;
        return Mathf.Max(0f, enchantmentCooldownEndTime - Time.time);
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
}
