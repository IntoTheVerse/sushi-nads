using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Character trait modifier for rarity-based hierarchies.
/// - Scans a hierarchy like: Body -> Common/Rare/Legendary -> Trait -> Variants.
/// - Lets you enable a specific trait variant per rarity (hat, glasses, outfit, etc.).
/// - Optional: switch entire character under a Characters root.
/// - Supports swapping skin materials on configured renderers.
/// </summary>
public class CharacterModification : MonoBehaviour
{
    public enum Rarity { Common = 0, Rare = 1, Legendary = 2 }

    [Serializable]
    public class SlotConfig
    {
        public string slotName; // e.g., "Backpack", "Glass", "Hat"
        public Transform common;
        public Transform rare;
        public Transform legendary;
    }

    [Header("Hierarchy Roots")]
    [Tooltip("Root that contains Common/Rare/Legendary. If empty, uses active child of Characters Root or this object.")]
    public Transform traitsRoot;
    [Tooltip("Optional root to swap entire characters. Each child is a full character root.")]
    public Transform charactersRoot;

    [Header("Auto-Scan Settings")]
    public bool autoScanHierarchy = true;
    public string commonName = "Common";
    public string rareName = "Rare";
    public string legendaryName = "Legendary";
    public bool includeInactive = true;
    public bool treatEmptyTraitAsSingleOption = true;

    [Header("Optional: Manual Slot Configuration (used when Auto-Scan is off)")]
    public List<SlotConfig> slots = new List<SlotConfig>();













    [Header("Skin Material Options")]
    public List<Material> skinMaterials = new List<Material>();
    public Renderer[] skinRenderers;

    // Slot runtime cache (operates on pre-placed child objects via enable/disable).
    // Runtime cache structures are defined further down and initialized in Awake().

    #region Public API

    // Wrapper using new container-based API (operates on pre-placed children)
    public void SetBody(int index) => SetSlot("Body", index);
    public void SetHair(int index) => SetSlot("Hair", index);
    public void SetGlasses(int index) => SetSlot("Glass", index);
    public void SetClothes(int index) => SetSlot("Outfit", index);
    public void SetHat(int index) => SetSlot("Hat", index);

    /// <summary>
    /// Set skin material by index. Applies to all configured skin renderers.
    /// index &lt; 0 clears to shared (no change). Out-of-range is ignored.
    /// </summary>
    public void SetSkinMaterial(int index)
    {
        if (skinRenderers == null || skinRenderers.Length == 0) return;
        if (index < 0 || index >= skinMaterials.Count) return;
        var mat = skinMaterials[index];
        if (mat == null) return;
        foreach (var r in skinRenderers)
        {
            if (r == null) continue;
            // Use material so it becomes instance-specific at runtime
            r.material = mat;
        }
    }

    public int GetOptionCount(string slot)
    {
        if (string.IsNullOrEmpty(slot)) return 0;
        if (!cache.TryGetValue(slot, out var sc)) return 0;
        var total = 0;
        if (sc.common != null) total += sc.common.Count;
        if (sc.rare != null) total += sc.rare.Count;
        if (sc.legendary != null) total += sc.legendary.Count;
        return total;
    }

    public void RandomizeAll() => RandomizeAll(true);

    public void SetCharacter(int index, bool rebuildTraits = true)
    {
        if (charactersRoot == null) return;
        if (index < 0 || index >= charactersRoot.childCount) return;
        for (int i = 0; i < charactersRoot.childCount; i++)
        {
            var child = charactersRoot.GetChild(i);
            if (child == null) continue;
            child.gameObject.SetActive(i == index);
        }
        if (rebuildTraits) BuildCache();
    }

    public void SetCharacterByName(string name, bool rebuildTraits = true)
    {
        if (charactersRoot == null || string.IsNullOrEmpty(name)) return;
        for (int i = 0; i < charactersRoot.childCount; i++)
        {
            var child = charactersRoot.GetChild(i);
            if (child == null) continue;
            child.gameObject.SetActive(string.Equals(child.name, name, StringComparison.OrdinalIgnoreCase));
        }
        if (rebuildTraits) BuildCache();
    }

