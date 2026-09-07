using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages holographic ghost materials and drone skin on TutorialRigRoot.
/// Supports 3 easily-switchable visual themes for different build versions:
///   - Version 1: Cyan Hologram Ghost + Red Highlight (Default)
///   - Version 2: Carbon Fiber Drone + Yellow Highlight
///   - Version 3: Carbon Fiber Drone + Cyan Highlight
///   - Custom: Manual material assignment
/// </summary>
public class TutorialGhostSkin : MonoBehaviour
{
    public enum SkinTheme
    {
        [InspectorName("Version 1: Cyan Ghost + Red Highlight (Default)")]
        CyanGhost_RedHighlight = 0,

        [InspectorName("Version 2: Carbon Fiber + Yellow Highlight")]
        CarbonFiber_YellowHighlight = 1,

        [InspectorName("Version 3: Carbon Fiber + Cyan Highlight")]
        CarbonFiber_CyanHighlight = 2,

        [InspectorName("Custom (Manual Materials)")]
        Custom = 3
    }

    [Header("Theme Preset Selection")]
    [Tooltip("Select one of the 3 requested visual themes for different builds.")]
    [SerializeField] private SkinTheme currentTheme = SkinTheme.CyanGhost_RedHighlight;
    public SkinTheme CurrentTheme => currentTheme;

    [Tooltip("In Version 2 & 3, keep hands as translucent cyan ghost so they look distinct from the carbon drone body.")]
    public bool keepHandsAsGhost = true;

    [Header("Active Materials")]
    [Tooltip("Base material applied to drone rig parts (e.g. M_TutorialGhost or CarbonFiber).")]
    public Material hologramMaterial;

    [Tooltip("Highlight material applied to active/changing parts per tutorial step (e.g. Red, Yellow, Cyan).")]
    public Material highlightMaterial;

    [Tooltip("Material for ghost hands. If assigned and keepHandsAsGhost is true, hands will use this material.")]
    public Material handMaterial;

    [Header("Preset Material References")]
    [SerializeField] private Material cyanGhostMaterial;
    [SerializeField] private Material redGhostMaterial;
    [SerializeField] private Material yellowGhostMaterial;
    [SerializeField] private Material carbonFiberMaterial;

    [Header("Camera & Background")]
    [Tooltip("Target camera for background color updates. If null, Camera.main or first camera found is used.")]
    public Camera targetCamera;

    [Tooltip("Camera background color for Version 1 (Dark slate for glowing cyan ghost).")]
    public Color darkBackgroundColor = new Color(0.10f, 0.12f, 0.16f, 1f);

    [Tooltip("Camera background color for Version 2 & 3 (Clean light studio grey for dark carbon fiber drone).")]
    public Color lightBackgroundColor = new Color(0.85f, 0.88f, 0.90f, 1f);

    [Tooltip("Whether to automatically switch camera background color based on theme.")]
    public bool syncCameraBackground = true;

    [Header("Settings")]
    public List<Transform> ghostRoots = new List<Transform>();
    public bool applyOnAwake = true;
    public bool skipHands = false;
    public bool debugLog = true;

    [Header("Hotkeys (Play Mode)")]
    [Tooltip("Allow pressing 1, 2, 3 on keyboard during Play mode to switch themes live.")]
    public bool enableHotkeySwitching = true;

    // Cache of all transforms under the rig indexed by name (case-insensitive)
    private readonly Dictionary<string, List<Transform>> transformCache = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<Renderer> currentHighlightedRenderers = new();
    private bool cacheBuilt = false;

    private const string PathCyanGhost = "Assets/Screens/FoldForStorageScreen/Materials/M_TutorialGhost.mat";
    private const string PathRedGhost = "Assets/Screens/FoldForStorageScreen/Materials/M_TutorialGhost_Red.mat";
    private const string PathYellowGhost = "Assets/Screens/FoldForStorageScreen/Materials/M_TutorialGhost_Yellow.mat";
    private const string PathCarbonFiber = "Assets/Screens/FoldForStorageScreen/Materials/CarbonFiber.mat";

