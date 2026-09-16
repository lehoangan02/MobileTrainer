using System;
using UnityEngine;

/// <summary>
/// Serializable data structure representing a camera view/pose for a tutorial step or scene default.
/// Encapsulates both orbit parameters (pivot, distance, pitch, yaw) and world transform (position, rotationEuler).
/// </summary>
[System.Serializable]
public struct StepCameraPose
{
    [Tooltip("If true, this step uses this custom camera position. If false, uses the scene default camera.")]
    public bool enabled;

    [Tooltip("World position of the camera.")]
    public Vector3 position;

    [Tooltip("Camera rotation in Euler degrees (Pitch = X, Yaw = Y, Roll = Z).")]
    public Vector3 rotationEuler;

    [Tooltip("Orbit pivot point in world space.")]
    public Vector3 pivot;

    [Tooltip("Distance from the pivot to the camera.")]
    public float distance;

    [Tooltip("Vertical orbit pitch in degrees.")]
    public float pitch;

    [Tooltip("Horizontal orbit yaw in degrees.")]
    public float yaw;

    [Tooltip("Optional field of view override (0 = keep current camera FOV).")]
    public float fieldOfView;

    public StepCameraPose(Vector3 pos, Vector3 rotEuler, Vector3 pvt, float dist, float fov = 0f)
    {
        enabled = true;
        position = pos;
        pitch = NormalizeAngle(rotEuler.x);
        yaw = NormalizeAngle(rotEuler.y);
        rotationEuler = new Vector3(pitch, yaw, 0f);
        pivot = pvt;
        distance = dist > 0.05f ? dist : 2.5f;
        fieldOfView = fov;
    }

    public static float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }

    /// <summary>
    /// Creates a StepCameraPose from a camera and an optional target pivot transform.
    /// </summary>
    public static StepCameraPose CreateFromCamera(Camera cam, Transform pivotTarget = null)
    {
        if (cam == null) return default;
        Vector3 pos = cam.transform.position;
        Vector3 euler = cam.transform.eulerAngles;
        float pitch = NormalizeAngle(euler.x);
        float yaw = NormalizeAngle(euler.y);
        Vector3 fwd = cam.transform.forward;

        float dist = 2.5f;
        Vector3 pivot;
        if (pivotTarget != null)
        {
            Vector3 toTarget = pivotTarget.position - pos;
            float projected = Vector3.Dot(toTarget, fwd);
            dist = projected > 0.2f ? projected : 2.5f;
            pivot = pos + fwd * dist;
        }
        else
        {
            pivot = pos + fwd * dist;
        }

        return new StepCameraPose(pos, new Vector3(pitch, yaw, 0f), pivot, dist, cam.fieldOfView);
    }

    /// <summary>
    /// Creates a StepCameraPose from an arbitrary Transform in the 3D scene (e.g. a camera anchor).
    /// </summary>
    public static StepCameraPose CreateFromTransform(Transform t, Transform pivotTarget = null, float fov = 55f)
    {
        if (t == null) return default;
        Vector3 pos = t.position;
        Vector3 euler = t.eulerAngles;
        float pitch = NormalizeAngle(euler.x);
        float yaw = NormalizeAngle(euler.y);
        Vector3 fwd = t.forward;

        float dist = 2.5f;
        Vector3 pivot;
        if (pivotTarget != null)
        {
            Vector3 toTarget = pivotTarget.position - pos;
            float projected = Vector3.Dot(toTarget, fwd);
            dist = projected > 0.2f ? projected : 2.5f;
            pivot = pos + fwd * dist;
        }
        else
        {
            pivot = pos + fwd * dist;
        }

        var pose = new StepCameraPose(pos, new Vector3(pitch, yaw, 0f), pivot, dist, fov);
        pose.enabled = true;
        return pose;
    }

    /// <summary>
    /// Creates a StepCameraPose from an existing ModelCameraController state.
    /// </summary>
    public static StepCameraPose CreateFromController(ModelCameraController controller)
    {
        if (controller == null) return default;
        Camera cam = controller.GetComponent<Camera>();
        float fov = cam != null ? cam.fieldOfView : 55f;

        Vector3 pos = controller.transform.position;
        float pitch = controller.TargetPitch;
        float yaw = controller.TargetYaw;
        Vector3 pivot = controller.TargetPivot;
        float dist = controller.TargetDistance;

        return new StepCameraPose(pos, new Vector3(pitch, yaw, 0f), pivot, dist, fov);
    }

#if UNITY_EDITOR
    /// <summary>
    /// Captures the current SceneView camera view into a StepCameraPose.
    /// </summary>
    public static StepCameraPose CreateFromSceneView(Transform pivotTarget = null)
    {
        var sv = UnityEditor.SceneView.lastActiveSceneView;
        if (sv != null)
        {
            Vector3 pivot = sv.pivot;
            Quaternion rot = sv.rotation;
            Vector3 euler = rot.eulerAngles;
            float pitch = NormalizeAngle(euler.x);
            float yaw = NormalizeAngle(euler.y);
            float dist = sv.size > 0.1f ? sv.size : 2.5f;
            Vector3 fwd = rot * Vector3.forward;
            Vector3 pos = pivot - fwd * dist;

            Camera svCam = sv.camera;
            float fov = svCam != null ? svCam.fieldOfView : 55f;

            return new StepCameraPose(pos, new Vector3(pitch, yaw, 0f), pivot, dist, fov);
        }
        return default;
    }

    /// <summary>
    /// Previews this camera pose in the Unity Editor by aligning the SceneView and Main Camera.
    /// </summary>
    public void ApplyPreviewInEditor(Camera sceneCam = null)
    {
        if (!enabled) return;

        // 1. Move Main Camera
        if (sceneCam == null)
        {
            sceneCam = Camera.main;
            if (sceneCam == null)
            {
                sceneCam = UnityEngine.Object.FindFirstObjectByType<Camera>();
            }
        }
        if (sceneCam != null)
        {
            UnityEditor.Undo.RecordObject(sceneCam.transform, "Preview Step Camera");
            sceneCam.transform.position = position;
            sceneCam.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            if (fieldOfView > 10f)
            {
                UnityEditor.Undo.RecordObject(sceneCam, "Preview Step Camera FOV");
                sceneCam.fieldOfView = fieldOfView;
            }
        }

        // 2. Move Scene View
        var sv = UnityEditor.SceneView.lastActiveSceneView;
        if (sv != null)
        {
            sv.pivot = pivot;
            sv.rotation = Quaternion.Euler(pitch, yaw, 0f);
            sv.size = distance;
            if (fieldOfView > 10f && sv.camera != null)
            {
                sv.camera.fieldOfView = fieldOfView;
            }
            sv.Repaint();
        }

        UnityEditor.SceneView.RepaintAll();
    }
#endif

    public string SummaryText
    {
        get
        {
            if (!enabled) return "Disabled (Using Scene Default)";
            return $"Pos: ({position.x:F1}, {position.y:F1}, {position.z:F1}) | Pitch: {pitch:F0}° | Yaw: {yaw:F0}° | Dist: {distance:F1}m";
        }
    }
}
