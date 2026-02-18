using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Listens to player pick-up events and highlights the nearest counter that can process
/// the currently-held KitchenObjectSO (cut, cook, grind, etc.). Uses `CounterHighlighter` components.
/// </summary>
public class NextCounterHighlighterManager : MonoBehaviour
{
    [Tooltip("If >0, only highlight the nearest N matching counters (default 1)")]
    [SerializeField] private int highlightNearestCount = 1;

    private CounterHighlighter[] highlighters = new CounterHighlighter[0];

    private void Start()
    {
        RefreshHighlighters();
        if (Player.Instance != null)
        {
            Player.Instance.OnPickedSomething += Player_OnPickedSomething;
        }
        BaseCounter.OnAnyObjectPlacedHere += BaseCounter_OnAnyObjectPlacedHere;
        BaseCounter.OnAnyObjectRemovedHere += BaseCounter_OnAnyObjectRemovedHere;
    }

    private void OnDestroy()
    {
        if (Player.Instance != null)
        {
            Player.Instance.OnPickedSomething -= Player_OnPickedSomething;
        }
        BaseCounter.OnAnyObjectPlacedHere -= BaseCounter_OnAnyObjectPlacedHere;
        BaseCounter.OnAnyObjectRemovedHere -= BaseCounter_OnAnyObjectRemovedHere;
    }

