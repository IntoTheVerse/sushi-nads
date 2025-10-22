using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DeliveryManagerUI : MonoBehaviour
{
    [SerializeField] private Transform container;
    [SerializeField] private Transform recipeTemplate;
    [SerializeField] private Sprite _selectedOrderSprite;
    [SerializeField] private Sprite _defaultOrderSprite; // captured from the template at runtime

    private readonly List<DeliveryManagerSingleUI> rows = new();

    private void Awake() {
        recipeTemplate.gameObject.SetActive(false); 
    }
    private void OnEnable()
    {
        if (DeliveryManager.Instance != null)
        {
            DeliveryManager.Instance.OnRecipeSpawned += OnRecipesChanged;
            DeliveryManager.Instance.OnRecipeCompleted += OnRecipesChanged;
        }
        PlateKitchenObject.OnAnyIngredientAdded += OnPlateIngredientsChanged;
    }
    private void Start() {
        DeliveryManager.Instance.OnRecipeSpawned += DeliveryManager_OnRecipeSpawned;
        DeliveryManager.Instance.OnRecipeCompleted += DeliveryManager_OnRecipeCompleted;
        UpdateVisual();
    }
    private void OnRecipesChanged(object s, System.EventArgs e)
    {
        UpdateVisual();
        RefreshCompletedVisuals();
    }

    private void OnPlateIngredientsChanged(object s, System.EventArgs e)
    {
        RefreshCompletedVisuals();
    }
    private void DeliveryManager_OnRecipeCompleted(object sender, System.EventArgs e) {
        UpdateVisual();
    }

    private void DeliveryManager_OnRecipeSpawned(object sender, System.EventArgs e) {
        UpdateVisual();
    }

    private void UpdateVisual() {
        foreach (Transform child in container)
        {
            if (child == recipeTemplate) continue;
            Destroy(child.gameObject);
        }
        rows.Clear();

        int i = 0;
        foreach (RecipeSO recipeSO in DeliveryManager.Instance.GetWaitingRecipeSOList())
        {
            Transform recipeTransform = Instantiate(recipeTemplate, container);
            recipeTransform.gameObject.SetActive(true);

            var row = recipeTransform.GetComponent<DeliveryManagerSingleUI>();
            row.SetRecipeSO(recipeSO);
            rows.Add(row);

            // set background sprite: first item = selected (green), others = default
            var bg = recipeTransform.GetComponent<DeliveryManagerSingleUI>();
            if (bg)
            {
                bg.bg.sprite = (i == 0 && _selectedOrderSprite != null) ? _selectedOrderSprite : _defaultOrderSprite;
            }
            i++;
        }

        /* foreach (Transform child in container) {
            if (child == recipeTemplate) continue;
            Destroy(child.gameObject);
        }

        foreach (RecipeSO recipeSO in DeliveryManager.Instance.GetWaitingRecipeSOList()) {
            Transform recipeTransform = Instantiate(recipeTemplate, container);
            recipeTransform.gameObject.SetActive(true);
            recipeTransform.GetComponent<DeliveryManagerSingleUI>().SetRecipeSO(recipeSO);
        }*/
    }
    private void RefreshCompletedVisuals()
    {
        // find ingredients on the player's current plate (if any)
        List<KitchenObjectSO> currentPlate = null;
        if (Player.Instance != null && Player.Instance.HasKitchenObject())
        {
            if (Player.Instance.GetKitchenObject().TryGetPlate(out PlateKitchenObject plate))
            {
                currentPlate = plate.GetKitchenObjectSOList();
            }
        }

        // tell every visible row to update
        foreach (var row in rows)
        {
            row.RefreshAgainst(currentPlate);
        }
    }
}
