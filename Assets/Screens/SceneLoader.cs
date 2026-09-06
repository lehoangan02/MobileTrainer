using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneLoader : MonoBehaviour
{
    [Header("Transition Settings")]
    [SerializeField] private float fadeDuration = 0.5f;
    [SerializeField] private string targetScene = "TutorialSelectScreen";

    private CanvasGroup canvasGroup;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }

    /// <summary>
    /// Responds to SendMessage("BeginTransitions") from the button
    /// </summary>
    public void BeginTransitions()
    {
        StartCoroutine(TransitionRoutine(targetScene));
    }

    /// <summary>
    /// Can also be called directly as SceneLoader.LoadScene(string)
    /// </summary>
    public void LoadScene(string sceneName)
    {
        StartCoroutine(TransitionRoutine(sceneName));
    }

    private IEnumerator TransitionRoutine(string sceneName)
    {
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;

            // 1. Fade Out to Black in current scene (0 -> 1)
            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Clamp01(t / fadeDuration);
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

        // 4. Wait one frame for the new scene to initialize
        yield return null;

        // 5. Fade In from Black to transparent in the new scene (1 -> 0)
        if (canvasGroup != null)
        {
            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                canvasGroup.alpha = 1f - Mathf.Clamp01(t / fadeDuration);
                yield return null;
            }
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }

        // 6. Clean up this transition overlay
        Destroy(gameObject);
    }

    private void EnsurePersistentCanvas()
    {
        // Detach from StartScreen's Canvas so it doesn't get destroyed when the scene unloads
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        // Give it its own Canvas so it can render on top of the new scene
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

        // Ensure it stretches across the entire screen
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
