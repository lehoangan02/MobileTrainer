using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Bridges pointer down, up, and drag events from a UI Slider to external listeners.
/// Ensures reliable dragging, dropping, and instantaneous jumping when tapping anywhere on the slider.
/// Automatically provisions a generous invisible touch hit target so mobile touches never miss the track.
/// </summary>
public class SliderEventBridge : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, IPointerClickHandler
{
    public Slider targetSlider;
    public event Action<PointerEventData> OnPointerDownEvent;
    public event Action<PointerEventData> OnPointerUpEvent;
    public event Action<PointerEventData> OnDragEvent;

    [Header("Touch Hit Area Settings")]
    [SerializeField] private float hitTargetHeight = 70f;
    [SerializeField] private float hitTargetHorizontalPadding = 40f;

    private bool draggingFromHandle = false;

    private void Awake()
    {
        if (targetSlider == null)
        {
            targetSlider = GetComponent<Slider>();
        }
        EnsureTouchHitArea();
    }

    private void Start()
    {
        EnsureTouchHitArea();
    }

    /// <summary>
    /// Creates or verifies a transparent hit area behind the visible track
    /// so mobile touches within the hit zone reliably register and jump.
    /// </summary>
    public void EnsureTouchHitArea()
    {
        if (targetSlider == null)
        {
            targetSlider = GetComponent<Slider>();
        }
        if (targetSlider == null) return;

        Transform existing = targetSlider.transform.Find("TouchHitArea");
        if (existing != null) return;

        GameObject hitGo = new GameObject("TouchHitArea", typeof(RectTransform));
        hitGo.transform.SetParent(targetSlider.transform, false);
        hitGo.transform.SetAsFirstSibling();

        RectTransform rt = hitGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(hitTargetHorizontalPadding, hitTargetHeight);

        Image img = hitGo.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f);
        img.raycastTarget = true;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (targetSlider == null)
        {
            targetSlider = GetComponent<Slider>();
        }

        Camera cam = GetEventCamera(eventData);

        // Check if the user specifically touched the handle knob
        draggingFromHandle = (targetSlider != null && targetSlider.handleRect != null &&
            RectTransformUtility.RectangleContainsScreenPoint(targetSlider.handleRect, eventData.position, cam));

        // Notify pointer down first so scrubbing state and pause logic are established
        OnPointerDownEvent?.Invoke(eventData);

        // If user tapped outside the handle (tapping to jump), instantly jump slider to that exact point
        if (!draggingFromHandle)
        {
            JumpToPoint(eventData);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        // If dragging from track/hit-area, continue tracking position smoothly
        if (!draggingFromHandle)
        {
            JumpToPoint(eventData);
        }

        OnDragEvent?.Invoke(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!draggingFromHandle)
        {
            JumpToPoint(eventData);
        }
        draggingFromHandle = false;
        OnPointerUpEvent?.Invoke(eventData);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!draggingFromHandle)
        {
            JumpToPoint(eventData);
        }
    }

    public void JumpToPoint(PointerEventData eventData)
    {
        if (targetSlider == null)
        {
            targetSlider = GetComponent<Slider>();
        }
        if (targetSlider == null) return;

        // Container is the handle's parent rect (slide area), or the slider rect itself
        RectTransform container = (targetSlider.handleRect != null && targetSlider.handleRect.parent != null)
            ? targetSlider.handleRect.parent.GetComponent<RectTransform>()
            : targetSlider.GetComponent<RectTransform>();

        Camera cam = GetEventCamera(eventData);

        if (container != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(container, eventData.position, cam, out Vector2 localPoint))
        {
            float width = container.rect.width;
            if (width > 0f)
            {
                float normalized = Mathf.Clamp01((localPoint.x - container.rect.xMin) / width);
                float targetVal = targetSlider.minValue + normalized * (targetSlider.maxValue - targetSlider.minValue);
                targetSlider.value = targetVal;
            }
        }
    }

    private Camera GetEventCamera(PointerEventData eventData)
    {
        if (eventData != null && eventData.pressEventCamera != null)
        {
            return eventData.pressEventCamera;
        }

        if (targetSlider != null)
        {
            Canvas canvas = targetSlider.GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                return canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
            }
        }

        return null;
    }
}