    private void Awake()
    {
        EnsurePresetMaterialsLoaded();
        if (currentTheme != SkinTheme.Custom && PlayerPrefs.HasKey("SelectedDroneTheme"))
        {
            currentTheme = (SkinTheme)Mathf.Clamp(PlayerPrefs.GetInt("SelectedDroneTheme"), 0, 2);
        }

        if (currentTheme != SkinTheme.Custom)
        {
            ApplyThemePreset(currentTheme);
        }
        ApplyCameraBackground(currentTheme);

        BuildTransformCache();

        if (applyOnAwake)
        {
            ApplySkin();
        }
    }

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (enableHotkeySwitching && Application.isPlaying)
        {
            CheckHotkeyInput();
        }
#endif
    }

    private void CheckHotkeyInput()
    {
        if (UnityEngine.InputSystem.Keyboard.current == null) return;
        var kb = UnityEngine.InputSystem.Keyboard.current;

        if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame)
        {
            SetTheme(SkinTheme.CyanGhost_RedHighlight);
        }
        else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame)
        {
            SetTheme(SkinTheme.CarbonFiber_YellowHighlight);
        }
        else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame)
        {
            SetTheme(SkinTheme.CarbonFiber_CyanHighlight);
        }
    }

    /// <summary>
    /// Switches the visual theme, updates the camera background, and reapplies materials to all renderers.
    /// In Play mode, also refreshes the current tutorial step highlight.
    /// </summary>
    public void SetTheme(SkinTheme newTheme)
    {
        currentTheme = newTheme;
        if (newTheme != SkinTheme.Custom)
        {
            PlayerPrefs.SetInt("SelectedDroneTheme", (int)newTheme);
            PlayerPrefs.Save();
        }

        ApplyThemePreset(currentTheme);
        ApplyCameraBackground(currentTheme);
        ApplySkin();

        // Refresh the step highlight in FoldTutorialManager if present
        var manager = UnityEngine.Object.FindFirstObjectByType<FoldTutorialManager>();
        if (manager != null)
        {
            manager.RefreshCurrentHighlight();
        }

        if (debugLog) Debug.Log($"[TutorialGhostSkin] Switched to {currentTheme}");

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorUtility.SetDirty(this);
            if (gameObject.scene.isLoaded)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
        }
#endif
    }

    /// <summary>
    /// Updates the camera background color: Dark slate for Version 1, Light studio grey for Version 2 & 3.
    /// </summary>
    public void ApplyCameraBackground(SkinTheme theme)
    {
        if (!syncCameraBackground) return;

        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null) cam = UnityEngine.Object.FindFirstObjectByType<Camera>();
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = (theme == SkinTheme.CyanGhost_RedHighlight) ? darkBackgroundColor : lightBackgroundColor;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(cam);
                if (cam.gameObject.scene.isLoaded)
                {
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(cam.gameObject.scene);
                }
            }
#endif
        }
    }

    /// <summary>
    /// Configures the material fields according to the given theme preset.
    /// </summary>
    public void ApplyThemePreset(SkinTheme theme)
    {
        EnsurePresetMaterialsLoaded();

        switch (theme)
        {
            case SkinTheme.CyanGhost_RedHighlight:
                hologramMaterial = cyanGhostMaterial;
                highlightMaterial = redGhostMaterial;
                handMaterial = cyanGhostMaterial;
                break;

            case SkinTheme.CarbonFiber_YellowHighlight:
                hologramMaterial = carbonFiberMaterial;
                highlightMaterial = yellowGhostMaterial;
                handMaterial = keepHandsAsGhost ? cyanGhostMaterial : carbonFiberMaterial;
                break;

            case SkinTheme.CarbonFiber_CyanHighlight:
                hologramMaterial = carbonFiberMaterial;
                highlightMaterial = cyanGhostMaterial;
                handMaterial = keepHandsAsGhost ? cyanGhostMaterial : carbonFiberMaterial;
                break;

            case SkinTheme.Custom:
                // Retain existing manually assigned materials
                break;
        }
    }

    /// <summary>
    /// Ensures all 4 preset materials are loaded from disk if not yet assigned.
    /// </summary>
    public void EnsurePresetMaterialsLoaded()
    {
#if UNITY_EDITOR
        if (cyanGhostMaterial == null)
            cyanGhostMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(PathCyanGhost);
        if (redGhostMaterial == null)
            redGhostMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(PathRedGhost);
        if (yellowGhostMaterial == null)
            yellowGhostMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(PathYellowGhost);
        if (carbonFiberMaterial == null)
            carbonFiberMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(PathCarbonFiber);
#endif
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (currentTheme != SkinTheme.Custom)
        {
            ApplyThemePreset(currentTheme);
        }

        if (!Application.isPlaying && gameObject.scene.isLoaded)
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null && gameObject != null)
                {
                    ApplySkin();
                    ApplyCameraBackground(currentTheme);
                }
            };
        }
    }
