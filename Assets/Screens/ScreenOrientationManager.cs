using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Enforces landscape-only (horizontal) screen orientation across the entire application.
/// Disables portrait / vertical mode completely, supporting auto-rotation between LandscapeLeft and LandscapeRight.
/// </summary>
public class ScreenOrientationManager : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void EnforceLandscapeOrientation()
    {
        // Disallow vertical / portrait modes
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;

        // Allow both horizontal / landscape modes
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;

        // Force landscape if currently in portrait
        if (Screen.orientation == ScreenOrientation.Portrait ||
            Screen.orientation == ScreenOrientation.PortraitUpsideDown ||
            Screen.width < Screen.height)
        {
            Screen.orientation = ScreenOrientation.LandscapeLeft;
        }

        // Enable auto-rotation between allowed horizontal orientations
        Screen.orientation = ScreenOrientation.AutoRotation;

        Debug.Log("[ScreenOrientationManager] Enforced horizontal / landscape orientation (Vertical mode disabled).");
    }

    private void Awake()
    {
        EnforceLandscapeOrientation();
    }

#if UNITY_EDITOR
    [InitializeOnLoadMethod]
    private static void ValidatePlayerSettingsOrientation()
    {
        bool changed = false;
        if (PlayerSettings.allowedAutorotateToPortrait || PlayerSettings.allowedAutorotateToPortraitUpsideDown)
        {
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            changed = true;
        }

        if (!PlayerSettings.allowedAutorotateToLandscapeLeft || !PlayerSettings.allowedAutorotateToLandscapeRight)
        {
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            changed = true;
        }

        if (PlayerSettings.defaultInterfaceOrientation != UIOrientation.AutoRotation)
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            changed = true;
        }

        if (changed)
        {
            AssetDatabase.SaveAssets();
            Debug.Log("[ScreenOrientationManager] Configured PlayerSettings to Landscape only (Vertical mode disabled).");
        }
    }

    [MenuItem("Tools/Enforce Landscape Orientation", priority = 100)]
    public static void MenuEnforceLandscape()
    {
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        AssetDatabase.SaveAssets();
        Debug.Log("[ScreenOrientationManager] PlayerSettings updated: Horizontal only (Portrait disabled).");
    }
#endif
}
