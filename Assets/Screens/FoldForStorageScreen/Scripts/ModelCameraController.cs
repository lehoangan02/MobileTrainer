using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using CW.Common;

/// <summary>
/// Camera controller supporting multi-touch orbit (rotate), pan, and pinch-to-zoom for mobile 3D inspection,
/// with full mouse/scroll-wheel simulation in the Unity Editor and a smooth reset-to-original orientation feature.
/// Preserves the exact scene camera framing without jumping or shifting off-center upon Play.
/// </summary>
[RequireComponent(typeof(Camera))]
public class ModelCameraController : MonoBehaviour
{
    [Header("Target & Pivot")]
    [Tooltip("Transform to orbit around. If null, automatically discovers Ghost_Drone or TutorialRigRoot.")]
    [SerializeField] private Transform targetPivotTransform;
    [SerializeField] private Vector3 customPivotOffset = Vector3.zero;

    [Header("Distance (Zoom) Limits")]
    [SerializeField] private float minDistance = 0.5f;
    [SerializeField] private float maxDistance = 8.0f;

    [Header("Pitch (Vertical) Limits")]
    [SerializeField] private float minPitch = -35f;
    [SerializeField] private float maxPitch = 85f;

    [Header("Touch Sensitivity")]
    [SerializeField] private float orbitSensitivity = 0.22f;
    [SerializeField] private float pinchZoomSensitivity = 1.8f;
    [SerializeField] private float panSensitivity = 1.4f;

    [Header("Desktop / Editor Sensitivity")]
    [SerializeField] private float mouseOrbitSensitivity = 0.25f;
    [SerializeField] private float mousePanSensitivity = 1.2f;
    [SerializeField] private float mouseScrollSensitivity = 0.6f;

    [Header("Smooth Damping")]
    [SerializeField] private float smoothTime = 0.07f;

    [Header("Reset Animation")]
    [SerializeField] private float resetDuration = 0.45f;

    // Initial default state
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private Vector3 initialPivot;
    private float initialYaw;
    private float initialPitch;
    private float initialDistance;

    // Current smoothed state
    private Vector3 currentPivot;
    private float currentYaw;
    private float currentPitch;
    private float currentDistance;

    // Target state
    private Vector3 targetPivot;
    private float targetYaw;
    private float targetPitch;
    private float targetDistance;

    // SmoothDamp velocities
    private Vector3 pivotVelocity;
    private float yawVelocity;
    private float pitchVelocity;
    private float distanceVelocity;

    // Reset coroutine
    private Coroutine resetRoutine;

    // Touch tracking helper
    private class TouchInfo
    {
        public int id;
        public Vector2 currentPos;
        public Vector2 prevPos;
        public bool startedOverUI;
        public bool isNew;
    }

    private readonly Dictionary<int, TouchInfo> activeTouches = new();
    private readonly List<TouchInfo> validTouches = new();

    // Mouse tracking helper
    private Vector2 lastMousePos;
    private bool mouse0StartedOverUI;
    private bool mousePanStartedOverUI;

    public Vector3 InitialPivot => initialPivot;
    public Vector3 CurrentPivot => currentPivot;

    private void Awake()
    {
        // 1. Capture the camera's EXACT initial scene transform to guarantee zero jumping
        initialPosition = transform.position;
        initialRotation = transform.rotation;

        Vector3 euler = initialRotation.eulerAngles;
        initialPitch = euler.x > 180f ? euler.x - 360f : euler.x;
        initialYaw = euler.y > 180f ? euler.y - 360f : euler.y;

        // 2. Discover the drone target to project depth along the camera's forward ray
        Transform pivotTarget = targetPivotTransform;
        if (pivotTarget == null)
        {
            var player = Object.FindFirstObjectByType<TutorialPlayer>();
            if (player != null && player.rigRoot != null)
            {
                Transform drone = player.rigRoot.Find("Ghost_Drone");
                pivotTarget = drone != null ? drone : player.rigRoot;
            }
        }
        else
        {
            // If targetPivotTransform is TutorialRigRoot, prioritize child Ghost_Drone for the true visual center
            Transform drone = pivotTarget.Find("Ghost_Drone");
            if (drone != null)
            {
                pivotTarget = drone;
            }
        }

        if (pivotTarget != null)
        {
            Vector3 toTarget = (pivotTarget.position + customPivotOffset) - initialPosition;
            float projectedDist = Vector3.Dot(toTarget, transform.forward);
            initialDistance = Mathf.Clamp(projectedDist > 0.5f ? projectedDist : 2.5f, minDistance, maxDistance);
        }
        else
        {
            initialDistance = Mathf.Clamp(2.5f, minDistance, maxDistance);
        }

        // 3. Compute pivot strictly along the camera's initial forward ray.
        // This ensures pos = initialPivot - rot * forward * initialDistance exactly equals initialPosition on frame 0.
        initialPivot = initialPosition + transform.forward * initialDistance;

        currentPivot = targetPivot = initialPivot;
        currentYaw = targetYaw = initialYaw;
        currentPitch = targetPitch = initialPitch;
        currentDistance = targetDistance = initialDistance;

        // Apply immediately
        transform.rotation = initialRotation;
        transform.position = initialPosition;

        Debug.Log($"[ModelCameraController] Initialized cleanly. Pivot: {initialPivot}, Camera: {initialPosition}, Distance: {initialDistance:F2}m, Pitch: {initialPitch:F1}°, Yaw: {initialYaw:F1}°");
    }

