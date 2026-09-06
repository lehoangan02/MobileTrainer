using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages holographic ghost materials on TutorialRigRoot.
/// Applies hologramMaterial (cyan ghost) by default, and dynamically highlights
/// specific active/changing parts using highlightMaterial (red ghost) per tutorial step.
/// </summary>
public class TutorialGhostSkin : MonoBehaviour
{
    [Header("Hologram Materials")]
    [Tooltip("Base hologram material applied to all ghost rig parts (e.g. M_TutorialGhost - cyan).")]
    public Material hologramMaterial;
    [Tooltip("Highlight hologram material applied to parts currently changing (e.g. M_TutorialGhost_Red).")]
    public Material highlightMaterial;

    [Header("Settings")]
    public List<Transform> ghostRoots = new List<Transform>();
    public bool applyOnAwake = true;
    public bool skipHands = false;
    public bool debugLog = true;

    // Cache of all transforms under the rig indexed by name (case-insensitive)
    private readonly Dictionary<string, List<Transform>> transformCache = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<Renderer> currentHighlightedRenderers = new();
    private bool cacheBuilt = false;

    private void Awake()
    {
        EnsureMaterialsLoaded();
        BuildTransformCache();

        if (applyOnAwake)
        {
            ApplySkin();
        }
    }

    private void EnsureMaterialsLoaded()
    {
#if UNITY_EDITOR
        if (hologramMaterial == null)
        {
            hologramMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Screens/FoldForStorageScreen/Materials/M_TutorialGhost.mat");
        }
        if (highlightMaterial == null)
        {
            highlightMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Screens/FoldForStorageScreen/Materials/M_TutorialGhost_Red.mat");
        }
#endif
    }

    public void BuildTransformCache()
    {
        transformCache.Clear();
        Transform[] all = GetComponentsInChildren<Transform>(true);
        foreach (var t in all)
        {
            if (t == null) continue;
            if (!transformCache.TryGetValue(t.name, out var list))
            {
                list = new List<Transform>();
                transformCache[t.name] = list;
            }
            list.Add(t);
        }
        cacheBuilt = true;
    }

    /// <summary>
    /// Applies base hologramMaterial to all renderers in ghostRoots, clearing any highlights.
    /// </summary>
    [ContextMenu("Apply Skin")]
    public void ApplySkin()
    {
        EnsureMaterialsLoaded();
        if (hologramMaterial == null)
        {
            if (debugLog) Debug.LogWarning("[TutorialGhostSkin] No hologram material assigned.");
            return;
        }

        currentHighlightedRenderers.Clear();

        List<Transform> targets = (ghostRoots != null && ghostRoots.Count > 0) ? ghostRoots : new List<Transform> { transform };

        int count = 0;
        foreach (var root in targets)
        {
            if (root == null) continue;

            if (skipHands && IsHand(root))
                continue;

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r == null) continue;
                if (skipHands && IsHand(r.transform))
                    continue;

                ApplyMaterialToRenderer(r, hologramMaterial);
                count++;
            }
        }

        if (debugLog) Debug.Log($"[TutorialGhostSkin] Applied base ghost skin to {count} renderers.");
    }

    /// <summary>
    /// Reverts all currently highlighted renderers back to the base hologramMaterial.
    /// </summary>
    public void ClearHighlights()
    {
        EnsureMaterialsLoaded();
        if (hologramMaterial == null) return;

        foreach (var r in currentHighlightedRenderers)
        {
            if (r != null)
            {
                ApplyMaterialToRenderer(r, hologramMaterial);
            }
        }
        currentHighlightedRenderers.Clear();
    }

    /// <summary>
    /// Highlights specific parts by GameObject names (e.g. 'Clamp_Body_Right_Latch', 'Ghost_Battery_Upper').
    /// Reverts previously highlighted parts back to base hologramMaterial. Hands are never highlighted.
    /// </summary>
    public void HighlightByNames(params string[] partNames)
    {
        if (partNames == null || partNames.Length == 0)
        {
            ClearHighlights();
            return;
        }

        if (!cacheBuilt || transformCache.Count == 0)
        {
            BuildTransformCache();
        }

        List<Transform> targets = new();
        foreach (var name in partNames)
        {
            if (string.IsNullOrEmpty(name)) continue;

            if (transformCache.TryGetValue(name.Trim(), out var list))
            {
                targets.AddRange(list);
            }
            else
            {
                // Fallback: search by partial name or path
                Transform found = FindChildRecursive(transform, name.Trim());
                if (found != null)
                {
                    targets.Add(found);
                }
                else if (debugLog)
                {
                    Debug.LogWarning($"[TutorialGhostSkin] Target part '{name}' not found under {transform.name}.");
                }
            }
        }

        HighlightParts(targets);
    }

    /// <summary>
    /// Highlights the specified root transforms (and their child renderers, excluding hands) using highlightMaterial.
    /// </summary>
    public void HighlightParts(IEnumerable<Transform> targetRoots)
    {
        ClearHighlights();

        EnsureMaterialsLoaded();
        if (highlightMaterial == null)
        {
            if (debugLog) Debug.LogWarning("[TutorialGhostSkin] No highlight material assigned.");
            return;
        }

        if (targetRoots == null) return;

        int count = 0;
        foreach (var root in targetRoots)
        {
            if (root == null) continue;
            if (IsHand(root)) continue;

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r == null) continue;
                if (IsHand(r.transform)) continue;

                ApplyMaterialToRenderer(r, highlightMaterial);
                currentHighlightedRenderers.Add(r);
                count++;
            }
        }

        if (debugLog) Debug.Log($"[TutorialGhostSkin] Highlighted {count} renderers in red for active part.");
    }

    private void ApplyMaterialToRenderer(Renderer r, Material mat)
    {
        Material[] existing = r.sharedMaterials;
        int len = existing != null && existing.Length > 0 ? existing.Length : 1;
        Material[] newMats = new Material[len];
        for (int i = 0; i < len; i++)
        {
            newMats[i] = mat;
        }
        r.sharedMaterials = newMats;
    }

    private bool IsHand(Transform t)
    {
        while (t != null && t != transform)
        {
            string n = t.name;
            if (n.StartsWith("Ghost_Hand") || n.Contains("XRHand") || n.Contains("OpenXR") || n == "LeftHand" || n == "RightHand")
                return true;
            t = t.parent;
        }
        return false;
    }

    private Transform FindChildRecursive(Transform parent, string targetName)
    {
        if (parent.name.Equals(targetName, System.StringComparison.OrdinalIgnoreCase))
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform result = FindChildRecursive(parent.GetChild(i), targetName);
            if (result != null) return result;
        }
        return null;
    }
}
