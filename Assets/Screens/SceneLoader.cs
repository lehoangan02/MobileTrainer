using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneLoader : MonoBehaviour
{
    [Header("Transition Settings")]
    [SerializeField] private float fadeOutDuration = 0.5f;
    [SerializeField] private float fadeInDuration = 0.6f;
    [SerializeField] private string targetScene = "TutorialSelectScreen";

    [Header("Back Navigation")]
    [Tooltip("If true, pressing the Android back gesture or Escape key will return to the previous scene.")]
    [SerializeField] private bool handleBackGesture = true;

    [Tooltip("Optional custom fallback scene if history is empty. If left empty, automatic hierarchy is used.")]
    [SerializeField] private string customFallbackBackScene = "";

    // Static scene history stack shared across all scenes
    private static readonly Stack<string> sceneHistory = new Stack<string>();
    private static bool isTransitioning = false;

    private CanvasGroup canvasGroup;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }

    private void Start()
    {
        // Reset transitioning flag in new scene
        isTransitioning = false;

        // If on the root/home screen, clear any stale history
        if (SceneManager.GetActiveScene().name == "StartScreen")
        {
            sceneHistory.Clear();
        }
    }

    private void Update()
    {
        if (!handleBackGesture || isTransitioning)
            return;

        if (WasBackPressed())
        {
            // 1. If any LeanWindow (modal/popup) is open, let LeanWindowCloser close it first
            if (IsAnyModalOpen())
            {
                return;
            }

            // 2. Otherwise, navigate back to the previous scene
            GoBack();
        }
    }

    /// <summary>
    /// Checks for the Android Back gesture / Escape key across both New and Legacy input systems.
    /// </summary>
    private bool WasBackPressed()
    {
        if (CW.Common.CwInput.GetKeyWentDown(KeyCode.Escape))
        {
            return true;
        }

#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Keyboard.current != null &&
            UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            return true;
        }
#endif

        return false;
    }

    /// <summary>
    /// Checks if any LeanWindow popup/modal is currently open in this scene.
    /// </summary>
    private bool IsAnyModalOpen()
    {
        if (Lean.Gui.LeanWindowCloser.Instances.Count > 0)
        {
            var closer = Lean.Gui.LeanWindowCloser.Instances[0];
            if (closer != null && closer.WindowOrder != null)
            {
                for (int i = 0; i < closer.WindowOrder.Count; i++)
                {
                    if (closer.WindowOrder[i] != null && closer.WindowOrder[i].On)
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Navigates back to the previous scene with a smooth fade.
    /// </summary>
    public void GoBack()
    {
        if (isTransitioning)
            return;

        string currentScene = SceneManager.GetActiveScene().name;
        string previous = null;

        // Try popping from history stack
        while (sceneHistory.Count > 0)
        {
            string candidate = sceneHistory.Pop();
            if (!string.IsNullOrEmpty(candidate) && candidate != currentScene)
            {
                previous = candidate;
                break;
            }
        }

        // Fallback if testing scene directly with empty history
        if (string.IsNullOrEmpty(previous))
        {
            if (!string.IsNullOrEmpty(customFallbackBackScene))
            {
                previous = customFallbackBackScene;
            }
            else
            {
                previous = GetDefaultBackScene(currentScene);
            }
        }

        if (!string.IsNullOrEmpty(previous) && previous != currentScene)
        {
            StartCoroutine(TransitionRoutine(previous, isGoingBack: true));
        }
        else if (currentScene == "StartScreen")
        {
            // On the home screen, exit app on Android if Back is pressed
#if UNITY_ANDROID && !UNITY_EDITOR
            Application.Quit();
#endif
        }
    }

    /// <summary>
    /// Fallback hierarchy map for scenes tested directly without prior navigation history.
    /// </summary>
    private string GetDefaultBackScene(string currentScene)
    {
        switch (currentScene)
        {
            case "FoldForStorage":
            case "DeployForFlightScreen":
                return "TutorialSelectScreen";
            case "TutorialSelectScreen":
                return "StartScreen";
            default:
                return "";
        }
    }

    /// <summary>
    /// Responds to SendMessage("BeginTransitions") from a UI button
    /// </summary>
    public void BeginTransitions()
    {
        LoadScene(targetScene);
    }

    /// <summary>
    /// Can be called by any button or script to transition forward to another scene.
    /// </summary>
    public void LoadScene(string sceneName)
    {
        if (isTransitioning)
            return;

        StartCoroutine(TransitionRoutine(sceneName, isGoingBack: false));
    }

    private IEnumerator TransitionRoutine(string sceneName, bool isGoingBack)
    {
        isTransitioning = true;

        string currentScene = SceneManager.GetActiveScene().name;
        if (!isGoingBack && !string.IsNullOrEmpty(currentScene))
        {
            sceneHistory.Push(currentScene);
        }

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;

            // 1. Fade Out to Black in current scene (0 -> 1)
            float t = 0f;
            while (t < fadeOutDuration)
            {
                t += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                float progress = Mathf.Clamp01(t / fadeOutDuration);
                canvasGroup.alpha = Mathf.SmoothStep(0f, 1f, progress);
                yield return null;
            }
            canvasGroup.alpha = 1f;
        }

        // 2. Detach and persist this overlay so it survives into the new scene
        EnsurePersistentCanvas();

        // 3. Load the new scene in the background
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        while (!op.isDone)
        {
            yield return null;
        }

        // 4. Wait for the new scene to finish its first full render frame
        yield return new WaitForEndOfFrame();
        yield return null;

        // 5. Fade In from Black to transparent in the new scene (1 -> 0)
        if (canvasGroup != null)
        {
            float t = 0f;
            while (t < fadeInDuration)
            {
                t += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                float progress = Mathf.Clamp01(t / fadeInDuration);
                canvasGroup.alpha = Mathf.SmoothStep(1f, 0f, progress);
                yield return null;
            }
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }

        isTransitioning = false;

        // 6. Clean up this transition overlay
        Destroy(gameObject);
    }

    private void EnsurePersistentCanvas()
    {
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
        }
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;

        if (GetComponent<CanvasScaler>() == null)
        {
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
        }

        RectTransform rt = GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
        }
    }
}
