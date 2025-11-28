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

        // Find all matching counters that can process this SO and are currently free
        var candidates = new List<(CounterHighlighter highlighter, float dist)>();

        Vector3 playerPos = Player.Instance.transform.position;

        foreach (var h in highlighters)
        {
            if (h == null) continue;
            var counter = h.GetCounter();
            if (counter == null) continue;

            // Skip occupied counters (player should not try to put ingredient where there's already an item)
            if (counter.HasKitchenObject()) continue;

            bool match = false;

            // CuttingCounter
            if (counter is CuttingCounter cutting)
            {
                try
                {
                    if (cutting.CanProcess(so)) match = true;
                }
                catch (Exception)
                {
                    // ignore
                }
            }

            // StoveCounter (frying, rice cooker, grinder, etc.)
            if (!match && counter is StoveCounter stove)
            {
                try
                {
                    if (stove.CanProcess(so)) match = true;
                }
                catch (Exception)
                {
                    // ignore
                }
            }

            // ContainerCounter (e.g., spawners) are not targets for processing

            if (match)
            {
                float d = Vector3.Distance(playerPos, counter.transform.position);
                candidates.Add((h, d));
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
}
