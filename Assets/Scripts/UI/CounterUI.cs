using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CounterUI : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private BaseCounter counter;        // auto-filled from parent if left empty
    [SerializeField] private Button button;              // auto-filled from self if left empty
    [SerializeField] private TMP_Text label;             // auto-filled from children if left empty

    [Header("Behavior")]
    [Tooltip("Enable on dispensers that give items directly to the player (cheese/bun/etc).")]
    [SerializeField] private bool treatAsItemSource = false;

    private enum Action { None, Pick, Drop, Chop }

    // Cutting progress tracking (for Chop -> Pick switch)
    private bool hasProgressSource;
    private float progressNormalized; // 0..1 from IHasProgress

    private void Awake()
    {
        if (!counter) counter = GetComponentInParent<BaseCounter>();
        if (!button) button = GetComponent<Button>();
        if (!label) label = GetComponentInChildren<TMP_Text>();
        button.onClick.AddListener(OnClick);

        // If this counter reports progress (CuttingCounter does), subscribe so we know when it's done
        if (counter is IHasProgress hp)
        {
            hasProgressSource = true;
            hp.OnProgressChanged += OnProgressChanged;
        }

        Refresh();
    }

    private void OnDestroy()
    {
        if (hasProgressSource && counter is IHasProgress hp)
            hp.OnProgressChanged -= OnProgressChanged;
    }

    private void Update() => Refresh();

    private void OnProgressChanged(object sender, IHasProgress.OnProgressChangedEventArgs e)
    {
        progressNormalized = e.progressNormalized;
        Refresh();
    }

    private void Refresh()
    {
        var a = ComputeAction();

        label.text = a switch
        {
            Action.Pick => "Pick",
            Action.Drop => "Drop",
            Action.Chop => "Chop",
            _ => ""
        };

        button.interactable = a != Action.None && KitchenGameManager.Instance.IsGamePlaying();
    }

    private Action ComputeAction()
    {
        if (!KitchenGameManager.Instance.IsGamePlaying()) return Action.None;
        if (!counter || !Player.Instance) return Action.None;

        bool playerHas = Player.Instance.HasKitchenObject();
        bool counterHas = counter.HasKitchenObject();

        // 1) Dispensers (cheese/bun/etc): show Pick when player empty-handed
        if (treatAsItemSource)
            return playerHas ? Action.None : Action.Pick;

        // 2) Cutting counter: Chop until done, then Pick
        if (counter is CuttingCounter)
        {
            if (counterHas)
            {
                // If we receive progress, use it; else default to Chop
                if (hasProgressSource)
                    return progressNormalized >= 1f ? Action.Pick : Action.Chop;
                return Action.Chop;
            }
            // No item on board
            return playerHas ? Action.Drop : Action.None;
        }

        // 3) Any other counter
        if (!counterHas)               // empty surface
            return playerHas ? Action.Drop : Action.None;

        return Action.Pick;            // has an item: stove/clear/etc -> Pick
    }

    private void OnClick()
    {
        if (!counter || !Player.Instance) return;

        switch (ComputeAction())
        {
            case Action.Chop:
                counter.InteractAlternate(Player.Instance); // F
                break;
            case Action.Pick:
            case Action.Drop:
                counter.Interact(Player.Instance);          // E
                break;
            case Action.None:
                return;
        }

        Refresh();
    }
}
