using UnityEngine;

public class CharacterCustomizationUI : MonoBehaviour
{
    public CharacterModification character;
    public GameObject commonPanel;
    public GameObject rarePanel;
    public GameObject legendaryPanel;

    private CharacterModification.Rarity current = CharacterModification.Rarity.Common;

    public void SetRarityCommon() { current = CharacterModification.Rarity.Common; Show(commonPanel); }
    public void SetRarityRare() { current = CharacterModification.Rarity.Rare; Show(rarePanel); }
    public void SetRarityLegendary() { current = CharacterModification.Rarity.Legendary; Show(legendaryPanel); }

    private void Show(GameObject active)
    {
        if (commonPanel) commonPanel.SetActive(active == commonPanel);
        if (rarePanel) rarePanel.SetActive(active == rarePanel);
        if (legendaryPanel) legendaryPanel.SetActive(active == legendaryPanel);
    }

    public void PickHat(int index) => character.SetSlot("Hat", current, index);
    public void PickGlasses(int index) => character.SetSlot("Glass", current, index);
    public void PickBackpack(int index) => character.SetSlot("Backpack", current, index);
    public void PickOutfit(int index) => character.SetSlot("Outfits", current, index);
    public void PickEye(int index) => character.SetSlot("Eye", current, index);
    public void PickMouth(int index) => character.SetSlot("Mouth", current, index);
}
