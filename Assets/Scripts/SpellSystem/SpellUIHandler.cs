using System;
using System.Collections;
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

    [Tooltip("Key to call a plate into hand")]
    [SerializeField] private KeyCode plateCallKey = KeyCode.R;

    [Tooltip("Key to swap between nori roll and cone")]
    [SerializeField] private KeyCode noriSwapKey = KeyCode.T;

    [Tooltip("Key to magnet-plate prepared ingredients")]
    [SerializeField] private KeyCode magnetPlatingKey = KeyCode.F;

    [Header("Haste Spell UI")]
    [SerializeField] private GameObject hasteButtonUI; // visual button for haste
    [SerializeField] private Image hasteRadialFill; // radial fill image for cooldown
    [SerializeField] private TMP_Text hasteButtonText; // optional text label

    [Header("Enchantment Spell UI")]
    [SerializeField] private GameObject enchantmentButtonUI; // visual button for enchantment
    [SerializeField] private Image enchantmentRadialFill; // radial fill image for cooldown
    [SerializeField] private TMP_Text enchantmentButtonText; // optional text label

    [Header("Plate Call Spell UI")]
    [SerializeField] private GameObject plateCallButtonUI;
    [SerializeField] private Image plateCallRadialFill;
    [SerializeField] private TMP_Text plateCallButtonText;

    [Header("Nori Swap Spell UI")]
    [SerializeField] private GameObject noriSwapButtonUI;
    [SerializeField] private Image noriSwapRadialFill;
    [SerializeField] private TMP_Text noriSwapButtonText;

    [Header("Magnet Plating Spell UI")]
    [SerializeField] private GameObject magnetPlatingButtonUI;
    [SerializeField] private Image magnetPlatingRadialFill;
    [SerializeField] private TMP_Text magnetPlatingButtonText;

    [Header("Spell Panel Toggle")]
    [SerializeField] private RectTransform spellPanel;
    [SerializeField] private GameObject spellPanelToggleButton;
    [SerializeField] private float slideDuration = 0.25f;
    [SerializeField] private float slideOffset = 950f;
    [SerializeField] private bool startHidden = true;
    [SerializeField] private bool slideFromRight = true;

    private Vector2 panelShownPosition;
    private Vector2 panelHiddenPosition;
    private bool isPanelVisible;
    private Coroutine slideRoutine;

    private void Start()
    {
        var spell = SpellManager.Instance;
        if (spell != null)
        {
            spell.OnHasteActivated += OnHasteActivated;
            spell.OnEnchantmentActivated += OnEnchantmentActivated;
            spell.OnEnchantmentFailed += OnEnchantmentFailed;
            spell.OnPlateCallActivated += OnPlateCallActivated;
            spell.OnPlateCallFailed += OnPlateCallFailed;
            spell.OnNoriSwapActivated += OnNoriSwapActivated;
            spell.OnNoriSwapFailed += OnNoriSwapFailed;
            spell.OnMagnetPlatingActivated += OnMagnetPlatingActivated;
            spell.OnMagnetPlatingFailed += OnMagnetPlatingFailed;
        }

        WireButton(hasteButtonUI, OnHasteButtonClicked);
        WireButton(enchantmentButtonUI, OnEnchantmentButtonClicked);
        WireButton(plateCallButtonUI, OnPlateCallButtonClicked);
        WireButton(noriSwapButtonUI, OnNoriSwapButtonClicked);
        WireButton(magnetPlatingButtonUI, OnMagnetPlatingButtonClicked);
        WireButton(spellPanelToggleButton, TogglePanel);

        SetupPanelPositions();
    }

    private void OnDestroy()
    {
        var spell = SpellManager.Instance;
        if (spell != null)
        {
            spell.OnHasteActivated -= OnHasteActivated;
            spell.OnEnchantmentActivated -= OnEnchantmentActivated;
            spell.OnEnchantmentFailed -= OnEnchantmentFailed;
            spell.OnPlateCallActivated -= OnPlateCallActivated;
            spell.OnPlateCallFailed -= OnPlateCallFailed;
            spell.OnNoriSwapActivated -= OnNoriSwapActivated;
            spell.OnNoriSwapFailed -= OnNoriSwapFailed;
            spell.OnMagnetPlatingActivated -= OnMagnetPlatingActivated;
            spell.OnMagnetPlatingFailed -= OnMagnetPlatingFailed;
        }
    }

    private void Update()
    {
        if (!KitchenGameManager.Instance || !KitchenGameManager.Instance.IsGamePlaying())
            return;

        // Update radial fill displays
        UpdateHasteCooldownUI();
        UpdateEnchantmentCooldownUI();
        UpdatePlateCallCooldownUI();
        UpdateNoriSwapCooldownUI();
        UpdateMagnetPlatingCooldownUI();

        // Check for spell inputs
        var spell = SpellManager.Instance;
        if (spell == null) return;

        if (Input.GetKeyDown(hasteKey))
        {
            TryCastSpell(() => spell.ActivateHaste());
        }

        if (Input.GetKeyDown(enchantmentKey))
        {
            TryCastSpell(() => spell.ActivateEnchantment());
        }

        if (Input.GetKeyDown(plateCallKey))
        {
            TryCastSpell(() => spell.ActivatePlateCall());
        }

        if (Input.GetKeyDown(noriSwapKey))
        {
            TryCastSpell(() => spell.ActivateNoriSwap());
        }

        if (Input.GetKeyDown(magnetPlatingKey))
        {
            TryCastSpell(() => spell.ActivateMagnetPlating());
        }
    }

    private void WireButton(GameObject buttonUI, UnityEngine.Events.UnityAction action)
    {
        if (buttonUI == null || action == null) return;
        Button btn = buttonUI.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.AddListener(action);
        }
    }

    private void OnHasteButtonClicked() => TryCastSpell(() => SpellManager.Instance.ActivateHaste());
    private void OnEnchantmentButtonClicked() => TryCastSpell(() => SpellManager.Instance.ActivateEnchantment());
    private void OnPlateCallButtonClicked() => TryCastSpell(() => SpellManager.Instance.ActivatePlateCall());
    private void OnNoriSwapButtonClicked() => TryCastSpell(() => SpellManager.Instance.ActivateNoriSwap());
    private void OnMagnetPlatingButtonClicked() => TryCastSpell(() => SpellManager.Instance.ActivateMagnetPlating());

    private void TogglePanel()
    {
        if (isPanelVisible)
        {
            HidePanel();
        }
        else
        {
            ShowPanel();
        }
    }

    private void ShowPanel()
    {
        if (spellPanel == null) return;
        if (isPanelVisible) return;
        isPanelVisible = true;
        if (spellPanelToggleButton != null){}// spellPanelToggleButton.SetActive(false);
        SlidePanel(panelShownPosition);
    }

    private void HidePanel()
    {
        if (spellPanel == null) return;
        if (!isPanelVisible) return;
        isPanelVisible = false;
        SlidePanel(panelHiddenPosition, () =>
        {
            if (spellPanelToggleButton != null) {}//spellPanelToggleButton.SetActive(true);
        });
    }

    private void SlidePanel(Vector2 targetPosition, System.Action onComplete = null)
    {
        if (slideRoutine != null) StopCoroutine(slideRoutine);
        slideRoutine = StartCoroutine(SlideRoutine(targetPosition, onComplete));
    }

    private IEnumerator SlideRoutine(Vector2 targetPosition, System.Action onComplete)
    {
        if (spellPanel == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        Vector2 start = spellPanel.anchoredPosition;
        if (Mathf.Approximately(slideDuration, 0f))
        {
            spellPanel.anchoredPosition = targetPosition;
            onComplete?.Invoke();
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / slideDuration);
            spellPanel.anchoredPosition = Vector2.Lerp(start, targetPosition, t);
            yield return null;
        }
        spellPanel.anchoredPosition = targetPosition;
        onComplete?.Invoke();
    }

    private void SetupPanelPositions()
    {
        if (spellPanel == null) return;

        panelShownPosition = spellPanel.anchoredPosition;
        float offset = slideOffset > 0f ? slideOffset : spellPanel.rect.width;
        Vector2 dir = slideFromRight ? Vector2.right : Vector2.left;
        panelHiddenPosition = panelShownPosition + dir * offset;

        if (startHidden)
        {
            spellPanel.anchoredPosition = panelHiddenPosition;
            isPanelVisible = false;
            if (spellPanelToggleButton != null) spellPanelToggleButton.SetActive(true);
        }
        else
        {
            isPanelVisible = true;
            if (spellPanelToggleButton != null) spellPanelToggleButton.SetActive(false);
        }
    }

    private bool TryCastSpell(System.Func<bool> castSpell)
    {
        if (SpellManager.Instance == null) return false;
        bool casted = castSpell();
        if (casted)
        {
            HidePanel();
        }
        return casted;
    }

    private void UpdateHasteCooldownUI()
    {
        if (SpellManager.Instance == null) return;
        UpdateCooldownUI(hasteButtonUI, hasteRadialFill, hasteButtonText,
            SpellManager.Instance.GetHasteCooldownRemaining(),
            SpellManager.Instance.GetHasteCooldownDuration(),
            hasteKey.ToString());
    }

    private void UpdateEnchantmentCooldownUI()
    {
        if (SpellManager.Instance == null) return;
        UpdateCooldownUI(enchantmentButtonUI, enchantmentRadialFill, enchantmentButtonText,
            SpellManager.Instance.GetEnchantmentCooldownRemaining(),
            SpellManager.Instance.GetEnchantmentCooldownDuration(),
            enchantmentKey.ToString());
    }

    private void UpdatePlateCallCooldownUI()
    {
        if (SpellManager.Instance == null) return;
        UpdateCooldownUI(plateCallButtonUI, plateCallRadialFill, plateCallButtonText,
            SpellManager.Instance.GetPlateCallCooldownRemaining(),
            SpellManager.Instance.GetPlateCallCooldownDuration(),
            plateCallKey.ToString());
    }

    private void UpdateNoriSwapCooldownUI()
    {
        if (SpellManager.Instance == null) return;
        UpdateCooldownUI(noriSwapButtonUI, noriSwapRadialFill, noriSwapButtonText,
            SpellManager.Instance.GetNoriSwapCooldownRemaining(),
            SpellManager.Instance.GetNoriSwapCooldownDuration(),
            noriSwapKey.ToString());
    }

    private void UpdateMagnetPlatingCooldownUI()
    {
        if (SpellManager.Instance == null) return;
        UpdateCooldownUI(magnetPlatingButtonUI, magnetPlatingRadialFill, magnetPlatingButtonText,
            SpellManager.Instance.GetMagnetPlatingCooldownRemaining(),
            SpellManager.Instance.GetMagnetPlatingCooldownDuration(),
            magnetPlatingKey.ToString());
    }

    private void UpdateCooldownUI(GameObject buttonUI, Image radialFill, TMP_Text label, float cooldownRemaining, float cooldownTotal, string keyLabel)
    {
        if (radialFill != null && cooldownTotal > 0f)
        {
            // Inverted fill: cover shrinks from full (1) to empty (0) as cooldown expires
            float fillAmount = cooldownRemaining / cooldownTotal;
            radialFill.fillAmount = Mathf.Clamp01(fillAmount);
        }

        if (buttonUI != null)
        {
            var btn = buttonUI.GetComponent<Button>();
            if (btn != null) btn.interactable = (cooldownRemaining <= 0f);
        }

        if (label != null)
        {
            if (cooldownRemaining > 0f)
                label.text = $"{keyLabel}\n{cooldownRemaining:F1}s";
            else
                label.text = $"{keyLabel}\nReady";
        }
    }

    private void OnHasteActivated(object sender, System.EventArgs e)
    {
        Debug.Log("Haste activated.");
    }

    private void OnEnchantmentActivated(object sender, System.EventArgs e)
    {
        Debug.Log("Enchantment activated.");
    }

    private void OnEnchantmentFailed(object sender, System.EventArgs e)
    {
        Debug.Log("Enchantment failed or on cooldown.");
    }

    private void OnPlateCallActivated(object sender, System.EventArgs e)
    {
        Debug.Log("Plate Call activated.");
    }

    private void OnPlateCallFailed(object sender, System.EventArgs e)
    {
        Debug.Log("Plate Call failed or on cooldown.");
    }

    private void OnNoriSwapActivated(object sender, System.EventArgs e)
    {
        Debug.Log("Nori Swap activated.");
    }

    private void OnNoriSwapFailed(object sender, System.EventArgs e)
    {
        Debug.Log("Nori Swap failed or on cooldown.");
    }

    private void OnMagnetPlatingActivated(object sender, System.EventArgs e)
    {
        Debug.Log("Magnet Plating activated.");
    }

    private void OnMagnetPlatingFailed(object sender, System.EventArgs e)
    {
        Debug.Log("Magnet Plating failed or on cooldown.");
    }
}
