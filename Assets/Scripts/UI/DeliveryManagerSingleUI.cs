using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using UnityEngine;

public class DeliveryManagerSingleUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI recipeNameText;
    [SerializeField] private Transform iconContainer;
    [SerializeField] private Transform iconTemplate;
    private RecipeSO recipeSO;
    private readonly List<IngredientIconUI> iconUIs = new List<IngredientIconUI>();
    private readonly List<KitchenObjectSO> required = new List<KitchenObjectSO>();

    private void Awake() {
        iconTemplate.gameObject.SetActive(false);
    }

    public void SetRecipeSO(RecipeSO recipe) {
        recipeSO = recipe;
        iconUIs.Clear();
        required.Clear();
        recipeNameText.text = recipe.recipeName;
        foreach (Transform child in iconContainer) {
            if (child == iconTemplate) continue;
            Destroy(child.gameObject);
        }

        foreach (KitchenObjectSO kitchenObjectSO in recipe.kitchenObjectSOList) {
            Transform iconTransform = Instantiate(iconTemplate, iconContainer);
            iconTransform.gameObject.SetActive(true);
            var iconUI = iconTransform.GetComponent<IngredientIconUI>();
            if (iconUI != null) 
            {
                iconUI.SetIngredient(kitchenObjectSO);
                iconUI.SetCompleted(false);
                iconUIs.Add(iconUI);
                required.Add(kitchenObjectSO);
            } 
            iconTransform.GetComponent<Image>().sprite = kitchenObjectSO.sprite;
        }
    }

    // Call this whenever the player's plate changes
    public void RefreshAgainst(List<KitchenObjectSO> plateIngredients)
    {
        // multiset (counts) so duplicates are handled properly
        var counts = new Dictionary<KitchenObjectSO, int>();
        if (plateIngredients != null)
        {
            foreach (var so in plateIngredients)
            {
                counts.TryGetValue(so, out int c);
                counts[so] = c + 1;
            }
        }

        // for each required ingredient in order, tick off if available
        for (int i = 0; i < iconUIs.Count; i++)
        {
            var need = required[i];
            bool done = counts.TryGetValue(need, out int c) && c > 0;
            iconUIs[i].SetCompleted(done);
            if (done) counts[need] = c - 1;
        }
    }
}
