using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Single, player-mounted interaction UI that follows the currently selected counter.
/// Place this on the world-space canvas above the player's head and wire the Button and TMP Text.
/// It auto-detects the target from Player.Instance.OnSelectedCounterChanged and
/// updates the prompt/action (Pick/Drop/Chop) just like the old per-counter buttons.
/// </summary>
public class CounterUI_HeadButton : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private Button button;              // required
    [SerializeField] private Image label;             // required
    [SerializeField] private Sprite pick;             // required
    [SerializeField] private Sprite drop;             // required
    [SerializeField] private Sprite chop;             // required
    [SerializeField] private Sprite noneSprite;



    [Header("Behavior")]
    [Tooltip("If true, treat ContainerCounter / PlatesCounter as 'Pick' sources when player is empty-handed.")]
    [SerializeField] private bool autoDetectItemSources = true;

    [Tooltip("Force this UI to behave like an item source (always shows Pick when empty-handed). Leave OFF to auto-detect per counter.")]
    [SerializeField] private bool treatAsItemSourceOverride = false;

    [Header("Tutorial Pulse")]
    [SerializeField] private bool enableTutorialPulse = true;
    [SerializeField] private float tutorialPulseCyclesPerSecond = 2.1f;
    [SerializeField] [Range(0f, 0.5f)] private float tutorialPulseScaleAmount = 0.1f;
    private bool subscribed;

    private enum Action { None, Pick, Drop, Chop }
    private Action lastAction = Action.None;

    // Dynamic target driven by Player selection
    private BaseCounter currentCounter;

    // Cutting/Stove progress tracking (for Chop -> Pick switch)
    private IHasProgress currentProgressSource;
    private float progressNormalized; // 0..1 from IHasProgress

    private RectTransform pulseRectTransform;
    private Vector3 pulseBaseScale = Vector3.one;
    private bool pulseBaseScaleInitialized;
    private TutorialManager tutorialManager;

    private void Awake()
    {
        if (!button) button = GetComponentInChildren<Button>(includeInactive: true);
        if (!label) label = GetComponentInChildren<Image>(includeInactive: true);
        pulseRectTransform = button != null ? button.GetComponent<RectTransform>() : null;
        CachePulseBaseScale();
        button.onClick.AddListener(OnClick);
        SetVisible(false);
    }

    private void OnEnable()
    {
        TrySubscribe();
    }


    private void TrySubscribe()
    {
        if (subscribed) return;
        if (Player.Instance == null) return;

        Player.Instance.OnSelectedCounterChanged += OnSelectedCounterChanged;
        subscribed = true;

        // Optional: if your Player exposes the current selection, set it immediately
        // if (Player.Instance.GetSelectedCounter() != null)
        //     SetTargetCounter(Player.Instance.GetSelectedCounter());
    }

    private void Update()
    {
        if (!subscribed) TrySubscribe();
        Refresh();
        UpdateTutorialPulse();
    }

    private void OnSelectedCounterChanged(object sender, Player.OnSelectedCounterChangedEventArgs e)
    {
        SetTargetCounter(e.selectedCounter);
    }

    private void SetTargetCounter(BaseCounter newCounter)
    {
        if (currentCounter == newCounter) return;

        // Unhook progress from old target
        DetachFromProgressSource();
        ResetPulseVisuals();

        currentCounter = newCounter;
        progressNormalized = 0f;

        // Hook progress if available on the new target (CuttingCounter, StoveCounter, etc.)
        if (currentCounter is IHasProgress hp)
        {
            currentProgressSource = hp;
            currentProgressSource.OnProgressChanged += OnProgressChanged;
        }

        Refresh();
    }

    private void DetachFromProgressSource()
    {
        if (currentProgressSource != null)
        {
            currentProgressSource.OnProgressChanged -= OnProgressChanged;
            currentProgressSource = null;
        }
    }

    private void OnProgressChanged(object sender, IHasProgress.OnProgressChangedEventArgs e)
    {
        progressNormalized = e.progressNormalized;
        Refresh();
    }

    private void Refresh()
    {
        bool isPlaying = KitchenGameManager.Instance != null && KitchenGameManager.Instance.IsGamePlaying();
        bool hasSelection = currentCounter != null;

        lastAction = (isPlaying && hasSelection) ? ComputeAction() : Action.None;

        // Icon
        label.sprite = lastAction switch
        {
            Action.Pick => pick,
            Action.Drop => drop,
            Action.Chop => chop,
            _ => noneSprite
        };

        // Show when there is a selection and game is playing.
        // (We only disable components; never disable the GO.)
        SetVisible(isPlaying && hasSelection);
    }

    private void SetVisible(bool show)
    {
        if (button)
        {
            // Button stays enabled only when visible
            button.enabled = show;
            // Clickable only when there's an actionable state
            button.interactable = show && lastAction != Action.None;

            // Make sure the Button uses the same Image as its graphic
            if (button.targetGraphic == null) button.targetGraphic = label;
        }
        if (label)
        {
            // Visuals shown only when visible
            label.enabled = show && label.sprite != null;
            // Don't block clicks when hidden
            label.raycastTarget = show;
        }

        if (!show)
        {
            ResetPulseVisuals();
        }
    }

    private Action ComputeAction()
    {
        if (KitchenGameManager.Instance == null || !KitchenGameManager.Instance.IsGamePlaying()) return Action.None;
        if (Player.Instance == null || currentCounter == null) return Action.None;

        bool playerHas = Player.Instance.HasKitchenObject();
        bool counterHas = currentCounter.HasKitchenObject();

        // 0) Delivery counter hint (optional): show Drop only when the player holds something (handled by general logic)

        // 1) Dispensers (cheese/bun/plates/etc): show Pick when player empty-handed
        if (IsItemSource(currentCounter))
            return playerHas ? Action.None : Action.Pick;

        // 2) Cutting counter: Chop until done, then Pick
        if (currentCounter is CuttingCounter)
        {
            if (counterHas)
            {
                // If we receive progress, use it; else default to Chop
                if (currentProgressSource != null)
                    return progressNormalized >= 1f ? Action.Pick : Action.Chop;
                return Action.Chop;
            }
            // No item on board
            return playerHas ? Action.Drop : Action.None;
        }

        // 3) Generic counters (clear/stove/etc)
        if (!counterHas)               // empty surface
            return playerHas ? Action.Drop : Action.None;

        return Action.Pick;            // surface has an item -> Pick
    }

    private bool IsItemSource(BaseCounter c)
    {
        if (treatAsItemSourceOverride) return true;
        if (!autoDetectItemSources || c == null) return false;
        // ContainerCounter gives an item directly to the player; PlatesCounter spawns plate to player
        return c is ContainerCounter || c is PlatesCounter;
    }

    private void OnClick()
    {
        if (currentCounter == null || Player.Instance == null) return;

        switch (ComputeAction())
        {
            case Action.Chop:
                currentCounter.InteractAlternate(Player.Instance); // alternate action (e.g., chopping)
                break;
            case Action.Pick:
            case Action.Drop:
                currentCounter.Interact(Player.Instance);          // primary action (pick/drop/serve)
                break;
            case Action.None:
                return;
        }

        Refresh();
    }
    private void OnDisable()
    {
        if (subscribed && Player.Instance != null)
            Player.Instance.OnSelectedCounterChanged -= OnSelectedCounterChanged;
        subscribed = false;
        DetachFromProgressSource();
        ResetPulseVisuals();
    }

    private bool IsTutorialActive()
    {
        if (tutorialManager == null)
        {
            tutorialManager = FindFirstObjectByType<TutorialManager>();
        }

        return tutorialManager != null && tutorialManager.isActiveAndEnabled;
    }

    private void CachePulseBaseScale()
    {
        if (pulseRectTransform == null)
        {
            return;
        }

        pulseBaseScale = pulseRectTransform.localScale;
        pulseBaseScaleInitialized = true;
    }

    private void UpdateTutorialPulse()
    {
        if (!enableTutorialPulse || pulseRectTransform == null)
        {
            return;
        }

        bool shouldPulse =
            IsTutorialActive() &&
            button != null &&
            button.enabled &&
            button.interactable &&
            label != null &&
            label.enabled;

        if (!shouldPulse)
        {
            ResetPulseVisuals();
            return;
        }

        if (!pulseBaseScaleInitialized)
        {
            CachePulseBaseScale();
        }

        float wave = Mathf.Sin(Time.unscaledTime * tutorialPulseCyclesPerSecond * Mathf.PI * 2f);
        float scale = 1f + (wave * tutorialPulseScaleAmount);
        pulseRectTransform.localScale = pulseBaseScale * scale;
    }

    private void ResetPulseVisuals()
    {
        if (!pulseBaseScaleInitialized || pulseRectTransform == null)
        {
            return;
        }

        pulseRectTransform.localScale = pulseBaseScale;
    }
}
