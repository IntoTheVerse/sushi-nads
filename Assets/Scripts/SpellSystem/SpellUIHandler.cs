using UnityEngine;
using UnityEngine.UI;
using TMPro;
/// <summary>
/// Handles spell input, UI interactions, and cooldown display with radial fill images.
/// Listens for spell button presses and calls SpellManager to activate spells.
/// </summary>
public class SpellUIHandler : MonoBehaviour
{
    [Tooltip("Key to cast haste spell")]
    [SerializeField] private KeyCode hasteKey = KeyCode.Q;

    [Tooltip("Key to cast enchantment spell")]
    [SerializeField] private KeyCode enchantmentKey = KeyCode.E;

    [Header("Haste Spell UI")]
    [SerializeField] private GameObject hasteButtonUI; // visual button for haste
    [SerializeField] private Image hasteRadialFill; // radial fill image for cooldown
    [SerializeField] private TMP_Text hasteButtonText; // optional text label

    [Header("Enchantment Spell UI")]
    [SerializeField] private GameObject enchantmentButtonUI; // visual button for enchantment
    [SerializeField] private Image enchantmentRadialFill; // radial fill image for cooldown
    [SerializeField] private TMP_Text enchantmentButtonText; // optional text label

    private float hasteTotalCooldown = 0f;
    private float enchantmentTotalCooldown = 0f;

    private void Start()
    {
        if (SpellManager.Instance != null)
        {
            SpellManager.Instance.OnHasteActivated += OnHasteActivated;
            SpellManager.Instance.OnEnchantmentActivated += OnEnchantmentActivated;
            SpellManager.Instance.OnEnchantmentFailed += OnEnchantmentFailed;

            // Cache cooldown durations (via reflection from SpellManager inspector values if needed)
            // For now, we'll update them dynamically in Update()
        }

        // Hook up button click listeners
        if (hasteButtonUI != null)
        {
            Button hasteBtn = hasteButtonUI.GetComponent<Button>();
            if (hasteBtn != null)
            {
                hasteBtn.onClick.AddListener(() => OnHasteButtonClicked());
                Debug.Log("✅ Haste button hooked up successfully");
            }
        }

        if (enchantmentButtonUI != null)
        {
            Button enchantBtn = enchantmentButtonUI.GetComponent<Button>();
            if (enchantBtn != null)
            {
                enchantBtn.onClick.AddListener(() => OnEnchantmentButtonClicked());
                Debug.Log("✅ Enchantment button hooked up successfully");
            }
        }
    }

    private void OnDestroy()
    {
        if (SpellManager.Instance != null)
        {
            SpellManager.Instance.OnHasteActivated -= OnHasteActivated;
            SpellManager.Instance.OnEnchantmentActivated -= OnEnchantmentActivated;
            SpellManager.Instance.OnEnchantmentFailed -= OnEnchantmentFailed;
        }
    }

    private void Update()
    {
        if (!KitchenGameManager.Instance || !KitchenGameManager.Instance.IsGamePlaying())
            return;

        // Update radial fill displays
        UpdateHasteCooldownUI();
        UpdateEnchantmentCooldownUI();

        // Check for haste spell input
        if (Input.GetKeyDown(hasteKey))
        {
            if (SpellManager.Instance != null)
            {
                SpellManager.Instance.ActivateHaste();
            }
        }

        // Check for enchantment spell input
        if (Input.GetKeyDown(enchantmentKey))
        {
            if (SpellManager.Instance != null)
            {
                SpellManager.Instance.ActivateEnchantment();
            }
        }
    }

    private void OnHasteButtonClicked()
    {
        Debug.Log("🏃 Haste button clicked!");
        if (SpellManager.Instance != null)
        {
            SpellManager.Instance.ActivateHaste();
        }
    }

    private void OnEnchantmentButtonClicked()
    {
        Debug.Log("✨ Enchantment button clicked!");
        if (SpellManager.Instance != null)
        {
            SpellManager.Instance.ActivateEnchantment();
        }
    }

    private void UpdateHasteCooldownUI()
    {
        if (SpellManager.Instance == null || hasteRadialFill == null) return;

        float cooldownRemaining = SpellManager.Instance.GetHasteCooldownRemaining();
        
        // For simplicity, assume cooldown is 10 seconds (you can make this configurable)
        float cooldownTotal = 10f;
        float fillAmount = 1f - (cooldownRemaining / cooldownTotal);
        hasteRadialFill.fillAmount = Mathf.Clamp01(fillAmount);

        // Update button interactivity
        if (hasteButtonUI != null)
        {
            hasteButtonUI.GetComponent<Button>().interactable = (cooldownRemaining <= 0f);
        }

        // Update text label
        if (hasteButtonText != null)
        {
            if (cooldownRemaining > 0f)
                hasteButtonText.text = $"Q\n{cooldownRemaining:F1}s";
            else
                hasteButtonText.text = "Q\nReady";
        }
    }

    private void UpdateEnchantmentCooldownUI()
    {
        if (SpellManager.Instance == null || enchantmentRadialFill == null) return;

        float cooldownRemaining = SpellManager.Instance.GetEnchantmentCooldownRemaining();
        
        // For simplicity, assume cooldown is 8 seconds (you can make this configurable)
        float cooldownTotal = 8f;
        float fillAmount = 1f - (cooldownRemaining / cooldownTotal);
        enchantmentRadialFill.fillAmount = Mathf.Clamp01(fillAmount);

        // Update button interactivity
        if (enchantmentButtonUI != null)
        {
            enchantmentButtonUI.GetComponent<Button>().interactable = (cooldownRemaining <= 0f);
        }

        // Update text label
        if (enchantmentButtonText != null)
        {
            if (cooldownRemaining > 0f)
                enchantmentButtonText.text = $"E\n{cooldownRemaining:F1}s";
            else
                enchantmentButtonText.text = "E\nReady";
        }
    }

    private void OnHasteActivated(object sender, System.EventArgs e)
    {
        Debug.Log("🏃 Haste activated!");
        // Optional: trigger visual/audio feedback
    }

    private void OnEnchantmentActivated(object sender, System.EventArgs e)
    {
        Debug.Log("✨ Enchantment activated!");
        // Optional: trigger visual/audio feedback
    }

    private void OnEnchantmentFailed(object sender, System.EventArgs e)
    {
        Debug.Log("❌ Enchantment failed or on cooldown");
        // Optional: trigger failure feedback
    }
}