    #endregion

    #region Helpers



    #endregion

    #region Internals

    private class SlotCache
    {
        public SlotConfig config;
        public List<GameObject> common = new List<GameObject>();
        public List<GameObject> rare = new List<GameObject>();
        public List<GameObject> legendary = new List<GameObject>();
        public (Rarity rarity, int index)? currentSelection = null;
    }

    private Dictionary<string, SlotCache> cache = new Dictionary<string, SlotCache>(StringComparer.OrdinalIgnoreCase);

    private void Awake()
    {
        BuildCache();
    }

    private void BuildCache()
    {
        cache.Clear();

        if (!autoScanHierarchy)
        {
            foreach (var scfg in slots)
            {
                if (string.IsNullOrEmpty(scfg.slotName)) continue;
                var sc = new SlotCache { config = scfg };
                sc.common = MakeOptionList(scfg.common);
                sc.rare = MakeOptionList(scfg.rare);
                sc.legendary = MakeOptionList(scfg.legendary);
                cache[scfg.slotName] = sc;
            }
            return;
        }

        var root = ResolveTraitsRoot();
        if (root == null) return;

        var commonRoot = FindByName(root, commonName);
        var rareRoot = FindByName(root, rareName);
        var legendaryRoot = FindByName(root, legendaryName);

        AddRaritySlots(commonRoot, Rarity.Common);
        AddRaritySlots(rareRoot, Rarity.Rare);
        AddRaritySlots(legendaryRoot, Rarity.Legendary);
    }

    private Transform ResolveTraitsRoot()
    {
        if (traitsRoot != null) return traitsRoot;
        if (charactersRoot != null)
        {
            for (int i = 0; i < charactersRoot.childCount; i++)
            {
                var child = charactersRoot.GetChild(i);
                if (child != null && child.gameObject.activeInHierarchy) return child;
            }
            if (charactersRoot.childCount > 0) return charactersRoot.GetChild(0);
        }
        return transform;
    }

    private void AddRaritySlots(Transform rarityRoot, Rarity rarity)
    {
        if (rarityRoot == null) return;
        for (int i = 0; i < rarityRoot.childCount; i++)
        {
            var trait = rarityRoot.GetChild(i);
            if (trait == null) continue;
            var slotName = trait.name;
            if (string.IsNullOrEmpty(slotName)) continue;
            if (!cache.TryGetValue(slotName, out var sc))
            {
                sc = new SlotCache { config = new SlotConfig { slotName = slotName } };
                cache[slotName] = sc;
            }
            var options = MakeOptionList(trait);
            switch (rarity)
            {
                case Rarity.Common: sc.common = options; break;
                case Rarity.Rare: sc.rare = options; break;
                case Rarity.Legendary: sc.legendary = options; break;
            }
        }
    }

    private List<GameObject> MakeOptionList(Transform parent)
    {
        var list = new List<GameObject>();
        if (parent == null) return list;
        if (parent.childCount == 0)
        {
            if (treatEmptyTraitAsSingleOption) list.Add(parent.gameObject);
            return list;
        }
        for (int i = 0; i < parent.childCount; i++)
        {
            var c = parent.GetChild(i).gameObject;
            if (c != null) list.Add(c);
        }
        return list;
    }

