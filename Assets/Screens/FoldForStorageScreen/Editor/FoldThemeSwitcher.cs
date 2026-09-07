using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Provides 1-click Editor menu items and batch build automation for the 3 requested visual versions:
///   1. Version 1: Cyan Ghost + Red Highlight (Default)
///   2. Version 2: Carbon Fiber + Yellow Highlight
///   3. Version 3: Carbon Fiber + Cyan Highlight
/// </summary>
public static class FoldThemeSwitcher
{
    private const string FoldScenePath = "Assets/Screens/FoldForStorageScreen/FoldForStorage.unity";

    [MenuItem("Tools/Fold Tutorial Theme/Version 1: Cyan Ghost + Red Highlight", priority = 10)]
    public static void SwitchToVersion1()
    {
        ApplyThemeToFoldScene(TutorialGhostSkin.SkinTheme.CyanGhost_RedHighlight);
    }

    [MenuItem("Tools/Fold Tutorial Theme/Version 2: Carbon Fiber + Yellow Highlight", priority = 11)]
    public static void SwitchToVersion2()
    {
        ApplyThemeToFoldScene(TutorialGhostSkin.SkinTheme.CarbonFiber_YellowHighlight);
    }

    [MenuItem("Tools/Fold Tutorial Theme/Version 3: Carbon Fiber + Cyan Highlight", priority = 12)]
    public static void SwitchToVersion3()
    {
        ApplyThemeToFoldScene(TutorialGhostSkin.SkinTheme.CarbonFiber_CyanHighlight);
    }

    [InitializeOnLoadMethod]
    [MenuItem("Tools/Enforce Landscape Only (Horizontal)", priority = 100)]
    public static void EnforceLandscapePlayerSettings()
    {
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;

        var ps = Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings");
        if (ps != null)
        {
            EditorUtility.SetDirty(ps);
        }
        AssetDatabase.SaveAssets();
        EditorApplication.ExecuteMenuItem("File/Save Project");
        Debug.Log("<color=green><b>[FoldThemeSwitcher]</b></color> Configured PlayerSettings to Landscape only and saved project.");
    }

    /// <summary>
    /// Loads the FoldForStorage scene if not active, applies the requested theme, and saves the scene.
    /// </summary>
    public static bool ApplyThemeToFoldScene(TutorialGhostSkin.SkinTheme theme)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.path != FoldScenePath)
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                activeScene = EditorSceneManager.OpenScene(FoldScenePath);
            }
            else
            {
                Debug.LogWarning("[FoldThemeSwitcher] Open scene cancelled.");
                return false;
            }
        }

        TutorialGhostSkin ghostSkin = UnityEngine.Object.FindFirstObjectByType<TutorialGhostSkin>();
        if (ghostSkin == null)
        {
            GameObject rigRoot = GameObject.Find("TutorialRigRoot");
            if (rigRoot != null)
            {
                ghostSkin = rigRoot.GetComponent<TutorialGhostSkin>();
            }
        }

        if (ghostSkin == null)
        {
            Debug.LogError("[FoldThemeSwitcher] Could not find TutorialGhostSkin in scene!");
            return false;
        }

        ghostSkin.SetTheme(theme);
        
        Camera cam = UnityEngine.Object.FindFirstObjectByType<Camera>();
        if (cam != null)
        {
            Undo.RecordObject(cam, "Change Theme Camera Background");
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = (theme == TutorialGhostSkin.SkinTheme.CyanGhost_RedHighlight)
                ? ghostSkin.darkBackgroundColor
                : ghostSkin.lightBackgroundColor;
            EditorUtility.SetDirty(cam);
        }

        EditorUtility.SetDirty(ghostSkin);
        EditorSceneManager.MarkSceneDirty(ghostSkin.gameObject.scene);
        EditorSceneManager.SaveScene(ghostSkin.gameObject.scene);

        Debug.Log($"<color=green><b>[FoldThemeSwitcher]</b></color> Successfully switched and saved FoldForStorage to <b>{theme}</b> (Camera BG: {(theme == TutorialGhostSkin.SkinTheme.CyanGhost_RedHighlight ? "Dark" : "Light")})!");
        return true;
    }

    [MenuItem("Tools/Fold Tutorial Theme/Batch Build All 3 APKs...", priority = 50)]
    public static void BatchBuildAllThreeVersions()
    {
        string defaultFolder = Path.Combine(Directory.GetCurrentDirectory(), "Builds");
        if (!Directory.Exists(defaultFolder))
        {
            Directory.CreateDirectory(defaultFolder);
        }

        string chosenFolder = EditorUtility.SaveFolderPanel("Choose Build Output Directory", defaultFolder, "");
        if (string.IsNullOrEmpty(chosenFolder))
        {
            Debug.Log("[FoldThemeSwitcher] Batch build cancelled by user.");
            return;
        }

        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0)
        {
            Debug.LogError("[FoldThemeSwitcher] No scenes enabled in EditorBuildSettings!");
            return;
        }

        var builds = new (TutorialGhostSkin.SkinTheme theme, string filename)[]
        {
            (TutorialGhostSkin.SkinTheme.CyanGhost_RedHighlight, "MobileTrainer_v1_CyanGhost_RedHighlight.apk"),
            (TutorialGhostSkin.SkinTheme.CarbonFiber_YellowHighlight, "MobileTrainer_v2_CarbonFiber_YellowHighlight.apk"),
            (TutorialGhostSkin.SkinTheme.CarbonFiber_CyanHighlight, "MobileTrainer_v3_CarbonFiber_CyanHighlight.apk")
        };

        try
        {
            for (int i = 0; i < builds.Length; i++)
            {
                var b = builds[i];
                EditorUtility.DisplayProgressBar("Batch Building APKs", $"Building {b.theme} ({i + 1}/{builds.Length})...", (float)i / builds.Length);

                ApplyThemeToFoldScene(b.theme);

                BuildPlayerOptions opt = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = Path.Combine(chosenFolder, b.filename),
                    target = EditorUserBuildSettings.activeBuildTarget,
                    options = BuildOptions.None
                };

                Debug.Log($"[FoldThemeSwitcher] Building {b.filename} for {opt.target}...");
                var report = BuildPipeline.BuildPlayer(opt);
                if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                {
                    EditorUtility.ClearProgressBar();
                    Debug.LogError($"[FoldThemeSwitcher] Failed building {b.filename}: {report.summary.result}");
                    return;
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Debug.Log($"<color=green><b>[FoldThemeSwitcher]</b></color> All 3 versions built successfully into: {chosenFolder}");
        EditorUtility.RevealInFinder(chosenFolder);
    }
}