    private void Update()
    {
        int touchCount = CwInput.GetTouchCount();

        if (touchCount > 0)
        {
            HandleTouchInput(touchCount);
        }
        else if (CwInput.GetMouseExists())
        {
            HandleMouseInput();
        }
    }

    private void LateUpdate()
    {
        if (resetRoutine == null)
        {
            // Apply SmoothDamp
            currentYaw = Mathf.SmoothDamp(currentYaw, targetYaw, ref yawVelocity, smoothTime);
            currentPitch = Mathf.SmoothDamp(currentPitch, targetPitch, ref pitchVelocity, smoothTime);
            currentDistance = Mathf.SmoothDamp(currentDistance, targetDistance, ref distanceVelocity, smoothTime);
            currentPivot = Vector3.SmoothDamp(currentPivot, targetPivot, ref pivotVelocity, smoothTime);

            Quaternion rot = Quaternion.Euler(currentPitch, currentYaw, 0f);
            Vector3 pos = currentPivot - rot * Vector3.forward * currentDistance;

            transform.rotation = rot;
            transform.position = pos;
        }
    }

    private void HandleTouchInput(int touchCount)
    {
        // Mark all existing trackers as not new
        foreach (var kvp in activeTouches)
        {
            kvp.Value.isNew = false;
        }

        HashSet<int> presentIds = new();

        for (int i = 0; i < touchCount; i++)
        {
            CwInput.GetTouch(i, out int id, out Vector2 pos, out float pressure, out bool set);
            if (!set) continue;

            presentIds.Add(id);

            if (!activeTouches.TryGetValue(id, out TouchInfo info))
            {
                info = new TouchInfo
                {
                    id = id,
                    currentPos = pos,
                    prevPos = pos,
                    startedOverUI = IsPointerOverUI(pos, id),
                    isNew = true
                };
                activeTouches[id] = info;
            }
            else
            {
                info.prevPos = info.currentPos;
                info.currentPos = pos;
            }
        }

        // Clean up ended touches
        List<int> toRemove = null;
        foreach (var id in activeTouches.Keys)
        {
            if (!presentIds.Contains(id))
            {
                toRemove ??= new List<int>();
                toRemove.Add(id);
            }
        }
        if (toRemove != null)
        {
            for (int i = 0; i < toRemove.Count; i++)
            {
                activeTouches.Remove(toRemove[i]);
            }
        }

        // Collect valid touches that did NOT start over UI
        validTouches.Clear();
        foreach (var info in activeTouches.Values)
        {
            if (!info.startedOverUI)
            {
                validTouches.Add(info);
            }
        }

        if (validTouches.Count == 0)
            return;

        InterruptReset();

        // 1 Finger: Orbit / Rotate around pivot
        if (validTouches.Count == 1)
        {
            var t = validTouches[0];
            if (!t.isNew)
            {
                Vector2 delta = t.currentPos - t.prevPos;
                targetYaw += delta.x * orbitSensitivity;
                targetPitch -= delta.y * orbitSensitivity;
                targetPitch = Mathf.Clamp(targetPitch, minPitch, maxPitch);
            }
        }
        // 2 Fingers: Pinch-Zoom and Two-Finger Pan
        else if (validTouches.Count >= 2)
        {
            var t0 = validTouches[0];
            var t1 = validTouches[1];

            if (!t0.isNew && !t1.isNew)
            {
                // 1. Pinch to Zoom
                float curDist = Vector2.Distance(t0.currentPos, t1.currentPos);
                float prevDist = Vector2.Distance(t0.prevPos, t1.prevPos);
                float pinchDelta = curDist - prevDist;

                float zoomFactor = pinchZoomSensitivity * (targetDistance / Mathf.Max(Screen.height, 1f));
                targetDistance -= pinchDelta * zoomFactor;
                targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);

                // 2. Pan
                Vector2 delta0 = t0.currentPos - t0.prevPos;
                Vector2 delta1 = t1.currentPos - t1.prevPos;
                Vector2 panDelta = (delta0 + delta1) * 0.5f;

                float panFactor = panSensitivity * (targetDistance / Mathf.Max(Screen.height, 1f));
                Vector3 right = transform.right;
                Vector3 up = transform.up;
                targetPivot -= (right * panDelta.x + up * panDelta.y) * panFactor;
            }
        }
    }

    private void HandleMouseInput()
    {
        Vector2 mousePos = CwInput.GetMousePosition();

        if (CwInput.GetMouseWentDown(0))
        {
            mouse0StartedOverUI = IsPointerOverUI(mousePos, -1);
            lastMousePos = mousePos;
        }
        if (CwInput.GetMouseWentDown(1) || CwInput.GetMouseWentDown(2))
        {
            mousePanStartedOverUI = IsPointerOverUI(mousePos, -1);
            lastMousePos = mousePos;
        }

        Vector2 mouseDelta = mousePos - lastMousePos;

        // Left Click Drag: Orbit around pivot
        if (CwInput.GetMouseIsHeld(0) && !mouse0StartedOverUI)
        {
            if (mouseDelta.sqrMagnitude > 0.001f)
            {
                InterruptReset();
                targetYaw += mouseDelta.x * mouseOrbitSensitivity;
                targetPitch -= mouseDelta.y * mouseOrbitSensitivity;
                targetPitch = Mathf.Clamp(targetPitch, minPitch, maxPitch);
            }
        }

        // Right or Middle Click Drag: Pan
        if ((CwInput.GetMouseIsHeld(1) || CwInput.GetMouseIsHeld(2)) && !mousePanStartedOverUI)
        {
            if (mouseDelta.sqrMagnitude > 0.001f)
            {
                InterruptReset();
                float panFactor = mousePanSensitivity * (targetDistance / Mathf.Max(Screen.height, 1f));
                Vector3 right = transform.right;
                Vector3 up = transform.up;
                targetPivot -= (right * mouseDelta.x + up * mouseDelta.y) * panFactor;
            }
        }

        // Scroll Wheel: Zoom
        float scroll = CwInput.GetMouseWheelDelta();
        if (Mathf.Abs(scroll) > 0.01f && !IsPointerOverUI(mousePos, -1))
        {
            InterruptReset();
            targetDistance -= scroll * mouseScrollSensitivity * (targetDistance * 0.1f);
            targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);
        }

        lastMousePos = mousePos;
    }

    /// <summary>
    /// Smoothly animates the camera back to its initial original position, rotation, framing, and zoom distance.
    /// </summary>
    public void ResetOrientation()
    {
        ResetOrientation(resetDuration);
    }

    public void ResetOrientation(float duration)
    {
        if (resetRoutine != null)
        {
            StopCoroutine(resetRoutine);
        }
        resetRoutine = StartCoroutine(ResetRoutine(duration));
    }

    private IEnumerator ResetRoutine(float duration)
    {
        Vector3 startPivot = currentPivot;
        float startYaw = currentYaw;
        float startPitch = currentPitch;
        float startDistance = currentDistance;

        // Pick shortest angular path for yaw
        float diffYaw = Mathf.DeltaAngle(startYaw, initialYaw);
        float endYaw = startYaw + diffYaw;

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(t / duration);
            float ease = Mathf.SmoothStep(0f, 1f, progress);

            currentPivot = targetPivot = Vector3.Lerp(startPivot, initialPivot, ease);
            currentPitch = targetPitch = Mathf.Lerp(startPitch, initialPitch, ease);
            currentYaw = targetYaw = Mathf.Lerp(startYaw, endYaw, ease);
            currentDistance = targetDistance = Mathf.Lerp(startDistance, initialDistance, ease);

            Quaternion rot = Quaternion.Euler(currentPitch, currentYaw, 0f);
            transform.rotation = rot;
            transform.position = currentPivot - rot * Vector3.forward * currentDistance;

            yield return null;
        }

        currentPivot = targetPivot = initialPivot;
        currentPitch = targetPitch = initialPitch;
        currentYaw = targetYaw = initialYaw;
        currentDistance = targetDistance = initialDistance;

        Quaternion finalRot = Quaternion.Euler(initialPitch, initialYaw, 0f);
        transform.rotation = finalRot;
        transform.position = initialPivot - finalRot * Vector3.forward * initialDistance;

        resetRoutine = null;
    }

    private void InterruptReset()
    {
        if (resetRoutine != null)
        {
            StopCoroutine(resetRoutine);
            resetRoutine = null;
        }
    }

    private bool IsPointerOverUI(Vector2 screenPos, int fingerId)
    {
        if (EventSystem.current == null)
            return false;

        if (fingerId >= 0 && EventSystem.current.IsPointerOverGameObject(fingerId))
            return true;

        if (fingerId < 0 && EventSystem.current.IsPointerOverGameObject())
            return true;

        // Pointer event data raycast fallback
        var eventData = new PointerEventData(EventSystem.current) { position = screenPos };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        return results.Count > 0;
    }
}