#endif

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
    /// Applies base hologramMaterial (or handMaterial for hands) to all renderers in ghostRoots,
    /// clearing any active highlights.
    /// </summary>
    [ContextMenu("Apply Skin")]
    public void ApplySkin()
    {
        EnsurePresetMaterialsLoaded();
        if (hologramMaterial == null && currentTheme != SkinTheme.Custom)
        {
            ApplyThemePreset(currentTheme);
        }

        if (hologramMaterial == null)
        {
            if (debugLog) Debug.LogWarning("[TutorialGhostSkin] No hologram/drone material assigned.");
            return;
        }

        currentHighlightedRenderers.Clear();

        List<Transform> targets = (ghostRoots != null && ghostRoots.Count > 0) ? ghostRoots : new List<Transform> { transform };

        int count = 0;
        foreach (var root in targets)
        {
            if (root == null) continue;

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r == null) continue;
                bool isHand = IsHand(r.transform);

                if (isHand)
                {
                    if (skipHands)
                        continue;

                    Material handMat = (keepHandsAsGhost && handMaterial != null) ? handMaterial : hologramMaterial;
                    ApplyMaterialToRenderer(r, handMat);
                    count++;
                }
                else
                {
                    ApplyMaterialToRenderer(r, hologramMaterial);
                    count++;
                }
            }
        }

        if (debugLog) Debug.Log($"[TutorialGhostSkin] Applied theme '{currentTheme}' ({hologramMaterial.name}) to {count} renderers.");
    }

    /// <summary>
    /// Reverts all currently highlighted renderers back to their base material.
    /// </summary>
    public void ClearHighlights()
    {
        EnsurePresetMaterialsLoaded();
        if (hologramMaterial == null) return;

        foreach (var r in currentHighlightedRenderers)
        {
            if (r != null)
            {
                bool isHand = IsHand(r.transform);
                Material baseMat = (isHand && keepHandsAsGhost && handMaterial != null) ? handMaterial : hologramMaterial;
                ApplyMaterialToRenderer(r, baseMat);
            }
        }
        currentHighlightedRenderers.Clear();
    }

    /// <summary>
    /// Highlights specific parts by GameObject names (e.g. 'Clamp_Body_Right_Latch', 'Ghost_Battery_Upper').
    /// Reverts previously highlighted parts back to base material. Hands are never highlighted.
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

        EnsurePresetMaterialsLoaded();
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

        if (debugLog) Debug.Log($"[TutorialGhostSkin] Highlighted {count} renderers with {highlightMaterial.name} for active part.");
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

    // Context Menu Items
    [ContextMenu("Theme: Version 1 (Cyan Ghost + Red Highlight)")]
    public void SetVersion1() => SetTheme(SkinTheme.CyanGhost_RedHighlight);

    [ContextMenu("Theme: Version 2 (Carbon Fiber + Yellow Highlight)")]
    public void SetVersion2() => SetTheme(SkinTheme.CarbonFiber_YellowHighlight);

    [ContextMenu("Theme: Version 3 (Carbon Fiber + Cyan Highlight)")]
    public void SetVersion3() => SetTheme(SkinTheme.CarbonFiber_CyanHighlight);
}
