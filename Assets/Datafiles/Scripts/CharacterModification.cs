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
    public bool debugLogs = false;

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
        slot = NormalizeSlotName(slot);
        if (string.IsNullOrEmpty(slot)) return 0;
        if (cache.Count == 0) BuildCache();
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
        var roots = GetCharacterRoots();
        if (roots.Count == 0) return;
        if (index < 0 || index >= roots.Count) return;
        for (int i = 0; i < roots.Count; i++)
        {
            var child = roots[i];
            if (child == null) continue;
            child.gameObject.SetActive(i == index);
        }
        if (rebuildTraits) BuildCache();
    }

    public void SetCharacterByName(string name, bool rebuildTraits = true)
    {
        if (string.IsNullOrEmpty(name)) return;
        var roots = GetCharacterRoots();
        if (roots.Count == 0) return;
        bool matchedAny = false;
        for (int i = 0; i < roots.Count; i++)
        {
            var child = roots[i];
            if (child == null) continue;
            bool isMatch = CharacterNameMatches(child.name, name);
            child.gameObject.SetActive(isMatch);
            matchedAny |= isMatch;
        }

        if (!matchedAny)
        {
            if (debugLogs)
            {
                Debug.LogWarning($"[CharacterModification] No character matched '{name}'. Falling back to first character root.");
            }

            for (int i = 0; i < roots.Count; i++)
            {
                var child = roots[i];
                if (child == null) continue;
                child.gameObject.SetActive(i == 0);
            }
        }
        if (rebuildTraits) BuildCache();
    }

    #endregion

    #region Helpers

    private bool CharacterNameMatches(string actualName, string requestedName)
    {
        if (string.IsNullOrWhiteSpace(actualName) || string.IsNullOrWhiteSpace(requestedName))
        {
            return false;
        }

        string actual = NormalizeNameToken(actualName);
        string requested = NormalizeNameToken(requestedName);

        if (string.IsNullOrEmpty(actual) || string.IsNullOrEmpty(requested))
        {
            return false;
        }

        if (actual == requested) return true;

        // Allow loose matching for minor naming differences in hierarchy labels.
        return actual.Contains(requested) || requested.Contains(actual);
    }

    private string NormalizeNameToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var chars = value.Trim().ToLowerInvariant().ToCharArray();
        var compact = new System.Text.StringBuilder(chars.Length);
        for (int i = 0; i < chars.Length; i++)
        {
            if (char.IsLetterOrDigit(chars[i]))
            {
                compact.Append(chars[i]);
            }
        }
        return compact.ToString();
    }

    private Transform FindBestTraitsRoot()
    {
        Transform best = null;
        int bestScore = -1;

        void Consider(Transform candidate)
        {
            if (candidate == null) return;
            int score = GetTraitsRootScore(candidate);
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        Consider(traitsRoot);
        Consider(charactersRoot);
        Consider(transform);

        var all = FindObjectsOfType<Transform>(includeInactive);
        for (int i = 0; i < all.Length; i++)
        {
            Consider(all[i]);
        }

        if (debugLogs)
        {
            string bestName = best != null ? best.name : "null";
            Debug.Log($"[CharacterModification] FindBestTraitsRoot best={bestName}, score={bestScore}");
        }

        return bestScore > 0 ? best : null;
    }

    private int GetTraitsRootScore(Transform root)
    {
        if (root == null) return -1;

        int score = 0;

        if (FindDirectChildByName(root, commonName) != null) score += 4;
        if (FindDirectChildByName(root, rareName) != null) score += 4;
        if (FindDirectChildByName(root, legendaryName) != null) score += 4;

        int characterLikeChildren = GetCharacterLikeChildCount(root);
        score += characterLikeChildren * 5;

        if (HasRaritySubtree(root)) score += 2;

        string normalized = NormalizeNameToken(root.name);
        if (normalized.Contains("trait")) score += 2;
        if (normalized.Contains("sushinad")) score += 2;
        if (normalized.Contains("character")) score += 1;

        return score;
    }

    private int GetCharacterLikeChildCount(Transform root)
    {
        if (root == null) return 0;

        int count = 0;
        for (int i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            if (child == null) continue;

            if (NameEquals(child.name, commonName) || NameEquals(child.name, rareName) || NameEquals(child.name, legendaryName))
            {
                continue;
            }

            if (HasRaritySubtree(child))
            {
                count++;
            }
        }

        return count;
    }

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

    private struct RarityRootRef
    {
        public Transform root;
        public Rarity rarity;
    }

    private Dictionary<string, SlotCache> cache = new Dictionary<string, SlotCache>(StringComparer.OrdinalIgnoreCase);
    private Transform resolvedTraitsRoot;

    private void Awake()
    {
        BuildCache();
    }

    private void BuildCache()
    {
        cache.Clear();
        resolvedTraitsRoot = null;

        if (!autoScanHierarchy)
        {
            foreach (var scfg in slots)
            {
                if (string.IsNullOrEmpty(scfg.slotName)) continue;
                string key = NormalizeSlotName(scfg.slotName);
                if (string.IsNullOrEmpty(key)) continue;

                if (!cache.TryGetValue(key, out var sc))
                {
                    sc = new SlotCache { config = scfg };
                    cache[key] = sc;
                }

                MergeOptionLists(sc.common, MakeOptionList(scfg.common));
                MergeOptionLists(sc.rare, MakeOptionList(scfg.rare));
                MergeOptionLists(sc.legendary, MakeOptionList(scfg.legendary));
            }
            return;
        }

        Transform root = ResolveTraitsRoot();
        bool built = TryBuildCacheFromRoot(root);

        if (!built)
        {
            Transform fallback = FindBestTraitsRoot();
            if (fallback != root)
            {
                built = TryBuildCacheFromRoot(fallback);
            }
        }

        if (!built && debugLogs)
        {
            Debug.LogWarning("[CharacterModification] BuildCache found no trait slots. Check traitsRoot/character hierarchy.");
        }
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

    private List<Transform> GetCharacterRoots()
    {
        var roots = new List<Transform>();

        if (charactersRoot != null)
        {
            for (int i = 0; i < charactersRoot.childCount; i++)
            {
                var child = charactersRoot.GetChild(i);
                if (child != null) roots.Add(child);
            }
            return roots;
        }

        Transform root = resolvedTraitsRoot != null ? resolvedTraitsRoot : ResolveTraitsRoot();
        if (root == null || root.childCount == 0)
        {
            root = FindBestTraitsRoot();
        }
        if (root == null) return roots;

        for (int i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            if (child == null) continue;
            if (NameEquals(child.name, commonName) || NameEquals(child.name, rareName) || NameEquals(child.name, legendaryName))
            {
                continue;
            }

            if (HasRaritySubtree(child))
            {
                roots.Add(child);
            }
        }

        return roots;
    }

    private bool TryBuildCacheFromRoot(Transform root)
    {
        if (root == null) return false;

        int before = cache.Count;
        var rootsToScan = CollectAutoScanRarityRoots(root);
        for (int i = 0; i < rootsToScan.Count; i++)
        {
            AddRaritySlots(rootsToScan[i].root, rootsToScan[i].rarity);
        }

        if (cache.Count > before)
        {
            resolvedTraitsRoot = root;
            if (debugLogs)
            {
                Debug.Log($"[CharacterModification] BuildCache success. root={root.name}, slots={cache.Count}");
            }
            return true;
        }

        return false;
    }

    private void AddRaritySlots(Transform rarityRoot, Rarity rarity)
    {
        if (rarityRoot == null) return;
        for (int i = 0; i < rarityRoot.childCount; i++)
        {
            var trait = rarityRoot.GetChild(i);
            if (trait == null) continue;
            var slotName = NormalizeSlotName(trait.name);
            if (string.IsNullOrEmpty(slotName)) continue;
            if (!cache.TryGetValue(slotName, out var sc))
            {
                sc = new SlotCache { config = new SlotConfig { slotName = slotName } };
                cache[slotName] = sc;
            }
            var options = MakeOptionList(trait);
            switch (rarity)
            {
                case Rarity.Common: MergeOptionLists(sc.common, options); break;
                case Rarity.Rare: MergeOptionLists(sc.rare, options); break;
                case Rarity.Legendary: MergeOptionLists(sc.legendary, options); break;
            }
        }
    }

    private List<RarityRootRef> CollectAutoScanRarityRoots(Transform root)
    {
        var result = new List<RarityRootRef>();
        if (root == null) return result;

        // 1) Global rarity roots directly under traits root (for shared slots like outfits/backpacks)
        AddRarityRootIfValid(result, FindDirectChildByName(root, commonName), Rarity.Common);
        AddRarityRootIfValid(result, FindDirectChildByName(root, rareName), Rarity.Rare);
        AddRarityRootIfValid(result, FindDirectChildByName(root, legendaryName), Rarity.Legendary);

        // 2) Active character rarity roots (for per-character slots like eye/hat/mouth/eyewear)
        var activeCharacterRoot = ResolveActiveCharacterRoot(root);
        if (activeCharacterRoot != null)
        {
            AddRarityRootIfValid(result, FindByName(activeCharacterRoot, commonName), Rarity.Common);
            AddRarityRootIfValid(result, FindByName(activeCharacterRoot, rareName), Rarity.Rare);
            AddRarityRootIfValid(result, FindByName(activeCharacterRoot, legendaryName), Rarity.Legendary);
        }

        return result;
    }

    private void AddRarityRootIfValid(List<RarityRootRef> target, Transform rarityRoot, Rarity rarity)
    {
        if (target == null || rarityRoot == null) return;
        for (int i = 0; i < target.Count; i++)
        {
            if (target[i].root == rarityRoot) return;
        }
        target.Add(new RarityRootRef { root = rarityRoot, rarity = rarity });
    }

    private Transform ResolveActiveCharacterRoot(Transform root)
    {
        // Prefer explicit characters root if configured.
        if (charactersRoot != null)
        {
            Transform fallback = null;
            for (int i = 0; i < charactersRoot.childCount; i++)
            {
                var child = charactersRoot.GetChild(i);
                if (child == null) continue;
                if (!HasRaritySubtree(child)) continue;

                if (child.gameObject.activeInHierarchy) return child;
                if (fallback == null) fallback = child;
            }
            if (fallback != null) return fallback;
        }

        if (root == null) return null;

        // Fallback: infer character root from traits root children (excluding global rarity folders).
        Transform inferredFallback = null;
        for (int i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            if (child == null) continue;
            if (NameEquals(child.name, commonName) || NameEquals(child.name, rareName) || NameEquals(child.name, legendaryName))
            {
                continue;
            }
            if (!HasRaritySubtree(child)) continue;

            if (child.gameObject.activeInHierarchy) return child;
            if (inferredFallback == null) inferredFallback = child;
        }

        return inferredFallback;
    }

    private bool HasRaritySubtree(Transform root)
    {
        if (root == null) return false;
        return FindByName(root, commonName) != null
            || FindByName(root, rareName) != null
            || FindByName(root, legendaryName) != null;
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
            if (t != null && NameEquals(t.name, name))
            {
                return t;
            }
        }
        return null;
    }

    private Transform FindDirectChildByName(Transform root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name)) return null;
        for (int i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            if (child != null && NameEquals(child.name, name))
            {
                return child;
            }
        }
        return null;
    }

    private static bool NameEquals(string a, string b)
    {
        return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    private static void MergeOptionLists(List<GameObject> target, List<GameObject> incoming)
    {
        if (target == null || incoming == null) return;
        for (int i = 0; i < incoming.Count; i++)
        {
            var go = incoming[i];
            if (go == null) continue;
            if (!target.Contains(go))
            {
                target.Add(go);
            }
        }
    }

    private string NormalizeSlotName(string slotName)
    {
        if (string.IsNullOrWhiteSpace(slotName)) return slotName;

        string key = slotName.Trim().Replace("_", string.Empty).Replace(" ", string.Empty).ToLowerInvariant();
        switch (key)
        {
            case "backpack":
            case "backpacks":
            case "bag":
            case "bags":
                return "Backpack";

            case "outfit":
            case "outfits":
            case "clothes":
            case "cloth":
                return "Outfit";

            case "glass":
            case "glasses":
            case "eyewear":
                return "Glass";

            case "eye":
                return "Eye";

            case "hat":
                return "Hat";

            case "mouth":
                return "Mouth";

            case "hair":
                return "Hair";

            case "body":
                return "Body";

            default:
                return slotName.Trim();
        }
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

    private List<(Rarity rarity, int idx)> BuildCombinedIndexMap(SlotCache sc)
    {
        var flat = new List<(Rarity rarity, int idx)>();
        if (sc == null) return flat;

        void Add(List<GameObject> list, Rarity rarity)
        {
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                flat.Add((rarity, i));
            }
        }

        // Stable order when UI does not expose rarity tabs.
        Add(sc.common, Rarity.Common);
        Add(sc.rare, Rarity.Rare);
        Add(sc.legendary, Rarity.Legendary);
        return flat;
    }

    public void SetSlot(string slotName, Rarity rarity, int index)
    {
        slotName = NormalizeSlotName(slotName);
        if (string.IsNullOrEmpty(slotName)) return;
        if (cache.Count == 0) BuildCache();
        if (!cache.TryGetValue(slotName, out var sc))
        {
            if (debugLogs) Debug.LogWarning($"[CharacterModification] Slot '{slotName}' not found. cacheCount={cache.Count}");
            return;
        }

        DisableAllInSlot(sc);

        if (index < 0) { sc.currentSelection = null; return; }

        var list = GetListForRarity(sc, rarity);
        if (list == null || index >= list.Count)
        {
            if (debugLogs) Debug.LogWarning($"[CharacterModification] Slot '{slotName}' rarity={rarity} index={index} out of range count={(list == null ? 0 : list.Count)}");
            return;
        }
        var go = list[index];
        if (go == null) return;
        go.SetActive(true);
        sc.currentSelection = (rarity, index);
    }

    public void SetSlot(string slotName, int index)
    {
        slotName = NormalizeSlotName(slotName);
        if (string.IsNullOrEmpty(slotName)) return;
        if (cache.Count == 0) BuildCache();
        if (!cache.TryGetValue(slotName, out var sc))
        {
            if (debugLogs) Debug.LogWarning($"[CharacterModification] Slot '{slotName}' not found for flat index selection. cacheCount={cache.Count}");
            return;
        }

        if (index < 0)
        {
            ClearSlot(slotName);
            return;
        }

        var flat = BuildCombinedIndexMap(sc);
        if (index >= flat.Count)
        {
            if (debugLogs) Debug.LogWarning($"[CharacterModification] Slot '{slotName}' flat index={index} out of range count={flat.Count}");
            return;
        }

        var pick = flat[index];
        SetSlot(slotName, pick.rarity, pick.idx);
    }

    public void SetSlotByName(string slotName, string childName)
    {
        slotName = NormalizeSlotName(slotName);
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
        slotName = NormalizeSlotName(slotName);
        if (string.IsNullOrEmpty(slotName)) return;
        if (cache.Count == 0) BuildCache();
        if (!cache.TryGetValue(slotName, out var sc)) return;
        DisableAllInSlot(sc);
        sc.currentSelection = null;
    }

    public void RandomizeSlot(string slotName)
    {
        slotName = NormalizeSlotName(slotName);
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
        slotName = NormalizeSlotName(slotName);
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
