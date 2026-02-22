using UnityEngine;

public class CharacterCustomizationUI : MonoBehaviour
{
    public CharacterModification character;
    public bool debugLogs;

    [Header("Legacy Rarity Panels (Optional)")]
    public GameObject commonPanel;
    public GameObject rarePanel;
    public GameObject legendaryPanel;

    [Header("Character Panels (Optional)")]
    public GameObject monadPanel;
    public GameObject monkeyPanel;
    public GameObject mousePanel;
    public GameObject piggiePanel;

    private CharacterModification.Rarity current = CharacterModification.Rarity.Common;

    private void Awake()
    {
        if (character == null)
        {
            character = FindFirstObjectByType<CharacterModification>();
        }

        if (debugLogs)
        {
            string name = character != null ? character.name : "null";
            Debug.Log($"[CharacterCustomizationUI] Awake character={name}");
        }
    }

    public void SetRarityCommon() { current = CharacterModification.Rarity.Common; Show(commonPanel); }
    public void SetRarityRare() { current = CharacterModification.Rarity.Rare; Show(rarePanel); }
    public void SetRarityLegendary() { current = CharacterModification.Rarity.Legendary; Show(legendaryPanel); }

    private void Show(GameObject active)
    {
        if (commonPanel) commonPanel.SetActive(active == commonPanel);
        if (rarePanel) rarePanel.SetActive(active == rarePanel);
        if (legendaryPanel) legendaryPanel.SetActive(active == legendaryPanel);
    }

    public void PickHat(int index) => ApplySlot("Hat", index);
    public void PickGlasses(int index) => ApplySlot("Glass", index);
    public void PickBackpack(int index) => ApplySlot("Backpack", index);
    public void PickOutfit(int index) => ApplySlot("Outfits", index);
    public void PickEye(int index) => ApplySlot("Eye", index);
    public void PickMouth(int index) => ApplySlot("Mouth", index);

    public void SelectMonad() => SelectCharacter("monad", monadPanel);
    public void SelectMonkey() => SelectCharacter("Monkey", monkeyPanel);
    public void SelectMouse() => SelectCharacter("Mouse", mousePanel);
    public void SelectPiggie() => SelectCharacter("Piggie", piggiePanel);

    private void SelectCharacter(string characterName, GameObject panelToShow)
    {
        if (TryGetCharacter(out var target))
        {
            if (debugLogs) Debug.Log($"[CharacterCustomizationUI] SelectCharacter {characterName}");
            target.SetCharacterByName(characterName, true);
        }

        ShowCharacterPanel(panelToShow);
    }

    private void ShowCharacterPanel(GameObject active)
    {
        if (monadPanel) monadPanel.SetActive(active == monadPanel);
        if (monkeyPanel) monkeyPanel.SetActive(active == monkeyPanel);
        if (mousePanel) mousePanel.SetActive(active == mousePanel);
        if (piggiePanel) piggiePanel.SetActive(active == piggiePanel);
    }

    private void ApplySlot(string slotName, int index)
    {
        if (!TryGetCharacter(out var target))
        {
            if (debugLogs) Debug.LogWarning($"[CharacterCustomizationUI] Missing CharacterModification for slot {slotName}.");
            return;
        }

        if (debugLogs) Debug.Log($"[CharacterCustomizationUI] Pick {slotName} index={index}");
        target.SetSlot(slotName, index);
    }

    private bool TryGetCharacter(out CharacterModification target)
    {
        if (character == null)
        {
            character = FindFirstObjectByType<CharacterModification>();
        }

        target = character;
        return target != null;
    }
}