    private void RefreshHighlighters()
    {
        // Ensure every BaseCounter has a CounterHighlighter so designers don't have to manually add them
        var counters = FindObjectsOfType<BaseCounter>(true);
        foreach (var c in counters)
        {
            if (c == null) continue;
            var existing = c.GetComponent<CounterHighlighter>();
            if (existing == null)
            {
                existing = c.gameObject.AddComponent<CounterHighlighter>();
                // try to assign the baseCounter reference if the inspector field is empty
                var field = existing;
                try { field.GetType().GetField("baseCounter", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(field, c); } catch { }
            }
        }

        highlighters = FindObjectsOfType<CounterHighlighter>(true);
    }

    private void Player_OnPickedSomething(object sender, EventArgs e)
    {
        UpdateHighlightsForPlayerItem();
    }

    private void BaseCounter_OnAnyObjectPlacedHere(object sender, EventArgs e)
    {
        // When something was placed on any counter, the player's held object likely changed;
        // re-evaluate highlights so we don't keep showing stale targets
        UpdateHighlightsForPlayerItem();
    }

    private void BaseCounter_OnAnyObjectRemovedHere(object sender, EventArgs e)
    {
        // Re-evaluate in case a counter became free
        UpdateHighlightsForPlayerItem();
    }

    private void UpdateHighlightsForPlayerItem()
    {
        if (Player.Instance == null)
        {
            ClearAllHighlights();
            return;
        }

        if (!Player.Instance.HasKitchenObject())
        {
            ClearAllHighlights();
            return;
        }

        var kitchenObject = Player.Instance.GetKitchenObject();
        if (kitchenObject == null)
        {
            ClearAllHighlights();
            return;
        }

        var so = kitchenObject.GetKitchenObjectSO();
        if (so == null)
        {
            ClearAllHighlights();
            return;
        }

        // If the held item cannot be processed any further (cut/cook),
        // don't pulse any counter for it.
        if (!CanHeldItemBeProcessedFurther(so))
        {
            ClearAllHighlights();
            return;
        }

        // Use DeliveryManager's top-most order to prefer counters that produce the required recipe ingredient
        var waiting = DeliveryManager.Instance != null ? DeliveryManager.Instance.GetWaitingRecipeSOList() : null;
        RecipeSO topRecipe = null;
        if (waiting != null && waiting.Count > 0) topRecipe = waiting[0];

        Vector3 playerPos = Player.Instance.transform.position;
        var candidates = new List<(CounterHighlighter highlighter, float dist)>();

        // Build global sets to reason about processing stages
        var cuttingOutputs = new HashSet<KitchenObjectSO>();
        var cuttingInputs = new HashSet<KitchenObjectSO>();
        var stoveInputs = new HashSet<KitchenObjectSO>();
        var stoveOutputs = new HashSet<KitchenObjectSO>();

        foreach (var h in highlighters)
        {
            if (h == null) continue;
            var ctr = h.GetCounter();
            if (ctr == null) continue;

            if (ctr is CuttingCounter cc)
            {
                try
                {
                    // collect all cutting recipes
                    var field = typeof(CuttingCounter).GetField("cuttingRecipeSOArray", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.FlattenHierarchy);
                    var arr = field?.GetValue(cc) as CuttingRecipeSO[];
                    if (arr != null)
                    {
                        foreach (var r in arr)
                        {
                            if (r == null) continue;
                            cuttingInputs.Add(r.input);
                            cuttingOutputs.Add(r.output);
                        }
                    }
                }
                catch { }
            }

            if (ctr is StoveCounter sc)
            {
                try
                {
                    var fField = typeof(StoveCounter).GetField("fryingRecipeSOArray", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.FlattenHierarchy);
                    var bField = typeof(StoveCounter).GetField("burningRecipeSOArray", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.FlattenHierarchy);
                    var fArr = fField?.GetValue(sc) as FryingRecipeSO[];
                    var bArr = bField?.GetValue(sc) as BurningRecipeSO[];
                    if (fArr != null)
                    {
                        foreach (var r in fArr)
                        {
                            if (r == null) continue;
                            stoveInputs.Add(r.input);
                            stoveOutputs.Add(r.output);
                        }
                    }
                    if (bArr != null)
                    {
                        foreach (var r in bArr)
                        {
                            if (r == null) continue;
                            stoveInputs.Add(r.input);
                            stoveOutputs.Add(r.output);
                        }
                    }
                }
                catch { }
            }
        }

        bool heldIsCuttingOutput = cuttingOutputs.Contains(so);
        bool heldIsStoveInput = stoveInputs.Contains(so);
        bool heldIsStoveOutput = stoveOutputs.Contains(so);

        // 1) If we have a top recipe, try to match counters that will produce that recipe's ingredient from the player's held item
        if (topRecipe != null)
        {
            // If held item can be transformed into any ingredient required by the top recipe, prefer those counters
            foreach (var h in highlighters)
            {
                if (h == null) continue;
                var counter = h.GetCounter();
                if (counter == null) continue;
                if (counter.HasKitchenObject()) continue; // skip busy counters

                // CuttingCounter: will it produce a recipe ingredient from the held SO?
                if (counter is CuttingCounter cutting)
                {
                    try
                    {
                        // If the held item is already the output of a cutting recipe or is a stove output,
                        // don't suggest cutting counters unless it is also an input to the stove (needs further processing).
                        if ((heldIsCuttingOutput || heldIsStoveOutput) && !heldIsStoveInput) { continue; }

                        var outSO = cutting.GetOutputForInputPublic(so);
                        if (outSO != null && topRecipe.kitchenObjectSOList.Contains(outSO))
                        {
                            float d = Vector3.Distance(playerPos, counter.transform.position);
                            candidates.Add((h, d));
                        }
                    }
                    catch { }
                }

                // StoveCounter: similar check
                if (counter is StoveCounter stove)
                {
                    try
                    {
                        var outSO = stove.GetOutputForInputPublic(so);
                        if (outSO != null && topRecipe.kitchenObjectSOList.Contains(outSO))
                        {
                            float d = Vector3.Distance(playerPos, counter.transform.position);
                            candidates.Add((h, d));
                        }
                    }
                    catch { }
                }
            }

            // 2) If none of the above matched, but the player is holding an item that already matches one of the top recipe's ingredients,
            // then highlight counters that can produce other required ingredients (e.g., recipe requires both sliced & minced variants).
            if (candidates.Count == 0)
            {
                bool heldIsRecipeIngredient = topRecipe.kitchenObjectSOList.Contains(so);
                if (heldIsRecipeIngredient)
                {
                    // For each other required ingredient, find counters that can produce it
                    foreach (var required in topRecipe.kitchenObjectSOList)
                    {
                        if (required == so) continue; // skip the one we already have

                        foreach (var h in highlighters)
                        {
                            if (h == null) continue;
                            var counter = h.GetCounter();
                            if (counter == null) continue;
                            if (counter.HasKitchenObject()) continue;

                            // CuttingCounter can produce 'required'?
                            if (counter is CuttingCounter cutting)
                            {
                                try
                                {
                                    if (cutting.CanProduce(required))
                                    {
                                        float d = Vector3.Distance(playerPos, counter.transform.position);
                                        candidates.Add((h, d));
                                    }
                                }
                                catch { }
                            }

                            // StoveCounter can produce 'required'?
                            if (counter is StoveCounter stove)
                            {
                                try
                                {
                                    if (stove.CanProduce(required))
                                    {
                                        float d = Vector3.Distance(playerPos, counter.transform.position);
                                        candidates.Add((h, d));
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                }
            }
        }

        // 3) Fallback: if no candidates found using top recipe, fall back to generic "can process held item" behavior
        if (candidates.Count == 0)
        {
            foreach (var h in highlighters)
            {
                if (h == null) continue;
                var counter = h.GetCounter();
                if (counter == null) continue;
                if (counter.HasKitchenObject()) continue;

                bool match = false;

                if (counter is CuttingCounter cutting)
                {
                    // Skip suggesting cutting if the held item is already processed (cut or stove-processed) and doesn't need further stove processing
                    if ((heldIsCuttingOutput || heldIsStoveOutput) && !heldIsStoveInput) { continue; }
                    try { if (cutting.CanProcess(so)) match = true; } catch { }
                }
                if (!match && counter is StoveCounter stove)
                {
                    try { if (stove.CanProcess(so)) match = true; } catch { }
                }

                if (match)
                {
                    float d = Vector3.Distance(playerPos, counter.transform.position);
                    candidates.Add((h, d));
                }
            }
        }

        if (candidates.Count == 0)
        {
            ClearAllHighlights();
            return;
        }

        // Sort by distance ascending and highlight nearest N
        var chosen = candidates.OrderBy(c => c.dist).Take(Math.Max(1, highlightNearestCount)).ToArray();
        HashSet<CounterHighlighter> toShow = new HashSet<CounterHighlighter>(chosen.Select(c => c.highlighter));
        foreach (var h in highlighters)
        {
            if (h == null) continue;
            if (toShow.Contains(h)) h.ShowHighlight(); else h.HideHighlight();
        }
    }

    private void ClearAllHighlights()
    {
        foreach (var h in highlighters)
        {
            if (h == null) continue;
            h.HideHighlight();
        }
    }

    private bool CanHeldItemBeProcessedFurther(KitchenObjectSO heldSo)
    {
        if (heldSo == null)
        {
            return false;
        }

        foreach (var h in highlighters)
        {
            if (h == null) continue;
            var counter = h.GetCounter();
            if (counter == null) continue;

            if (counter is CuttingCounter cutting)
            {
                try
                {
                    if (cutting.CanProcess(heldSo))
                    {
                        return true;
                    }
                }
                catch { }
            }

            if (counter is StoveCounter stove)
            {
                try
                {
                    if (stove.CanProcess(heldSo))
                    {
                        return true;
                    }
                }
                catch { }
            }
        }

        return false;
    }
}