    private Transform FindByName(Transform root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name)) return null;
        var children = root.GetComponentsInChildren<Transform>(includeInactive);
        foreach (var t in children)
        {
            if (t != null && string.Equals(t.name, name, StringComparison.OrdinalIgnoreCase))
            {
                return t;
            }
        }
        return null;
    }

    private List<GameObject> GetListForRarity(SlotCache sc, Rarity r)
    {
        switch (r)
        {
            case Rarity.Common: return sc.common;
            case Rarity.Rare: return sc.rare;
            case Rarity.Legendary: return sc.legendary;
            default: return sc.common;
        }
    }

    private void DisableAllInSlot(SlotCache sc)
    {
        Action<List<GameObject>> disableList = (list) => { if (list == null) return; foreach (var g in list) if (g != null) g.SetActive(false); };
        disableList(sc.common); disableList(sc.rare); disableList(sc.legendary);
    }

    public void SetSlot(string slotName, Rarity rarity, int index)
    {
        if (string.IsNullOrEmpty(slotName)) return;
        if (cache.Count == 0) BuildCache();
        if (!cache.TryGetValue(slotName, out var sc)) return;

        DisableAllInSlot(sc);

        if (index < 0) { sc.currentSelection = null; return; }

        var list = GetListForRarity(sc, rarity);
        if (list == null || index >= list.Count) return;
        var go = list[index];
        if (go == null) return;
        go.SetActive(true);
        sc.currentSelection = (rarity, index);
    }

    public void SetSlot(string slotName, int index) => SetSlot(slotName, Rarity.Common, index);

    public void SetSlotByName(string slotName, string childName)
    {
        if (string.IsNullOrEmpty(slotName) || string.IsNullOrEmpty(childName)) return;
        if (cache.Count == 0) BuildCache();
        if (!cache.TryGetValue(slotName, out var sc)) return;
        for (int r = 0; r <= 2; r++)
        {
            var rarity = (Rarity)r;
            var list = GetListForRarity(sc, rarity);
            if (list == null) continue;
            for (int i = 0; i < list.Count; i++)
            {
                var go = list[i];
                if (go != null && string.Equals(go.name, childName, StringComparison.OrdinalIgnoreCase))
                {
                    SetSlot(slotName, rarity, i);
                    return;
                }
            }
        }
    }

    public void ClearSlot(string slotName)
    {
        if (string.IsNullOrEmpty(slotName)) return;
        if (cache.Count == 0) BuildCache();
        if (!cache.TryGetValue(slotName, out var sc)) return;
        DisableAllInSlot(sc);
        sc.currentSelection = null;
    }

    public void RandomizeSlot(string slotName)
    {
        if (string.IsNullOrEmpty(slotName)) return;
        if (cache.Count == 0) BuildCache();
        if (!cache.TryGetValue(slotName, out var sc)) return;
        var flat = new List<(Rarity rarity, int idx)>();
        Action<List<GameObject>, Rarity> add = (list, r) => { if (list != null) for (int i = 0; i < list.Count; i++) flat.Add((r, i)); };
        add(sc.common, Rarity.Common); add(sc.rare, Rarity.Rare); add(sc.legendary, Rarity.Legendary);
        if (flat.Count == 0) { ClearSlot(slotName); return; }
        var pick = flat[UnityEngine.Random.Range(0, flat.Count)];
        SetSlot(slotName, pick.rarity, pick.idx);
    }

    public int GetOptionCount(string slotName, Rarity rarity)
    {
        if (string.IsNullOrEmpty(slotName)) return 0;
        if (cache.Count == 0) BuildCache();
        if (!cache.TryGetValue(slotName, out var sc)) return 0;
        var list = GetListForRarity(sc, rarity);
        return list?.Count ?? 0;
    }

    public void RandomizeAll(bool randomizeSkin = true)
    {
        if (cache.Count == 0) BuildCache();
        foreach (var kv in cache)
        {
            RandomizeSlot(kv.Key);
        }
        if (randomizeSkin && skinMaterials != null && skinMaterials.Count > 0)
        {
            var idx = UnityEngine.Random.Range(0, skinMaterials.Count);
            SetSkinMaterial(idx);
        }
    }

    public void SetBackpack(int index) => SetSlot("Backpack", index);
    public void SetHairSlot(int index) => SetSlot("Hair", index);
    public void SetGlassesSlot(int index) => SetSlot("Glass", index);
    public void SetHatSlot(int index) => SetSlot("Hat", index);

    [ContextMenu("Clear All Traits")]
    public void ClearAll()
    {
        foreach (var kv in cache) DisableAllInSlot(kv.Value);
    }

    #endregion
}
