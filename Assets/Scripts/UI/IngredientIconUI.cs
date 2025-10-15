using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class IngredientIconUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private KitchenObjectSO ingredient; // set at runtime
    [SerializeField] private bool showOnHover = false;   // optional
    [SerializeField] private Image iconImage;           // assign in Inspector (or GetComponent in Awake)
    [SerializeField] private CanvasGroup canvasGroup;   // optional; add a CanvasGroup to the icon GO
    private bool completed;

    void Awake()
    {
        if (!iconImage) iconImage = GetComponent<Image>();
        if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>();
    }
    public void SetIngredient(KitchenObjectSO so)
    {
        ingredient = so;
    }
    public void SetCompleted(bool value)
    {
        completed = value;
        // dull + non-interactable
        if (canvasGroup) canvasGroup.alpha = completed ? 0.35f : 1f;
        if (iconImage) iconImage.raycastTarget = !completed;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log("OnPointerClick");
        if (ingredient == null || ToastManager.Instance == null) return;
        ToastManager.Instance.Show(ingredient.objectName, eventData.position);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Debug.Log("OnPointerEnter");
        if (!showOnHover) return;
        if (ingredient == null || ToastManager.Instance == null) return;
        ToastManager.Instance.Show(ingredient.objectName, eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Debug.Log("OnPointerExit");
        // Let ToastManager auto-hide on timer; no action needed
    }

    public KitchenObjectSO GetIngredientSO() => ingredient;

}
