using UnityEngine;

/// <summary>
/// Keeps a stretched RectTransform inside Screen.safeArea so content clears the camera cutout
/// (punch hole) and rounded corners. Put it on a full-size container and parent the content to it.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SafeAreaFitter : MonoBehaviour
{
    [Tooltip("Only inset left/right. Use for bars that sit in a vertical slice of the canvas, e.g. a header.")]
    [SerializeField] private bool horizontalOnly;

    private RectTransform rt;
    private Rect applied;
    private Vector2Int appliedScreen;

    private void OnEnable() => Apply(true);

    private void LateUpdate() => Apply(false);

    private void Apply(bool force)
    {
        Rect safe = Screen.safeArea;
        var screen = new Vector2Int(Screen.width, Screen.height);
        if (!force && safe == applied && screen == appliedScreen) return;
        if (screen.x <= 0 || screen.y <= 0) return;

        applied = safe;
        appliedScreen = screen;
        if (rt == null) rt = (RectTransform)transform;

        Vector2 min = new Vector2(safe.xMin / screen.x, safe.yMin / screen.y);
        Vector2 max = new Vector2(safe.xMax / screen.x, safe.yMax / screen.y);
        if (horizontalOnly)
        {
            min.y = 0f;
            max.y = 1f;
        }
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
